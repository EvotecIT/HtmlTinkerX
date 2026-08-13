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
        const guardCache = (cache, document = popup.document) => {
            if (cache == null) return cache;
            cacheStates.set(cache, (name, method, args) => {
                let normalized;
                try {
                    if (args.length === 0) throw new TypeError(`Failed to execute '${name}': 1 argument required`);
                    normalized = name === 'addAll'
                        ? [Array.from(args[0], value => normalizeRequest(value, document))]
                        : [normalizeRequest(args[0], document)];
                } catch (error) { return popup.Promise.reject(error); }
                return new popup.Promise((resolve, reject) => runWhenReady(() => {
                    try { reflectApply(method, cache, normalized).then(resolve, reject); }
                    catch (error) { reject(error); }
                }));
            });
            return cache;
        };
        const guardWindow = target => {
            if (target == null) return;
            installCacheRoutes(target.Cache?.prototype);
            installStorageRoute(target.CacheStorage?.prototype);
            if (target.caches != null) storageStates.set(target.caches, cache => guardCache(cache, target.document));
        };
        guardWindow(popup);
        return { guardCache, guardWindow };
    };
})();
