(() => {
    if (globalThis.__htmlTinkerXPopupNavigationShimInstalled === true) return;
    Object.defineProperty(globalThis, '__htmlTinkerXPopupNavigationShimInstalled', {
        value: true,
        configurable: false
    });
    const originalOpen = window.open;
    const originalSubmit = HTMLFormElement.prototype.submit;
    const originalAddEventListener = EventTarget.prototype.addEventListener;
    const originalRemoveEventListener = EventTarget.prototype.removeEventListener;
    const originalPreventDefault = Event.prototype.preventDefault;
    const defaultPreventedDescriptor = Object.getOwnPropertyDescriptor(Event.prototype, 'defaultPrevented');
    const internallyCancelledEvents = new WeakSet();
    const pageCancelledEvents = new WeakSet();
    const specialTargets = ['_self', '_parent', '_top'];
    const imageSubmitCoordinates = new WeakMap();

    Event.prototype.preventDefault = function() {
        pageCancelledEvents.add(this);
        return originalPreventDefault.call(this);
    };
    Object.defineProperty(Event.prototype, 'defaultPrevented', {
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
            for (const frame of currentDocument.querySelectorAll('iframe, frame')) {
                if (frame.getAttribute('name') === expected) return true;
                try {
                    if (frame.contentDocument && containsNamedFrame(frame.contentDocument)) return true;
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
            try { delete popup.__htmlTinkerXReleasePopupNavigation; } catch { }
            navigate();
        };
        popup.__htmlTinkerXReleasePopupNavigation = release;
        if (popup.__htmlTinkerXPopupHeadersReady === true) {
            release();
        }
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
        const queued = [];
        const guardedResources = [];
        const requestAttributes = new Map([
            ['src', 'src'], ['srcset', 'srcset'], ['href', 'href'], ['action', 'action'],
            ['poster', 'poster'], ['data', 'data'], ['formaction', 'formAction']
        ]);
        const runWhenReady = action => {
            if (ready) action();
            else queued.push(action);
        };
        const openerFetch = globalThis.fetch.bind(globalThis);
        popup.fetch = (...args) => new Promise((resolve, reject) => {
            runWhenReady(() => openerFetch(...args).then(resolve, reject));
        });
        const nativeLocation = popup.location;
        let popupFacade;
        const locationFacade = new Proxy({}, {
            get(_, property) {
                const value = Reflect.get(nativeLocation, property, nativeLocation);
                if (['assign', 'replace', 'reload'].includes(property)) {
                    return (...args) => runWhenReady(() => Reflect.apply(value, nativeLocation, args));
                }
                return typeof value === 'function' ? value.bind(nativeLocation) : value;
            },
            set(_, property, value) {
                runWhenReady(() => Reflect.set(nativeLocation, property, value, nativeLocation));
                return true;
            }
        });
        const nativeObjects = new WeakMap();
        const guardResourceAttributes = (element, initialValues) => {
            const values = new Map(initialValues);
            const guardedProperties = [];
            for (const [attribute, property] of requestAttributes) {
                if (!(property in element)) continue;
                let descriptor = null;
                let prototype = element;
                while (prototype && descriptor == null) {
                    descriptor = Object.getOwnPropertyDescriptor(prototype, property);
                    prototype = Object.getPrototypeOf(prototype);
                }
                if (descriptor == null || descriptor.configurable === false && Object.prototype.hasOwnProperty.call(element, property)) continue;
                Object.defineProperty(element, property, {
                    configurable: true,
                    enumerable: descriptor.enumerable,
                    get() {
                        if (values.has(attribute)) return values.get(attribute);
                        return descriptor.get ? descriptor.get.call(element) : '';
                    },
                    set(value) { values.set(attribute, String(value)); }
                });
                guardedProperties.push([attribute, property]);
            }
            const nativeSetAttribute = element.setAttribute;
            const nativeRemoveAttribute = element.removeAttribute;
            Object.defineProperty(element, 'setAttribute', {
                configurable: true,
                value(name, value) {
                    const attribute = String(name).toLowerCase();
                    if (requestAttributes.has(attribute)) {
                        values.set(attribute, String(value));
                        return;
                    }
                    return nativeSetAttribute.call(this, name, value);
                }
            });
            Object.defineProperty(element, 'removeAttribute', {
                configurable: true,
                value(name) {
                    const attribute = String(name).toLowerCase();
                    if (requestAttributes.has(attribute)) {
                        values.delete(attribute);
                        return;
                    }
                    return nativeRemoveAttribute.call(this, name);
                }
            });
            guardedResources.push(() => {
                for (const [, property] of guardedProperties) delete element[property];
                delete element.setAttribute;
                delete element.removeAttribute;
                for (const [attribute, value] of values) nativeSetAttribute.call(element, attribute, value);
            });
        };
        const writeStagedMarkup = (method, args) => {
            const nativeDocument = popup.document;
            const template = nativeDocument.createElement('template');
            template.innerHTML = args.map(value => String(value)).join('');
            const descriptors = [];
            let markerIndex = 0;
            for (const element of template.content.querySelectorAll('*')) {
                const values = [];
                for (const attribute of requestAttributes.keys()) {
                    if (!element.hasAttribute(attribute)) continue;
                    values.push([attribute, element.getAttribute(attribute)]);
                    element.removeAttribute(attribute);
                }
                const marker = `htmltinkerx-${Date.now()}-${markerIndex++}-${Math.random().toString(36).slice(2)}`;
                element.setAttribute('data-htmltinkerx-staged-resource', marker);
                descriptors.push([marker, values]);
            }
            const nativeWrite = method === 'writeln'
                ? nativeDocument.writeln
                : nativeDocument.write;
            Reflect.apply(nativeWrite, nativeDocument, [template.innerHTML]);
            for (const [marker, values] of descriptors) {
                const element = nativeDocument.querySelector(`[data-htmltinkerx-staged-resource="${marker}"]`);
                if (!element) continue;
                element.removeAttribute('data-htmltinkerx-staged-resource');
                guardResourceAttributes(element, values);
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
        const stagedObject = resolve => {
            const value = resolve();
            if (value === nativeLocation) return locationFacade;
            if (value === popup) return popupFacade;
            if (!value || !(value instanceof popup.Node)) return value;
            const facade = new Proxy({}, {
                get(_, property) {
                    const target = resolve();
                    const member = Reflect.get(target, property, target);
                    if (typeof member !== 'function') {
                        return member instanceof popup.Node
                            ? stagedObject(() => Reflect.get(resolve(), property, resolve()))
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
                                const currentMember = Reflect.get(current, property, current);
                                return Reflect.apply(currentMember, current, args.map(unwrap));
                            };
                            const initialResult = invoke();
                            if (!initialResult || !(initialResult instanceof popup.Node)) return initialResult;
                            return initialResult;
                        }
                        const result = stagedMutationResult(resolve, property, args);
                        documentMutationQueued = true;
                        if (property === 'write' || property === 'writeln') documentWriteQueued = true;
                        if (property === 'close') documentCloseQueued = true;
                        queued.push(() => {
                            const current = resolve();
                            const currentMember = Reflect.get(current, property, current);
                            Reflect.apply(currentMember, current, args.map(unwrap));
                        });
                        return result;
                    };
                },
                set(_, property, valueToSet) {
                    if (!ready) documentMutationQueued = true;
                    runWhenReady(() => {
                        const current = resolve();
                        Reflect.set(current, property, unwrap(valueToSet), current);
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
                const value = Reflect.get(targetWindow, property, targetWindow);
                return typeof value === 'function' ? value.bind(targetWindow) : value;
            },
            set(targetWindow, property, value) {
                if (property === 'location') {
                    runWhenReady(() => { targetWindow.location = value; });
                    return true;
                }
                return Reflect.set(targetWindow, property, value, targetWindow);
            }
        });
        armBlankPopup(popup, target, () => {
            // Run document replacement from the opener realm. Performing document.open()
            // in the popup's release evaluation destroys that evaluation context before
            // queued mutations can be replayed.
            globalThis.setTimeout(() => {
                if (documentMutationQueued && !documentWrittenSynchronously) {
                    popup.document.open();
                    popup.document.write('<!doctype html><html><head></head><body></body></html>');
                    popup.document.close();
                }
                ready = true;
                while (guardedResources.length > 0) guardedResources.shift()();
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
    Object.defineProperty(Window.prototype, 'open', {
        value: stagedWindowOpen,
        writable: false,
        configurable: false
    });
    Object.defineProperty(window, 'open', {
        value: stagedWindowOpen,
        writable: false,
        configurable: false
    });

    const effectiveTarget = (element, submitter) => {
        if (submitter != null && submitter.hasAttribute('formtarget')) {
            return submitter.getAttribute('formtarget') || '';
        }
        if (element.hasAttribute('target')) {
            return element.getAttribute('target') || '';
        }
        const base = document.querySelector('base[target]');
        return base == null ? '' : base.getAttribute('target') || '';
    };

    const hasExplicitEmptyTarget = (element, submitter) => {
        if (submitter != null && submitter.hasAttribute('formtarget')) {
            return (submitter.getAttribute('formtarget') || '') === '';
        }
        return element.hasAttribute('target') && (element.getAttribute('target') || '') === '';
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
        const action = new URL(submitter?.formAction || form.action || document.URL, document.baseURI);
        return String(submitter?.formMethod || form.method).toLowerCase() !== 'dialog';
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
                if (!submitter.hasAttribute(submitterAttribute)) continue;
                previous.push([formAttribute, form.getAttribute(formAttribute)]);
                form.setAttribute(formAttribute, submitter.getAttribute(submitterAttribute));
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
            } else if (!submitter.disabled && submitter.getAttribute('name')) {
                appendSuccessfulControl(submitter.getAttribute('name'), submitter.value || '');
            }
        }
        const restore = () => {
            for (const control of successfulControls) control.remove();
            for (const [attribute, value] of previous) {
                if (value === null) form.removeAttribute(attribute);
                else form.setAttribute(attribute, value);
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
        const path = typeof event.composedPath === 'function' ? event.composedPath() : [];
        const anchor = path.find(node => node instanceof HTMLAnchorElement)
            || (event.target instanceof Element ? event.target.closest('a[href]') : null);
        if (!(anchor instanceof HTMLAnchorElement) || anchor.hasAttribute('download')) return null;
        const target = effectiveTarget(anchor, null);
        const explicitlyCurrent = hasExplicitEmptyTarget(anchor, null);
        const destination = new URL(anchor.href, document.baseURI);
        if (!explicitlyCurrent
            && specialTargets.includes(normalizedDeclarativeTarget(target))) return null;
        return { anchor, target, explicitlyCurrent, destination, path };
    };

    const recordImageSubmitCoordinates = event => {
        if (event.button !== 0) return;
        const path = typeof event.composedPath === 'function' ? event.composedPath() : [];
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
