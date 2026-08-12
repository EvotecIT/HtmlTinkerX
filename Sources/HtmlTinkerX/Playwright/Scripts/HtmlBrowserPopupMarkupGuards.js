(() => {
    const arrayFrom = Array.from;
    const defineProperty = Object.defineProperty;
    const getOwnPropertyDescriptor = Object.getOwnPropertyDescriptor;
    const promiseThen = Promise.prototype.then;
    const stringValueNative = String;
    globalThis.__htmlTinkerXCreatePopupMarkupStager = ({
        popup,
        innerHtml,
        outerHtml,
        insertAdjacentHtml,
        reflectApply,
        stringValue,
        shouldDeferAttribute,
        guardDeferredAttributes,
        guardedResources
    }) => {
        const stager = (element, method, args) => {
            const markup = stringValue(args[method === 'insertAdjacentHTML' ? 1 : 0]);
            const template = popup.document.createElement('template');
            template.innerHTML = markup;
            const descriptors = [];
            let markerIndex = 0;
            for (const descendant of template.content.querySelectorAll('*')) {
                const values = [];
                for (const attribute of arrayFrom(descendant.attributes)) {
                    const name = attribute.name.toLowerCase();
                    if (!shouldDeferAttribute(descendant, name)) continue;
                    values.push([name, attribute.value]);
                    descendant.removeAttribute(attribute.name);
                }
                const styleText = descendant.localName === 'style' ? descendant.textContent : null;
                if (styleText !== null) descendant.textContent = '';
                const marker = 'htmltinkerx-fragment-' + Date.now() + '-' + markerIndex++ + '-' + Math.random().toString(36).slice(2);
                descendant.setAttribute('data-htmltinkerx-staged-resource', marker);
                descriptors.push({ marker, values, styleText });
            }
            let searchRoot;
            if (method === 'innerHTML') {
                innerHtml.set.call(element, template.innerHTML);
                searchRoot = element;
            } else if (method === 'outerHTML') {
                searchRoot = element.parentNode;
                outerHtml.set.call(element, template.innerHTML);
            } else {
                searchRoot = element.parentNode || element;
                reflectApply(insertAdjacentHtml, element, [args[0], template.innerHTML]);
            }
            if (searchRoot?.querySelector) for (const { marker, values, styleText } of descriptors) {
                const descendant = searchRoot.querySelector('[data-htmltinkerx-staged-resource="' + marker + '"]');
                if (!descendant) continue;
                descendant.removeAttribute('data-htmltinkerx-staged-resource');
                guardDeferredAttributes(descendant, values);
                if (styleText !== null) guardedResources.push(() => { descendant.textContent = styleText; });
            }
            return true;
        };
        stager.replaceParserBlockingScript = (element, replacement, completed) => {
            const document = element.ownerDocument;
            const boundaries = [];
            let current = element;
            while (current?.parentNode && current !== document.documentElement) {
                const boundary = document.createComment('htmltinkerx-parser-position');
                const remainder = document.createDocumentFragment();
                current.parentNode.insertBefore(boundary, current.nextSibling);
                while (boundary.nextSibling) remainder.appendChild(boundary.nextSibling);
                boundaries.push({ boundary, remainder });
                current = current.parentNode;
            }
            const writeDescriptors = new Map();
            const insertWrittenMarkup = values => {
                const template = document.createElement('template');
                template.innerHTML = values.join('');
                boundaries[0].boundary.parentNode.insertBefore(template.content, boundaries[0].boundary);
            };
            for (const name of ['write', 'writeln']) {
                writeDescriptors.set(name, getOwnPropertyDescriptor(document, name));
                defineProperty(document, name, {
                    configurable: true,
                    value(...values) {
                        insertWrittenMarkup(values.map(stringValueNative).concat(name === 'writeln' ? ['\n'] : []));
                    }
                });
            }
            const restore = () => {
                for (const name of ['write', 'writeln']) {
                    const descriptor = writeDescriptors.get(name);
                    if (descriptor == null) delete document[name];
                    else defineProperty(document, name, descriptor);
                }
                for (const { boundary, remainder } of boundaries) boundary.replaceWith(remainder);
            };
            try { element.replaceWith(replacement); }
            catch (error) { restore(); throw error; }
            if (completed == null) {
                restore();
                return null;
            }
            return reflectApply(promiseThen, completed, [
                value => { restore(); return value; },
                error => { restore(); throw error; }
            ]);
        };
        return stager;
    };
})();
