(() => {
    const bind = Function.prototype.bind;
    const defineProperty = Object.defineProperty;
    const reflectApply = Reflect.apply;
    const reflectConstruct = Reflect.construct;
    const reflectGet = Reflect.get;
    const weakMap = WeakMap;

    const createCodeGuards = ({ popup, isReady, runWhenReady, stringValue }) => {
        const realms = new weakMap();
        const install = target => {
            const existing = realms.get(target);
            if (existing != null) return existing;
            const members = new Map();
            realms.set(target, members);
            const nativeEval = target.eval;
            if (typeof nativeEval === 'function') members.set('eval', value => {
                if (typeof value !== 'string' || isReady()) return reflectApply(nativeEval, target, [value]);
                runWhenReady(() => reflectApply(nativeEval, target, [value]));
                return undefined;
            });
            const nativeFunction = target.Function;
            if (typeof nativeFunction === 'function') {
                const stageFunction = compiled => new Proxy(compiled, {
                    apply(current, thisArg, args) {
                        if (isReady()) return reflectApply(current, thisArg, args);
                        const snapshot = args.slice();
                        runWhenReady(() => reflectApply(current, thisArg, snapshot));
                        return undefined;
                    },
                    construct(current, args, newTarget) {
                        if (!isReady()) throw new target.DOMException('Constructing a staged function before popup interception is ready is not supported.', 'NotSupportedError');
                        return reflectConstruct(current, args, newTarget);
                    }
                });
                const stagedTarget = reflectApply(nativeFunction, target, []);
                let stagedFunction;
                stagedFunction = new Proxy(stagedTarget, {
                    apply(_, thisArg, args) { return stageFunction(reflectApply(nativeFunction, thisArg, args)); },
                    construct(_, args) { return stageFunction(reflectConstruct(nativeFunction, args)); }
                });
                defineProperty(stagedTarget.prototype, 'constructor', { value: stagedFunction, writable: false, configurable: false });
                members.set('Function', stagedFunction);
            }
            const timers = new Map();
            let nextIdentifier = -1;
            const installTimer = (setName, clearName, repeating) => {
                const nativeSet = target[setName];
                const nativeClear = target[clearName];
                if (typeof nativeSet !== 'function' || typeof nativeClear !== 'function') return;
                members.set(setName, function(handler, delay, ...args) {
                    if (arguments.length === 0) throw new TypeError(`Failed to execute '${setName}': 1 argument required`);
                    if (isReady()) return reflectApply(nativeSet, target, [handler, delay, ...args]);
                    const normalizedHandler = typeof handler === 'function' ? handler : stringValue(handler);
                    const identifier = nextIdentifier--;
                    const state = { actual: null, cancelled: false };
                    timers.set(identifier, state);
                    runWhenReady(() => {
                        if (state.cancelled) return;
                        const invoke = typeof normalizedHandler === 'function'
                            ? callbackArgs => reflectApply(normalizedHandler, target, callbackArgs)
                            : () => reflectApply(nativeEval, target, [normalizedHandler]);
                        const scheduled = repeating
                            ? (...callbackArgs) => invoke(callbackArgs)
                            : (...callbackArgs) => { timers.delete(identifier); return invoke(callbackArgs); };
                        state.actual = reflectApply(nativeSet, target, [scheduled, Number(delay) || 0, ...args]);
                    });
                    return identifier;
                });
                members.set(clearName, identifier => {
                    const normalized = identifier === undefined ? 0 : identifier >> 0;
                    const state = timers.get(normalized);
                    if (state == null) return reflectApply(nativeClear, target, [normalized]);
                    state.cancelled = true;
                    timers.delete(normalized);
                    if (state.actual != null) reflectApply(nativeClear, target, [state.actual]);
                });
            };
            installTimer('setTimeout', 'clearTimeout', false);
            installTimer('setInterval', 'clearInterval', true);
            const callbacks = new Map();
            const installCallback = (requestName, cancelName) => {
                const nativeRequest = target[requestName];
                const nativeCancel = target[cancelName];
                if (typeof nativeRequest !== 'function') return;
                members.set(requestName, callback => {
                    if (typeof callback !== 'function') return reflectApply(nativeRequest, target, [callback]);
                    if (isReady()) return reflectApply(nativeRequest, target, [callback]);
                    const identifier = nextIdentifier--;
                    const state = { actual: null, cancelled: false };
                    callbacks.set(identifier, state);
                    runWhenReady(() => {
                        if (state.cancelled) return;
                        state.actual = reflectApply(nativeRequest, target, [(...args) => {
                            callbacks.delete(identifier);
                            return reflectApply(callback, target, args);
                        }]);
                    });
                    return identifier;
                });
                if (typeof nativeCancel === 'function') members.set(cancelName, identifier => {
                    const normalized = identifier === undefined ? 0 : identifier >> 0;
                    const state = callbacks.get(normalized);
                    if (state == null) return reflectApply(nativeCancel, target, [normalized]);
                    state.cancelled = true;
                    callbacks.delete(normalized);
                    if (state.actual != null) reflectApply(nativeCancel, target, [state.actual]);
                });
            };
            installCallback('requestAnimationFrame', 'cancelAnimationFrame');
            installCallback('requestIdleCallback', 'cancelIdleCallback');
            const nativeQueueMicrotask = target.queueMicrotask;
            if (typeof nativeQueueMicrotask === 'function') members.set('queueMicrotask', callback => {
                if (typeof callback !== 'function') return reflectApply(nativeQueueMicrotask, target, [callback]);
                runWhenReady(() => reflectApply(nativeQueueMicrotask, target, [reflectApply(bind, callback, [target])]));
            });
            return members;
        };
        return { forWindow: install };
    };
    defineProperty(globalThis, '__htmlTinkerXCreatePopupCodeGuards', { value: createCodeGuards, configurable: true });
})();
