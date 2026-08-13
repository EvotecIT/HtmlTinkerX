(() => {
    const proxy = Proxy;
    globalThis.__htmlTinkerXCreatePopupAnimatedAttributeGuard = ({
        target,
        readNative,
        isReleased,
        isDeferred,
        hasStaged,
        readStaged,
        writeStaged,
        normalize,
        stringValue,
        reflectGet,
        reflectSet
    }) => {
        let facade = null;
        return () => {
            if (facade != null) return facade;
            const animated = readNative();
            if (animated == null || typeof animated !== 'object' || !('baseVal' in animated) || !('animVal' in animated)) return null;
            facade = new proxy(animated, {
                get(value, member) {
                    if ((member === 'baseVal' || member === 'animVal') && !isReleased() && isDeferred() && hasStaged()) return normalize(readStaged());
                    const result = reflectGet(value, member, value);
                    return typeof result === 'function' ? result.bind(value) : result;
                },
                set(value, member, next) {
                    if (member === 'baseVal' && !isReleased() && isDeferred()) {
                        writeStaged(stringValue(next));
                        return true;
                    }
                    return reflectSet(value, member, next, value);
                }
            });
            return facade;
        };
    };
})();
