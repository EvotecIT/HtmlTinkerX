(() => {
    const createGuards = ({ defineProperty, getOwnPropertyDescriptor, getPrototypeOf, reflectApply, stringValue, booleanValue }) => {
        const states = new WeakMap();
        const insertionStates = new WeakMap();
        const prototypes = new WeakSet();
        const propertyGuards = new WeakMap();
        const textPropertyGuards = new WeakMap();
        const factoryGuards = new WeakMap();
        const legacyHandlers = new WeakMap();
        const attrStates = new WeakMap();
        const attrPrototypes = new WeakSet();
        const installAttr = prototype => {
            if (!prototype || attrPrototypes.has(prototype)) return;
            attrPrototypes.add(prototype);
            for (const name of ['value', 'nodeValue', 'textContent']) {
                let owner = prototype, descriptor = null;
                while (owner && descriptor == null) { descriptor = getOwnPropertyDescriptor(owner, name); if (descriptor == null) owner = getPrototypeOf(owner); }
                if (!descriptor?.get || !descriptor?.set) continue;
                defineProperty(prototype, name, {
                    configurable: false,
                    enumerable: descriptor.enumerable,
                    get() { return attrStates.get(this)?.get() ?? descriptor.get.call(this); },
                    set(value) { const state = attrStates.get(this); if (state) state.set(stringValue(value)); else descriptor.set.call(this, value); }
                });
            }
        };
        const installText = (prototype, name) => {
            if (!prototype) return;
            let guarded = textPropertyGuards.get(prototype);
            if (!guarded) textPropertyGuards.set(prototype, guarded = new Set());
            if (guarded.has(name)) return;
            const text = getOwnPropertyDescriptor(prototype, name);
            if (!text?.get || !text?.set) return;
            guarded.add(name);
            defineProperty(prototype, name, {
                ...text,
                get() {
                    const staged = states.get(this)?.textContent();
                    return staged?.handled ? staged.value : text.get.call(this);
                },
                set(value) {
                    if (states.get(this)?.setTextContent(value)) return;
                    return text.set.call(this, value);
                }
            });
        };
        const install = prototype => {
            if (!prototype || prototypes.has(prototype)) return;
            prototypes.add(prototype);
            for (const name of ['setAttribute', 'setAttributeNS', 'setAttributeNode', 'setAttributeNodeNS', 'removeAttribute', 'removeAttributeNS', 'removeAttributeNode', 'toggleAttribute']) {
                const method = prototype[name];
                if (typeof method !== 'function') continue;
                defineProperty(prototype, name, {
                    configurable: false,
                    writable: false,
                    value(...args) {
                        const state = states.get(this);
                        if (state && state[name](...args)) return state.result;
                        return reflectApply(method, this, args);
                    }
                });
            }
            for (const name of ['getAttribute', 'getAttributeNS', 'hasAttribute', 'hasAttributeNS', 'getAttributeNode', 'getAttributeNodeNS']) {
                const method = prototype[name];
                if (typeof method !== 'function') continue;
                defineProperty(prototype, name, {
                    configurable: false,
                    writable: false,
                    value(...args) {
                        const read = states.get(this)?.read(name, args);
                        return read?.handled ? read.value : reflectApply(method, this, args);
                    }
                });
            }
            for (const name of ['innerHTML', 'outerHTML']) {
                const descriptor = getOwnPropertyDescriptor(prototype, name);
                if (!descriptor || typeof descriptor.set !== 'function') continue;
                defineProperty(prototype, name, {
                    ...descriptor,
                    set(value) {
                        const state = states.get(this);
                        if (state?.markup(name, [value])) return;
                        return descriptor.set.call(this, value);
                    }
                });
            }
            for (const name of ['text', 'innerText']) installText(prototype, name);
            const insertAdjacentHTML = prototype.insertAdjacentHTML;
            if (typeof insertAdjacentHTML === 'function') {
                defineProperty(prototype, 'insertAdjacentHTML', {
                    configurable: false,
                    writable: false,
                    value(...args) {
                        const state = states.get(this);
                        if (state?.markup('insertAdjacentHTML', args)) return;
                        return reflectApply(insertAdjacentHTML, this, args);
                    }
                });
            }
            const insertAdjacentText = prototype.insertAdjacentText;
            if (typeof insertAdjacentText === 'function') {
                defineProperty(prototype, 'insertAdjacentText', {
                    configurable: false,
                    writable: false,
                    value(...args) {
                        const staged = states.get(this)?.insertAdjacentText(args);
                        return staged?.handled ? staged.value : reflectApply(insertAdjacentText, this, args);
                    }
                });
            }
            const setHTMLUnsafe = prototype.setHTMLUnsafe;
            if (typeof setHTMLUnsafe === 'function') {
                defineProperty(prototype, 'setHTMLUnsafe', {
                    configurable: false,
                    writable: false,
                    value(...args) {
                        if (args.length === 0) return reflectApply(setHTMLUnsafe, this, args);
                        const state = states.get(this);
                        if (state?.markup('setHTMLUnsafe', args)) return;
                        return reflectApply(setHTMLUnsafe, this, args);
                    }
                });
            }
            for (const name of ['append', 'prepend', 'replaceChildren']) {
                const method = prototype[name];
                if (typeof method !== 'function') continue;
                defineProperty(prototype, name, {
                    configurable: false,
                    writable: false,
                    value(...args) {
                        const state = states.get(this);
                        (state?.guardInsertion ?? insertionStates.get(this))?.(name, args);
                        const staged = state?.mutateText(method, args);
                        return staged?.handled ? staged.value : reflectApply(method, this, args);
                    }
                });
            }
            for (const name of ['after', 'before', 'replaceWith', 'insertAdjacentElement']) {
                const method = prototype[name];
                if (typeof method !== 'function') continue;
                defineProperty(prototype, name, {
                    configurable: false,
                    writable: false,
                    value(...args) {
                        (states.get(this)?.guardInsertion ?? insertionStates.get(this))?.(name, args);
                        return reflectApply(method, this, args);
                    }
                });
            }
        };
        const installFactories = (prototype, names) => {
            if (!prototype) return;
            let guarded = factoryGuards.get(prototype);
            if (!guarded) factoryGuards.set(prototype, guarded = new Set());
            for (const name of names) {
                if (guarded.has(name)) continue;
                const method = prototype[name];
                if (typeof method !== 'function') continue;
                guarded.add(name);
                defineProperty(prototype, name, {
                    configurable: false,
                    writable: false,
                    value(...args) {
                        const result = reflectApply(method, this, args);
                        return states.get(this)?.guardResult(result) ?? result;
                    }
                });
            }
        };
        const installNamedNodeMap = prototype => {
            if (!prototype || prototypes.has(prototype)) return;
            prototypes.add(prototype);
            for (const name of ['setNamedItem', 'setNamedItemNS', 'removeNamedItem', 'removeNamedItemNS']) {
                const method = prototype[name];
                defineProperty(prototype, name, {
                    configurable: false,
                    writable: false,
                    value(...args) {
                        const state = states.get(this);
                        if (state && state[name](...args)) return state.result;
                        return reflectApply(method, this, args);
                    }
                });
            }
        };
        const installNode = prototype => {
            if (!prototype || prototypes.has(prototype)) return;
            prototypes.add(prototype);
            const ownerDocument = getOwnPropertyDescriptor(prototype, 'ownerDocument');
            const getRootNode = prototype.getRootNode;
            const cloneNode = prototype.cloneNode;
            const textContent = getOwnPropertyDescriptor(prototype, 'textContent');
            for (const name of ['appendChild', 'insertBefore', 'replaceChild', 'removeChild']) {
                const method = prototype[name];
                if (typeof method !== 'function') continue;
                defineProperty(prototype, name, {
                    configurable: false,
                    writable: false,
                    value(...args) {
                        const state = states.get(this);
                        (state?.guardInsertion ?? insertionStates.get(this))?.(name, args);
                        const staged = state?.mutateText(method, args);
                        return staged?.handled ? staged.value : reflectApply(method, this, args);
                    }
                });
            }
            defineProperty(prototype, 'ownerDocument', {
                ...ownerDocument,
                get() { return states.get(this)?.document() ?? ownerDocument.get.call(this); }
            });
            defineProperty(prototype, 'getRootNode', {
                configurable: false,
                writable: false,
                value(...args) {
                    const root = reflectApply(getRootNode, this, args);
                    return root?.nodeType === 9 ? states.get(this)?.document() ?? root : root;
                }
            });
            defineProperty(prototype, 'cloneNode', {
                configurable: false,
                writable: false,
                value(...args) {
                    const clone = reflectApply(cloneNode, this, args);
                    return states.get(this)?.clone(clone) ?? clone;
                }
            });
            if (textContent?.get && textContent?.set) defineProperty(prototype, 'textContent', {
                ...textContent,
                get() {
                    const staged = states.get(this)?.textContent();
                    return staged?.handled ? staged.value : textContent.get.call(this);
                },
                set(value) {
                    if (states.get(this)?.setTextContent(value)) return;
                    return textContent.set.call(this, value);
                }
            });
        };
        const installFrame = prototype => {
            if (!prototype || prototypes.has(prototype)) return;
            prototypes.add(prototype);
            const contentDocument = getOwnPropertyDescriptor(prototype, 'contentDocument');
            const contentWindow = getOwnPropertyDescriptor(prototype, 'contentWindow');
            if (contentDocument?.get) defineProperty(prototype, 'contentDocument', {
                ...contentDocument,
                get() {
                    const document = contentDocument.get.call(this);
                    return document == null ? document : states.get(this)?.document(document) ?? document;
                }
            });
            if (contentWindow?.get) defineProperty(prototype, 'contentWindow', {
                ...contentWindow,
                get() {
                    const target = contentWindow.get.call(this);
                    const state = states.get(this);
                    return target == null ? target : state?.window(target) ?? target;
                }
            });
        };
        const installProperty = (prototype, property, attribute) => {
            if (!prototype) return;
            let guarded = propertyGuards.get(prototype);
            if (!guarded) propertyGuards.set(prototype, guarded = new Set());
            if (guarded.has(property)) return;
            const descriptor = getOwnPropertyDescriptor(prototype, property);
            if (!descriptor || typeof descriptor.set !== 'function') return;
            guarded.add(property);
            defineProperty(prototype, property, {
                ...descriptor,
                set(value) {
                    const state = states.get(this);
                    if (state && state.setAttribute(attribute, value)) return;
                    return descriptor.set.call(this, value);
                }
            });
        };
        const createState = (element, values, namespacedValues, isReleased, shouldDefer, document, window, stageMarkup, guardClone, guardTree, synchronizeAttribute, textState, touch = () => { }) => {
            const normalized = name => stringValue(name).toLowerCase();
            const state = {
                result: undefined,
                document,
                window,
                clone(value) { return typeof guardClone === 'function' ? guardClone(value) : value; },
                guardResult(value) {
                    if (!isReleased() && typeof guardTree === 'function') guardTree(value);
                    return value;
                },
                guardInsertion(method, args) {
                    if (isReleased() || typeof guardTree !== 'function') return;
                    if (method === 'insertAdjacentElement') {
                        if (args.length > 1) guardTree(args[1]);
                        return;
                    }
                    if (method === 'appendChild' || method === 'insertBefore' || method === 'replaceChild') {
                        if (args.length > 0) guardTree(args[0]);
                        return;
                    }
                    if (['append', 'prepend', 'replaceChildren', 'after', 'before', 'replaceWith'].includes(method)) {
                        for (const value of args) guardTree(value);
                    }
                },
                textContent() {
                    return !isReleased() && textState != null
                        ? { handled: true, value: textState.get() }
                        : { handled: false };
                },
                setTextContent(value) {
                    if (isReleased() || textState == null) return false;
                    textState.set(stringValue(value));
                    return true;
                },
                insertAdjacentText(args) {
                    if (isReleased() || textState == null || args.length < 2) return { handled: false };
                    const position = stringValue(args[0]).toLowerCase();
                    if (position !== 'afterbegin' && position !== 'beforeend') return { handled: false };
                    const value = stringValue(args[1]);
                    textState.set(position === 'afterbegin' ? value + textState.get() : textState.get() + value);
                    return { handled: true, value: undefined };
                },
                copy(target) {
                    for (const [name, value] of values) target.setAttribute(name, value);
                    for (const value of namespacedValues.values()) {
                        target.setAttributeNS(value.namespace, value.qualified, value.value);
                    }
                    return target;
                },
                markup(method, args) {
                    if (!isReleased() && textState != null && method === 'innerHTML') {
                        textState.set(stringValue(args[0]));
                        return true;
                    }
                    return !isReleased() && typeof stageMarkup === 'function' && stageMarkup(method, args);
                },
                mutateText(method, args) {
                    if (isReleased() || textState?.target == null) return { handled: false };
                    return { handled: true, value: reflectApply(method, textState.target, args) };
                },
                read(method, args) {
                    const namespaceAware = method.endsWith('NS');
                    const namespace = namespaceAware && args[0] != null ? stringValue(args[0]) : null;
                    const name = normalized(args[namespaceAware ? 1 : 0]);
                    if (isReleased() || !shouldDefer(element, name)) return { handled: false };
                    if (typeof synchronizeAttribute === 'function') synchronizeAttribute(name);
                    let value = namespace == null || namespace.length === 0 ? values.get(name) : undefined;
                    let qualified = name;
                    if (namespace != null && namespace.length > 0) for (const staged of namespacedValues.values()) {
                        if (staged.namespace === namespace && normalized(staged.qualified.split(':').pop()) === name) {
                            value = staged.value;
                            qualified = staged.qualified;
                            break;
                        }
                    }
                    if (method.startsWith('has')) return { handled: true, value: value !== undefined };
                    if (method.includes('Node')) {
                        if (value === undefined) return { handled: true, value: null };
                        const attribute = namespace == null || namespace.length === 0
                            ? document().createAttribute(qualified)
                            : document().createAttributeNS(namespace, qualified);
                        attribute.value = value;
                        attrStates.set(attribute, {
                            get: () => {
                                if (isReleased()) return namespace == null || namespace.length === 0
                                    ? element.getAttribute(qualified) ?? ''
                                    : element.getAttributeNS(namespace, name) ?? '';
                                if (namespace == null || namespace.length === 0) return values.get(name) ?? '';
                                return namespacedValues.get(`${namespace}\0${qualified}`)?.value ?? '';
                            },
                            set: next => {
                                if (isReleased()) {
                                    if (namespace == null || namespace.length === 0) element.setAttribute(qualified, next);
                                    else element.setAttributeNS(namespace, qualified, next);
                                    return;
                                }
                                if (namespace == null || namespace.length === 0) values.set(name, next);
                                else namespacedValues.set(`${namespace}\0${qualified}`, { namespace, qualified, value: next });
                                touch();
                            }
                        });
                        return { handled: true, value: attribute };
                    }
                    return { handled: true, value: value ?? null };
                },
                setAttribute(name, value) {
                    const attribute = normalized(name);
                    if (isReleased() || !shouldDefer(element, attribute)) return false;
                    values.set(attribute, stringValue(value));
                    touch();
                    return true;
                },
                removeAttribute(name) {
                    const attribute = normalized(name);
                    if (isReleased() || !shouldDefer(element, attribute)) return false;
                    values.delete(attribute);
                    touch();
                    return true;
                },
                setAttributeNS(namespaceUri, qualifiedName, value) {
                    const namespace = namespaceUri == null ? null : stringValue(namespaceUri);
                    const qualified = stringValue(qualifiedName);
                    const attribute = normalized(qualified.split(':').pop());
                    if (isReleased() || !shouldDefer(element, attribute)) return false;
                    if (namespace == null || namespace.length === 0) values.set(attribute, stringValue(value));
                    else namespacedValues.set(`${namespace}\0${qualified}`, { namespace, qualified, value: stringValue(value) });
                    touch();
                    return true;
                },
                setAttributeNode(attribute) { return stageAttributeNode(attribute); },
                setAttributeNodeNS(attribute) { return stageAttributeNode(attribute); },
                setNamedItem(attribute) { return stageAttributeNode(attribute); },
                setNamedItemNS(attribute) { return stageAttributeNode(attribute); },
                removeAttributeNS(namespaceUri, localName) {
                    const namespace = namespaceUri == null ? null : stringValue(namespaceUri);
                    const attribute = normalized(localName);
                    if (isReleased() || !shouldDefer(element, attribute)) return false;
                    if (namespace == null || namespace.length === 0) values.delete(attribute);
                    else for (const [key, value] of namespacedValues) {
                        if (value.namespace === namespace && normalized(value.qualified.split(':').pop()) === attribute) {
                            namespacedValues.delete(key);
                        }
                    }
                    touch();
                    return true;
                },
                removeAttributeNode(attribute) { return removeAttributeNode(attribute); },
                removeNamedItem(name) { return removeNamedItem(null, name); },
                removeNamedItemNS(namespaceUri, localName) { return removeNamedItem(namespaceUri, localName); },
                toggleAttribute(name, force) {
                    const attribute = normalized(name);
                    if (isReleased() || !shouldDefer(element, attribute)) return false;
                    const enabled = arguments.length < 2 ? !values.has(attribute) : booleanValue(force);
                    if (enabled) values.set(attribute, '');
                    else values.delete(attribute);
                    touch();
                    state.result = enabled;
                    return true;
                }
            };
            const stageAttributeNode = attribute => {
                const name = normalized(attribute.localName || attribute.name);
                if (isReleased() || !shouldDefer(element, name)) return false;
                const namespace = attribute.namespaceURI == null ? null : stringValue(attribute.namespaceURI);
                const qualified = stringValue(attribute.name);
                if (namespace == null || namespace.length === 0) values.set(name, stringValue(attribute.value));
                else namespacedValues.set(`${namespace}\0${qualified}`, { namespace, qualified, value: stringValue(attribute.value) });
                touch();
                state.result = null;
                return true;
            };
            const removeAttributeNode = attribute => {
                const name = normalized(attribute.name);
                if (isReleased() || !shouldDefer(element, name)) return false;
                values.delete(name);
                touch();
                state.result = attribute;
                return true;
            };
            const removeNamedItem = (namespaceUri, localName) => {
                const namespace = namespaceUri == null ? null : stringValue(namespaceUri);
                const name = normalized(localName);
                if (isReleased() || !shouldDefer(element, name)) return false;
                if (namespace == null || namespace.length === 0) values.delete(name);
                else for (const [key, value] of namespacedValues) {
                    if (value.namespace === namespace && normalized(value.qualified.split(':').pop()) === name) namespacedValues.delete(key);
                }
                touch();
                state.result = null;
                return true;
            };
            states.set(element, state);
            states.set(element.attributes, state);
            return state;
        };
        const guardLegacyHandler = (target, property, cancellations) => {
            if (!target) return;
            const current = target[property];
            if (typeof current !== 'function') return;
            let handlers = legacyHandlers.get(target);
            if (!handlers) legacyHandlers.set(target, handlers = new Map());
            const existing = handlers.get(property);
            if (existing?.wrapper === current || existing?.source === current) return;
            const wrapper = function(...args) {
                const result = reflectApply(current, this, args);
                if (result === false && args[0]) cancellations.add(args[0]);
                return result;
            };
            handlers.set(property, { source: current, wrapper });
            target[property] = wrapper;
        };
        return {
            install,
            installNamedNodeMap,
            installNode,
            installFrame,
            installFactories,
            installAttr,
            installProperty,
            installText,
            guardLegacyHandler,
            guardInsertionTarget(target, guard) { insertionStates.set(target, guard); },
            releaseInsertionTarget(target) { insertionStates.delete(target); },
            createState,
            copy(source, target) { return states.get(source)?.copy(target) ?? target; },
            release(element) { states.delete(element.attributes); states.delete(element); }
        };
    };
    Object.defineProperty(globalThis, '__htmlTinkerXCreatePopupAttributeGuards', {
        value: createGuards,
        configurable: true
    });
})();
