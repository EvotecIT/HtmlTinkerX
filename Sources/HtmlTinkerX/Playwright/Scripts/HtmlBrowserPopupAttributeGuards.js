(() => {
    const createGuards = ({ defineProperty, getOwnPropertyDescriptor, reflectApply, stringValue, booleanValue }) => {
        const states = new WeakMap();
        const prototypes = new WeakSet();
        const propertyGuards = new WeakMap();
        const legacyHandlers = new WeakMap();
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
            defineProperty(prototype, 'ownerDocument', {
                ...ownerDocument,
                get() { return states.get(this)?.document() ?? ownerDocument.get.call(this); }
            });
            defineProperty(prototype, 'getRootNode', {
                configurable: false,
                writable: false,
                value(...args) { return states.get(this)?.document() ?? reflectApply(getRootNode, this, args); }
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
        const createState = (element, values, namespacedValues, isReleased, shouldDefer, document) => {
            const normalized = name => stringValue(name).toLowerCase();
            const state = {
                result: undefined,
                document,
                setAttribute(name, value) {
                    const attribute = normalized(name);
                    if (isReleased() || !shouldDefer(element, attribute)) return false;
                    values.set(attribute, stringValue(value));
                    return true;
                },
                removeAttribute(name) {
                    const attribute = normalized(name);
                    if (isReleased() || !shouldDefer(element, attribute)) return false;
                    values.delete(attribute);
                    return true;
                },
                setAttributeNS(namespaceUri, qualifiedName, value) {
                    const namespace = namespaceUri == null ? null : stringValue(namespaceUri);
                    const qualified = stringValue(qualifiedName);
                    const attribute = normalized(qualified.split(':').pop());
                    if (isReleased() || !shouldDefer(element, attribute)) return false;
                    if (namespace == null || namespace.length === 0) values.set(attribute, stringValue(value));
                    else namespacedValues.set(`${namespace}\0${qualified}`, { namespace, qualified, value: stringValue(value) });
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
                state.result = null;
                return true;
            };
            const removeAttributeNode = attribute => {
                const name = normalized(attribute.name);
                if (isReleased() || !shouldDefer(element, name)) return false;
                values.delete(name);
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
            installProperty,
            guardLegacyHandler,
            createState,
            release(element) { states.delete(element.attributes); states.delete(element); }
        };
    };
    Object.defineProperty(globalThis, '__htmlTinkerXCreatePopupAttributeGuards', {
        value: createGuards,
        configurable: true
    });
})();
