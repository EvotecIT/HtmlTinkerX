(() => {
    if (globalThis.__htmlTinkerXPopupNavigationShimInstalled === true) return;
    const nativeDefineProperty = Object.defineProperty;
    const nativeObject = Object;
    const nativeString = String;
    const nativeBoolean = Boolean;
    const nativeUrl = URL;
    const nativeGetOwnPropertyDescriptor = Object.getOwnPropertyDescriptor;
    const nativeGetPrototypeOf = Object.getPrototypeOf;
    const nativeHasOwnProperty = Object.prototype.hasOwnProperty;
    const nativeReflectApply = Reflect.apply;
    const nativeReflectConstruct = Reflect.construct;
    const nativeReflectGet = Reflect.get;
    const nativeReflectSet = Reflect.set;
    const nativeQuerySelectorAll = Document.prototype.querySelectorAll;
    const nativeQuerySelector = Document.prototype.querySelector;
    const nativeElementQuerySelectorAll = Element.prototype.querySelectorAll;
    const nativeClosest = Element.prototype.closest;
    const nativeGetAttribute = Element.prototype.getAttribute;
    const nativeHasAttribute = Element.prototype.hasAttribute;
    const nativeRemoveAttribute = Element.prototype.removeAttribute;
    const nativeSetAttribute = Element.prototype.setAttribute;
    const nativeSetAttributeNS = Element.prototype.setAttributeNS;
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
    const popupReleaseToken = '__HTMLTINKERX_POPUP_RELEASE_TOKEN__';
    const createMarkupStager = globalThis.__htmlTinkerXCreatePopupMarkupStager; delete globalThis.__htmlTinkerXCreatePopupMarkupStager;
    const createRealmGuards = globalThis.__htmlTinkerXCreatePopupRealmGuards; delete globalThis.__htmlTinkerXCreatePopupRealmGuards;
    const createTransportGuards = globalThis.__htmlTinkerXCreatePopupTransportGuards;
    delete globalThis.__htmlTinkerXCreatePopupTransportGuards;
    const createFrameGuards = globalThis.__htmlTinkerXCreatePopupFrameGuards; delete globalThis.__htmlTinkerXCreatePopupFrameGuards; const createCodeGuards = globalThis.__htmlTinkerXCreatePopupCodeGuards; delete globalThis.__htmlTinkerXCreatePopupCodeGuards; const createDomGuards = globalThis.__htmlTinkerXCreatePopupDomGuards; delete globalThis.__htmlTinkerXCreatePopupDomGuards;
    const createAsyncConstructors = globalThis.__htmlTinkerXCreatePopupAsyncConstructors({
        defineProperty: nativeDefineProperty, getOwnPropertyDescriptor: nativeGetOwnPropertyDescriptor,
        reflectApply: nativeReflectApply,
        reflectConstruct: nativeReflectConstruct,
        reflectGet: nativeReflectGet,
        reflectSet: nativeReflectSet
    });
    delete globalThis.__htmlTinkerXCreatePopupAsyncConstructors;
    const attributeGuards = globalThis.__htmlTinkerXCreatePopupAttributeGuards({
        defineProperty: nativeDefineProperty,
        getOwnPropertyDescriptor: nativeGetOwnPropertyDescriptor,
        reflectApply: nativeReflectApply,
        stringValue: nativeString,
        booleanValue: nativeBoolean
    });
    delete globalThis.__htmlTinkerXCreatePopupAttributeGuards;
    attributeGuards.install(Element.prototype);
    attributeGuards.installNamedNodeMap(NamedNodeMap.prototype);
    attributeGuards.installNode(Node.prototype);
    const createXhrStager = globalThis.__htmlTinkerXCreatePopupXhrStager;
    delete globalThis.__htmlTinkerXCreatePopupXhrStager;
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
    const normalizedTarget = target => target == null || nativeString(target).length === 0 ? '_blank' : nativeString(target).toLowerCase();
    const normalizedDeclarativeTarget = target => target == null || nativeString(target).length === 0 ? '_self' : nativeString(target).toLowerCase();
    const targetsExistingFrame = target => {
        if (target == null || nativeString(target).length === 0) return false;
        const expected = nativeString(target);
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
        let released = false;
        let completed = false;
        nativeDefineProperty(popup, popupReleaseProperty, {
            configurable: false,
            enumerable: false,
            get() { return completed ? popupReleaseToken : undefined; },
            set(value) {
                if (released || value !== popupReleaseToken) return;
                released = true;
                navigate(() => { completed = true; });
            }
        });
    };
    const openStagedBlankPopup = function(url, target, features, initialAction, existingAction) {
        const popup = originalOpen.call(this, url, target, features);
        if (!popup || specialTargets.includes(normalizedTarget(target)) || targetsExistingFrame(target)) return popup;
        try {
            if (popup.location.href !== 'about:blank') {
                if (typeof existingAction === 'function') existingAction(popup);
                return popup;
            }
        } catch {
            if (typeof existingAction === 'function') existingAction(popup);
            return popup;
        }
        const popupInnerHtml = nativeGetOwnPropertyDescriptor(popup.Element.prototype, 'innerHTML'); const popupOuterHtml = nativeGetOwnPropertyDescriptor(popup.Element.prototype, 'outerHTML');
        const popupTextContent = nativeGetOwnPropertyDescriptor(popup.Node.prototype, 'textContent'); const popupNodeType = nativeGetOwnPropertyDescriptor(popup.Node.prototype, 'nodeType').get; const popupCloneNode = popup.Node.prototype.cloneNode; const popupReplaceChild = popup.Node.prototype.replaceChild; const popupFragmentQuerySelectorAll = popup.DocumentFragment.prototype.querySelectorAll;
        const popupInsertAdjacentHtml = popup.Element.prototype.insertAdjacentHTML;
        const createHtmlDocument = popup.DOMImplementation.prototype.createHTMLDocument;
        attributeGuards.install(popup.Element.prototype); attributeGuards.install(popup.HTMLElement.prototype); attributeGuards.install(popup.HTMLScriptElement.prototype);
        attributeGuards.installNamedNodeMap(popup.NamedNodeMap.prototype);
        attributeGuards.installNode(popup.Node.prototype);
        attributeGuards.installFrame(popup.HTMLIFrameElement.prototype); attributeGuards.installFrame(popup.HTMLFrameElement?.prototype);
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
            ['poster', 'poster'], ['data', 'data'], ['formaction', 'formAction'], ['background', 'background']
        ]);
        const runWhenReady = action => {
            if (ready) action();
            else queued.push(action);
        };
        if (typeof initialAction === 'function') runWhenReady(() => initialAction(popup));
        const toDomString = value => {
            if (typeof value === 'symbol') throw new TypeError('Cannot convert a Symbol value to a string');
            return nativeString(value);
        };
        const codeGuards = createCodeGuards({ popup, isReady: () => ready, runWhenReady, stringValue: toDomString }); const stagedCodeMembers = codeGuards.forWindow(popup);
        const transportGuards = createTransportGuards({ popup, fallbackBaseUri: document.baseURI, isReady: () => ready, runWhenReady, toDomString });
        const popupFetch = popup.fetch.bind(popup);
        const stagedFetch = (...args) => {
            let snapshot;
            try { snapshot = transportGuards.snapshotFetchArguments(args); }
            catch (error) { return Promise.reject(error); }
            return new Promise((resolve, reject) => {
                runWhenReady(() => {
                    try { popupFetch(...snapshot).then(resolve, reject); }
                    catch (error) { reject(error); }
                });
            });
        };
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
        const nativeXhrConstructor = popup.XMLHttpRequest;
        const stagedXhrConstructor = createXhrStager({
            popup,
            runWhenReady,
            snapshotBodyArguments: transportGuards.snapshotBodyArguments
        });
        nativeDefineProperty(nativeXhrConstructor.prototype, 'constructor', {
            value: stagedXhrConstructor,
            writable: false,
            configurable: false
        });
        nativeDefineProperty(popup.Window.prototype, 'XMLHttpRequest', {
            value: stagedXhrConstructor,
            writable: false,
            configurable: false
        });
        nativeDefineProperty(popup, 'XMLHttpRequest', {
            value: stagedXhrConstructor,
            writable: false,
            configurable: false
        });
        const normalizeConstructorArguments = (name, args) => {
            if (args.length === 0) throw new TypeError(`Failed to construct '${name}': 1 argument required`);
            const resolvedUrl = new nativeUrl(toDomString(args[0]), popup.document.baseURI);
            const url = resolvedUrl.href;
            if (name === 'EventSource') {
                const options = args[1] == null ? {} : nativeObject(args[1]);
                const withCredentials = options.withCredentials;
                return [url, { withCredentials: nativeBoolean(withCredentials) }];
            }
            transportGuards.validateWorkerUrl(resolvedUrl);
            const options = args[1] == null ? {} : nativeObject(args[1]);
            const normalized = {};
            const typeValue = options.type;
            if (typeValue !== undefined) {
                const type = toDomString(typeValue);
                if (!['classic', 'module'].includes(type)) throw new TypeError(`Invalid Worker type '${type}'`);
                normalized.type = type;
            }
            const credentialsValue = options.credentials;
            if (credentialsValue !== undefined) {
                const credentials = toDomString(credentialsValue);
                if (!['omit', 'same-origin', 'include'].includes(credentials)) {
                    throw new TypeError(`Invalid Worker credentials '${credentials}'`);
                }
                normalized.credentials = credentials;
            }
            const nameValue = options.name;
            if (nameValue !== undefined) normalized.name = toDomString(nameValue);
            return [url, normalized];
        };
        const stagedAsyncConstructors = createAsyncConstructors({
            popup,
            runWhenReady,
            normalizeArguments: normalizeConstructorArguments,
            normalizeOperation: transportGuards.normalizeOperation
        });
        const nativeLocation = popup.location;
        const nativeNavigation = popup.navigation;
        let popupFacade;
        const locationFacade = new Proxy({}, {
            get(_, property) {
                const value = nativeReflectGet(nativeLocation, property, nativeLocation);
                if (['assign', 'replace', 'reload'].includes(property)) {
                    return (...args) => { const normalized = transportGuards.normalizeLocationArguments(property, args); return runWhenReady(() => nativeReflectApply(value, nativeLocation, normalized)); };
                }
                if (typeof value !== 'function') return value;
                return (...args) => {
                    const result = nativeReflectApply(value, nativeLocation, args);
                    return result === nativeLocation ? locationFacade : result;
                };
            },
            set(_, property, value) {
                const normalized = transportGuards.normalizeLocationSetter(property, value);
                runWhenReady(() => nativeReflectSet(nativeLocation, property, normalized, nativeLocation));
                return true;
            }
        });
        transportGuards.guardNavigation(nativeNavigation, popup.Navigation);
        const nativeObjects = new WeakMap();
        let stagedDocument, stagedChildWindow;
        const shouldDeferAttribute = (element, attribute) => requestAttributes.has(attribute)
            || attribute === 'style'
            || attribute.startsWith('on')
            || ((element.localName === 'iframe' || element.localName === 'frame') && attribute === 'srcdoc')
            || (element.localName === 'meta' && attribute === 'content');
        let domGuards; const guardDeferredAttributes = (element, initialValues) => {
            if (guardedElements.has(element)) return;
            guardedElements.add(element); domGuards?.guardActivation(element);
            const values = new Map(initialValues); const namespacedValues = new Map();
            const stagesStyleText = element.localName === 'style'; const stagesScriptText = element.localName === 'script' && nativeGetAttribute.call(element, 'type') !== 'application/x-htmltinkerx-staged'; let stagedText = stagesStyleText || stagesScriptText ? popupTextContent.get.call(element) : null; const stagedTextNodes = stagesScriptText ? popup.document.createElement('script') : null; if (stagedTextNodes != null) { nativeSetAttribute.call(stagedTextNodes, 'type', 'application/x-htmltinkerx-staged'); popupTextContent.set.call(stagedTextNodes, stagedText); }
            if (stagesStyleText || stagesScriptText) popupTextContent.set.call(element, '');
            let released = false; const styleGuard = transportGuards.createStyleGuard(element, values, () => released); const sheetGuard = transportGuards.createStyleSheetGuard(element, stagedText, () => released);
            const state = attributeGuards.createState(
                element,
                values,
                namespacedValues,
                () => released,
                shouldDeferAttribute,
                value => stagedDocument(value ?? popup.document), value => stagedChildWindow(value),
                (method, args) => stageElementMarkup(element, method, args),
                clone => guardClonedTree(element, clone),
                attribute => { if (attribute === 'style') styleGuard?.synchronize(); },
                stagesStyleText || stagesScriptText ? { get: () => stagedTextNodes == null ? stagedText : popupTextContent.get.call(stagedTextNodes), set: value => { stagedText = value; if (stagedTextNodes != null) popupTextContent.set.call(stagedTextNodes, value); if (sheetGuard != null) sheetGuard.text = value; }, target: stagedTextNodes } : null);
            if (styleGuard != null) nativeDefineProperty(element, 'style', {
                configurable: false,
                enumerable: true,
                get() { return styleGuard.facade; }
            });
            if (styleGuard?.attributeStyleMapFacade != null) nativeDefineProperty(element, 'attributeStyleMap', {
                configurable: false,
                enumerable: true,
                get() { return styleGuard.attributeStyleMapFacade; }
            });
            const deferredProperties = [...requestAttributes];
            if (element.localName === 'iframe' || element.localName === 'frame') deferredProperties.push(['srcdoc', 'srcdoc']);
            if (element.localName === 'meta') deferredProperties.push(['content', 'content']);
            for (const [attribute, property] of deferredProperties) {
                if (!(property in element)) continue;
                let descriptor = null;
                let prototype = element;
                let descriptorOwner = null;
                while (prototype && descriptor == null) {
                    descriptor = nativeGetOwnPropertyDescriptor(prototype, property);
                    if (descriptor != null) descriptorOwner = prototype;
                    prototype = nativeGetPrototypeOf(prototype);
                }
                if (descriptor == null || descriptor.configurable === false && nativeHasOwnProperty.call(element, property)) continue;
                attributeGuards.installProperty(descriptorOwner, property, attribute);
                nativeDefineProperty(element, property, {
                    configurable: false,
                    enumerable: descriptor.enumerable,
                    get() {
                        if (!released && shouldDeferAttribute(element, attribute) && values.has(attribute)) return transportGuards.normalizeDeferredProperty(attribute, values.get(attribute));
                        return descriptor.get ? descriptor.get.call(element) : '';
                    },
                    set(value) {
                        if (!released && shouldDeferAttribute(element, attribute)) values.set(attribute, nativeString(value));
                        else if (descriptor.set) descriptor.set.call(element, value);
                        else nativeSetAttribute.call(element, attribute, nativeString(value));
                    }
                });
            }
            const elementSetAttribute = element.setAttribute;
            const elementRemoveAttribute = element.removeAttribute;
            nativeDefineProperty(element, 'setAttribute', {
                configurable: false,
                value(name, value) {
                    if (state.setAttribute(name, value)) return;
                    return elementSetAttribute.call(this, name, value);
                }
            });
            nativeDefineProperty(element, 'removeAttribute', {
                configurable: false,
                value(name) {
                    if (state.removeAttribute(name)) return;
                    return elementRemoveAttribute.call(this, name);
                }
            });
            guardedResources.push(() => {
                if (styleGuard != null) styleGuard.release();
                released = true;
                const applyAttributes = target => { for (const [attribute, value] of values) nativeSetAttribute.call(target, attribute, value); for (const value of namespacedValues.values()) nativeSetAttributeNS.call(target, value.namespace, value.qualified, value.value); };
                attributeGuards.release(element);
                if (stagesScriptText) stagedText = popupTextContent.get.call(stagedTextNodes); if (stagesScriptText && element.parentNode != null && !values.has('src')) {
                    const replacement = nativeReflectApply(popupCloneNode, element, [false]); applyAttributes(replacement);
                    popupTextContent.set.call(replacement, stagedText); nativeReflectApply(popupReplaceChild, element.parentNode, [replacement, element]);
                } else {
                    applyAttributes(element); if (stagesStyleText) popupTextContent.set.call(element, sheetGuard?.release() ?? stagedText);
                    else if (stagesScriptText) popupTextContent.set.call(element, stagedText);
                }
            });
        };
        const stageElementMarkup = createMarkupStager({
            popup,
            innerHtml: popupInnerHtml,
            outerHtml: popupOuterHtml,
            insertAdjacentHtml: popupInsertAdjacentHtml,
            reflectApply: nativeReflectApply,
            stringValue: nativeString,
            shouldDeferAttribute,
            guardDeferredAttributes,
            guardedResources
        });
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
        const guardCreatedTree = element => {
            guardCreatedElement(element);
            if (element instanceof popup.Element || element instanceof popup.DocumentFragment) for (const descendant of nativeReflectApply(element instanceof popup.Element ? nativeElementQuerySelectorAll : popupFragmentQuerySelectorAll, element, ['*'])) guardCreatedElement(descendant);
            return element;
        }; domGuards = createDomGuards({ popup, runWhenReady, guardCreatedTree });
        const guardClonedTree = (source, clone) => {
            guardCreatedTree(clone);
            attributeGuards.copy(source, clone);
            if (source instanceof popup.Element && clone instanceof popup.Element) {
                const sourceDescendants = nativeElementQuerySelectorAll.call(source, '*');
                const cloneDescendants = nativeElementQuerySelectorAll.call(clone, '*');
                const count = sourceDescendants.length < cloneDescendants.length
                    ? sourceDescendants.length
                    : cloneDescendants.length;
                for (let index = 0; index < count; index++) {
                    attributeGuards.copy(sourceDescendants[index], cloneDescendants[index]);
                }
            }
            return clone;
        };
        const stagedElementConstructors = new Map();
        for (const name of ['Image', 'Audio']) {
            const constructor = popup[name];
            if (typeof constructor !== 'function') continue;
            stagedElementConstructors.set(name, new Proxy(constructor, {
                construct(target, args) {
                    return guardCreatedTree(nativeReflectConstruct(target, args));
                }
            }));
        }
        const stagedRealmMembers = createRealmGuards({ popup, isReady: () => ready, runWhenReady, shouldDeferAttribute, guardDeferredAttributes, guardedResources, stringValue: toDomString });
        const writeStagedMarkup = (method, args) => {
            const nativeDocument = popup.document;
            documentWriteParts.push(args.map(value => nativeString(value)).join('') + (method === 'writeln' ? '\n' : ''));
            let previousRoot = null;
            if (documentWriteParts.length > 1) {
                previousRoot = nativeDocument.documentElement;
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
            let desiredRoot = null;
            if (previousRoot == null) {
                nativeReflectApply(nativeDocument.write, nativeDocument, [template.innerHTML]);
            } else {
                const stagedDocument = nativeReflectApply(createHtmlDocument, nativeDocument.implementation, ['']);
                nativeReflectApply(stagedDocument.open, stagedDocument, []);
                nativeReflectApply(stagedDocument.write, stagedDocument, [template.innerHTML]);
                nativeReflectApply(stagedDocument.close, stagedDocument, []);
                desiredRoot = stagedDocument.documentElement;
            }
            const reusedWriteNodes = stageElementMarkup.preserveWrittenNodes(nativeDocument, previousRoot, desiredRoot);
            for (const { marker, values, styleText, script } of descriptors) {
                const element = nativeDocument.querySelector(`[data-htmltinkerx-staged-resource="${marker}"]`);
                if (!element) continue;
                element.removeAttribute('data-htmltinkerx-staged-resource');
                if (reusedWriteNodes.has(element)) continue;
                guardDeferredAttributes(element, values);
                if (styleText !== null) {
                    guardedResources.push(() => { element.textContent = styleText; });
                }
                if (script !== null) {
                    guardedResources.push(() => {
                        const replacement = nativeDocument.createElement('script');
                        const attributes = new Map(script.attributes.map(([name, value]) => [name.toLowerCase(), value]));
                        const parserBlocking = !attributes.has('async')
                            && !attributes.has('defer')
                            && nativeString(attributes.get('type') || '').toLowerCase() !== 'module';
                        const blockingExternal = attributes.has('src') && parserBlocking;
                        let completed = null;
                        if (blockingExternal) {
                            completed = new Promise(resolve => {
                                replacement.addEventListener('load', resolve, { once: true });
                                replacement.addEventListener('error', resolve, { once: true });
                            });
                        }
                        for (const [name, value] of script.attributes) replacement.setAttribute(name, value);
                        replacement.textContent = script.text;
                        const stagedCompletion = parserBlocking
                            ? stageElementMarkup.replaceParserBlockingScript(element, replacement, completed)
                            : (element.replaceWith(replacement), completed);
                        if (blockingExternal && documentWriteQueued && !documentCloseQueued) {
                            nativeDocument.close();
                            documentCloseQueued = true;
                        }
                        return stagedCompletion;
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
        const stageReturnedValue = value => {
            transportGuards.guardReturnedNodes(value, guardCreatedTree);
            return value;
        };
        const stagedObject = resolve => {
            const value = resolve();
            if (value === nativeLocation) return locationFacade;
            if (value === popup) return popupFacade;
            if (!value || !isNodeValue(value)) return value;
            if (!isDocumentValue(value)) return stageReturnedValue(value);
            const facade = new Proxy({}, {
                get(_, property) {
                    const target = resolve();
                    const member = nativeReflectGet(target, property, target);
                    if (typeof member !== 'function') {
                        if (!ready) transportGuards.guardReturnedNodes(member, guardCreatedTree);
                        return isNodeValue(member)
                            ? stagedObject(() => nativeReflectGet(resolve(), property, resolve()))
                            : member === popup || member === nativeLocation ? stagedObject(() => member) : member;
                    }
                    return (...args) => {
                        if (!ready && property === 'open' && resolve() === popup.document && args.length < 3) {
                            const current = resolve();
                            const result = nativeReflectApply(nativeReflectGet(current, property, current), current, args);
                            documentWriteParts.length = 0;
                            guardedResources.length = 0;
                            documentWriteQueued = false;
                            documentCloseQueued = false;
                            documentWrittenSynchronously = false;
                            documentMutationQueued = true;
                            return stagedObject(() => result);
                        }
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
                            if (!ready && property === 'createRange') return domGuards.guardRange(initialResult); return ready ? initialResult : stageReturnedValue(initialResult);
                        }
                        const snapshot = transportGuards.snapshotMutationArguments(property, args.map(unwrap));
                        const result = stagedMutationResult(resolve, property, args);
                        documentMutationQueued = true;
                        for (const value of snapshot) guardCreatedTree(value);
                        if (property === 'write' || property === 'writeln') documentWriteQueued = true;
                        if (property === 'close') documentCloseQueued = true;
                        queued.push(() => {
                            const current = resolve();
                            const currentMember = nativeReflectGet(current, property, current);
                            nativeReflectApply(currentMember, current, snapshot);
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
        const isNodeValue = value => { if (!value || typeof value !== 'object') return false; try { nativeReflectApply(popupNodeType, value, []); return true; } catch { return false; } }; const isDocumentValue = value => { try { return nativeReflectApply(popupNodeType, value, []) === 9; } catch { return false; } }; const documentFacade = stagedObject(() => popup.document);
        const frameGuards = createFrameGuards({ popup, defineProperty: nativeDefineProperty, reflectApply: nativeReflectApply, reflectGet: nativeReflectGet, reflectSet: nativeReflectSet, runWhenReady, transportGuards, codeGuards, createDocumentFacade: value => stagedObject(() => value), mainDocumentFacade: documentFacade, mainWindowFacade: () => popupFacade, stagedXhrConstructor, stagedAsyncConstructors });
        stagedDocument = frameGuards.documentFor; stagedChildWindow = frameGuards.windowFor;
        popupFacade = new Proxy(popup, {
            get(targetWindow, property) {
                if (property === 'location') return locationFacade;
                if (property === 'navigation' && nativeNavigation != null) return nativeNavigation;
                if (property === 'document') return documentFacade;
                if (property === 'window' || property === 'self' || property === 'frames') return popupFacade;
                if (property === 'XMLHttpRequest') return stagedXhrConstructor;
                if (stagedRealmMembers.has(property)) return stagedRealmMembers.get(property);
                if (stagedCodeMembers.has(property)) return stagedCodeMembers.get(property);
                if (stagedAsyncConstructors.has(property)) return stagedAsyncConstructors.get(property);
                if (stagedElementConstructors.has(property)) return stagedElementConstructors.get(property);
                const value = nativeReflectGet(targetWindow, property, targetWindow);
                if (value === stagedFetch) return stagedFetch;
                if (value === targetWindow) return popupFacade;
                const guardedWindow = frameGuards.guardReturnedWindow(value); if (guardedWindow !== value) return guardedWindow; return typeof value === 'function' ? value.bind(targetWindow) : value;
            },
            set(targetWindow, property, value) {
                if (property === 'location') {
                    const normalized = transportGuards.normalizeLocationSetter('href', value);
                    runWhenReady(() => { targetWindow.location = normalized; });
                    return true;
                }
                return nativeReflectSet(targetWindow, property, value, targetWindow);
            }
        });
        stagedRealmMembers.registerFacade(popupFacade);
        const nestedWindowOpen = function(url, nestedTarget, nestedFeatures) {
            const resolved = url == null || nativeString(url).length === 0
                ? url
                : new nativeUrl(nativeString(url), popup.document.baseURI).href;
            return openWithReferrerPolicy.call(popup, resolved, nestedTarget, nestedFeatures, '');
        };
        const popupPrototypeOpen = nativeGetOwnPropertyDescriptor(popup.Window.prototype, 'open');
        if (popupPrototypeOpen?.configurable !== false) nativeDefineProperty(popup.Window.prototype, 'open', {
            value: nestedWindowOpen,
            writable: false,
            configurable: false
        });
        const popupOwnOpen = nativeGetOwnPropertyDescriptor(popup, 'open');
        if (popupOwnOpen?.configurable !== false) nativeDefineProperty(popup, 'open', {
            value: nestedWindowOpen,
            writable: false,
            configurable: false
        });
        armBlankPopup(popup, target, complete => {
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
                complete();
            }, 0);
        });
        return popupFacade;
    };
    const openWithReferrerPolicy = function(url, target, features, referrerPolicy) {
        if (url == null || nativeString(url).length === 0 || nativeString(url).toLowerCase() === 'about:blank') {
            return openStagedBlankPopup.call(this, url, target, features);
        }
        const destination = new nativeUrl(nativeString(url), document.baseURI).href;
        const featureTokens = features == null
            ? []
            : nativeString(features).split(',').map(token => token.trim()).filter(nativeBoolean);
        const isEnabled = name => featureTokens.some(token => {
            const parts = token.toLowerCase().split('=', 2);
            return parts[0] === name && (parts.length === 1 || !['0', 'no', 'false'].includes(parts[1]));
        });
        const suppressReferrer = isEnabled('noreferrer');
        const suppressOpener = suppressReferrer || isEnabled('noopener');
        const initialFeatures = suppressOpener
            ? featureTokens.filter(token => !['noopener', 'noreferrer'].includes(token.toLowerCase().split('=', 1)[0])).join(',')
            : features;
        if (specialTargets.includes(normalizedTarget(target)) || targetsExistingFrame(target)) {
            return originalOpen.call(this, destination, target, features);
        }
        const initialTarget = suppressOpener && !specialTargets.includes(normalizedTarget(target)) ? '_blank' : target;
        const popup = openStagedBlankPopup.call(this, '', initialTarget, initialFeatures, releasedPopup => {
            if (suppressReferrer || referrerPolicy) {
                const link = releasedPopup.document.createElement('a');
                link.href = destination;
                if (suppressReferrer) link.rel = 'noreferrer';
                if (referrerPolicy) link.referrerPolicy = referrerPolicy;
                link.target = '_self';
                (releasedPopup.document.body || releasedPopup.document.documentElement).appendChild(link);
                link.click();
            } else releasedPopup.location.href = destination;
        }, () => originalOpen.call(this, destination, target, features));
        if (popup) {
            if (suppressOpener) {
                try { popup.opener = null; } catch { }
            }
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
    const setTemporaryAttribute = (element, name, value) => {
        const existed = nativeHasAttribute.call(element, name);
        const previous = existed ? nativeGetAttribute.call(element, name) : null;
        nativeSetAttribute.call(element, name, value);
        return () => {
            if (existed) nativeSetAttribute.call(element, name, previous);
            else nativeRemoveAttribute.call(element, name);
        };
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

        armBlankPopup(popup, target, complete => {
            const restoreFormTarget = setTemporaryAttribute(form, 'target', submissionTarget);
            const restoreSubmitterTarget = submitter == null
                ? null
                : setTemporaryAttribute(submitter, 'formtarget', submissionTarget);
            const restoreTargets = () => {
                restoreFormTarget();
                if (restoreSubmitterTarget != null) restoreSubmitterTarget();
            };
            try {
                const restoreSubmission = submit();
                restoreAfterPopupNavigation(popup, () => {
                    if (typeof restoreSubmission === 'function') restoreSubmission();
                    restoreTargets();
                });
                complete();
            } catch (error) {
                restoreTargets();
                complete();
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
        return nativeString(method || 'get').toLowerCase() !== 'dialog';
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
                appendSuccessfulControl(`${prefix}x`, nativeString(coordinates.x));
                appendSuccessfulControl(`${prefix}y`, nativeString(coordinates.y));
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
        const restoreTarget = setTemporaryAttribute(form, 'target', '_self');
        try {
            const restoreSubmission = submit();
            let restored = false;
            const restore = () => {
                if (restored) return;
                restored = true;
                if (typeof restoreSubmission === 'function') restoreSubmission();
                restoreTarget();
            };
            globalThis.addEventListener('pagehide', restore, { once: true });
            globalThis.setTimeout(restore, 5000);
        } catch (error) {
            restoreTarget();
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

    const navigateCurrentWithReferrerPolicy = (destination, referrerPolicy) => {
        if (referrerPolicy) {
            const meta = document.createElement('meta');
            meta.name = 'referrer';
            meta.content = referrerPolicy;
            (document.head || document.documentElement).appendChild(meta);
        }
        originalOpen.call(window, destination, '_self');
    };

    const stagedClickAnchor = event => {
        if (event.button !== 0) return null;
        const path = typeof nativeComposedPath === 'function' ? nativeComposedPath.call(event) : [];
        const anchor = path.find(node => node instanceof HTMLAnchorElement)
            || (event.target instanceof Element ? nativeClosest.call(event.target, 'a[href]') : null);
        if (!(anchor instanceof HTMLAnchorElement) || nativeHasAttribute.call(anchor, 'download')) return null;
        const target = effectiveTarget(anchor, null);
        const explicitlyCurrent = hasExplicitEmptyTarget(anchor, null);
        const destination = new nativeUrl(anchor.href, document.baseURI);
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

    afterPagePropagationHandlers('click', event => { const staged = stagedClickAnchor(event); for (const node of staged?.path || []) attributeGuards.guardLegacyHandler(node, 'onclick', pageCancelledEvents); return staged !== null; }, event => {
        if (event.defaultPrevented) return;
        const staged = stagedClickAnchor(event);
        if (staged === null) return;
        const { anchor, target, explicitlyCurrent, destination } = staged;
        const rel = anchor.relList;
        if (explicitlyCurrent) {
            navigateCurrentWithReferrerPolicy(
                destination.href,
                rel.contains('noreferrer') ? 'no-referrer' : anchor.referrerPolicy || '');
            return;
        }

        const features = rel.contains('noreferrer')
            ? 'noreferrer'
            : rel.contains('noopener') || !rel.contains('opener') ? 'noopener' : undefined;
        openWithReferrerPolicy.call(window, destination.href, target, features, anchor.referrerPolicy || '');
    }, recordImageSubmitCoordinates);

    afterPagePropagationHandlers('submit', event => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement)) return false;
        for (const node of nativeComposedPath.call(event)) attributeGuards.guardLegacyHandler(node, 'onsubmit', pageCancelledEvents);
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
