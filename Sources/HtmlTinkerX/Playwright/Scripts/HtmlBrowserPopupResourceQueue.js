(() => {
    const arrayPush = Array.prototype.push;
    const arrayShift = Array.prototype.shift;
    const arraySort = Array.prototype.sort;
    const reflectApply = Reflect.apply;
    globalThis.__htmlTinkerXCreatePopupResourceQueue = () => {
        const queue = [];
        let sequence = 0;
        let active = new WeakSet();
        queue.push = action => {
            if (active.has(action)) return queue.length;
            active.add(action);
            action.htmlTinkerXOrder = ++sequence;
            return reflectApply(arrayPush, queue, [action]);
        };
        queue.touch = action => {
            if (action != null) action.htmlTinkerXOrder = ++sequence;
        };
        queue.drain = async () => {
            reflectApply(arraySort, queue, [(left, right) => left.htmlTinkerXOrder - right.htmlTinkerXOrder]);
            while (queue.length > 0) { const action = reflectApply(arrayShift, queue, []); await action(); active.delete(action); }
        };
        queue.clear = () => { queue.length = 0; active = new WeakSet(); };
        return queue;
    };
})();
