(() => {
    const createGuards = ({ defineProperty, reflectApply, stringValue, booleanValue }) => {
        const states = new WeakMap();
        const prototypes = new WeakSet();
        const install = prototype => {
            if (!prototype || prototypes.has(prototype)) return;
            prototypes.add(prototype);
            for (const name of ['setAttribute', 'setAttributeNS', 'removeAttribute', 'removeAttributeNS', 'toggleAttribute']) {
                const method = prototype[name];
                if (typeof method !== 'function') continue;
                defineProperty(prototype, name, {
                    configurable: false,
                    writable: false,
                    value(...args) {
                        const state = states.get(this);
                        if (state && state[name](...args)) return name === 'toggleAttribute' ? state.result : undefined;
                        return reflectApply(method, this, args);
                    }
                });
            }
        };
        const createState = (element, values, namespacedValues, isReleased, shouldDefer) => {
            const normalized = name => stringValue(name).toLowerCase();
            const state = {
                result: undefined,
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
            states.set(element, state);
            return state;
        };
        return { install, createState, release: element => states.delete(element) };
    };
    Object.defineProperty(globalThis, '__htmlTinkerXCreatePopupAttributeGuards', {
        value: createGuards,
        configurable: true
    });
})();
