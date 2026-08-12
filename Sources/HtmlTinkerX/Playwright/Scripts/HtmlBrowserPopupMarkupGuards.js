(() => {
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
    }) => (element, method, args) => {
        const markup = stringValue(args[method === 'insertAdjacentHTML' ? 1 : 0]);
        const template = popup.document.createElement('template');
        template.innerHTML = markup;
        const descriptors = [];
        let markerIndex = 0;
        for (const descendant of template.content.querySelectorAll('*')) {
            const values = [];
            for (const attribute of Array.from(descendant.attributes)) {
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
})();
