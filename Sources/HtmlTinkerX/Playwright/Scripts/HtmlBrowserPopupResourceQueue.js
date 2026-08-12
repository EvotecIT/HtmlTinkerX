(() => {
    const arrayPush = Array.prototype.push;
    const arrayShift = Array.prototype.shift;
    const arraySort = Array.prototype.sort;
    const reflectApply = Reflect.apply;
    globalThis.__htmlTinkerXCreatePopupResourceQueue = () => {
        const queue = [];
        let sequence = 0;
        queue.push = action => {
            action.htmlTinkerXOrder = ++sequence;
            return reflectApply(arrayPush, queue, [action]);
        };
        queue.touch = action => {
            if (action != null) action.htmlTinkerXOrder = ++sequence;
        };
        queue.drain = async () => {
            reflectApply(arraySort, queue, [(left, right) => left.htmlTinkerXOrder - right.htmlTinkerXOrder]);
            while (queue.length > 0) await reflectApply(arrayShift, queue, [])();
        };
        return queue;
    };
})();
