(() => {
    const defineProperty = Object.defineProperty;
    const reflectApply = Reflect.apply;
    const cacheStates = new WeakMap();
    const storageStates = new WeakMap();
    const installedCachePrototypes = new WeakSet();
    const installedStoragePrototypes = new WeakSet();
    const installCacheRoutes = prototype => {
        if (prototype == null || installedCachePrototypes.has(prototype)) return;
        installedCachePrototypes.add(prototype);
        for (const name of ['add', 'addAll']) {
            const method = prototype[name];
            if (typeof method !== 'function') continue;
            defineProperty(prototype, name, {
                configurable: false,
                writable: false,
                value(...args) {
                    const stage = cacheStates.get(this);
                    return stage == null ? reflectApply(method, this, args) : stage(name, method, args);
                }
            });
        }
    };
    const installStorageRoute = prototype => {
        if (prototype == null || installedStoragePrototypes.has(prototype)) return;
        installedStoragePrototypes.add(prototype);
        const open = prototype.open;
        if (typeof open !== 'function') return;
        defineProperty(prototype, 'open', {
            configurable: false,
            writable: false,
            value(...args) {
                const guard = storageStates.get(this);
                const result = reflectApply(open, this, args);
                return guard == null ? result : result.then(guard);
            }
        });
    };
    installCacheRoutes(globalThis.Cache?.prototype);
    installStorageRoute(globalThis.CacheStorage?.prototype);
    globalThis.__htmlTinkerXCreatePopupCacheGuards = ({ popup, runWhenReady, normalizeRequest }) => {
        installCacheRoutes(popup.Cache?.prototype);
        installStorageRoute(popup.CacheStorage?.prototype);
        const guardCache = cache => {
            if (cache == null) return cache;
            cacheStates.set(cache, (name, method, args) => {
                let normalized;
                try {
                    if (args.length === 0) throw new TypeError(`Failed to execute '${name}': 1 argument required`);
                    normalized = name === 'addAll'
                        ? [Array.from(args[0], normalizeRequest)]
                        : [normalizeRequest(args[0])];
                } catch (error) { return popup.Promise.reject(error); }
                return new popup.Promise((resolve, reject) => runWhenReady(() => {
                    try { reflectApply(method, cache, normalized).then(resolve, reject); }
                    catch (error) { reject(error); }
                }));
            });
            return cache;
        };
        if (popup.caches != null) storageStates.set(popup.caches, guardCache);
        return { guardCache };
    };
})();
