(() => {
    globalThis.__htmlTinkerXCreatePopupContextRegistry = ({
        stringValue,
        querySelectorAll,
        getAttribute,
        iframeContentDocument,
        frameContentDocument,
        specialTargets
    }) => {
        const namedPopups = new Map();
        const reusableName = target => {
            if (target == null) return null;
            const name = stringValue(target);
            if (name.length === 0 || name.toLowerCase() === '_blank' || specialTargets.includes(name.toLowerCase())) return null;
            return name;
        };
        const targetsExistingFrame = target => {
            const expected = reusableName(target);
            if (expected == null) return false;
            const visited = new WeakSet();
            const containsNamedFrame = currentDocument => {
                if (visited.has(currentDocument)) return false;
                visited.add(currentDocument);
                const frames = querySelectorAll.call(currentDocument, 'iframe, frame');
                for (let index = 0; index < frames.length; index++) {
                    const frame = frames[index];
                    if (getAttribute.call(frame, 'name') === expected) return true;
                    try {
                        let childDocument = null;
                        try { childDocument = iframeContentDocument == null ? null : iframeContentDocument.call(frame); }
                        catch { childDocument = frameContentDocument == null ? null : frameContentDocument.call(frame); }
                        if (childDocument && containsNamedFrame(childDocument)) return true;
                    } catch { }
                }
                return false;
            };
            let root = window;
            while (root !== root.parent) {
                try { void root.parent.document; root = root.parent; }
                catch { break; }
            }
            try { return containsNamedFrame(root.document); }
            catch { return false; }
        };
        return {
            targetsExistingFrame,
            claim(target, initialAction, existingAction) {
                const name = reusableName(target);
                if (name == null) return null;
                const existing = namedPopups.get(name);
                if (existing == null) return null;
                try {
                    if (existing.popup.closed) {
                        namedPopups.delete(name);
                        return null;
                    }
                } catch {
                    namedPopups.delete(name);
                    return null;
                }
                const action = typeof initialAction === 'function' ? initialAction : existingAction;
                if (typeof action === 'function') existing.runWhenReady(() => action(existing.popup));
                return existing.facade;
            },
            register(target, popup, facade, runWhenReady) {
                const name = reusableName(target);
                if (name != null) namedPopups.set(name, { popup, facade, runWhenReady });
            }
        };
    };
})();
