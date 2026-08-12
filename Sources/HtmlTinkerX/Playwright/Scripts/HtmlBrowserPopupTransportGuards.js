(() => {
    const arrayFrom = Array.from;
    const structuredCloneValue = structuredClone;
    const blob = Blob;
    const arrayBuffer = ArrayBuffer;
    const arrayBufferIsView = ArrayBuffer.isView;
    const domException = DOMException;
    const iterator = Symbol.iterator;
    const objectValue = Object;
    const defineProperty = Object.defineProperty;
    const reflectApply = Reflect.apply;
    const url = URL;
    globalThis.__htmlTinkerXCreatePopupTransportGuards = ({ popup, isReady, runWhenReady, toDomString }) => {
        let stagedBeaconBytes = 0;
        const nativeSendBeacon = popup.Navigator.prototype.sendBeacon;
        if (typeof nativeSendBeacon === 'function') {
            const payloadSize = body => {
                if (body == null) return 0;
                if ((typeof popup.Blob === 'function' && body instanceof popup.Blob) || body instanceof blob) return body.size;
                if ((typeof popup.ArrayBuffer === 'function' && body instanceof popup.ArrayBuffer) || body instanceof arrayBuffer) return body.byteLength;
                if (arrayBufferIsView(body) || popup.ArrayBuffer.isView(body)) return body.byteLength;
                if (typeof popup.URLSearchParams === 'function' && body instanceof popup.URLSearchParams) {
                    return new blob([body.toString()]).size;
                }
                if (typeof popup.FormData === 'function' && body instanceof popup.FormData) {
                    let size = 128;
                    for (const [name, value] of body.entries()) {
                        size += new blob([name]).size + 128;
                        size += typeof value === 'string' ? new blob([value]).size : value.size;
                    }
                    return size;
                }
                return new blob([toDomString(body)]).size;
            };
            const stagedSendBeacon = function(...args) {
                if (args.length === 0) throw new TypeError("Failed to execute 'sendBeacon': 1 argument required");
                const normalizedArgs = [new url(toDomString(args[0]), popup.document.baseURI).href, ...args.slice(1)];
                if (isReady()) return reflectApply(nativeSendBeacon, this, normalizedArgs);
                const size = payloadSize(normalizedArgs[1]);
                if (size > 64 * 1024 - stagedBeaconBytes) return false;
                stagedBeaconBytes += size;
                runWhenReady(() => reflectApply(nativeSendBeacon, this, normalizedArgs));
                return true;
            };
            defineProperty(popup.Navigator.prototype, 'sendBeacon', {
                value: stagedSendBeacon,
                writable: false,
                configurable: false
            });
        }
        return {
            validateWorkerUrl(resolvedUrl) {
                const sourceOrigin = new url(popup.document.baseURI).origin;
                if ((resolvedUrl.protocol === 'http:' || resolvedUrl.protocol === 'https:')
                    && resolvedUrl.origin !== sourceOrigin) {
                    throw new domException(
                        `Failed to construct 'Worker': Script at '${resolvedUrl.href}' cannot be accessed from origin '${sourceOrigin}'.`,
                        'SecurityError');
                }
            },
            normalizeOperation(name, property, args) {
                if (name !== 'Worker' || property !== 'postMessage') return args;
                if (args.length === 0) throw new TypeError("Failed to execute 'postMessage' on 'Worker': 1 argument required");
                let transfer = [];
                if (args.length > 1 && args[1] != null) {
                    const optionsOrTransfer = objectValue(args[1]);
                    transfer = typeof optionsOrTransfer[iterator] === 'function'
                        ? arrayFrom(optionsOrTransfer)
                        : optionsOrTransfer.transfer == null ? [] : arrayFrom(optionsOrTransfer.transfer);
                }
                const envelope = reflectApply(structuredCloneValue, popup, [
                    { message: args[0], transfer },
                    { transfer }
                ]);
                return [envelope.message, envelope.transfer];
            }
        };
    };
})();
