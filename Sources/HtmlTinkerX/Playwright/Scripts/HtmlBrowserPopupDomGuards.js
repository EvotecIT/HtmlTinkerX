(() => {
    const defineProperty = Object.defineProperty;
    const reflectApply = Reflect.apply;
    const rangeStates = new WeakMap();
    const activationStates = new WeakMap();
    const installedRangePrototypes = new WeakSet();
    const installedActivationPrototypes = new WeakSet();
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
    globalThis.__htmlTinkerXCreatePopupDomGuards = ({ popup, runWhenReady, guardCreatedTree }) => {
        installRangeRoutes(Range.prototype);
        installRangeRoutes(popup.Range?.prototype);
        installActivationRoute(HTMLElement.prototype);
        installActivationRoute(popup.HTMLElement?.prototype);
        return {
            guardRange(range) {
                if (range != null) rangeStates.set(range, guardCreatedTree);
                return range;
            },
            guardActivation(element) {
                if (element?.localName !== 'a' && element?.localName !== 'area') return;
                activationStates.set(element, (click, args) => {
                    runWhenReady(() => reflectApply(click, element, args));
                    return undefined;
                });
            }
        };
    };
})();
