(() => {
    const arrayFrom = Array.from;
    const defineProperty = Object.defineProperty;
    const getOwnPropertyDescriptor = Object.getOwnPropertyDescriptor;
    const getPrototypeOf = Object.getPrototypeOf;
    const reflectApply = Reflect.apply;
    const reflectGet = Reflect.get;
    const reflectSet = Reflect.set;
    const weakMap = WeakMap;
    const numberValue = Number;
    const openerAttachShadow = Element.prototype.attachShadow;
    const shadowRealmStates = new weakMap();
    const timerRealmStates = new weakMap();
    const routedAttachShadow = function(...args) {
        const view = this.ownerDocument?.defaultView;
        const route = view == null ? null : shadowRealmStates.get(view);
        return route == null ? reflectApply(openerAttachShadow, this, args) : route(this, args);
    };
    if (typeof openerAttachShadow === 'function') defineProperty(Element.prototype, 'attachShadow', {
        value: routedAttachShadow,
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
        guardedResources,
        stringValue
    }) => {
        const members = new Map();
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
            const setHtmlUnsafe = target.ShadowRoot.prototype.setHTMLUnsafe;
            let adoptedOwner = target.ShadowRoot.prototype;
            let adopted = null;
            while (adoptedOwner && adopted == null) {
                adopted = getOwnPropertyDescriptor(adoptedOwner, 'adoptedStyleSheets');
                adoptedOwner = getPrototypeOf(adoptedOwner);
            }
            const states = new weakMap();
            const stageMarkup = (root, markup, method = 'innerHTML') => {
                const template = target.document.createElement('template');
                template.innerHTML = stringValue(markup);
                const descriptors = [];
                let markerIndex = 0;
                for (const descendant of template.content.querySelectorAll('*')) {
                    const values = [];
                    for (const attribute of arrayFrom(descendant.attributes)) {
                        const name = attribute.name.toLowerCase();
                        if (!shouldDeferAttribute(descendant, name)) continue;
                        values.push([name, attribute.value]);
                        descendant.removeAttribute(attribute.name);
                    }
                    const styleText = descendant.localName === 'style' ? descendant.textContent : null;
                    if (styleText !== null) descendant.textContent = '';
                    const marker = `htmltinkerx-shadow-${Date.now()}-${markerIndex++}-${Math.random().toString(36).slice(2)}`;
                    descendant.setAttribute('data-htmltinkerx-staged-resource', marker);
                    descriptors.push({ marker, values, styleText });
                }
                if (method === 'setHTMLUnsafe') reflectApply(setHtmlUnsafe, root, [template.innerHTML]);
                else innerHtml.set.call(root, template.innerHTML);
                for (const { marker, values, styleText } of descriptors) {
                    const descendant = root.querySelector(`[data-htmltinkerx-staged-resource="${marker}"]`);
                    if (!descendant) continue;
                    descendant.removeAttribute('data-htmltinkerx-staged-resource');
                    guardDeferredAttributes(descendant, values);
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
            if (typeof setHtmlUnsafe === 'function') defineProperty(target.ShadowRoot.prototype, 'setHTMLUnsafe', {
                configurable: false,
                writable: false,
                value(...args) {
                    if (args.length === 0) return reflectApply(setHtmlUnsafe, this, args);
                    if (states.has(this) && !isReady()) {
                        stageMarkup(this, args[0], 'setHTMLUnsafe');
                        return undefined;
                    }
                    return reflectApply(setHtmlUnsafe, this, args);
                }
            });
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
                        guardedResources.push(() => {
                            states.delete(root);
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
        members.registerFacade = facade => timerRealmStates.set(facade, members);
        members.guardShadowRealm = installShadowRoots;
        return members;
    };
})();
