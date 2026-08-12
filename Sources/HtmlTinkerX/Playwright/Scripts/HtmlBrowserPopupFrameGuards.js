(() => {
    const bind = Function.prototype.bind;
    const createFrameGuards = ({ popup, defineProperty, reflectApply, reflectGet, reflectSet, runWhenReady, transportGuards, cacheGuards, codeGuards, guardRealm, createDocumentFacade, mainDocumentFacade, mainWindowFacade, stagedXhrConstructor, stagedAsyncConstructors }) => {
        const windows = new WeakMap();
        const locations = new WeakMap();
        const fetches = new WeakMap();
        const documents = new WeakMap([[popup.document, mainDocumentFacade]]);
        const documentFor = value => {
            if (value == null) return value;
            stagedAsyncConstructors.guardFontSet(value);
            const existing = documents.get(value);
            if (existing != null) return existing;
            const facade = createDocumentFacade(value);
            documents.set(value, facade);
            return facade;
        };
        const popupClose = popup.close;
        const stagedClose = (...args) => runWhenReady(() => reflectApply(popupClose, popup, args));
        defineProperty(popup.Window.prototype, 'close', { value: stagedClose, writable: false, configurable: false });
        const locationFor = target => {
            const location = target.location;
            const existing = locations.get(location);
            if (existing != null) return existing;
            const facade = new Proxy({}, {
                get(_, property) {
                    const value = reflectGet(location, property, location);
                    if (['assign', 'replace', 'reload'].includes(property)) return (...args) => {
                        const normalized = transportGuards.normalizeLocationArguments(property, args, transportGuards.documentBaseFor(target.document));
                        runWhenReady(() => reflectApply(value, location, normalized));
                    };
                    return typeof value === 'function' ? reflectApply(bind, value, [location]) : value;
                },
                set(_, property, value) {
                    const normalized = transportGuards.normalizeLocationSetter(property, value, transportGuards.documentBaseFor(target.document));
                    runWhenReady(() => reflectSet(location, property, normalized, location));
                    return true;
                }
            });
            locations.set(location, facade);
            return facade;
        };
        const fetchFor = target => {
            const existing = fetches.get(target);
            if (existing != null) return existing;
            const nativeFetch = reflectApply(bind, target.fetch, [target]);
            const staged = (...args) => {
                let snapshot;
                try { snapshot = transportGuards.snapshotFetchArguments(args, transportGuards.documentBaseFor(target.document)); }
                catch (error) { return Promise.reject(error); }
                return new Promise((resolve, reject) => runWhenReady(() => {
                    try { nativeFetch(...snapshot).then(resolve, reject); }
                    catch (error) { reject(error); }
                }));
            };
            fetches.set(target, staged);
            return staged;
        };
        const windowFor = target => {
            const existing = windows.get(target);
            if (existing != null) return existing;
            guardRealm(target);
            cacheGuards.guardWindow(target);
            stagedAsyncConstructors.guardFontSet(target.document);
            const codeMembers = codeGuards.forWindow(target);
            let facade;
            facade = new Proxy({}, {
                get(_, property) {
                    if (property === 'document') return documentFor(target.document);
                    if (property === 'window' || property === 'self' || property === 'frames') return facade;
                    if (property === 'parent' || property === 'top') {
                        const ancestor = reflectGet(target, property, target);
                        if (ancestor === popup) return mainWindowFacade();
                        return ancestor === target ? facade : windowFor(ancestor);
                    }
                    if (property === 'location') return locationFor(target);
                    if (property === 'fetch') return fetchFor(target);
                    if (property === 'XMLHttpRequest') return stagedXhrConstructor;
                    if (property === 'navigation' && target.navigation != null) {
                        transportGuards.guardNavigation(target.navigation, target.Navigation);
                        return target.navigation;
                    }
                    if (property === 'navigator') {
                        transportGuards.guardNavigator(target.navigator, target.Navigator);
                        return target.navigator;
                    }
                    if (property === 'close') return (...args) => runWhenReady(() => reflectApply(target.close, target, args));
                    if (codeMembers.has(property)) return codeMembers.get(property);
                    if (stagedAsyncConstructors.has(property)) return stagedAsyncConstructors.get(property);
                    const value = reflectGet(target, property, target);
                    if (value === target) return facade;
                    if (value === popup) return mainWindowFacade();
                    try { if (value != null && reflectGet(value, 'window', value) === value) return windowFor(value); } catch { }
                    return typeof value === 'function' ? reflectApply(bind, value, [target]) : value;
                },
                set(_, property, value) {
                    if (property === 'location') {
                        const normalized = transportGuards.normalizeLocationSetter('href', value, transportGuards.documentBaseFor(target.document));
                        runWhenReady(() => reflectSet(target, 'location', normalized, target));
                        return true;
                    }
                    return reflectSet(target, property, value, target);
                }
            });
            windows.set(target, facade);
            return facade;
        };
        const guardReturnedWindow = value => {
            if (value === popup) return mainWindowFacade();
            try { return value != null && reflectGet(value, 'window', value) === value ? windowFor(value) : value; }
            catch { return value; }
        };
        return { documentFor, windowFor, guardReturnedWindow };
    };
    Object.defineProperty(globalThis, '__htmlTinkerXCreatePopupFrameGuards', { value: createFrameGuards, configurable: true });
})();
