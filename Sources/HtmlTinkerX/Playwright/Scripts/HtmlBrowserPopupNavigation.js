(() => {
    if (globalThis.__htmlTinkerXPopupNavigationShimInstalled === true) return;
    Object.defineProperty(globalThis, '__htmlTinkerXPopupNavigationShimInstalled', {
        value: true,
        configurable: false
    });
    const originalOpen = window.open;
    const originalSubmit = HTMLFormElement.prototype.submit;
    const originalAddEventListener = EventTarget.prototype.addEventListener;
    const specialTargets = ['_self', '_parent', '_top'];
    const imageSubmitCoordinates = new WeakMap();

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
        const queued = [];
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
                        if (!mutationMethods.has(property) || ready) {
                            const invoke = () => {
                                const current = resolve();
                                const currentMember = Reflect.get(current, property, current);
                                return Reflect.apply(currentMember, current, args.map(unwrap));
                            };
                            const initialResult = invoke();
                            if (!initialResult || !(initialResult instanceof popup.Node)) return initialResult;
                            let currentResult = initialResult;
                            if (!ready) queued.push(() => { currentResult = invoke(); });
                            return stagedObject(() => currentResult);
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
                return Reflect.get(targetWindow, property, targetWindow);
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
                if (documentMutationQueued) {
                    popup.document.open();
                    popup.document.write('<!doctype html><html><head></head><body></body></html>');
                    popup.document.close();
                }
                ready = true;
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

    window.open = function(url, target, features) {
        return openWithReferrerPolicy.call(this, url, target, features, '');
    };

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
        const target = effectiveTarget(form, submitter);
        const normalized = normalizedDeclarativeTarget(target);
        if (specialTargets.includes(normalized)) return false;
        if (targetsExistingFrame(target)) return false;
        const action = new URL(submitter?.formAction || form.action || document.URL, document.baseURI);
        if (action.origin !== location.origin || String(submitter?.formMethod || form.method).toLowerCase() === 'dialog') return false;

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

    const afterPageWindowHandlers = (type, handler) => {
        originalAddEventListener.call(window, type, event => {
            originalAddEventListener.call(window, type, current => {
                if (current === event) handler(current);
            }, { capture: false, once: true });
        }, true);
    };

    afterPageWindowHandlers('click', event => {
        if (event.defaultPrevented || event.button !== 0) return;
        const path = typeof event.composedPath === 'function' ? event.composedPath() : [];
        const imageSubmitter = path.find(node => node instanceof HTMLInputElement && node.type.toLowerCase() === 'image');
        if (imageSubmitter instanceof HTMLInputElement) {
            imageSubmitCoordinates.set(imageSubmitter, {
                x: Math.max(0, Math.floor(event.offsetX || 0)),
                y: Math.max(0, Math.floor(event.offsetY || 0))
            });
        }
        const anchor = path.find(node => node instanceof HTMLAnchorElement)
            || (event.target instanceof Element ? event.target.closest('a[href]') : null);
        if (!(anchor instanceof HTMLAnchorElement) || anchor.hasAttribute('download')) return;
        const target = effectiveTarget(anchor, null);
        const explicitlyCurrent = hasExplicitEmptyTarget(anchor, null);
        const destination = new URL(anchor.href, document.baseURI);
        if (explicitlyCurrent) {
            event.preventDefault();
            originalOpen.call(window, destination.href, '_self');
            return;
        }
        if (specialTargets.includes(normalizedDeclarativeTarget(target))) return;
        if (destination.origin !== location.origin) return;

        event.preventDefault();
        const rel = anchor.relList;
        const features = rel.contains('noreferrer')
            ? 'noreferrer'
            : rel.contains('noopener') || !rel.contains('opener') ? 'noopener' : undefined;
        openWithReferrerPolicy.call(window, destination.href, target, features, anchor.referrerPolicy || '');
    });

    afterPageWindowHandlers('submit', event => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || event.defaultPrevented) return;
        const submitter = event.submitter instanceof HTMLElement ? event.submitter : null;
        if (hasExplicitEmptyTarget(form, submitter)) {
            event.preventDefault();
            submitInCurrentContext(form, () => submitWithoutRedispatch(form, submitter));
            return;
        }
        if (deferPopupFormSubmission(form, submitter, () => submitWithoutRedispatch(form, submitter))) event.preventDefault();
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
