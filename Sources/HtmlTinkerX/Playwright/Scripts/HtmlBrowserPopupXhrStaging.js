(() => {
    const defineProperty = Object.defineProperty;
    const getOwnPropertyDescriptor = Object.getOwnPropertyDescriptor;
    const reflectApply = Reflect.apply;
    const reflectConstruct = Reflect.construct;
    const proxy = Proxy;
    const weakMap = WeakMap;
    const operationsByRequest = new weakMap();

    const installRoutes = (prototype, methods, withCredentials) => {
        for (const [name, fallback] of methods) {
            defineProperty(prototype, name, {
                value: function(...args) {
                    const operations = operationsByRequest.get(this);
                    return operations == null
                        ? reflectApply(fallback, this, args)
                        : operations[name](this, args);
                },
                writable: false,
                configurable: false
            });
        }
        if (withCredentials?.get && withCredentials?.set) {
            defineProperty(prototype, 'withCredentials', {
                ...withCredentials,
                get() { return reflectApply(withCredentials.get, this, []); },
                set(value) {
                    const operations = operationsByRequest.get(this);
                    if (operations == null) reflectApply(withCredentials.set, this, [value]);
                    else operations.setWithCredentials(this, value);
                },
                configurable: false
            });
        }
    };

    const openerPrototype = XMLHttpRequest.prototype;
    installRoutes(openerPrototype, [
        ['open', openerPrototype.open],
        ['send', openerPrototype.send],
        ['abort', openerPrototype.abort],
        ['setRequestHeader', openerPrototype.setRequestHeader]
    ], getOwnPropertyDescriptor(openerPrototype, 'withCredentials'));

    globalThis.__htmlTinkerXCreatePopupXhrStager = ({ popup, runWhenReady, snapshotBodyArguments, normalizeUrl }) => {
        const constructors = new weakMap();
        const forWindow = targetWindow => {
            const existing = constructors.get(targetWindow);
            if (existing != null) return existing;
            const prototype = targetWindow.XMLHttpRequest.prototype;
            const nativeOpen = prototype.open;
            const nativeSend = prototype.send;
            const nativeAbort = prototype.abort;
            const nativeSetRequestHeader = prototype.setRequestHeader;
            const nativeReadyState = getOwnPropertyDescriptor(prototype, 'readyState').get;
            const nativeWithCredentials = getOwnPropertyDescriptor(prototype, 'withCredentials');
            const nativeDispatchEvent = targetWindow.EventTarget.prototype.dispatchEvent;
            const progressEvent = targetWindow.ProgressEvent;
            const opened = targetWindow.XMLHttpRequest.OPENED;
            const pendingSends = new weakMap();
            const domException = targetWindow.DOMException;
            const constructor = targetWindow.XMLHttpRequest;
            const cancelPending = request => {
                const pending = pendingSends.get(request);
                if (pending == null) return false;
                pending.aborted = true;
                pendingSends.delete(request);
                return true;
            };
            const throwInvalidState = () => { throw new domException('The object is in an invalid state.', 'InvalidStateError'); };
            const operations = {
                open(request, args) {
                    if (args.length >= 3 && args[2] !== undefined && !args[2]) {
                        throw new domException('Synchronous XMLHttpRequest is not supported while popup requests are staged.', 'NotSupportedError');
                    }
                    const normalized = args.slice();
                    if (normalized.length > 1) normalized[1] = normalizeUrl(normalized[1], targetWindow.document);
                    reflectApply(nativeOpen, reflectConstruct(constructor, []), normalized);
                    cancelPending(request);
                    return reflectApply(nativeOpen, request, normalized);
                },
                send(request, args) {
                    if (reflectApply(nativeReadyState, request, []) !== opened || pendingSends.has(request)) throwInvalidState();
                    const pending = { aborted: false, args: snapshotBodyArguments(args) };
                    pendingSends.set(request, pending);
                    runWhenReady(() => {
                        if (pending.aborted) return;
                        pendingSends.delete(request);
                        reflectApply(nativeSend, request, pending.args);
                    });
                },
                abort(request, args) {
                    const stagedSend = cancelPending(request);
                    const result = reflectApply(nativeAbort, request, args);
                    if (stagedSend) {
                        reflectApply(nativeDispatchEvent, request, [reflectConstruct(progressEvent, ['abort'])]);
                        reflectApply(nativeDispatchEvent, request, [reflectConstruct(progressEvent, ['loadend'])]);
                    }
                    return result;
                },
                setRequestHeader(request, args) {
                    if (pendingSends.has(request)) throwInvalidState();
                    return reflectApply(nativeSetRequestHeader, request, args);
                },
                setWithCredentials(request, value) {
                    if (pendingSends.has(request)) throwInvalidState();
                    return reflectApply(nativeWithCredentials.set, request, [value]);
                }
            };
            installRoutes(prototype, [
                ['open', nativeOpen], ['send', nativeSend], ['abort', nativeAbort], ['setRequestHeader', nativeSetRequestHeader]
            ], nativeWithCredentials);
            const staged = new proxy(constructor, {
                construct(target, args) {
                    const request = reflectConstruct(target, args);
                    operationsByRequest.set(request, operations);
                    return request;
                }
            });
            defineProperty(constructor.prototype, 'constructor', { value: staged, writable: false, configurable: false });
            defineProperty(targetWindow.Window.prototype, 'XMLHttpRequest', { value: staged, writable: false, configurable: false });
            defineProperty(targetWindow, 'XMLHttpRequest', { value: staged, writable: false, configurable: false });
            constructors.set(targetWindow, staged);
            return staged;
        };
        const staged = forWindow(popup);
        staged.forWindow = forWindow;
        return staged;
    };
})();
