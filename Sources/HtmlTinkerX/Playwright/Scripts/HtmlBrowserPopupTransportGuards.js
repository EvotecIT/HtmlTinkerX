(() => {
    const arrayFrom = Array.from;
    const structuredCloneValue = structuredClone;
    const blob = Blob;
    const arrayBuffer = ArrayBuffer;
    const arrayBufferIsView = ArrayBuffer.isView;
    const arrayBufferSlice = ArrayBuffer.prototype.slice;
    const documentValue = Document;
    const domException = DOMException;
    const formData = FormData;
    const formDataEntries = FormData.prototype.entries;
    const iterator = Symbol.iterator;
    const navigatorPrototype = Navigator.prototype;
    const nativeSendBeacon = navigatorPrototype.sendBeacon;
    const nativeRequest = Request;
    const nodeClone = Node.prototype.cloneNode;
    const objectValue = Object;
    const defineProperty = Object.defineProperty;
    const getOwnPropertyDescriptor = Object.getOwnPropertyDescriptor;
    const getPrototypeOf = Object.getPrototypeOf;
    const reflectApply = Reflect.apply;
    const reflectGet = Reflect.get;
    const reflectSet = Reflect.set;
    const uint8Array = Uint8Array;
    const uint8ArraySlice = Uint8Array.prototype.slice;
    const url = URL;
    const urlSearchParams = URLSearchParams;
    const urlSearchParamsToString = URLSearchParams.prototype.toString;
    const beaconStates = new WeakMap();
    let openerBeaconInstalled = false;
    const routedSendBeacon = function(...args) {
        const staged = beaconStates.get(this);
        return staged == null
            ? reflectApply(nativeSendBeacon, this, args)
            : staged(this, args);
    };
    globalThis.__htmlTinkerXCreatePopupTransportGuards = ({ popup, fallbackBaseUri, isReady, runWhenReady, toDomString }) => {
        const popupFormData = popup.FormData;
        const popupFormDataAppend = popupFormData.prototype.append;
        const popupGetAttribute = popup.Element.prototype.getAttribute;
        const popupQuerySelector = popup.Document.prototype.querySelector;
        const popupUrlSearchParams = popup.URLSearchParams;
        const isInstance = (value, popupType, openerType) => (typeof popupType === 'function' && value instanceof popupType)
            || (typeof openerType === 'function' && value instanceof openerType);
        const snapshotBody = body => {
            if (body == null) return body;
            if (isInstance(body, popup.Blob, blob)) return body;
            if (isInstance(body, popup.ArrayBuffer, arrayBuffer)) return reflectApply(arrayBufferSlice, body, [0]);
            if (arrayBufferIsView(body) || popup.ArrayBuffer.isView(body)) {
                const bytes = new uint8Array(body.buffer, body.byteOffset, body.byteLength);
                return reflectApply(uint8ArraySlice, bytes, []).buffer;
            }
            if (isInstance(body, popup.URLSearchParams, urlSearchParams)) {
                return new popupUrlSearchParams(reflectApply(urlSearchParamsToString, body, []));
            }
            if (isInstance(body, popup.FormData, formData)) {
                const copy = new popupFormData();
                for (const [name, value] of reflectApply(formDataEntries, body, [])) {
                    reflectApply(popupFormDataAppend, copy, [name, value]);
                }
                return copy;
            }
            if (isInstance(body, popup.Document, documentValue)) return reflectApply(nodeClone, body, [true]);
            return toDomString(body);
        };
        let stagedBeaconBytes = 0;
        const popupSendBeacon = popup.Navigator.prototype.sendBeacon;
        if (typeof popupSendBeacon === 'function') {
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
            const stagedSendBeacon = (receiver, args) => {
                if (args.length === 0) throw new TypeError("Failed to execute 'sendBeacon': 1 argument required");
                const normalizedArgs = [new url(toDomString(args[0]), popup.document.baseURI).href];
                if (args.length > 1) normalizedArgs.push(snapshotBody(args[1]));
                if (isReady()) return reflectApply(popupSendBeacon, receiver, normalizedArgs);
                const size = payloadSize(normalizedArgs[1]);
                if (size > 64 * 1024 - stagedBeaconBytes) return false;
                stagedBeaconBytes += size;
                runWhenReady(() => reflectApply(popupSendBeacon, receiver, normalizedArgs));
                return true;
            };
            beaconStates.set(popup.navigator, stagedSendBeacon);
            if (!openerBeaconInstalled && typeof nativeSendBeacon === 'function') {
                defineProperty(navigatorPrototype, 'sendBeacon', {
                    value: routedSendBeacon,
                    writable: false,
                    configurable: false
                });
                openerBeaconInstalled = true;
            }
            defineProperty(popup.Navigator.prototype, 'sendBeacon', {
                value: routedSendBeacon,
                writable: false,
                configurable: false
            });
        }
        return {
            snapshotFetchArguments(args) {
                if (args.length === 0) throw new TypeError("Failed to execute 'fetch': 1 argument required");
                const baseElement = reflectApply(popupQuerySelector, popup.document, ['base']);
                const stagedBase = baseElement == null ? null : reflectApply(popupGetAttribute, baseElement, ['href']);
                const documentBase = stagedBase == null
                    ? popup.document.baseURI.startsWith('about:') ? fallbackBaseUri : popup.document.baseURI
                    : new url(stagedBase, fallbackBaseUri).href;
                const input = isInstance(args[0], popup.Request, nativeRequest)
                    ? args[0]
                    : new url(toDomString(args[0]), documentBase).href;
                const request = args.length > 1
                    ? new popup.Request(input, args[1])
                    : new popup.Request(input);
                return [request];
            },
            normalizeLocationArguments(name, args) {
                if (name === 'reload') return [];
                if (args.length === 0) throw new TypeError(`Failed to execute '${name}': 1 argument required`);
                return [new url(toDomString(args[0]), popup.document.baseURI).href];
            },
            createStyleGuard(element, values, isReleased) {
                let owner = element;
                let descriptor = null;
                while (owner && descriptor == null) {
                    descriptor = getOwnPropertyDescriptor(owner, 'style');
                    owner = getPrototypeOf(owner);
                }
                if (descriptor == null || typeof descriptor.get !== 'function') return null;
                const staged = popup.document.createElement('span').style;
                let lastText = '';
                const synchronizeFromAttributes = () => {
                    const current = values.get('style') ?? '';
                    if (current !== lastText) {
                        staged.cssText = current;
                        lastText = staged.cssText;
                    }
                };
                const synchronizeToAttributes = () => {
                    lastText = staged.cssText;
                    if (lastText.length === 0) values.delete('style');
                    else values.set('style', lastText);
                };
                const facade = new Proxy(staged, {
                    get(_, property) {
                        const current = isReleased() ? descriptor.get.call(element) : staged;
                        if (!isReleased()) synchronizeFromAttributes();
                        const value = reflectGet(current, property, current);
                        if (typeof value !== 'function') return value;
                        return (...args) => {
                            const result = reflectApply(value, current, args);
                            if (!isReleased()) synchronizeToAttributes();
                            return result;
                        };
                    },
                    set(_, property, value) {
                        const current = isReleased() ? descriptor.get.call(element) : staged;
                        if (!isReleased()) synchronizeFromAttributes();
                        const result = reflectSet(current, property, value, current);
                        if (!isReleased()) synchronizeToAttributes();
                        return result;
                    }
                });
                return {
                    facade,
                    release() {
                        synchronizeFromAttributes();
                        synchronizeToAttributes();
                    }
                };
            },
            snapshotBodyArguments(args) {
                return args.length === 0 ? [] : [snapshotBody(args[0])];
            },
            guardReturnedNodes(value, guard) {
                if (value instanceof popup.Node) guard(value);
                else if ((typeof popup.NodeList === 'function' && value instanceof popup.NodeList)
                    || (typeof popup.HTMLCollection === 'function' && value instanceof popup.HTMLCollection)) {
                    for (let index = 0; index < value.length; index++) if (value[index] instanceof popup.Node) guard(value[index]);
                }
                return value;
            },
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
