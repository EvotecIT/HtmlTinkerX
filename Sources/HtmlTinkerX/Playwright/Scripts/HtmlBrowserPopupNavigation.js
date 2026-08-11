(() => {
    const originalOpen = window.open;
    const originalSubmit = HTMLFormElement.prototype.submit;
    const releasedForms = new WeakSet();
    const specialTargets = ['_self', '_parent', '_top'];

    const normalizedTarget = target => target == null || String(target).length === 0
        ? '_blank'
        : String(target).toLowerCase();

    const armBlankPopup = (popup, target, navigate) => {
        let currentUrl;
        try { currentUrl = popup.location.href; } catch { currentUrl = null; }
        const normalized = normalizedTarget(target);
        if (currentUrl !== 'about:blank' || specialTargets.includes(normalized)) {
            globalThis.setTimeout(navigate, 0);
            return;
        }

        let fallback;
        const release = () => {
            if (fallback != null) globalThis.clearTimeout(fallback);
            try { delete popup.__htmlTinkerXReleasePopupNavigation; } catch { }
            navigate();
        };
        popup.__htmlTinkerXReleasePopupNavigation = release;
        if (popup.__htmlTinkerXPopupHeadersReady === true) {
            release();
        } else if (normalized !== '_blank') {
            // Existing named about:blank contexts do not raise a new-page event.
            fallback = globalThis.setTimeout(release, 1000);
        }
    };

    window.open = function(url, target, features) {
        if (url == null || String(url).length === 0 || String(url).toLowerCase() === 'about:blank') {
            return originalOpen.call(this, url, target, features);
        }
        const destination = new URL(String(url), document.baseURI).href;
        const featureTokens = features == null
            ? []
            : String(features).split(',').map(token => token.trim()).filter(Boolean);
        const isEnabled = name => featureTokens.some(token => {
            const parts = token.toLowerCase().split('=', 2);
            return parts[0] === name && (parts.length === 1 || !['0', 'no', 'false'].includes(parts[1]));
        });
        const suppressReferrer = isEnabled('noreferrer');
        const suppressOpener = suppressReferrer || isEnabled('noopener');
        const initialFeatures = suppressOpener
            ? featureTokens.filter(token => !['noopener', 'noreferrer'].includes(token.toLowerCase().split('=', 1)[0])).join(',')
            : features;
        const popup = originalOpen.call(this, '', target, initialFeatures);
        if (popup) {
            if (suppressOpener) {
                try { popup.opener = null; } catch { }
            }
            const navigate = () => {
                try {
                    if (suppressReferrer) {
                        const link = popup.document.createElement('a');
                        link.href = destination;
                        link.rel = 'noreferrer';
                        link.target = '_self';
                        (popup.document.body || popup.document.documentElement).appendChild(link);
                        link.click();
                    } else {
                        popup.location.href = destination;
                    }
                } catch {
                    // Existing named contexts can be cross-origin. Native navigation is the
                    // standards-safe fallback when their WindowProxy cannot be inspected.
                    originalOpen.call(window, destination, target, features);
                }
            };
            armBlankPopup(popup, target, navigate);
        }
        return suppressOpener ? null : popup;
    };

    const effectiveTarget = (elementTarget, submitterTarget) => {
        const explicit = submitterTarget || elementTarget;
        if (explicit) return explicit;
        const base = document.querySelector('base[target]');
        return base == null ? '' : base.target;
    };

    document.addEventListener('click', event => {
        if (event.defaultPrevented || event.button !== 0) return;
        const path = typeof event.composedPath === 'function' ? event.composedPath() : [];
        const anchor = path.find(node => node instanceof HTMLAnchorElement)
            || (event.target instanceof Element ? event.target.closest('a[href]') : null);
        if (!(anchor instanceof HTMLAnchorElement) || anchor.hasAttribute('download')) return;
        if (normalizedTarget(effectiveTarget(anchor.target, '')) !== '_blank') return;
        const destination = new URL(anchor.href, document.baseURI);
        if (destination.origin !== location.origin) return;

        event.preventDefault();
        const rel = anchor.relList;
        const features = rel.contains('noreferrer')
            ? 'noreferrer'
            : rel.contains('noopener') || !rel.contains('opener') ? 'noopener' : undefined;
        window.open(destination.href, '_blank', features);
    }, false);

    document.addEventListener('submit', event => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || releasedForms.delete(form) || event.defaultPrevented) return;
        const submitter = event.submitter instanceof HTMLElement ? event.submitter : null;
        const target = effectiveTarget(form.target, submitter == null ? '' : submitter.formTarget);
        if (normalizedTarget(target) !== '_blank') return;
        const action = new URL(submitter?.formAction || form.action || document.URL, document.baseURI);
        if (action.origin !== location.origin || String(submitter?.formMethod || form.method).toLowerCase() === 'dialog') return;

        event.preventDefault();
        const popup = originalOpen.call(window, '', '_blank');
        if (!popup) return;
        try { popup.opener = null; } catch { }
        const popupName = `htmltinkerx-popup-${Date.now()}-${Math.random().toString(36).slice(2)}`;
        try { popup.name = popupName; } catch { }

        armBlankPopup(popup, '_blank', () => {
            const previousFormTarget = form.target;
            const previousSubmitterTarget = submitter == null ? null : submitter.formTarget;
            form.target = popupName;
            if (submitter != null) submitter.formTarget = popupName;
            releasedForms.add(form);
            try {
                if (typeof form.requestSubmit === 'function') {
                    submitter == null ? form.requestSubmit() : form.requestSubmit(submitter);
                } else {
                    originalSubmit.call(form);
                }
            } finally {
                releasedForms.delete(form);
                form.target = previousFormTarget;
                if (submitter != null && previousSubmitterTarget != null) submitter.formTarget = previousSubmitterTarget;
            }
        });
    }, false);
})();
