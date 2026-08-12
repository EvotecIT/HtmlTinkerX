(() => {
    globalThis.__htmlTinkerXCreatePopupAsyncConstructors = ({
        defineProperty,
        getOwnPropertyDescriptor,
        reflectApply,
        reflectConstruct,
        reflectGet,
        reflectSet
    }) => ({ popup, runWhenReady, normalizeArguments, normalizeOperation }) => {
        const constructors = new Map();
        const stage = (name, handlerNames, operationNames, stopName) => {
            const nativeConstructor = popup[name];
            if (typeof nativeConstructor !== 'function') return;
            const deferredInstance = (constructor, args) => {
                const normalizedArgs = normalizeArguments(name, args);
                let instance = null;
                let stopped = false;
                const handlers = new Map();
                const listeners = [];
                const callbackWrappers = new WeakMap();
                const pending = [];
                const handlerProperties = new Set(handlerNames);
                const operationProperties = new Set(operationNames);
                const target = {};
                let facade;
                const captureFlag = options => typeof options === 'boolean' ? options : Boolean(options?.capture);
                const listenerOptions = options => typeof options === 'boolean'
                    ? options
                    : options == null ? false : {
                        capture: Boolean(options.capture),
                        once: Boolean(options.once),
                        passive: Boolean(options.passive),
                        signal: options.signal
                    };
                const wrapCallback = callback => {
                    const callable = typeof callback === 'function';
                    if (!callable && (callback == null || typeof callback.handleEvent !== 'function')) return callback;
                    let wrapper = callbackWrappers.get(callback);
                    if (!wrapper) {
                        wrapper = event => {
                            const stagedEvent = new Proxy(event, {
                            get(current, property) {
                                const value = reflectGet(current, property, current);
                                if ((property === 'target' || property === 'currentTarget') && value === instance) return facade;
                                return typeof value === 'function' ? value.bind(current) : value;
                            }
                            });
                            return callable
                                ? reflectApply(callback, facade, [stagedEvent])
                                : reflectApply(callback.handleEvent, callback, [stagedEvent]);
                        };
                        callbackWrappers.set(callback, wrapper);
                    }
                    return wrapper;
                };
                facade = new Proxy(target, {
                    get(_, property) {
                        if (property === 'addEventListener') {
                            return (type, listener, options) => {
                                if (instance != null) return instance.addEventListener(type, wrapCallback(listener), options);
                                const normalizedOptions = listenerOptions(options);
                                listeners.push({ type, listener, options: normalizedOptions, capture: captureFlag(normalizedOptions) });
                            };
                        }
                        if (property === 'removeEventListener') {
                            return (type, listener, options) => {
                                if (instance != null) return instance.removeEventListener(type, wrapCallback(listener), options);
                                const capture = captureFlag(options);
                                for (let index = listeners.length - 1; index >= 0; index--) {
                                    const current = listeners[index];
                                    if (current.type === type && current.listener === listener && current.capture === capture) listeners.splice(index, 1);
                                }
                            };
                        }
                        if (instance != null) {
                            const value = reflectGet(instance, property, instance);
                            return typeof value === 'function' ? value.bind(instance) : value;
                        }
                        if (property === Symbol.toStringTag) return name;
                        if (name === 'EventSource' && property === 'readyState') {
                            return stopped ? nativeConstructor.CLOSED : nativeConstructor.CONNECTING;
                        }
                        if (name === 'EventSource' && property === 'url') return normalizedArgs[0];
                        if (name === 'EventSource' && property === 'withCredentials') return normalizedArgs[1].withCredentials;
                        if (name === 'EventSource' && ['CONNECTING', 'OPEN', 'CLOSED'].includes(property)) return constructor[property];
                        if (handlerProperties.has(property)) return handlers.get(property) ?? null;
                        if (operationProperties.has(property)) {
                            return (...operationArgs) => {
                                const normalizedOperationArgs = typeof normalizeOperation === 'function'
                                    ? normalizeOperation(name, property, operationArgs)
                                    : operationArgs;
                                pending.push(current => {
                                    const operation = reflectGet(current, property, current);
                                    reflectApply(operation, current, normalizedOperationArgs);
                                });
                            };
                        }
                        if (property === stopName) {
                            return () => {
                                stopped = true;
                                pending.length = 0;
                            };
                        }
                        return reflectGet(target, property, target);
                    },
                    set(_, property, value) {
                        if (handlerProperties.has(property)) {
                            handlers.set(property, value);
                            if (instance != null) reflectSet(instance, property, wrapCallback(value), instance);
                            return true;
                        }
                        return instance != null
                            ? reflectSet(instance, property, value, instance)
                            : reflectSet(target, property, value, target);
                    },
                    getPrototypeOf() {
                        return constructor.prototype;
                    }
                });
                runWhenReady(() => {
                    if (stopped) return;
                    instance = reflectConstruct(constructor, normalizedArgs);
                    for (const [property, value] of handlers) reflectSet(instance, property, wrapCallback(value), instance);
                    for (const { type, listener, options } of listeners) {
                        reflectApply(reflectGet(instance, 'addEventListener', instance), instance, [type, wrapCallback(listener), options]);
                    }
                    while (pending.length > 0) pending.shift()(instance);
                });
                return facade;
            };
            const stagedConstructor = new Proxy(nativeConstructor, {
                construct(target, args) {
                    return deferredInstance(target, args);
                }
            });
            defineProperty(nativeConstructor.prototype, 'constructor', {
                value: stagedConstructor,
                writable: false,
                configurable: false
            });
            defineProperty(popup.Window.prototype, name, {
                value: stagedConstructor,
                writable: false,
                configurable: false
            });
            defineProperty(popup, name, {
                value: stagedConstructor,
                writable: false,
                configurable: false
            });
            constructors.set(name, stagedConstructor);
        };
        const stageFontFace = () => {
            const nativeConstructor = popup.FontFace;
            if (typeof nativeConstructor !== 'function') return;
            const nativeLoad = nativeConstructor.prototype.load;
            if (typeof nativeLoad !== 'function') return;
            const fontStates = new WeakMap();
            const load = font => {
                const state = fontStates.get(font);
                if (state == null) return reflectApply(nativeLoad, font, []);
                if (state.loadPromise == null) state.loadPromise = new popup.Promise((resolve, reject) => runWhenReady(() => {
                    try { reflectApply(nativeLoad, font, []).then(resolve, reject); }
                    catch (error) { reject(error); }
                }));
                return state.loadPromise;
            };
            const stagedConstructor = new Proxy(nativeConstructor, {
                construct(target, args, newTarget) {
                    const font = reflectConstruct(target, args, newTarget === stagedConstructor ? target : newTarget);
                    fontStates.set(font, { loadPromise: null });
                    defineProperty(font, 'load', { value: () => load(font), writable: false, configurable: false });
                    return font;
                }
            });
            const loadDescriptor = getOwnPropertyDescriptor(nativeConstructor.prototype, 'load');
            if (loadDescriptor?.configurable !== false) defineProperty(nativeConstructor.prototype, 'load', {
                ...loadDescriptor,
                value() { return load(this); },
                writable: false,
                configurable: false
            });
            const constructorDescriptor = getOwnPropertyDescriptor(nativeConstructor.prototype, 'constructor');
            if (constructorDescriptor?.configurable !== false) defineProperty(nativeConstructor.prototype, 'constructor', {
                ...constructorDescriptor,
                value: stagedConstructor,
                writable: false,
                configurable: false
            });
            defineProperty(popup.Window.prototype, 'FontFace', { value: stagedConstructor, writable: false, configurable: false });
            defineProperty(popup, 'FontFace', { value: stagedConstructor, writable: false, configurable: false });
            constructors.set('FontFace', stagedConstructor);
        };
        stage('Worker', ['onerror', 'onmessage', 'onmessageerror'], ['postMessage'], 'terminate');
        stage('EventSource', ['onerror', 'onmessage', 'onopen'], [], 'close');
        stageFontFace();
        return constructors;
    };
})();
