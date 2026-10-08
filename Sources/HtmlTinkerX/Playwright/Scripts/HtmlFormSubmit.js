form => {
    const view = form.ownerDocument.defaultView;
    const prototype = view.HTMLFormElement.prototype;
    const getAttribute = view.Element.prototype.getAttribute;
    let submission = null;
    let entries = null;
    const record = event => { if (event.target === form) submission = event; };
    const recordEntries = event => { if (event.target === form) entries = event.formData; };
    view.addEventListener('submit', record, { capture: true });
    view.addEventListener('formdata', recordEntries, { capture: true });
    try {
        // The prototype method remains callable when a named input shadows requestSubmit or submit.
        prototype.requestSubmit.call(form);
    } finally {
        view.removeEventListener('submit', record, { capture: true });
        view.removeEventListener('formdata', recordEntries, { capture: true });
    }
    const target = getAttribute.call(form, 'target')
        || form.ownerDocument.querySelector('base[target]')?.getAttribute('target') || '';
    const method = Object.getOwnPropertyDescriptor(prototype, 'method').get.call(form);
    const navigatesHere = method !== 'dialog'
        && (target === '' || /^_(self|parent|top)$/i.test(target) || target === view.name);
    let sameDocumentUrl = null;
    if (submission !== null && !submission.defaultPrevented && navigatesHere && method === 'get' && entries !== null) {
        const destination = new view.URL(Object.getOwnPropertyDescriptor(prototype, 'action').get.call(form) || view.location.href);
        const normalize = value => value.replace(/\r\n|\r|\n/g, '\r\n');
        const values = Array.from(entries, ([name, value]) => [normalize(name), normalize(typeof value === 'string' ? value : value.name)]);
        // URLSearchParams matches native GET encoding for UTF-8 and for ASCII in legacy documents.
        // Observe the browser's entry list so formdata handlers run once and their changes are included.
        const utf8 = /utf-8/i.test(getAttribute.call(form, 'accept-charset') || form.ownerDocument.characterSet);
        if (destination.hash && (utf8 || values.every(pair => pair.every(value => /^[\x00-\x7f]*$/.test(value))))) {
            destination.search = new view.URLSearchParams(values).toString();
            const current = new view.URL(view.location.href);
            const fragment = destination.hash;
            destination.hash = '';
            current.hash = '';
            if (destination.href === current.href) {
                destination.hash = fragment;
                sameDocumentUrl = destination.href;
            }
        }
    }
    return { Submitted: submission !== null && !submission.defaultPrevented, NavigatesHere: navigatesHere, SameDocumentUrl: sameDocumentUrl };
}
