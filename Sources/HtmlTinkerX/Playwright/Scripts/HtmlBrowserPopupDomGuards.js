(() => {
    const defineProperty = Object.defineProperty;
    const reflectApply = Reflect.apply;
    const rangeStates = new WeakMap();
    const activationStates = new WeakMap();
    const imageDecodeStates = new WeakMap();
    const mediaPlayStates = new WeakMap();
    const installedRangePrototypes = new WeakSet();
    const installedActivationPrototypes = new WeakSet();
    const installedImagePrototypes = new WeakSet();
    const installedMediaPrototypes = new WeakSet();
    const installRangeRoutes = prototype => {
        if (prototype == null || installedRangePrototypes.has(prototype)) return;
        installedRangePrototypes.add(prototype);
        for (const name of ['cloneContents', 'createContextualFragment', 'extractContents', 'insertNode', 'surroundContents']) {
            const method = prototype[name];
            if (typeof method !== 'function') continue;
            defineProperty(prototype, name, {
                configurable: false,
                writable: false,
                value(...args) {
                    const guardTree = rangeStates.get(this);
                    if (guardTree == null) return reflectApply(method, this, args);
                    if (name === 'insertNode' || name === 'surroundContents') {
                        if (args.length > 0) guardTree(args[0]);
                        return reflectApply(method, this, args);
                    }
                    return guardTree(reflectApply(method, this, args));
                }
            });
        }
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
    globalThis.__htmlTinkerXCreatePopupDomGuards = ({ popup, runWhenReady, guardCreatedTree }) => {
        installRangeRoutes(Range.prototype);
        installRangeRoutes(popup.Range?.prototype);
        installActivationRoute(HTMLElement.prototype);
        installActivationRoute(popup.HTMLElement?.prototype);
        installImageDecodeRoute(HTMLImageElement.prototype);
        installImageDecodeRoute(popup.HTMLImageElement?.prototype);
        installMediaPlayRoute(HTMLMediaElement.prototype);
        installMediaPlayRoute(popup.HTMLMediaElement?.prototype);
        return {
            guardRange(range) {
                if (range != null) rangeStates.set(range, guardCreatedTree);
                return range;
            },
            guardActivation(element) {
                if (element?.localName !== 'a' && element?.localName !== 'area') return;
                installActivationRoute(element.ownerDocument?.defaultView?.HTMLElement?.prototype);
                activationStates.set(element, (click, args) => {
                    runWhenReady(() => reflectApply(click, element, args));
                    return undefined;
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
            }
        };
    };
})();
