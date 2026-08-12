(() => {
    if (globalThis.__htmlTinkerXPopupNavigationShimInstalled === true) return;
    const nativeDefineProperty = Object.defineProperty;
    const nativeGetOwnPropertyDescriptor = Object.getOwnPropertyDescriptor;
    const nativeGetPrototypeOf = Object.getPrototypeOf;
    const nativeHasOwnProperty = Object.prototype.hasOwnProperty;
    const nativeReflectApply = Reflect.apply;
    const nativeReflectConstruct = Reflect.construct;
    const nativeReflectGet = Reflect.get;
    const nativeReflectSet = Reflect.set;
    const nativeQuerySelectorAll = Document.prototype.querySelectorAll;
    const nativeQuerySelector = Document.prototype.querySelector;
    const nativeClosest = Element.prototype.closest;
    const nativeGetAttribute = Element.prototype.getAttribute;
    const nativeHasAttribute = Element.prototype.hasAttribute;
    const nativeRemoveAttribute = Element.prototype.removeAttribute;
    const nativeSetAttribute = Element.prototype.setAttribute;
    const nativeComposedPath = Event.prototype.composedPath;
    const iframePrototype = HTMLIFrameElement.prototype;
    const framePrototype = typeof HTMLFrameElement === 'undefined' ? null : HTMLFrameElement.prototype;
    const iframeContentDocument = nativeGetOwnPropertyDescriptor(iframePrototype, 'contentDocument')?.get;
    const frameContentDocument = framePrototype == null
        ? null
        : nativeGetOwnPropertyDescriptor(framePrototype, 'contentDocument')?.get;
    nativeDefineProperty(globalThis, '__htmlTinkerXPopupNavigationShimInstalled', {
        value: true,
        configurable: false
    });
    const originalOpen = window.open;
    const originalSubmit = HTMLFormElement.prototype.submit;
    const originalAddEventListener = EventTarget.prototype.addEventListener;
    const originalRemoveEventListener = EventTarget.prototype.removeEventListener;
    const originalPreventDefault = Event.prototype.preventDefault;
    const defaultPreventedDescriptor = nativeGetOwnPropertyDescriptor(Event.prototype, 'defaultPrevented');
    const internallyCancelledEvents = new WeakSet();
    const pageCancelledEvents = new WeakSet();
    const specialTargets = ['_self', '_parent', '_top'];
    const imageSubmitCoordinates = new WeakMap();
    const popupReleaseProperty = '__HTMLTINKERX_POPUP_RELEASE_PROPERTY__';

    Event.prototype.preventDefault = function() {
        pageCancelledEvents.add(this);
        return originalPreventDefault.call(this);
    };
    nativeDefineProperty(Event.prototype, 'defaultPrevented', {
        ...defaultPreventedDescriptor,
        get() {
            return internallyCancelledEvents.has(this)
                ? pageCancelledEvents.has(this)
                : defaultPreventedDescriptor.get.call(this);
        }
    });

    const normalizedTarget = target => target == null || String(target).length === 0
        ? '_blank'
        : String(target).toLowerCase();

    const normalizedDeclarativeTarget = target => target == null || String(target).length === 0
        ? '_self'
        : String(target).toLowerCase();

    const targetsExistingFrame = target => {
        if (target == null || String(target).length === 0) return false;
        const expected = String(target);
        const visited = new WeakSet();
        const containsNamedFrame = currentDocument => {
            if (visited.has(currentDocument)) return false;
            visited.add(currentDocument);
            const frames = nativeQuerySelectorAll.call(currentDocument, 'iframe, frame');
            for (let index = 0; index < frames.length; index++) {
                const frame = frames[index];
                if (nativeGetAttribute.call(frame, 'name') === expected) return true;
                try {
                    let childDocument = null;
                    try {
                        childDocument = iframeContentDocument == null ? null : iframeContentDocument.call(frame);
                    } catch {
                        childDocument = frameContentDocument == null ? null : frameContentDocument.call(frame);
                    }
                    if (childDocument && containsNamedFrame(childDocument)) return true;
                } catch { }
            }
            return false;
        };
        let root = window;
        while (root !== root.parent) {
            try {
                void root.parent.document;
                root = root.parent;
            } catch { break; }
        }
        try { return containsNamedFrame(root.document); } catch { return false; }
    };

    const armBlankPopup = (popup, target, navigate) => {
        let currentUrl;
        try { currentUrl = popup.location.href; } catch { currentUrl = null; }
        const normalized = normalizedTarget(target);
        if (currentUrl !== 'about:blank' || specialTargets.includes(normalized)) {
            globalThis.setTimeout(() => navigate(currentUrl === null), 0);
            return;
        }

        const release = () => {
            navigate();
        };
        nativeDefineProperty(popup, popupReleaseProperty, {
            value: release,
            configurable: true
        });
    };

    const openStagedBlankPopup = function(url, target, features) {
        const popup = originalOpen.call(this, url, target, features);
        if (!popup || specialTargets.includes(normalizedTarget(target)) || targetsExistingFrame(target)) return popup;
        try {
            if (popup.location.href !== 'about:blank') return popup;
        } catch {
            return popup;
        }

        let ready = false;
        let documentMutationQueued = false;
        let documentWriteQueued = false;
        let documentCloseQueued = false;
        let documentWrittenSynchronously = false;
        const documentWriteParts = [];
        const queued = [];
        const guardedResources = [];
        const guardedElements = new WeakSet();
        const requestAttributes = new Map([
            ['src', 'src'], ['srcset', 'srcset'], ['href', 'href'], ['action', 'action'],
            ['poster', 'poster'], ['data', 'data'], ['formaction', 'formAction']
        ]);
        const runWhenReady = action => {
            if (ready) action();
            else queued.push(action);
        };
        const popupFetch = popup.fetch.bind(popup);
        const stagedFetch = (...args) => new Promise((resolve, reject) => {
            runWhenReady(() => {
                try {
                    popupFetch(...args).then(resolve, reject);
                } catch (error) {
                    reject(error);
                }
            });
        });
        nativeDefineProperty(popup.Window.prototype, 'fetch', {
            value: stagedFetch,
            writable: false,
            configurable: false
        });
        nativeDefineProperty(popup, 'fetch', {
            value: stagedFetch,
            writable: false,
            configurable: false
        });
        const nativeXhrSend = popup.XMLHttpRequest.prototype.send;
        const stagedXhrSend = function(...args) {
            runWhenReady(() => nativeReflectApply(nativeXhrSend, this, args));
        };
        nativeDefineProperty(popup.XMLHttpRequest.prototype, 'send', {
            value: stagedXhrSend,
            writable: false,
            configurable: false
        });
        const nativeSendBeacon = popup.Navigator.prototype.sendBeacon;
        if (typeof nativeSendBeacon === 'function') {
            const stagedSendBeacon = function(...args) {
                runWhenReady(() => nativeReflectApply(nativeSendBeacon, this, args));
                return true;
            };
            nativeDefineProperty(popup.Navigator.prototype, 'sendBeacon', {
                value: stagedSendBeacon,
                writable: false,
                configurable: false
            });
        }
        const stagedAsyncConstructors = new Map();
        const stageAsyncConstructor = (name, handlerNames, operationNames, stopName) => {
            const nativeConstructor = popup[name];
            if (typeof nativeConstructor !== 'function') return;
            const deferredInstance = (constructor, args) => {
                let instance = null;
                let stopped = false;
                const handlers = new Map();
                const listeners = [];
                const pending = [];
                const handlerProperties = new Set(handlerNames);
                const operationProperties = new Set(operationNames);
                const target = {};
                const facade = new Proxy(target, {
                    get(_, property) {
                        if (instance != null) {
                            const value = nativeReflectGet(instance, property, instance);
                            return typeof value === 'function' ? value.bind(instance) : value;
                        }
                        if (property === Symbol.toStringTag) return name;
                        if (name === 'EventSource' && property === 'readyState') return nativeConstructor.CONNECTING;
                        if (name === 'EventSource' && property === 'url') {
                            return new URL(String(args[0]), popup.document.baseURI).href;
                        }
                        if (name === 'EventSource' && property === 'withCredentials') {
                            return Boolean(args[1]?.withCredentials);
                        }
                        if (handlerProperties.has(property)) return handlers.get(property) ?? null;
                        if (operationProperties.has(property)) {
                            return (...operationArgs) => pending.push(current => {
                                const operation = nativeReflectGet(current, property, current);
                                nativeReflectApply(operation, current, operationArgs);
                            });
                        }
                        if (property === 'addEventListener') {
                            return (type, listener, options) => listeners.push({ type, listener, options });
                        }
                        if (property === 'removeEventListener') {
                            return (type, listener) => {
                                for (let index = listeners.length - 1; index >= 0; index--) {
                                    const current = listeners[index];
                                    if (current.type === type && current.listener === listener) listeners.splice(index, 1);
                                }
                            };
                        }
                        if (property === stopName) {
                            return () => {
                                stopped = true;
                                pending.length = 0;
                            };
                        }
                        return nativeReflectGet(target, property, target);
                    },
                    set(_, property, value) {
                        if (instance != null) return nativeReflectSet(instance, property, value, instance);
                        if (handlerProperties.has(property)) {
                            handlers.set(property, value);
                            return true;
                        }
                        return nativeReflectSet(target, property, value, target);
                    },
                    getPrototypeOf() {
                        return constructor.prototype;
                    }
                });
                runWhenReady(() => {
                    if (stopped) return;
                    instance = nativeReflectConstruct(constructor, args);
                    for (const [property, value] of handlers) nativeReflectSet(instance, property, value, instance);
                    for (const { type, listener, options } of listeners) {
                        const addEventListener = nativeReflectGet(instance, 'addEventListener', instance);
                        nativeReflectApply(addEventListener, instance, [type, listener, options]);
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
            nativeDefineProperty(nativeConstructor.prototype, 'constructor', {
                value: stagedConstructor,
                writable: false,
                configurable: false
            });
            nativeDefineProperty(popup.Window.prototype, name, {
                value: stagedConstructor,
                writable: false,
                configurable: false
            });
            nativeDefineProperty(popup, name, {
                value: stagedConstructor,
                writable: false,
                configurable: false
            });
            stagedAsyncConstructors.set(name, stagedConstructor);
        };
        stageAsyncConstructor('Worker', ['onerror', 'onmessage', 'onmessageerror'], ['postMessage'], 'terminate');
        stageAsyncConstructor('EventSource', ['onerror', 'onmessage', 'onopen'], [], 'close');
        const nativeLocation = popup.location;
        let popupFacade;
        const locationFacade = new Proxy({}, {
            get(_, property) {
                const value = nativeReflectGet(nativeLocation, property, nativeLocation);
                if (['assign', 'replace', 'reload'].includes(property)) {
                    return (...args) => runWhenReady(() => nativeReflectApply(value, nativeLocation, args));
                }
                return typeof value === 'function' ? value.bind(nativeLocation) : value;
            },
            set(_, property, value) {
                runWhenReady(() => nativeReflectSet(nativeLocation, property, value, nativeLocation));
                return true;
            }
        });
        const nativeObjects = new WeakMap();
        const shouldDeferAttribute = (element, attribute) => requestAttributes.has(attribute)
            || attribute === 'style'
            || attribute.startsWith('on')
            || ((element.localName === 'iframe' || element.localName === 'frame') && attribute === 'srcdoc')
            || (element.localName === 'meta'
                && attribute === 'content'
                && String(element.getAttribute('http-equiv')).toLowerCase() === 'refresh');
        const guardDeferredAttributes = (element, initialValues) => {
            if (guardedElements.has(element)) return;
            guardedElements.add(element);
            const values = new Map(initialValues);
            let released = false;
            for (const [attribute, property] of requestAttributes) {
                if (!(property in element)) continue;
                let descriptor = null;
                let prototype = element;
                while (prototype && descriptor == null) {
                    descriptor = nativeGetOwnPropertyDescriptor(prototype, property);
                    prototype = nativeGetPrototypeOf(prototype);
                }
                if (descriptor == null || descriptor.configurable === false && nativeHasOwnProperty.call(element, property)) continue;
                nativeDefineProperty(element, property, {
                    configurable: false,
                    enumerable: descriptor.enumerable,
                    get() {
                        if (!released && values.has(attribute)) return values.get(attribute);
                        return descriptor.get ? descriptor.get.call(element) : '';
                    },
                    set(value) {
                        if (!released) values.set(attribute, String(value));
                        else if (descriptor.set) descriptor.set.call(element, value);
                        else nativeSetAttribute.call(element, attribute, String(value));
                    }
                });
            }
            const nativeSetAttribute = element.setAttribute;
            const nativeRemoveAttribute = element.removeAttribute;
            nativeDefineProperty(element, 'setAttribute', {
                configurable: false,
                value(name, value) {
                    const attribute = String(name).toLowerCase();
                    if (!released && shouldDeferAttribute(element, attribute)) {
                        values.set(attribute, String(value));
                        return;
                    }
                    return nativeSetAttribute.call(this, name, value);
                }
            });
            nativeDefineProperty(element, 'removeAttribute', {
                configurable: false,
                value(name) {
                    const attribute = String(name).toLowerCase();
                    if (!released && shouldDeferAttribute(element, attribute)) {
                        values.delete(attribute);
                        return;
                    }
                    return nativeRemoveAttribute.call(this, name);
                }
            });
            guardedResources.push(() => {
                released = true;
                for (const [attribute, value] of values) nativeSetAttribute.call(element, attribute, value);
            });
        };
        const guardCreatedElement = element => {
            if (!(element instanceof popup.Element) || guardedElements.has(element)) return element;
            const values = [];
            for (const attribute of Array.from(element.attributes)) {
                const name = attribute.name.toLowerCase();
                if (!shouldDeferAttribute(element, name)) continue;
                values.push([name, attribute.value]);
                element.removeAttribute(attribute.name);
            }
            guardDeferredAttributes(element, values);
            return element;
        };
        const stagedElementConstructors = new Map();
        for (const name of ['Image', 'Audio']) {
            const constructor = popup[name];
            if (typeof constructor !== 'function') continue;
            stagedElementConstructors.set(name, new Proxy(constructor, {
                construct(target, args) {
                    return guardCreatedElement(nativeReflectConstruct(target, args));
                }
            }));
        }
        const writeStagedMarkup = (method, args) => {
            const nativeDocument = popup.document;
            documentWriteParts.push(args.map(value => String(value)).join('') + (method === 'writeln' ? '\n' : ''));
            if (documentWriteParts.length > 1) {
                nativeDocument.open();
                guardedResources.length = 0;
                documentCloseQueued = false;
            }
            const template = nativeDocument.createElement('template');
            template.innerHTML = documentWriteParts.join('');
            const descriptors = [];
            let markerIndex = 0;
            for (const element of template.content.querySelectorAll('*')) {
                const script = element.localName === 'script'
                    ? {
                        attributes: Array.from(element.attributes, attribute => [attribute.name, attribute.value]),
                        text: element.textContent
                    }
                    : null;
                const values = [];
                if (script !== null) {
                    for (const attribute of Array.from(element.attributes)) element.removeAttribute(attribute.name);
                    element.setAttribute('type', 'application/x-htmltinkerx-staged');
                    element.textContent = '';
                } else {
                    for (const attribute of Array.from(element.attributes)) {
                        const name = attribute.name.toLowerCase();
                        if (!shouldDeferAttribute(element, name)) continue;
                        values.push([name, attribute.value]);
                        element.removeAttribute(attribute.name);
                    }
                }
                const styleText = element.localName === 'style' ? element.textContent : null;
                if (styleText !== null) element.textContent = '';
                const marker = `htmltinkerx-${Date.now()}-${markerIndex++}-${Math.random().toString(36).slice(2)}`;
                element.setAttribute('data-htmltinkerx-staged-resource', marker);
                descriptors.push({ marker, values, styleText, script });
            }
            nativeReflectApply(nativeDocument.write, nativeDocument, [template.innerHTML]);
            for (const { marker, values, styleText, script } of descriptors) {
                const element = nativeDocument.querySelector(`[data-htmltinkerx-staged-resource="${marker}"]`);
                if (!element) continue;
                element.removeAttribute('data-htmltinkerx-staged-resource');
                guardDeferredAttributes(element, values);
                if (styleText !== null) {
                    guardedResources.push(() => { element.textContent = styleText; });
                }
                if (script !== null) {
                    guardedResources.push(() => {
                        const replacement = nativeDocument.createElement('script');
                        const attributes = new Map(script.attributes.map(([name, value]) => [name.toLowerCase(), value]));
                        const blockingExternal = attributes.has('src')
                            && !attributes.has('async')
                            && !attributes.has('defer')
                            && String(attributes.get('type') || '').toLowerCase() !== 'module';
                        let completed = null;
                        if (blockingExternal) {
                            completed = new Promise(resolve => {
                                replacement.addEventListener('load', resolve, { once: true });
                                replacement.addEventListener('error', resolve, { once: true });
                            });
                        }
                        for (const [name, value] of script.attributes) replacement.setAttribute(name, value);
                        replacement.textContent = script.text;
                        element.replaceWith(replacement);
                        if (blockingExternal && documentWriteQueued && !documentCloseQueued) {
                            nativeDocument.close();
                            documentCloseQueued = true;
                        }
                        return completed;
                    });
                }
            }
            documentMutationQueued = true;
            documentWriteQueued = true;
            documentWrittenSynchronously = true;
        };
        const mutationMethods = new Set([
            'append', 'appendChild', 'after', 'before', 'click', 'close', 'insertAdjacentElement',
            'insertAdjacentHTML', 'insertAdjacentText', 'insertBefore', 'open', 'prepend',
            'remove', 'removeAttribute', 'removeAttributeNS', 'removeChild', 'replaceChild',
            'replaceChildren', 'replaceWith', 'requestSubmit', 'setAttribute', 'setAttributeNS',
            'submit', 'toggleAttribute', 'write', 'writeln'
        ]);
        const createdNodeMethods = new Set(['adoptNode', 'cloneNode', 'createElement', 'createElementNS', 'importNode']);
        const unwrap = value => {
            const resolve = nativeObjects.get(value);
            return resolve ? resolve() : value;
        };
        const stagedMutationResult = (resolve, property, args) => {
            if (property === 'open') return stagedObject(resolve);
            if (['appendChild', 'insertBefore', 'replaceChild', 'removeChild'].includes(property)) {
                return args.length === 0 ? undefined : args[0];
            }
            return undefined;
        };
        const stagedObject = resolve => {
            const value = resolve();
            if (value === nativeLocation) return locationFacade;
            if (value === popup) return popupFacade;
            if (!value || !(value instanceof popup.Node)) return value;
            const facade = new Proxy({}, {
                get(_, property) {
                    const target = resolve();
                    const member = nativeReflectGet(target, property, target);
                    if (typeof member !== 'function') {
                        return member instanceof popup.Node
                            ? stagedObject(() => nativeReflectGet(resolve(), property, resolve()))
                            : member === popup || member === nativeLocation ? stagedObject(() => member) : member;
                    }
                    return (...args) => {
                        if (!ready && (property === 'write' || property === 'writeln') && resolve() === popup.document) {
                            writeStagedMarkup(property, args);
                            return undefined;
                        }
                        if (!mutationMethods.has(property) || ready) {
                            const invoke = () => {
                                const current = resolve();
                                const currentMember = nativeReflectGet(current, property, current);
                                return nativeReflectApply(currentMember, current, args.map(unwrap));
                            };
                            const initialResult = invoke();
                            if (!initialResult || !(initialResult instanceof popup.Node)) return initialResult;
                            if (!ready && createdNodeMethods.has(property)) guardCreatedElement(initialResult);
                            return initialResult;
                        }
                        const result = stagedMutationResult(resolve, property, args);
                        documentMutationQueued = true;
                        if (property === 'write' || property === 'writeln') documentWriteQueued = true;
                        if (property === 'close') documentCloseQueued = true;
                        queued.push(() => {
                            const current = resolve();
                            const currentMember = nativeReflectGet(current, property, current);
                            nativeReflectApply(currentMember, current, args.map(unwrap));
                        });
                        return result;
                    };
                },
                set(_, property, valueToSet) {
                    if (!ready) documentMutationQueued = true;
                    runWhenReady(() => {
                        const current = resolve();
                        nativeReflectSet(current, property, unwrap(valueToSet), current);
                    });
                    return true;
                }
            });
            nativeObjects.set(facade, resolve);
            return facade;
        };
        const documentFacade = stagedObject(() => popup.document);
        popupFacade = new Proxy(popup, {
            get(targetWindow, property) {
                if (property === 'location') return locationFacade;
                if (property === 'document') return documentFacade;
                if (property === 'window' || property === 'self' || property === 'frames') return popupFacade;
                if (stagedAsyncConstructors.has(property)) return stagedAsyncConstructors.get(property);
                if (stagedElementConstructors.has(property)) return stagedElementConstructors.get(property);
                const value = nativeReflectGet(targetWindow, property, targetWindow);
                if (value === stagedFetch) return stagedFetch;
                if (value === targetWindow) return popupFacade;
                return typeof value === 'function' ? value.bind(targetWindow) : value;
            },
            set(targetWindow, property, value) {
                if (property === 'location') {
                    runWhenReady(() => { targetWindow.location = value; });
                    return true;
                }
                return nativeReflectSet(targetWindow, property, value, targetWindow);
            }
        });
        armBlankPopup(popup, target, () => {
            // Run document replacement from the opener realm. Performing document.open()
            // in the popup's release evaluation destroys that evaluation context before
            // queued mutations can be replayed.
            globalThis.setTimeout(async () => {
                if (documentMutationQueued && !documentWrittenSynchronously) {
                    popup.document.open();
                    popup.document.write('<!doctype html><html><head></head><body></body></html>');
                    popup.document.close();
                }
                ready = true;
                while (guardedResources.length > 0) await guardedResources.shift()();
                while (queued.length > 0) queued.shift()();
                if (documentWriteQueued && !documentCloseQueued) popup.document.close();
            }, 0);
        });
        return popupFacade;
    };

    const openWithReferrerPolicy = function(url, target, features, referrerPolicy) {
        if (url == null || String(url).length === 0 || String(url).toLowerCase() === 'about:blank') {
            return openStagedBlankPopup.call(this, url, target, features);
        }
        const destination = new URL(String(url), document.baseURI).href;
        const featureTokens = features == null
            ? []
            : String(features).split(',').map(token => token.trim()).filter(Boolean);
        const isEnabled = name => featureTokens.some(token => {
            const parts = token.toLowerCase().split('=', 2);
            return parts[0] === name && (parts.length === 1 || !['0', 'no', 'false'].includes(parts[1]));
        });
        const suppressReferrer = isEnabled('noreferrer');
        const suppressOpener = suppressReferrer || isEnabled('noopener');
        const initialFeatures = suppressOpener
            ? featureTokens.filter(token => !['noopener', 'noreferrer'].includes(token.toLowerCase().split('=', 1)[0])).join(',')
            : features;
        if (targetsExistingFrame(target)) {
            return originalOpen.call(this, destination, target, features);
        }
        const initialTarget = suppressOpener && !specialTargets.includes(normalizedTarget(target)) ? '_blank' : target;
        const popup = originalOpen.call(this, '', initialTarget, initialFeatures);
        if (popup) {
            if (suppressOpener) {
                try { popup.opener = null; } catch { }
            }
            const navigate = useNativeTargeting => {
                if (useNativeTargeting) {
                    originalOpen.call(window, destination, target, features);
                    return;
                }
                try {
                    if (suppressReferrer || referrerPolicy) {
                        const link = popup.document.createElement('a');
                        link.href = destination;
                        if (suppressReferrer) link.rel = 'noreferrer';
                        if (referrerPolicy) link.referrerPolicy = referrerPolicy;
                        link.target = '_self';
                        (popup.document.body || popup.document.documentElement).appendChild(link);
                        link.click();
                    } else {
                        popup.location.href = destination;
                    }
                } catch {
                    // Existing named contexts can be cross-origin. Native navigation is the
                    // standards-safe fallback when their WindowProxy cannot be inspected.
                    originalOpen.call(window, destination, target, features);
                }
            };
            armBlankPopup(popup, initialTarget, navigate);
        }
        return suppressOpener ? null : popup;
    };

    const stagedWindowOpen = function(url, target, features) {
        return openWithReferrerPolicy.call(this, url, target, features, '');
    };
    nativeDefineProperty(Window.prototype, 'open', {
        value: stagedWindowOpen,
        writable: false,
        configurable: false
    });
    nativeDefineProperty(window, 'open', {
        value: stagedWindowOpen,
        writable: false,
        configurable: false
    });

    const effectiveTarget = (element, submitter) => {
        if (submitter != null && nativeHasAttribute.call(submitter, 'formtarget')) {
            return nativeGetAttribute.call(submitter, 'formtarget') || '';
        }
        if (nativeHasAttribute.call(element, 'target')) {
            return nativeGetAttribute.call(element, 'target') || '';
        }
        const base = nativeQuerySelector.call(document, 'base[target]');
        return base == null ? '' : nativeGetAttribute.call(base, 'target') || '';
    };

    const hasExplicitEmptyTarget = (element, submitter) => {
        if (submitter != null && nativeHasAttribute.call(submitter, 'formtarget')) {
            return (nativeGetAttribute.call(submitter, 'formtarget') || '') === '';
        }
        return nativeHasAttribute.call(element, 'target')
            && (nativeGetAttribute.call(element, 'target') || '') === '';
    };

    const restoreAfterPopupNavigation = (popup, restore) => {
        let restored = false;
        let interval;
        let fallback;
        const restoreOnce = () => {
            if (restored) return;
            restored = true;
            if (interval != null) globalThis.clearInterval(interval);
            if (fallback != null) globalThis.clearTimeout(fallback);
            restore();
        };
        interval = globalThis.setInterval(() => {
            try {
                if (popup.closed || popup.location.href !== 'about:blank') restoreOnce();
            } catch {
                restoreOnce();
            }
        }, 10);
        fallback = globalThis.setTimeout(restoreOnce, 5000);
    };

    const deferPopupFormSubmission = (form, submitter, submit) => {
        if (!canDeferPopupFormSubmission(form, submitter)) return false;
        const target = effectiveTarget(form, submitter);
        const normalized = normalizedDeclarativeTarget(target);

        const popup = originalOpen.call(window, '', target);
        if (!popup) return false;
        const rel = form.relList;
        const suppressOpener = rel.contains('noreferrer')
            || rel.contains('noopener')
            || !rel.contains('opener');
        if (suppressOpener) {
            try { popup.opener = null; } catch { }
        }
        const submissionTarget = normalized === '_blank'
            ? `htmltinkerx-popup-${Date.now()}-${Math.random().toString(36).slice(2)}`
            : target;
        if (normalized === '_blank') {
            try { popup.name = submissionTarget; } catch { }
        }

        armBlankPopup(popup, target, () => {
            const previousFormTarget = form.target;
            const previousSubmitterTarget = submitter == null ? null : submitter.formTarget;
            form.target = submissionTarget;
            if (submitter != null) submitter.formTarget = submissionTarget;
            try {
                const restoreSubmission = submit();
                restoreAfterPopupNavigation(popup, () => {
                    if (typeof restoreSubmission === 'function') restoreSubmission();
                    form.target = previousFormTarget;
                    if (submitter != null && previousSubmitterTarget != null) submitter.formTarget = previousSubmitterTarget;
                });
            } catch (error) {
                form.target = previousFormTarget;
                if (submitter != null && previousSubmitterTarget != null) submitter.formTarget = previousSubmitterTarget;
                throw error;
            }
        });
        return true;
    };

    const canDeferPopupFormSubmission = (form, submitter) => {
        const target = effectiveTarget(form, submitter);
        const normalized = normalizedDeclarativeTarget(target);
        if (specialTargets.includes(normalized) || targetsExistingFrame(target)) return false;
        const method = submitter != null && nativeHasAttribute.call(submitter, 'formmethod')
            ? nativeGetAttribute.call(submitter, 'formmethod')
            : nativeGetAttribute.call(form, 'method');
        return String(method || 'get').toLowerCase() !== 'dialog';
    };

    const submitWithoutRedispatch = (form, submitter) => {
        const overrides = [
            ['action', 'formaction'],
            ['method', 'formmethod'],
            ['enctype', 'formenctype']
        ];
        const previous = [];
        const successfulControls = [];
        if (submitter != null) {
            for (const [formAttribute, submitterAttribute] of overrides) {
                if (!nativeHasAttribute.call(submitter, submitterAttribute)) continue;
                previous.push([formAttribute, nativeGetAttribute.call(form, formAttribute)]);
                nativeSetAttribute.call(form, formAttribute, nativeGetAttribute.call(submitter, submitterAttribute));
            }
            const appendSuccessfulControl = (name, value) => {
                const control = document.createElement('input');
                control.type = 'hidden';
                control.name = name;
                control.value = value;
                form.appendChild(control);
                successfulControls.push(control);
            };
            if (!submitter.disabled && submitter instanceof HTMLInputElement && submitter.type.toLowerCase() === 'image') {
                const coordinates = imageSubmitCoordinates.get(submitter) || { x: 0, y: 0 };
                const prefix = submitter.name ? `${submitter.name}.` : '';
                appendSuccessfulControl(`${prefix}x`, String(coordinates.x));
                appendSuccessfulControl(`${prefix}y`, String(coordinates.y));
            } else if (!submitter.disabled && nativeGetAttribute.call(submitter, 'name')) {
                appendSuccessfulControl(nativeGetAttribute.call(submitter, 'name'), submitter.value || '');
            }
        }
        const restore = () => {
            for (const control of successfulControls) control.remove();
            for (const [attribute, value] of previous) {
                if (value === null) nativeRemoveAttribute.call(form, attribute);
                else nativeSetAttribute.call(form, attribute, value);
            }
        };
        try {
            originalSubmit.call(form);
            return restore;
        } catch (error) {
            restore();
            throw error;
        }
    };

    const submitInCurrentContext = (form, submit) => {
        const previousTarget = form.target;
        form.target = '_self';
        try {
            const restoreSubmission = submit();
            let restored = false;
            const restore = () => {
                if (restored) return;
                restored = true;
                if (typeof restoreSubmission === 'function') restoreSubmission();
                form.target = previousTarget;
            };
            globalThis.addEventListener('pagehide', restore, { once: true });
            globalThis.setTimeout(restore, 5000);
        } catch (error) {
            form.target = previousTarget;
            throw error;
        }
    };

    const afterPagePropagationHandlers = (type, shouldStage, handler, observe) => {
        originalAddEventListener.call(window, type, event => {
            if (typeof observe === 'function') observe(event);
            if (!shouldStage(event)) return;
            internallyCancelledEvents.add(event);
            originalPreventDefault.call(event);
            globalThis.queueMicrotask(() => {
                try { handler(event); }
                finally {
                    internallyCancelledEvents.delete(event);
                    pageCancelledEvents.delete(event);
                }
            });
        }, true);
    };

    const stagedClickAnchor = event => {
        if (event.button !== 0) return null;
        const path = typeof nativeComposedPath === 'function' ? nativeComposedPath.call(event) : [];
        const anchor = path.find(node => node instanceof HTMLAnchorElement)
            || (event.target instanceof Element ? nativeClosest.call(event.target, 'a[href]') : null);
        if (!(anchor instanceof HTMLAnchorElement) || nativeHasAttribute.call(anchor, 'download')) return null;
        const target = effectiveTarget(anchor, null);
        const explicitlyCurrent = hasExplicitEmptyTarget(anchor, null);
        const destination = new URL(anchor.href, document.baseURI);
        if (!explicitlyCurrent
            && specialTargets.includes(normalizedDeclarativeTarget(target))) return null;
        return { anchor, target, explicitlyCurrent, destination, path };
    };

    const recordImageSubmitCoordinates = event => {
        if (event.button !== 0) return;
        const path = typeof nativeComposedPath === 'function' ? nativeComposedPath.call(event) : [];
        const submitter = path.find(node => node instanceof HTMLInputElement && node.type.toLowerCase() === 'image');
        if (!(submitter instanceof HTMLInputElement)) return;
        imageSubmitCoordinates.set(submitter, {
            x: Math.max(0, Math.floor(event.offsetX || 0)),
            y: Math.max(0, Math.floor(event.offsetY || 0))
        });
    };

    afterPagePropagationHandlers('click', event => stagedClickAnchor(event) !== null, event => {
        if (event.defaultPrevented) return;
        const staged = stagedClickAnchor(event);
        if (staged === null) return;
        const { anchor, target, explicitlyCurrent, destination } = staged;
        if (explicitlyCurrent) {
            originalOpen.call(window, destination.href, '_self');
            return;
        }

        const rel = anchor.relList;
        const features = rel.contains('noreferrer')
            ? 'noreferrer'
            : rel.contains('noopener') || !rel.contains('opener') ? 'noopener' : undefined;
        openWithReferrerPolicy.call(window, destination.href, target, features, anchor.referrerPolicy || '');
    }, recordImageSubmitCoordinates);

    afterPagePropagationHandlers('submit', event => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement)) return false;
        const submitter = event.submitter instanceof HTMLElement ? event.submitter : null;
        return hasExplicitEmptyTarget(form, submitter) || canDeferPopupFormSubmission(form, submitter);
    }, event => {
        if (event.defaultPrevented) return;
        const form = event.target;
        if (!(form instanceof HTMLFormElement)) return;
        const submitter = event.submitter instanceof HTMLElement ? event.submitter : null;
        if (hasExplicitEmptyTarget(form, submitter)) {
            submitInCurrentContext(form, () => submitWithoutRedispatch(form, submitter));
            return;
        }
        deferPopupFormSubmission(form, submitter, () => submitWithoutRedispatch(form, submitter));
    });

    HTMLFormElement.prototype.submit = function() {
        const form = this;
        if (hasExplicitEmptyTarget(form, null)) {
            return submitInCurrentContext(form, () => originalSubmit.call(form));
        }
        if (!deferPopupFormSubmission(form, null, () => originalSubmit.call(form))) {
            return originalSubmit.call(form);
        }
    };
})();
