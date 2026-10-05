form => {
    const view = form.ownerDocument.defaultView;
    const prototype = view.HTMLFormElement.prototype;
    const getAttribute = view.Element.prototype.getAttribute;
    let submission = null;
    const record = event => { if (event.target === form) submission = event; };
    view.addEventListener('submit', record, { capture: true });
    try {
        // The prototype method remains callable when a named input shadows requestSubmit or submit.
        prototype.requestSubmit.call(form);
    } finally {
        view.removeEventListener('submit', record, { capture: true });
    }
    const target = getAttribute.call(form, 'target')
        || form.ownerDocument.querySelector('base[target]')?.getAttribute('target') || '';
    const method = getAttribute.call(form, 'method') || 'get';
    const navigatesHere = method.toLowerCase() !== 'dialog'
        && (target === '' || /^_(self|parent|top)$/i.test(target) || target === view.name);
    return [submission !== null && !submission.defaultPrevented, navigatesHere];
}
