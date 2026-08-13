(() => {
    const arrayFrom = Array.from;
    const defineProperty = Object.defineProperty;
    const getOwnPropertyDescriptor = Object.getOwnPropertyDescriptor;
    const promiseThen = Promise.prototype.then;
    const stringValueNative = String;
    const weakMap = WeakMap;
    const weakSet = WeakSet;
    globalThis.__htmlTinkerXCreatePopupMarkupStager = ({
        popup,
        innerHtml,
        outerHtml,
        insertAdjacentHtml,
        setHtmlUnsafe,
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
            } else if (method === 'setHTMLUnsafe') {
                reflectApply(setHtmlUnsafe, element, [template.innerHTML]);
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
        const appendChild = popup.Node.prototype.appendChild;
        const childNodes = getOwnPropertyDescriptor(popup.Node.prototype, 'childNodes').get;
        const documentElement = getOwnPropertyDescriptor(popup.Document.prototype, 'documentElement').get;
        const nodeType = getOwnPropertyDescriptor(popup.Node.prototype, 'nodeType').get;
        const nodeName = getOwnPropertyDescriptor(popup.Node.prototype, 'nodeName').get;
        const nodeValue = getOwnPropertyDescriptor(popup.Node.prototype, 'nodeValue');
        const elementGetAttribute = popup.Element.prototype.getAttribute;
        const elementHasAttribute = popup.Element.prototype.hasAttribute;
        const elementSetAttribute = popup.Element.prototype.setAttribute;
        const markerAttribute = 'data-htmltinkerx-staged-resource';
        const writtenNodes = new weakSet();
        const writtenChildCounts = new weakMap();
        const writtenText = new weakMap();
        const compatible = (existing, desired) => reflectApply(nodeType, existing, []) === reflectApply(nodeType, desired, [])
            && reflectApply(nodeName, existing, []) === reflectApply(nodeName, desired, []);
        const rememberWriteTree = node => {
            writtenNodes.add(node);
            const children = arrayFrom(reflectApply(childNodes, node, []));
            writtenChildCounts.set(node, children.length);
            if (reflectApply(nodeType, node, []) === 3) writtenText.set(node, nodeValue.get.call(node) || '');
            for (const child of children) rememberWriteTree(child);
        };
        const mergeWriteTrees = (existing, desired, reused) => {
            reused.add(existing);
            if (reflectApply(nodeType, existing, []) === 3) {
                const previous = writtenText.get(existing) || '';
                const desiredText = nodeValue.get.call(desired) || '';
                const current = nodeValue.get.call(existing) || '';
                nodeValue.set.call(existing, desiredText.startsWith(previous) ? current + desiredText.slice(previous.length) : desiredText);
                writtenText.set(existing, desiredText);
                return;
            }
            if (existing instanceof popup.Element
                && desired instanceof popup.Element
                && reflectApply(elementHasAttribute, desired, [markerAttribute])) {
                reflectApply(elementSetAttribute, existing, [markerAttribute, reflectApply(elementGetAttribute, desired, [markerAttribute])]);
            }
            const existingChildren = arrayFrom(reflectApply(childNodes, existing, []));
            const desiredChildren = arrayFrom(reflectApply(childNodes, desired, []));
            const previousWrittenCount = writtenChildCounts.get(existing) || 0;
            const existingWrittenChildren = [];
            for (const child of existingChildren) if (writtenNodes.has(child)) existingWrittenChildren.push(child);
            const reusableCount = previousWrittenCount < desiredChildren.length ? previousWrittenCount : desiredChildren.length;
            for (let index = 0; index < reusableCount; index++) {
                const existingChild = existingWrittenChildren[index];
                if (existingChild && compatible(existingChild, desiredChildren[index])) {
                    mergeWriteTrees(existingChild, desiredChildren[index], reused);
                }
            }
            for (let index = previousWrittenCount; index < desiredChildren.length; index++) {
                const child = desiredChildren[index];
                reflectApply(appendChild, existing, [child]);
                rememberWriteTree(child);
            }
            writtenChildCounts.set(existing, desiredChildren.length);
        };
        stager.preserveWrittenNodes = (document, previousRoot, desiredRoot = null) => {
            const currentRoot = desiredRoot || reflectApply(documentElement, document, []);
            const reused = new weakSet();
            if (!currentRoot) return reused;
            if (!previousRoot) {
                rememberWriteTree(currentRoot);
                return reused;
            }
            if (!compatible(previousRoot, currentRoot)) return reused;
            mergeWriteTrees(previousRoot, currentRoot, reused);
            return reused;
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
