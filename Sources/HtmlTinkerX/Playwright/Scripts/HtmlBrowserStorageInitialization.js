({ expectedOrigin, marker, statusKey, local, session }) => {
    if (window !== window.top || location.origin !== expectedOrigin) return;
    const status = { completed: false, errors: [] };
    const publish = () => { globalThis[statusKey] = JSON.stringify(status); };
    const describe = error => error?.message || String(error);
    publish();
    try {
        if (sessionStorage.getItem(marker) === '1') {
            status.completed = true;
            publish();
            return;
        }
    } catch (error) {
        status.errors.push(`sessionStorage marker read: ${describe(error)}`);
    }
    for (const key of Object.keys(local)) {
        try { localStorage.setItem(key, local[key]); }
        catch (error) { status.errors.push(`localStorage ${key}: ${describe(error)}`); }
    }
    for (const key of Object.keys(session)) {
        try { sessionStorage.setItem(key, session[key]); }
        catch (error) { status.errors.push(`sessionStorage ${key}: ${describe(error)}`); }
    }
    if (status.errors.length === 0) {
        try { sessionStorage.setItem(marker, '1'); }
        catch (error) { status.errors.push(`sessionStorage marker write: ${describe(error)}`); }
    }
    status.completed = true;
    publish();
}
