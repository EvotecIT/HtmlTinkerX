(() => {
    const arrayFrom = Array.from;
    const defineProperty = Object.defineProperty;
    const getOwnPropertyDescriptor = Object.getOwnPropertyDescriptor;
    const getPrototypeOf = Object.getPrototypeOf;
    const reflectApply = Reflect.apply;
    const reflectGet = Reflect.get;
    const reflectSet = Reflect.set;
    const weakMap = WeakMap;
    const weakSet = WeakSet;
    const numberValue = Number;
    const objectCreate = Object.create;
    const openerAttachShadow = Element.prototype.attachShadow;
    const openerAnimate = Element.prototype.animate;
    const openerShadowRoot = getOwnPropertyDescriptor(Element.prototype, 'shadowRoot').get;
    const knownShadowRoots = new weakMap();
    const animationRealmStates = new weakMap();
    const customElementRealms = new weakSet();
    const shadowRealmStates = new weakMap();
    const timerRealmStates = new weakMap();
    const routedAttachShadow = function(...args) {
        const view = this.ownerDocument?.defaultView;
        const route = view == null ? null : shadowRealmStates.get(view);
        const root = route == null ? reflectApply(openerAttachShadow, this, args) : route(this, args);
        knownShadowRoots.set(this, root);
        return root;
    };
    if (typeof openerAttachShadow === 'function') defineProperty(Element.prototype, 'attachShadow', {
        value: routedAttachShadow,
        writable: false,
        configurable: false
    });
    const routedAnimate = function(...args) {
        const view = this.ownerDocument?.defaultView;
        const route = view == null ? null : animationRealmStates.get(view);
        return route == null ? reflectApply(openerAnimate, this, args) : route(this, args);
    };
    if (typeof openerAnimate === 'function') defineProperty(Element.prototype, 'animate', {
        value: routedAnimate,
        writable: false,
        configurable: false
    });
    for (const name of ['setTimeout', 'clearTimeout', 'setInterval', 'clearInterval']) {
        const nativeTimer = globalThis[name];
        const descriptor = getOwnPropertyDescriptor(Window.prototype, name);
        if (typeof nativeTimer !== 'function' || descriptor?.configurable === false) continue;
        defineProperty(Window.prototype, name, {
            ...descriptor,
            value: function(...args) {
                const route = timerRealmStates.get(this)?.get(name);
                return route == null ? reflectApply(nativeTimer, this, args) : reflectApply(route, this, args);
            },
            writable: false,
            configurable: false
        });
    }
    globalThis.__htmlTinkerXCreatePopupRealmGuards = ({
        popup,
        isReady,
        runWhenReady,
        shouldDeferAttribute,
        guardDeferredAttributes,
        guardInsertionTarget,
        releaseInsertionTarget,
        guardCreatedTree,
        guardedResources,
        stringValue
    }) => {
        const members = new Map();
        const installAnimations = target => {
            if (animationRealmStates.has(target)) return;
            const prototype = target?.Element?.prototype;
            const nativeAnimate = prototype?.animate;
            if (typeof nativeAnimate !== 'function') return;
            const animate = (receiver, args) => {
                    if (isReady()) return reflectApply(nativeAnimate, receiver, args);
                    const stagingElement = target.document.createElement(receiver.localName || 'div');
                    const staged = reflectApply(nativeAnimate, stagingElement, args);
                    let current = staged;
                    const facade = new Proxy(objectCreate(getPrototypeOf(staged)), {
                        get(_, property) {
                            const value = reflectGet(current, property, current);
                            return typeof value === 'function' ? (...values) => reflectApply(value, current, values) : value;
                        },
                        set(_, property, value) { return reflectSet(current, property, value, current); }
                    });
                    runWhenReady(() => {
                        const keyframes = staged.effect?.getKeyframes?.() ?? args[0];
                        const timing = staged.effect?.getTiming?.() ?? args[1];
                        const playState = staged.playState;
                        const currentTime = staged.currentTime;
                        const playbackRate = staged.playbackRate;
                        const handlers = [staged.oncancel, staged.onfinish, staged.onremove];
                        current = reflectApply(nativeAnimate, receiver, [keyframes, timing]);
                        current.playbackRate = playbackRate;
                        if (currentTime != null) current.currentTime = currentTime;
                        [current.oncancel, current.onfinish, current.onremove] = handlers;
                        if (playState === 'idle') current.cancel();
                        else if (playState === 'paused') current.pause();
                        else if (playState === 'finished') current.finish();
                    });
                    return facade;
                };
            animationRealmStates.set(target, animate);
            defineProperty(prototype, 'animate', {
                configurable: false,
                writable: false,
                value: routedAnimate
            });
        };
        const installCustomElements = target => {
            if (customElementRealms.has(target)) return;
            const registry = target?.customElements;
            const prototype = target?.CustomElementRegistry?.prototype;
            const nativeDefine = prototype?.define;
            if (registry == null || typeof nativeDefine !== 'function') return;
            customElementRealms.add(target);
            const pending = new Map();
            const nativeGet = prototype.get;
            const nativeGetName = prototype.getName;
            const nativeWhenDefined = prototype.whenDefined;
            defineProperty(prototype, 'define', {
                configurable: false,
                writable: false,
                value(name, constructor, options) {
                    if (isReady()) return reflectApply(nativeDefine, this, arguments);
                    const normalized = stringValue(name);
                    if (pending.has(normalized) || reflectApply(nativeGet, this, [normalized]) != null) throw new target.DOMException(`the name "${normalized}" has already been used`, 'NotSupportedError');
                    let resolveDefinition;
                    const completion = new Promise(resolve => { resolveDefinition = resolve; });
                    pending.set(normalized, { constructor, options, completion });
                    runWhenReady(() => { const definition = pending.get(normalized); pending.delete(normalized); reflectApply(nativeDefine, registry, [normalized, definition.constructor, definition.options]); resolveDefinition(definition.constructor); });
                }
            });
            defineProperty(prototype, 'get', {
                configurable: false,
                writable: false,
                value(name) { return pending.get(stringValue(name))?.constructor ?? reflectApply(nativeGet, this, arguments); }
            });
            if (typeof nativeGetName === 'function') defineProperty(prototype, 'getName', {
                configurable: false,
                writable: false,
                value(constructor) { for (const [name, definition] of pending) if (definition.constructor === constructor) return name; return reflectApply(nativeGetName, this, arguments); }
            });
            defineProperty(prototype, 'whenDefined', {
                configurable: false,
                writable: false,
                value(name) { const normalized = stringValue(name); return pending.get(normalized)?.completion ?? reflectApply(nativeWhenDefined, this, [normalized]); }
            });
        };
        const installTimers = () => {
            const pending = new Map();
            let nextIdentifier = -1;
            const install = (setName, clearName, repeating) => {
                const nativeSet = popup[setName];
                const nativeClear = popup[clearName];
                if (typeof nativeSet !== 'function' || typeof nativeClear !== 'function') return;
                const stagedSet = function(handler, delay, ...args) {
                    if (arguments.length === 0) throw new TypeError(`Failed to execute '${setName}': 1 argument required`);
                    if (isReady()) return reflectApply(nativeSet, popup, [handler, delay, ...args]);
                    const normalizedHandler = typeof handler === 'function' ? handler : stringValue(handler);
                    const normalizedDelay = delay === undefined ? 0 : numberValue(delay);
                    const identifier = nextIdentifier--;
                    const state = { actual: null, cancelled: false };
                    pending.set(identifier, state);
                    runWhenReady(() => {
                        if (state.cancelled) return;
                        const invoke = typeof normalizedHandler === 'function'
                            ? callbackArgs => reflectApply(normalizedHandler, popup, callbackArgs)
                            : () => reflectApply(popup.eval, popup, [normalizedHandler]);
                        const scheduledHandler = repeating
                            ? (...callbackArgs) => invoke(callbackArgs)
                            : (...callbackArgs) => { pending.delete(identifier); return invoke(callbackArgs); };
                        state.actual = reflectApply(nativeSet, popup, [scheduledHandler, normalizedDelay, ...args]);
                    });
                    return identifier;
                };
                const stagedClear = function(identifier) {
                    const normalizedIdentifier = identifier === undefined ? 0 : identifier >> 0;
                    const state = pending.get(normalizedIdentifier);
                    if (state == null) return reflectApply(nativeClear, popup, [normalizedIdentifier]);
                    state.cancelled = true;
                    pending.delete(normalizedIdentifier);
                    if (state.actual != null) reflectApply(nativeClear, popup, [state.actual]);
                };
                members.set(setName, stagedSet);
                members.set(clearName, stagedClear);
                for (const [name, value] of [[setName, stagedSet], [clearName, stagedClear]]) {
                    const descriptor = getOwnPropertyDescriptor(popup.Window.prototype, name);
                    if (descriptor?.configurable !== false) defineProperty(popup.Window.prototype, name, {
                        ...descriptor,
                        value,
                        writable: false,
                        configurable: false
                    });
                    const ownDescriptor = getOwnPropertyDescriptor(popup, name);
                    if (ownDescriptor?.configurable !== false) defineProperty(popup, name, {
                        value,
                        writable: false,
                        configurable: false
                    });
                }
            };
            install('setTimeout', 'clearTimeout', false);
            install('setInterval', 'clearInterval', true);
            timerRealmStates.set(popup, members);
        };

        const installShadowRoots = target => {
            if (typeof target?.ShadowRoot !== 'function' || shadowRealmStates.has(target)) return;
            const attachShadow = target.Element.prototype.attachShadow;
            const innerHtml = getOwnPropertyDescriptor(target.ShadowRoot.prototype, 'innerHTML');
            const setHtml = target.ShadowRoot.prototype.setHTML;
            const setHtmlUnsafe = target.ShadowRoot.prototype.setHTMLUnsafe;
            const nodeType = getOwnPropertyDescriptor(target.Node.prototype, 'nodeType').get;
            const elementQuerySelectorAll = target.Element.prototype.querySelectorAll;
            const elementGetAttribute = target.Element.prototype.getAttribute;
            const fragmentQuerySelectorAll = target.DocumentFragment.prototype.querySelectorAll;
            const elementShadowRoot = getOwnPropertyDescriptor(target.Element.prototype, 'shadowRoot').get;
            const templateContent = getOwnPropertyDescriptor(target.HTMLTemplateElement.prototype, 'content').get;
            const elementsOf = root => {
                const elements = [];
                const visit = current => {
                    const method = reflectApply(nodeType, current, []) === 1 ? elementQuerySelectorAll : fragmentQuerySelectorAll;
                    for (const descendant of reflectApply(method, current, ['*'])) {
                        elements.push(descendant);
                        if (descendant.localName === 'template') visit(templateContent.call(descendant));
                        const shadow = elementShadowRoot.call(descendant); if (shadow != null) visit(shadow);
                    }
                };
                visit(root);
                return elements;
            };
            const findMarker = (root, marker) => {
                for (const descendant of elementsOf(root)) if (reflectApply(elementGetAttribute, descendant, ['data-htmltinkerx-staged-resource']) === marker) return descendant;
                return null;
            };
            let adoptedOwner = target.ShadowRoot.prototype;
            let adopted = null;
            while (adoptedOwner && adopted == null) {
                adopted = getOwnPropertyDescriptor(adoptedOwner, 'adoptedStyleSheets');
                adoptedOwner = getPrototypeOf(adoptedOwner);
            }
            const states = new weakMap();
            const stageMarkup = (root, markup, method = 'innerHTML', methodArgs = [markup]) => {
                const template = target.document.createElement('template');
                template.innerHTML = stringValue(markup);
                const descriptors = [];
                let markerIndex = 0;
                for (const descendant of elementsOf(templateContent.call(template))) {
                    const values = [];
                    const namespacedValues = [];
                    for (const attribute of arrayFrom(descendant.attributes)) {
                        const name = attribute.localName.toLowerCase();
                        if (!shouldDeferAttribute(descendant, name)) continue;
                        if (attribute.namespaceURI == null) values.push([attribute.name.toLowerCase(), attribute.value]);
                        else namespacedValues.push({ namespace: attribute.namespaceURI, qualified: attribute.name, value: attribute.value });
                        if (attribute.namespaceURI == null) descendant.removeAttribute(attribute.name);
                        else descendant.removeAttributeNS(attribute.namespaceURI, attribute.localName);
                    }
                    const styleText = descendant.localName === 'style' ? descendant.textContent : null;
                    if (styleText !== null) descendant.textContent = '';
                    const marker = `htmltinkerx-shadow-${Date.now()}-${markerIndex++}-${Math.random().toString(36).slice(2)}`;
                    descendant.setAttribute('data-htmltinkerx-staged-resource', marker);
                    descriptors.push({ marker, values, namespacedValues, styleText });
                }
                if (method === 'setHTML' || method === 'setHTMLUnsafe') {
                    const invocationArgs = arrayFrom(methodArgs);
                    invocationArgs[0] = template.innerHTML;
                    reflectApply(method === 'setHTML' ? setHtml : setHtmlUnsafe, root, invocationArgs);
                }
                else innerHtml.set.call(root, template.innerHTML);
                for (const { marker, values, namespacedValues, styleText } of descriptors) {
                    const descendant = findMarker(root, marker);
                    if (!descendant) continue;
                    descendant.removeAttribute('data-htmltinkerx-staged-resource');
                    guardDeferredAttributes(descendant, values, namespacedValues);
                    if (styleText !== null) guardedResources.push(() => { descendant.textContent = styleText; });
                }
            };
            if (innerHtml?.get && innerHtml?.set && innerHtml.configurable !== false) {
                defineProperty(target.ShadowRoot.prototype, 'innerHTML', {
                    ...innerHtml,
                    configurable: false,
                    get() { return innerHtml.get.call(this); },
                    set(value) {
                        if (states.has(this) && !isReady()) stageMarkup(this, value);
                        else innerHtml.set.call(this, value);
                    }
                });
            }
            for (const [name, method] of [['setHTML', setHtml], ['setHTMLUnsafe', setHtmlUnsafe]]) {
                if (typeof method === 'function') defineProperty(target.ShadowRoot.prototype, name, {
                    configurable: false,
                    writable: false,
                    value(...args) {
                        if (args.length === 0) return reflectApply(method, this, args);
                        if (states.has(this) && !isReady()) {
                            stageMarkup(this, args[0], name, args);
                            return undefined;
                        }
                        return reflectApply(method, this, args);
                    }
                });
            }
            if (adopted?.get && adopted?.set && adopted.configurable !== false) {
                defineProperty(target.ShadowRoot.prototype, 'adoptedStyleSheets', {
                    ...adopted,
                    configurable: false,
                    get() {
                        const state = states.get(this);
                        return state?.sheets ?? adopted.get.call(this);
                    },
                    set(value) {
                        const sheets = arrayFrom(value);
                        const state = states.get(this);
                        if (state != null && !isReady()) state.sheets = sheets;
                        else adopted.set.call(this, sheets);
                    }
                });
            }
            const attach = (receiver, args) => {
                    const root = reflectApply(attachShadow, receiver, args);
                    if (!isReady()) {
                        const state = { sheets: null };
                        states.set(root, state);
                        guardInsertionTarget(root, (method, values) => {
                            if (method === 'insertAdjacentElement') { if (values.length > 1) guardCreatedTree(values[1]); return; }
                            if (method === 'appendChild' || method === 'insertBefore' || method === 'replaceChild') { if (values.length > 0) guardCreatedTree(values[0]); return; }
                            if (['append', 'prepend', 'replaceChildren', 'after', 'before', 'replaceWith'].includes(method)) for (const value of values) guardCreatedTree(value);
                        });
                        guardedResources.push(() => {
                            states.delete(root);
                            releaseInsertionTarget(root);
                            if (state.sheets != null && adopted?.set) adopted.set.call(root, state.sheets);
                        });
                    }
                    return root;
                };
            shadowRealmStates.set(target, attach);
            if (typeof attachShadow === 'function') defineProperty(target.Element.prototype, 'attachShadow', {
                value: routedAttachShadow,
                writable: false,
                configurable: false
            });
        };

        installTimers();
        installShadowRoots(popup);
        installAnimations(popup);
        installCustomElements(popup);
        members.registerFacade = facade => timerRealmStates.set(facade, members);
        members.guardShadowRealm = target => { installShadowRoots(target); installAnimations(target); installCustomElements(target); };
        members.shadowRootFor = element => knownShadowRoots.get(element) ?? reflectApply(openerShadowRoot, element, []);
        return members;
    };
})();
