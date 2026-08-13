(() => {
    const defineProperty = Object.defineProperty;
    const getOwnPropertyDescriptor = Object.getOwnPropertyDescriptor;
    const getPrototypeOf = Object.getPrototypeOf;
    const reflectApply = Reflect.apply;
    const reflectConstruct = Reflect.construct;
    const rangeStates = new WeakMap();
    const selectionStates = new WeakMap();
    const activationStates = new WeakMap();
    const imageDecodeStates = new WeakMap();
    const mediaPlayStates = new WeakMap();
    const formSubmissionStates = new WeakMap();
    const installedRangePrototypes = new WeakSet();
    const installedSelectionPrototypes = new WeakSet();
    const installedActivationPrototypes = new WeakSet();
    const installedActivationEventPrototypes = new WeakSet();
    const installedCancellationPrototypes = new WeakSet();
    const cancellationStates = new WeakMap();
    const nativePreventDefault = new WeakMap();
    const nativeDefaultPrevented = new WeakMap();
    const installedImagePrototypes = new WeakSet();
    const installedMediaPrototypes = new WeakSet();
    const installedFormPrototypes = new WeakSet();
    const parserRoutes = new WeakMap();
    const parserConstructors = new WeakMap();
    const installedParserPrototypes = new WeakSet();
    const buttonType = getOwnPropertyDescriptor(HTMLButtonElement.prototype, 'type')?.get;
    const buttonForm = getOwnPropertyDescriptor(HTMLButtonElement.prototype, 'form')?.get;
    const inputType = getOwnPropertyDescriptor(HTMLInputElement.prototype, 'type')?.get;
    const inputForm = getOwnPropertyDescriptor(HTMLInputElement.prototype, 'form')?.get;
    const eventType = getOwnPropertyDescriptor(Event.prototype, 'type')?.get;
    const eventCancelable = getOwnPropertyDescriptor(Event.prototype, 'cancelable')?.get;
    const formAttributes = ['accept-charset', 'action', 'enctype', 'id', 'method', 'name', 'novalidate', 'rel', 'target'];
    const submitterAttributes = ['form', 'formaction', 'formenctype', 'formmethod', 'formnovalidate', 'formtarget', 'name', 'type', 'value'];
    const submitterState = value => {
        try {
            const type = reflectApply(buttonType, value, []);
            return { valid: type === 'submit', form: reflectApply(buttonForm, value, []) };
        } catch { }
        try {
            const type = reflectApply(inputType, value, []);
            return { valid: type === 'submit' || type === 'image', form: reflectApply(inputForm, value, []) };
        } catch { return { valid: false, form: null }; }
    };
    const installRangeRoutes = prototype => {
        if (prototype == null || installedRangePrototypes.has(prototype)) return;
        installedRangePrototypes.add(prototype);
        for (const name of ['cloneContents', 'cloneRange', 'createContextualFragment', 'extractContents', 'insertNode', 'surroundContents']) {
            const method = prototype[name];
            if (typeof method !== 'function') continue;
            defineProperty(prototype, name, {
                configurable: false,
                writable: false,
                value(...args) {
                    const guardTree = rangeStates.get(this);
                    if (guardTree == null) return reflectApply(method, this, args);
                    if (name === 'cloneRange') {
                        const clone = reflectApply(method, this, args);
                        rangeStates.set(clone, guardTree);
                        return clone;
                    }
                    if (name === 'insertNode' || name === 'surroundContents') {
                        if (args.length > 0) guardTree(args[0]);
                        return reflectApply(method, this, args);
                    }
                    return guardTree(reflectApply(method, this, args));
                }
            });
        }
    };
    const installSelectionRoutes = prototype => {
        if (prototype == null || installedSelectionPrototypes.has(prototype)) return;
        installedSelectionPrototypes.add(prototype);
        const getRangeAt = prototype.getRangeAt;
        if (typeof getRangeAt !== 'function') return;
        defineProperty(prototype, 'getRangeAt', {
            configurable: false,
            writable: false,
            value(...args) {
                const range = reflectApply(getRangeAt, this, args);
                const guardTree = selectionStates.get(this);
                if (guardTree != null) rangeStates.set(range, guardTree);
                return range;
            }
        });
    };
    const installActivationRoute = prototype => {
        if (prototype == null || installedActivationPrototypes.has(prototype)) return;
        installedActivationPrototypes.add(prototype);
        const click = prototype.click;
        if (typeof click !== 'function') return;
        defineProperty(prototype, 'click', {
            configurable: false,
            writable: false,
            value(...args) {
                const stage = activationStates.get(this);
                return stage == null ? reflectApply(click, this, args) : stage(click, args);
            }
        });
    };
    const installActivationEventRoute = prototype => {
        if (prototype == null || installedActivationEventPrototypes.has(prototype)) return;
        installedActivationEventPrototypes.add(prototype);
        const dispatchEvent = prototype.dispatchEvent;
        if (typeof dispatchEvent !== 'function') return;
        defineProperty(prototype, 'dispatchEvent', {
            configurable: false,
            writable: false,
            value(...args) {
                const stage = activationStates.get(this);
                let type = null;
                try { type = reflectApply(eventType, args[0], []); } catch { }
                return stage == null || type !== 'click'
                    ? reflectApply(dispatchEvent, this, args)
                    : stage(dispatchEvent, args, true);
            }
        });
    };
    const installCancellationRoute = prototype => {
        if (prototype == null || installedCancellationPrototypes.has(prototype)) return;
        const preventDefault = prototype.preventDefault;
        const defaultPrevented = getOwnPropertyDescriptor(prototype, 'defaultPrevented');
        if (typeof preventDefault !== 'function' || typeof defaultPrevented?.get !== 'function') return;
        installedCancellationPrototypes.add(prototype);
        nativePreventDefault.set(prototype, preventDefault);
        nativeDefaultPrevented.set(prototype, defaultPrevented.get);
        defineProperty(prototype, 'preventDefault', {
            configurable: false,
            writable: false,
            value(...args) {
                const state = cancellationStates.get(this);
                if (state != null) state.cancelled = true;
                return reflectApply(preventDefault, this, args);
            }
        });
        defineProperty(prototype, 'defaultPrevented', {
            ...defaultPrevented,
            configurable: false,
            get() { return cancellationStates.get(this)?.cancelled ?? defaultPrevented.get.call(this); }
        });
    };
    const dispatchWithoutActivation = (target, dispatch, args) => {
        const event = args[0];
        let prototype = getPrototypeOf(event);
        while (prototype != null && !nativePreventDefault.has(prototype)) prototype = getPrototypeOf(prototype);
        const preventDefault = nativePreventDefault.get(prototype);
        const defaultPrevented = nativeDefaultPrevented.get(prototype);
        let cancelable = false;
        try { cancelable = reflectApply(eventCancelable, event, []); } catch { }
        if (!cancelable || preventDefault == null || defaultPrevented == null) return null;
        const state = { cancelled: reflectApply(defaultPrevented, event, []) };
        cancellationStates.set(event, state);
        reflectApply(preventDefault, event, []);
        reflectApply(dispatch, target, args);
        return !state.cancelled;
    };
    const snapshotAttributes = (element, names) => names.map(name => [name, element.hasAttribute(name), element.getAttribute(name)]);
    const applyAttributes = (element, snapshot) => {
        for (const [name, present, value] of snapshot) {
            if (present) element.setAttribute(name, value);
            else element.removeAttribute(name);
        }
    };
    const restorePosition = (element, parent, next) => {
        if (parent == null) { element.remove(); return; }
        parent.insertBefore(element, next?.parentNode === parent ? next : null);
    };
    const snapshotSubmission = (form, submitter, invoke) => {
        const formSnapshot = snapshotAttributes(form, formAttributes);
        const submitterSnapshot = submitter == null ? null : snapshotAttributes(submitter, submitterAttributes);
        return () => {
            const formCurrent = snapshotAttributes(form, formAttributes);
            const submitterCurrent = submitter == null ? null : snapshotAttributes(submitter, submitterAttributes);
            const formParent = form.parentNode;
            const formNext = form.nextSibling;
            const submitterParent = submitter?.parentNode ?? null;
            const submitterNext = submitter?.nextSibling ?? null;
            applyAttributes(form, formSnapshot);
            if (!form.isConnected) (form.ownerDocument.body || form.ownerDocument.documentElement).appendChild(form);
            if (submitter != null) {
                applyAttributes(submitter, submitterSnapshot);
                if (submitter.form !== form) form.appendChild(submitter);
            }
            try { invoke(); }
            finally {
                if (submitter != null) {
                    applyAttributes(submitter, submitterCurrent);
                    restorePosition(submitter, submitterParent, submitterNext);
                }
                applyAttributes(form, formCurrent);
                restorePosition(form, formParent, formNext);
            }
        };
    };
    const installImageDecodeRoute = prototype => {
        if (prototype == null || installedImagePrototypes.has(prototype)) return;
        installedImagePrototypes.add(prototype);
        const decode = prototype.decode;
        if (typeof decode !== 'function') return;
        defineProperty(prototype, 'decode', {
            configurable: false,
            writable: false,
            value(...args) {
                const stage = imageDecodeStates.get(this);
                return stage == null ? reflectApply(decode, this, args) : stage(decode, args);
            }
        });
    };
    const installMediaPlayRoute = prototype => {
        if (prototype == null || installedMediaPrototypes.has(prototype)) return;
        installedMediaPrototypes.add(prototype);
        const play = prototype.play;
        if (typeof play !== 'function') return;
        defineProperty(prototype, 'play', {
            configurable: false,
            writable: false,
            value(...args) {
                const stage = mediaPlayStates.get(this);
                return stage == null ? reflectApply(play, this, args) : stage(play, args);
            }
        });
    };
    const installFormRoutes = prototype => {
        if (prototype == null || installedFormPrototypes.has(prototype)) return;
        installedFormPrototypes.add(prototype);
        for (const name of ['submit', 'requestSubmit']) {
            const method = prototype[name];
            if (typeof method !== 'function') continue;
            defineProperty(prototype, name, {
                configurable: false,
                writable: false,
                value(...args) {
                    const stage = formSubmissionStates.get(this);
                    return stage == null ? reflectApply(method, this, args) : stage(name, method, args);
                }
            });
        }
    };
    const installParserRoute = target => {
        const prototype = target?.DOMParser?.prototype;
        if (prototype == null || installedParserPrototypes.has(prototype)) return;
        const parse = prototype.parseFromString;
        if (typeof parse !== 'function') return;
        installedParserPrototypes.add(prototype);
        defineProperty(prototype, 'parseFromString', {
            configurable: false,
            writable: false,
            value(...args) {
                const route = parserRoutes.get(this);
                return route == null ? reflectApply(parse, this, args) : route(parse, args);
            }
        });
    };
    installParserRoute(globalThis);
    globalThis.__htmlTinkerXCreatePopupDomGuards = ({ popup, isReady, runWhenReady, guardCreatedTree }) => {
        installRangeRoutes(Range.prototype);
        installRangeRoutes(popup.Range?.prototype);
        installSelectionRoutes(Selection.prototype);
        installSelectionRoutes(popup.Selection?.prototype);
        installActivationRoute(HTMLElement.prototype);
        installActivationRoute(popup.HTMLElement?.prototype);
        installActivationEventRoute(EventTarget.prototype);
        installActivationEventRoute(popup.EventTarget?.prototype);
        installCancellationRoute(Event.prototype);
        installCancellationRoute(popup.Event?.prototype);
        installImageDecodeRoute(HTMLImageElement.prototype);
        installImageDecodeRoute(popup.HTMLImageElement?.prototype);
        installMediaPlayRoute(HTMLMediaElement.prototype);
        installMediaPlayRoute(popup.HTMLMediaElement?.prototype);
        installFormRoutes(HTMLFormElement.prototype);
        installFormRoutes(popup.HTMLFormElement?.prototype);
        installParserRoute(popup);
        const constructorFor = target => {
            const existing = parserConstructors.get(target);
            if (existing != null) return existing;
            installParserRoute(target);
            const constructor = target.DOMParser;
            const routed = new Proxy(constructor, {
                construct(current, args, newTarget) {
                    const parser = reflectConstruct(current, args, newTarget === routed ? current : newTarget);
                    parserRoutes.set(parser, (parse, parseArgs) => {
                        const document = reflectApply(parse, parser, parseArgs);
                        if (!isReady()) guardCreatedTree(document.documentElement);
                        return document;
                    });
                    return parser;
                }
            });
            parserConstructors.set(target, routed);
            return routed;
        };
        return {
            constructorFor,
            guardRealm(target) { installParserRoute(target); installRangeRoutes(target?.Range?.prototype); installSelectionRoutes(target?.Selection?.prototype); installActivationEventRoute(target?.EventTarget?.prototype); installFormRoutes(target?.HTMLFormElement?.prototype); },
            guardRange(range) {
                if (range != null) rangeStates.set(range, guardCreatedTree);
                return range;
            },
            guardSelection(selection) {
                if (selection != null) selectionStates.set(selection, guardCreatedTree);
                return selection;
            },
            guardActivation(element) {
                const link = element?.localName === 'a' || element?.localName === 'area';
                if (!link && element?.localName !== 'button' && element?.localName !== 'input') return;
                installActivationRoute(element.ownerDocument?.defaultView?.HTMLElement?.prototype);
                installActivationEventRoute(element.ownerDocument?.defaultView?.EventTarget?.prototype);
                activationStates.set(element, (click, args, queuedResult) => {
                    const state = link ? null : submitterState(element);
                    if (!link && !state.valid) return reflectApply(click, element, args);
                    if (queuedResult) {
                        const result = dispatchWithoutActivation(element, click, args);
                        if (result == null) {
                            runWhenReady(() => reflectApply(click, element, args));
                            return true;
                        }
                        if (!result) return false;
                        if (link) {
                            const clone = element.cloneNode(true);
                            runWhenReady(() => clone.click());
                        } else if (state.form != null) {
                            const form = state.form;
                            runWhenReady(snapshotSubmission(form, element, () => form.requestSubmit(element)));
                        }
                        return true;
                    }
                    runWhenReady(() => {
                        if (link || state.form == null) { reflectApply(click, element, args); return; }
                        const form = state.form;
                        const formParent = form.parentNode;
                        const formNext = form.nextSibling;
                        const submitterParent = element.parentNode;
                        const submitterNext = element.nextSibling;
                        if (!form.isConnected) (form.ownerDocument.body || form.ownerDocument.documentElement).appendChild(form);
                        if (element.form !== form) form.appendChild(element);
                        try { reflectApply(click, element, args); }
                        finally {
                            if (submitterParent == null) element.remove(); else submitterParent.insertBefore(element, submitterNext);
                            if (formParent == null) form.remove(); else formParent.insertBefore(form, formNext);
                        }
                    });
                    return queuedResult;
                });
            },
            guardImageDecode(element) {
                if (element?.localName !== 'img') return;
                installImageDecodeRoute(element.ownerDocument?.defaultView?.HTMLImageElement?.prototype);
                imageDecodeStates.set(element, (decode, args) => new popup.Promise((resolve, reject) => runWhenReady(() => {
                    try { reflectApply(decode, element, args).then(resolve, reject); }
                    catch (error) { reject(error); }
                })));
            },
            guardMediaPlayback(element) {
                if (element?.localName !== 'audio' && element?.localName !== 'video') return;
                installMediaPlayRoute(element.ownerDocument?.defaultView?.HTMLMediaElement?.prototype);
                mediaPlayStates.set(element, (play, args) => new popup.Promise((resolve, reject) => runWhenReady(() => {
                    try { reflectApply(play, element, args).then(resolve, reject); }
                    catch (error) { reject(error); }
                })));
            },
            guardFormSubmission(element) {
                if (element?.localName !== 'form') return;
                installFormRoutes(element.ownerDocument?.defaultView?.HTMLFormElement?.prototype);
                formSubmissionStates.set(element, (name, method, args) => {
                    if (isReady()) return reflectApply(method, element, args);
                    const normalized = [];
                    if (name === 'requestSubmit' && args.length > 0 && args[0] != null) {
                        const submitter = args[0];
                        const state = submitterState(submitter);
                        if (!state.valid) {
                            throw new TypeError("Failed to execute 'requestSubmit': the specified element is not a submit button");
                        }
                        if (state.form !== element) throw new popup.DOMException('The specified element is not owned by this form element', 'NotFoundError');
                        normalized.push(submitter);
                    }
                    const submitter = normalized[0] ?? null;
                    runWhenReady(snapshotSubmission(element, submitter, () => reflectApply(method, element, normalized)));
                    return undefined;
                });
            }
        };
    };
})();
