(() => {
    const bind = Function.prototype.bind;
    const createFrameGuards = ({ popup, defineProperty, getOwnPropertyDescriptor, reflectApply, reflectConstruct, reflectGet, reflectSet, isReady, runWhenReady, transportGuards, cacheGuards, codeGuards, domGuards, guardCreatedTree, guardRealm, createDocumentFacade, mainDocumentFacade, mainWindowFacade, stagedXhrConstructor, stagedAsyncConstructors, stringValue, openForWindow }) => {
        const windows = new WeakMap();
        const locations = new WeakMap();
        const fetches = new WeakMap();
        const elementConstructors = new WeakMap();
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
        const elementConstructorsFor = targetWindow => {
            const existing = elementConstructors.get(targetWindow);
            if (existing != null) return existing;
            const constructors = new Map();
            for (const name of ['Image', 'Audio']) {
                const constructor = targetWindow[name];
                if (typeof constructor !== 'function') continue;
                constructors.set(name, new Proxy(constructor, {
                    construct(target, args, newTarget) {
                        if (name === 'Audio' && !isReady() && args.length > 0) {
                            const source = stringValue(args[0]);
                            const audio = guardCreatedTree(reflectConstruct(target, [], newTarget));
                            audio.src = source;
                            return audio;
                        }
                        return guardCreatedTree(reflectConstruct(target, args, newTarget));
                    }
                }));
            }
            elementConstructors.set(targetWindow, constructors);
            return constructors;
        };
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
            const asyncConstructors = stagedAsyncConstructors.forWindow(target);
            const elementConstructors = elementConstructorsFor(target);
            const stagedOpen = openForWindow(target);
            const openDescriptor = getOwnPropertyDescriptor(target.Window.prototype, 'open');
            if (openDescriptor?.configurable !== false) defineProperty(target.Window.prototype, 'open', { ...openDescriptor, value: stagedOpen, writable: false, configurable: false });
            const ownOpenDescriptor = getOwnPropertyDescriptor(target, 'open');
            if (ownOpenDescriptor?.configurable !== false) defineProperty(target, 'open', { value: stagedOpen, writable: false, configurable: false });
            for (const [name, constructor] of elementConstructors) {
                const descriptor = getOwnPropertyDescriptor(target.Window.prototype, name);
                if (descriptor?.configurable !== false) defineProperty(target.Window.prototype, name, { ...descriptor, value: constructor, writable: false, configurable: false });
                const ownDescriptor = getOwnPropertyDescriptor(target, name);
                if (ownDescriptor?.configurable !== false) defineProperty(target, name, { value: constructor, writable: false, configurable: false });
            }
            let facade;
            const overrides = new Set();
            facade = new Proxy({}, {
                get(_, property) {
                    if (overrides.has(property)) { const value = reflectGet(target, property, target); return typeof value === 'function' ? reflectApply(bind, value, [target]) : value; }
                    if (property === 'document') return documentFor(target.document);
                    if (property === 'window' || property === 'self' || property === 'frames') return facade;
                    if (property === 'parent' || property === 'top') {
                        const ancestor = reflectGet(target, property, target);
                        if (ancestor === popup) return mainWindowFacade();
                        return ancestor === target ? facade : windowFor(ancestor);
                    }
                    if (property === 'location') return locationFor(target);
                    if (property === 'fetch') return fetchFor(target);
                    if (property === 'XMLHttpRequest') return stagedXhrConstructor.forWindow(target);
                    if (property === 'navigation' && target.navigation != null) {
                        transportGuards.guardNavigation(target.navigation, target.Navigation, () => target.document);
                        return target.navigation;
                    }
                    if (property === 'navigator') {
                        transportGuards.guardNavigator(target.navigator, target.Navigator, () => target.document);
                        return target.navigator;
                    }
                    if (property === 'CSS') return transportGuards.guardCss(target.CSS, () => target.document);
                    if (property === 'AudioContext' || property === 'OfflineAudioContext' || property === 'webkitAudioContext') return transportGuards.audioContextConstructorFor(target, property, () => target.document);
                    if (property === 'getSelection') return (...args) => domGuards.guardSelection(reflectApply(target.getSelection, target, args));
                    if (property === 'DOMParser') return domGuards.constructorFor(target);
                    if (property === 'Request') return transportGuards.requestConstructorFor(target);
                    if (property === 'open') return stagedOpen;
                    if (property === 'close') return (...args) => runWhenReady(() => reflectApply(target.close, target, args));
                    if (codeMembers.has(property)) return codeMembers.get(property);
                    if (asyncConstructors.has(property)) return asyncConstructors.get(property);
                    if (elementConstructors.has(property)) return elementConstructors.get(property);
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
                    overrides.add(property);
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
        return { documentFor, windowFor, guardReturnedWindow, elementConstructorsFor };
    };
    Object.defineProperty(globalThis, '__htmlTinkerXCreatePopupFrameGuards', { value: createFrameGuards, configurable: true });
})();
