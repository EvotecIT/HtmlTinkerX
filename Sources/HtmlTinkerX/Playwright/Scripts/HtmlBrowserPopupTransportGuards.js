(() => {
    const arrayFrom = Array.from;
    const structuredCloneValue = structuredClone;
    const blob = Blob;
    const booleanValue = Boolean;
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
    const beaconPrototypes = new WeakSet();
    const navigationStates = new WeakMap();
    const sheetStates = new WeakMap();
    const sheetMutationStates = new WeakMap();
    const installedSheetPrototypes = new WeakSet();
    const styleStates = new WeakMap();
    const workletStates = new WeakMap();
    const installedWorkletPrototypes = new WeakSet();
    const installWorkletRoute = prototype => {
        if (prototype == null || installedWorkletPrototypes.has(prototype)) return;
        installedWorkletPrototypes.add(prototype);
        const addModule = prototype.addModule;
        if (typeof addModule !== 'function') return;
        defineProperty(prototype, 'addModule', {
            configurable: false,
            writable: false,
            value(...args) {
                const stage = workletStates.get(this);
                return stage == null ? reflectApply(addModule, this, args) : stage(addModule, args);
            }
        });
    };
    const installWorklet = worklet => {
        if (worklet == null) return;
        let prototype = getPrototypeOf(worklet);
        while (prototype != null && typeof prototype.addModule !== 'function') prototype = getPrototypeOf(prototype);
        installWorkletRoute(prototype);
    };
    for (const name of ['paintWorklet', 'animationWorklet', 'layoutWorklet']) installWorklet(globalThis.CSS?.[name]);
    const installSheetMutations = prototype => {
        if (prototype == null || installedSheetPrototypes.has(prototype)) return;
        installedSheetPrototypes.add(prototype);
        for (const name of ['deleteRule', 'insertRule', 'replace', 'replaceSync']) {
            const method = prototype[name];
            if (typeof method !== 'function') continue;
            defineProperty(prototype, name, {
                configurable: false,
                writable: false,
                value(...args) {
                    const stage = sheetMutationStates.get(this);
                    return stage == null ? reflectApply(method, this, args) : stage(name, args, method);
                }
            });
        }
    };
    const installSheetRoute = prototype => {
        let owner = prototype;
        let descriptor = null;
        while (owner && descriptor == null) {
            descriptor = getOwnPropertyDescriptor(owner, 'sheet');
            if (descriptor == null) owner = getPrototypeOf(owner);
        }
        if (descriptor?.get && descriptor.configurable !== false) defineProperty(owner, 'sheet', {
            ...descriptor,
            configurable: false,
            get() {
                const staged = sheetStates.get(this);
                return staged == null ? descriptor.get.call(this) : staged();
            }
        });
        return descriptor;
    };
    const installStyleRoute = prototype => {
        let owner = prototype;
        let descriptor = null;
        while (owner && descriptor == null) {
            descriptor = getOwnPropertyDescriptor(owner, 'style');
            if (descriptor == null) owner = getPrototypeOf(owner);
        }
        if (descriptor?.get && descriptor.configurable !== false) defineProperty(owner, 'style', {
            ...descriptor,
            configurable: false,
            get() {
                const staged = styleStates.get(this);
                return staged == null ? descriptor.get.call(this) : staged();
            }
        });
        return descriptor;
    };
    installSheetMutations(CSSStyleSheet.prototype);
    installSheetRoute(HTMLStyleElement.prototype);
    installStyleRoute(HTMLElement.prototype);
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
        const documentBase = () => {
            const baseElement = reflectApply(popupQuerySelector, popup.document, ['base']);
            const stagedBase = baseElement == null ? null : reflectApply(popupGetAttribute, baseElement, ['href']);
            return stagedBase == null
                ? popup.document.baseURI.startsWith('about:') ? fallbackBaseUri : popup.document.baseURI
                : new url(stagedBase, fallbackBaseUri).href;
        };
        installSheetMutations(popup.CSSStyleSheet.prototype);
        const sheetDescriptor = installSheetRoute(popup.HTMLStyleElement.prototype);
        const styleDescriptor = installStyleRoute(popup.HTMLElement.prototype);
        const guardWorklet = worklet => {
            if (worklet == null) return worklet;
            installWorklet(worklet);
            workletStates.set(worklet, (addModule, args) => {
                let normalized;
                try {
                    if (args.length === 0) throw new TypeError("Failed to execute 'addModule': 1 argument required");
                    normalized = [new url(toDomString(args[0]), documentBase()).href];
                    if (args.length > 1) {
                        const source = args[1] == null ? {} : objectValue(args[1]);
                        const options = {};
                        if (source.credentials !== undefined) {
                            const credentials = toDomString(source.credentials);
                            if (!['omit', 'same-origin', 'include'].includes(credentials)) throw new TypeError(`Invalid Worklet credentials '${credentials}'`);
                            options.credentials = credentials;
                        }
                        normalized.push(options);
                    }
                } catch (error) { return popup.Promise.reject(error); }
                return new popup.Promise((resolve, reject) => runWhenReady(() => {
                    try { reflectApply(addModule, worklet, normalized).then(resolve, reject); }
                    catch (error) { reject(error); }
                }));
            });
            return worklet;
        };
        for (const name of ['paintWorklet', 'animationWorklet', 'layoutWorklet']) guardWorklet(popup.CSS?.[name]);
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
        let guardNavigator = () => { };
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
                        size += new blob([name]).size * 3 + 512;
                        if (typeof value === 'string') size += new blob([value]).size;
                        else size += value.size + new blob([value.name, value.type]).size * 3;
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
            guardNavigator = (navigator, navigatorType) => {
                if (navigator == null || typeof navigatorType !== 'function') return;
                beaconStates.set(navigator, stagedSendBeacon);
                const prototype = navigatorType.prototype;
                if (beaconPrototypes.has(prototype)) return;
                const descriptor = getOwnPropertyDescriptor(prototype, 'sendBeacon');
                if (descriptor == null || descriptor.configurable === false) return;
                defineProperty(prototype, 'sendBeacon', { ...descriptor, value: routedSendBeacon, configurable: false });
                beaconPrototypes.add(prototype);
            };
            guardNavigator(popup.navigator, popup.Navigator);
            if (!openerBeaconInstalled && typeof nativeSendBeacon === 'function') {
                defineProperty(navigatorPrototype, 'sendBeacon', {
                    value: routedSendBeacon,
                    writable: false,
                    configurable: false
                });
                openerBeaconInstalled = true;
            }
        }
        const normalizeNavigationArguments = (name, args) => {
            const snapshotOptions = value => {
                const source = value == null ? {} : objectValue(value);
                const options = {};
                if (source.info !== undefined) options.info = source.info;
                if ((name === 'navigate' || name === 'reload') && source.state !== undefined) {
                    options.state = structuredCloneValue(source.state);
                }
                if (name === 'navigate' && source.history !== undefined) {
                    const history = toDomString(source.history);
                    if (!['auto', 'push', 'replace'].includes(history)) throw new TypeError(`Invalid Navigation history '${history}'`);
                    options.history = history;
                }
                return options;
            };
            if (name === 'navigate') {
                if (args.length === 0) throw new TypeError("Failed to execute 'navigate': 1 argument required");
                const normalized = [new url(toDomString(args[0]), popup.document.baseURI).href];
                if (args.length > 1) normalized.push(snapshotOptions(args[1]));
                return normalized;
            }
            if (name === 'traverseTo') {
                if (args.length === 0) throw new TypeError("Failed to execute 'traverseTo': 1 argument required");
                const normalized = [toDomString(args[0])];
                if (args.length > 1) normalized.push(snapshotOptions(args[1]));
                return normalized;
            }
            return args.length === 0 ? [] : [snapshotOptions(args[0])];
        };
        return {
            guardNavigator,
            snapshotFetchArguments(args) {
                if (args.length === 0) throw new TypeError("Failed to execute 'fetch': 1 argument required");
                const input = isInstance(args[0], popup.Request, nativeRequest)
                    ? args[0]
                    : new url(toDomString(args[0]), documentBase()).href;
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
            normalizeLocationSetter(property, value) {
                const normalized = toDomString(value);
                return property === 'href'
                    ? new url(normalized, popup.document.baseURI).href
                    : normalized;
            },
            guardNavigation(navigation, navigationType) {
                if (navigation == null || typeof navigationType !== 'function') return;
                navigationStates.set(navigation, (property, args, nativeMethod) => {
                    const normalized = normalizeNavigationArguments(property, args);
                    let resolveCommitted, rejectCommitted, resolveFinished, rejectFinished;
                    const committed = new Promise((resolve, reject) => { resolveCommitted = resolve; rejectCommitted = reject; });
                    const finished = new Promise((resolve, reject) => { resolveFinished = resolve; rejectFinished = reject; });
                    runWhenReady(() => {
                        try {
                            const result = reflectApply(nativeMethod, navigation, normalized);
                            result.committed.then(resolveCommitted, rejectCommitted);
                            result.finished.then(resolveFinished, rejectFinished);
                        } catch (error) {
                            rejectCommitted(error);
                            rejectFinished(error);
                        }
                    });
                    return { committed, finished };
                });
                for (const property of ['navigate', 'reload', 'back', 'forward', 'traverseTo']) {
                    const descriptor = getOwnPropertyDescriptor(navigationType.prototype, property);
                    if (descriptor == null || typeof descriptor.value !== 'function' || descriptor.configurable === false) continue;
                    const nativeMethod = descriptor.value;
                    defineProperty(navigationType.prototype, property, {
                        ...descriptor,
                        configurable: false,
                        value(...args) {
                            const stage = navigationStates.get(this);
                            return stage == null ? reflectApply(nativeMethod, this, args) : stage(property, args, nativeMethod);
                        }
                    });
                }
            },
            createStyleGuard(element, values, isReleased) {
                const descriptor = styleDescriptor;
                if (descriptor == null || typeof descriptor.get !== 'function') return null;
                const stagedElement = popup.document.createElement('span');
                const staged = stagedElement.style;
                let lastText = '';
                const synchronizeFromAttributes = () => {
                    if (staged.cssText !== lastText) {
                        lastText = staged.cssText;
                        if (lastText.length === 0) values.delete('style');
                        else values.set('style', lastText);
                        return;
                    }
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
                styleStates.set(element, () => isReleased() ? descriptor.get.call(element) : facade);
                const stagedAttributeStyleMap = stagedElement.attributeStyleMap;
                let attributeStyleMapOwner = element;
                let attributeStyleMapDescriptor = null;
                while (attributeStyleMapOwner && attributeStyleMapDescriptor == null) {
                    attributeStyleMapDescriptor = getOwnPropertyDescriptor(attributeStyleMapOwner, 'attributeStyleMap');
                    attributeStyleMapOwner = getPrototypeOf(attributeStyleMapOwner);
                }
                return {
                    facade,
                    get attributeStyleMapFacade() {
                        return isReleased() && typeof attributeStyleMapDescriptor?.get === 'function'
                            ? attributeStyleMapDescriptor.get.call(element)
                            : stagedAttributeStyleMap;
                    },
                    synchronize: synchronizeFromAttributes,
                    release() {
                        synchronizeFromAttributes();
                        synchronizeToAttributes();
                        styleStates.delete(element);
                    }
                };
            },
            createStyleSheetGuard(element, initialText, isReleased) {
                if (element.localName !== 'style' || sheetDescriptor?.get == null) return null;
                const stagingDocument = popup.document.implementation.createHTMLDocument('');
                const stagingElement = stagingDocument.createElement('style');
                stagingDocument.head.append(stagingElement);
                const stagingSheet = stagingElement.sheet;
                if (stagingSheet == null) return null;
                let stagedText = initialText;
                sheetStates.set(element, () => isReleased() ? sheetDescriptor.get.call(element) : stagingSheet);
                sheetMutationStates.set(stagingSheet, (name, args, method) => {
                    const result = reflectApply(method, stagingSheet, args);
                    if (name === 'replace') return result.then(value => { stagedText = ''; return value; });
                    if (name !== 'insertRule') stagedText = '';
                    return result;
                });
                const guard = {
                    set text(value) {
                        stagedText = toDomString(value);
                        stagingElement.textContent = stagedText;
                    },
                    release() {
                        sheetStates.delete(element);
                        sheetMutationStates.delete(stagingSheet);
                        const rules = arrayFrom(stagingSheet.cssRules, rule => rule.cssText).join('\n');
                        return [stagedText, rules].filter(value => value.length > 0).join('\n');
                    }
                };
                guard.text = initialText;
                return guard;
            },
            normalizeDeferredProperty(attribute, value) {
                if (!['action', 'background', 'data', 'formaction', 'href', 'poster', 'src'].includes(attribute)) return value;
                try { return new url(value, popup.document.baseURI).href; }
                catch { return value; }
            },
            snapshotBodyArguments(args) {
                return args.length === 0 ? [] : [snapshotBody(args[0])];
            },
            snapshotMutationArguments(property, args) {
                const required = { appendChild: 1, insertAdjacentElement: 2, insertAdjacentHTML: 2, insertAdjacentText: 2, insertBefore: 2, removeAttribute: 1, removeAttributeNS: 2, removeChild: 1, replaceChild: 2, setAttribute: 2, setAttributeNS: 3, toggleAttribute: 1 }[property] ?? 0;
                if (args.length < required) throw new TypeError(`Failed to execute '${property}': ${required} argument${required === 1 ? '' : 's'} required`);
                const requireNode = index => { if (!(args[index] instanceof popup.Node)) throw new TypeError(`Failed to execute '${property}': parameter ${index + 1} is not of type 'Node'`); };
                if (property === 'appendChild' || property === 'removeChild') requireNode(0);
                if (property === 'insertBefore') { requireNode(0); if (args[1] != null) requireNode(1); }
                if (property === 'replaceChild') { requireNode(0); requireNode(1); }
                if (property === 'insertAdjacentElement' && args[1] != null) requireNode(1);
                if (['append', 'after', 'before', 'prepend', 'replaceChildren', 'replaceWith'].includes(property)) for (let index = 0; index < args.length; index++) if (!(args[index] instanceof popup.Node)) args[index] = toDomString(args[index]);
                if (['insertAdjacentElement', 'insertAdjacentHTML', 'insertAdjacentText'].includes(property)) args[0] = toDomString(args[0]).toLowerCase();
                if (['insertAdjacentHTML', 'insertAdjacentText'].includes(property)) args[1] = toDomString(args[1]);
                if (property === 'setAttribute') { args[0] = toDomString(args[0]); args[1] = toDomString(args[1]); }
                if (property === 'setAttributeNS') { args[0] = args[0] == null ? null : toDomString(args[0]); args[1] = toDomString(args[1]); args[2] = toDomString(args[2]); }
                if (property === 'removeAttribute') args[0] = toDomString(args[0]);
                if (property === 'removeAttributeNS') { args[0] = args[0] == null ? null : toDomString(args[0]); args[1] = toDomString(args[1]); }
                if (property === 'toggleAttribute') { args[0] = toDomString(args[0]); if (args.length > 1) args[1] = booleanValue(args[1]); }
                return args;
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
