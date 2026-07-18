using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Browser element inspection and storage helpers used by extraction workflows.
/// </summary>
public static partial class HtmlBrowser {
    private const string ElementInfoScript = @"(el, args) => {
        const includeAttributes = args.includeAttributes === true;
        const includeHtml = args.includeHtml === true;
        const attr = name => el.getAttribute(name);
        const text = (el.innerText || el.textContent || '').replace(/\s+/g, ' ').trim();
        const rect = el.getBoundingClientRect();
        const tag = el.tagName ? el.tagName.toLowerCase() : '';
        const style = window.getComputedStyle(el);
        const disabled = (el.matches && el.matches(':disabled')) || el.disabled === true || el.getAttribute('aria-disabled') === 'true';
        const visible = !!(rect.width || rect.height) &&
            style.display !== 'none' &&
            style.visibility !== 'hidden' &&
            parseFloat(style.opacity || '1') !== 0;
        const inViewport = rect.bottom >= 0 &&
            rect.right >= 0 &&
            rect.top <= (window.innerHeight || document.documentElement.clientHeight) &&
            rect.left <= (window.innerWidth || document.documentElement.clientWidth);
        const esc = (globalThis.CSS && CSS.escape) ? CSS.escape : (s) => String(s).replace(/[^a-zA-Z0-9_-]/g, '\\$&');
        const selector = (() => {
            if (el.id) return `${tag}#${esc(el.id)}`;
            const name = el.getAttribute('name');
            if (name) return `${tag}[name='${String(name).replace(/'/g, ""\\'"")}']`;
            const href = el.getAttribute('href');
            if (href) return `${tag}[href='${String(href).replace(/'/g, ""\\'"")}']`;
            if (el.className && typeof el.className === 'string') {
                const classes = el.className.trim().split(/\s+/).filter(Boolean).slice(0, 3);
                if (classes.length) return `${tag}.${classes.map(esc).join('.')}`;
            }
            return tag;
        })();
        const attributes = {};
        if (includeAttributes) {
            for (const item of Array.from(el.attributes || [])) {
                attributes[item.name] = item.value;
            }
        }
        const result = {
            selector,
            tag,
            text,
            innerHtml: includeHtml ? el.innerHTML : null,
            outerHtml: includeHtml ? el.outerHTML : null,
            id: attr('id'),
            className: attr('class'),
            name: attr('name'),
            type: attr('type'),
            role: attr('role'),
            href: attr('href'),
            value: ('value' in el) ? String(el.value ?? '') : null,
            attributes,
            visible,
            enabled: !disabled,
            editable: !disabled && (el.isContentEditable || ['input', 'textarea', 'select'].includes(tag)),
            checked: ('checked' in el) ? el.checked === true : null,
            selected: ('selected' in el) ? el.selected === true : null,
            inViewport,
            x: rect.x,
            y: rect.y,
            width: rect.width,
            height: rect.height
        };
        return JSON.stringify(result);
    }";

    /// <summary>
    /// Waits until a selector resolves to an exact number of rendered elements.
    /// </summary>
    /// <param name="session">Active browser session.</param>
    /// <param name="selector">CSS selector to count.</param>
    /// <param name="expectedCount">Expected number of matching elements.</param>
    /// <param name="timeout">Maximum wait time in milliseconds.</param>
    /// <param name="pollMilliseconds">Polling interval in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task WaitForElementCountAsync(
        HtmlBrowserSession session,
        string selector,
        int expectedCount,
        int timeout = 10000,
        int pollMilliseconds = 50,
        CancellationToken cancellationToken = default) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        if (string.IsNullOrWhiteSpace(selector)) {
            throw new ArgumentException("Selector cannot be empty.", nameof(selector));
        }

        if (expectedCount < 0) {
            throw new ArgumentOutOfRangeException(nameof(expectedCount), "Expected count cannot be negative.");
        }

        if (timeout <= 0) {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be greater than zero.");
        }

        if (pollMilliseconds <= 0) {
            throw new ArgumentOutOfRangeException(nameof(pollMilliseconds), "Polling interval must be greater than zero.");
        }

        ILocator locator = session.Page.Locator(selector);
        Stopwatch stopwatch = Stopwatch.StartNew();
        int actualCount = -1;
        while (stopwatch.ElapsedMilliseconds <= timeout) {
            cancellationToken.ThrowIfCancellationRequested();
            actualCount = await locator.CountAsync().ConfigureAwait(false);
            if (actualCount == expectedCount) {
                return;
            }

            await session.Page.WaitForTimeoutAsync(pollMilliseconds)
                .WaitWithCancellationAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        throw new TimeoutException($"Selector '{selector}' resolved to {actualCount} element(s), not {expectedCount}, within {timeout}ms.");
    }

    /// <summary>
    /// Returns browser-observed element information for a CSS selector.
    /// </summary>
    public static async Task<IReadOnlyList<HtmlBrowserElementInfo>> GetElementsAsync(
        HtmlBrowserSession session,
        string selector = "*",
        bool visibleOnly = false,
        bool includeAttributes = false,
        bool includeHtml = false,
        int limit = 100,
        int timeout = 10000,
        CancellationToken cancellationToken = default) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        if (string.IsNullOrWhiteSpace(selector)) {
            selector = "*";
        }

        if (limit <= 0) {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than zero.");
        }

        ILocator locator = session.Page.Locator(selector);
        await locator.First.WaitForAsync(new LocatorWaitForOptions { Timeout = timeout, State = WaitForSelectorState.Attached }).WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        int count = Math.Min(await locator.CountAsync().ConfigureAwait(false), limit);
        List<HtmlBrowserElementInfo> results = new();

        for (int i = 0; i < count; i++) {
            cancellationToken.ThrowIfCancellationRequested();
            ILocator item = locator.Nth(i);
            string json = await item.EvaluateAsync<string>(
                ElementInfoScript,
                new {
                    includeAttributes,
                    includeHtml
                }).ConfigureAwait(false);
            HtmlBrowserElementInfo info = ParseElementInfo(json);
            if (visibleOnly && !info.Visible) {
                continue;
            }

            info.Index = results.Count;
            info.QuerySelector = selector;
            results.Add(info);
        }

        return results;
    }

    /// <summary>
    /// Returns browser-observed state for the active element.
    /// </summary>
    public static async Task<HtmlBrowserElementInfo?> GetActiveElementAsync(
        HtmlBrowserSession session,
        bool includeAttributes = false,
        bool includeHtml = false,
        CancellationToken cancellationToken = default) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        cancellationToken.ThrowIfCancellationRequested();
        string? json = await session.Page.EvaluateAsync<string?>(
            @"(args) => {
                const element = document.activeElement;
                if (!element || element === document.body || element === document.documentElement) return null;
                return (" + ElementInfoScript + @")(element, { includeAttributes: args.includeAttributes, includeHtml: args.includeHtml });
            }",
            new {
                includeAttributes,
                includeHtml
            }).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(json)) {
            return null;
        }

        HtmlBrowserElementInfo info = ParseElementInfo(json!);
        info.Index = 0;
        info.QuerySelector = ":focus";
        return info;
    }

    /// <summary>
    /// Tests whether a selector satisfies common browser-observed element states.
    /// </summary>
    public static async Task<bool> TestElementAsync(
        HtmlBrowserSession session,
        string selector,
        bool visible = false,
        bool hidden = false,
        bool enabled = false,
        bool disabled = false,
        bool checkedState = false,
        bool uncheckedState = false,
        bool selected = false,
        bool inViewport = false,
        int timeout = 10000,
        CancellationToken cancellationToken = default) {
        IReadOnlyList<HtmlBrowserElementInfo> elements;
        try {
            elements = await GetElementsAsync(session, selector, visibleOnly: false, includeAttributes: false, includeHtml: false, limit: 1, timeout: timeout, cancellationToken: cancellationToken).ConfigureAwait(false);
        } catch (TimeoutException) {
            return hidden;
        } catch (PlaywrightException) {
            return hidden;
        }

        HtmlBrowserElementInfo? element = elements.FirstOrDefault();
        if (element == null) {
            return hidden;
        }

        if (visible && !element.Visible) return false;
        if (hidden && element.Visible) return false;
        if (enabled && !element.Enabled) return false;
        if (disabled && element.Enabled) return false;
        if (checkedState && element.Checked != true) return false;
        if (uncheckedState && element.Checked != false) return false;
        if (selected && element.Selected != true) return false;
        if (inViewport && !element.InViewport) return false;
        return true;
    }

    /// <summary>
    /// Waits until a selector satisfies common browser-observed element states.
    /// </summary>
    public static async Task WaitForElementStateAsync(
        HtmlBrowserSession session,
        string selector,
        bool visible = false,
        bool hidden = false,
        bool enabled = false,
        bool disabled = false,
        bool checkedState = false,
        bool uncheckedState = false,
        bool selected = false,
        bool inViewport = false,
        int timeout = 10000,
        int pollMilliseconds = 100,
        CancellationToken cancellationToken = default) {
        if (pollMilliseconds <= 0) {
            throw new ArgumentOutOfRangeException(nameof(pollMilliseconds), "PollMilliseconds must be greater than zero.");
        }

        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeout);
        while (DateTimeOffset.UtcNow <= deadline) {
            cancellationToken.ThrowIfCancellationRequested();
            if (await TestElementAsync(session, selector, visible, hidden, enabled, disabled, checkedState, uncheckedState, selected, inViewport, Math.Min(timeout, pollMilliseconds), cancellationToken).ConfigureAwait(false)) {
                return;
            }

            await session.Page.WaitForTimeoutAsync(pollMilliseconds).WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"Element '{selector}' did not reach the requested state within {timeout}ms.");
    }

    /// <summary>
    /// Retrieves a list of elements that can be interacted with (links, buttons, etc.).
    /// </summary>
    /// <param name="page">Playwright page instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of interactable element descriptions.</returns>
    public static async Task<List<HtmlInteractableInfo>> GetInteractablesAsync(IPage page, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        var elements = await page.QuerySelectorAllAsync("a,button,[role=button],input[type=button],input[type=submit]");
        List<HtmlInteractableInfo> list = new();
        int index = 0;
        foreach (var el in elements) {
            cancellationToken.ThrowIfCancellationRequested();
            string rawText = await el.InnerTextAsync();
            string text = Regex.Replace(rawText, "\\s+", " ").Trim();
            string tag = await el.EvaluateAsync<string>("el => el.tagName.toLowerCase()");
            string? href = await el.GetAttributeAsync("href");
            string? id = await el.GetAttributeAsync("id");
            string? cls = await el.GetAttributeAsync("class");
            bool visible = await el.IsVisibleAsync();
            bool enabled = await el.IsEnabledAsync();
            bool editable = await el.EvaluateAsync<bool>(@"el => {
                if (!el || el.disabled || el.getAttribute('aria-disabled') === 'true') return false;
                const tag = el.tagName ? el.tagName.toLowerCase() : '';
                if (tag === 'textarea') return true;
                if (tag === 'select') return true;
                if (tag === 'input') {
                    const type = (el.getAttribute('type') || 'text').toLowerCase();
                    return !['button','checkbox','color','file','hidden','image','radio','range','reset','submit'].includes(type);
                }
                return el.isContentEditable === true || el.getAttribute('contenteditable') === 'true';
            }");
            bool potentiallyHidden = await el.EvaluateAsync<bool>(@"el => {
                const check = node => {
                    if (!node) return false;
                    if (node.getAttribute('aria-hidden') === 'true' || node.hidden) {
                        return true;
                    }
                    const style = window.getComputedStyle(node);
                    if (!style) return false;
                    return style.display === 'none' || style.visibility === 'hidden' || parseFloat(style.opacity) === 0;
                };
                for (let n = el; n; n = n.parentElement) {
                    if (check(n)) return true;
                }
                return false;
            }");
            HtmlBrowserElementInfo elementInfo = ParseElementInfo(await el.EvaluateAsync<string>(
                ElementInfoScript,
                new {
                    includeAttributes = false,
                    includeHtml = false
                }).ConfigureAwait(false));
            cancellationToken.ThrowIfCancellationRequested();
            string selector = await el.EvaluateAsync<string>(@"el => {
                const esc = (CSS && CSS.escape) ? CSS.escape : (s => s);
                let sel = el.tagName.toLowerCase();
                if (el.id) return sel + '#' + esc(el.id);
                const href = el.getAttribute('href');
                if (href) return `${sel}[href='${href.replace(/'/g, ""\\'"")}']`;
                const cls = el.className;
                if (cls) return sel + '.' + cls.trim().split(/\s+/).map(esc).join('.');
                return sel;
            }");
            list.Add(new HtmlInteractableInfo {
                Index = index++,
                Text = text,
                Tag = tag,
                Selector = selector,
                Href = href,
                Id = id,
                Class = cls,
                Visible = visible,
                PotentiallyHidden = potentiallyHidden,
                Enabled = enabled,
                Editable = editable,
                InViewport = elementInfo.InViewport,
                X = elementInfo.X,
                Y = elementInfo.Y,
                Width = elementInfo.Width,
                Height = elementInfo.Height
            });
        }
        return list;
    }

    /// <summary>
    /// Gets entries from localStorage, sessionStorage, or both scopes.
    /// </summary>
    public static async Task<IReadOnlyList<HtmlBrowserStorageItem>> GetStorageAsync(HtmlBrowserSession session, string scope = "All", string? key = null, CancellationToken cancellationToken = default) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        string normalizedScope = NormalizeStorageScope(scope);
        string json = await session.Page.EvaluateAsync<string>(
            @"(args) => {
                const read = (storage, scopeName) => {
                    const values = [];
                    for (let i = 0; i < storage.length; i++) {
                        const key = storage.key(i);
                        if (args.key && key !== args.key) continue;
                        values.push({ scope: scopeName, key, value: storage.getItem(key) });
                    }
                    return values;
                };
                let output = [];
                if (args.scope === 'All' || args.scope === 'Local') output = output.concat(read(window.localStorage, 'Local'));
                if (args.scope === 'All' || args.scope === 'Session') output = output.concat(read(window.sessionStorage, 'Session'));
                return JSON.stringify(output);
            }",
            new {
                scope = normalizedScope,
                key
            }).ConfigureAwait(false);

        List<HtmlBrowserStorageItem> items = new();
        using JsonDocument document = JsonDocument.Parse(json);
        foreach (JsonElement element in document.RootElement.EnumerateArray()) {
            items.Add(new HtmlBrowserStorageItem {
                Scope = GetJsonString(element, "scope") ?? string.Empty,
                Key = GetJsonString(element, "key") ?? string.Empty,
                Value = GetJsonString(element, "value")
            });
        }

        return items;
    }

    /// <summary>
    /// Sets or removes one browser storage item.
    /// </summary>
    public static Task SetStorageAsync(HtmlBrowserSession session, string scope, string key, string? value, bool remove = false, CancellationToken cancellationToken = default) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        if (string.IsNullOrEmpty(key)) {
            throw new ArgumentException("Storage key cannot be empty.", nameof(key));
        }

        string normalizedScope = NormalizeStorageScope(scope, allowAll: false);
        return session.Page.EvaluateAsync(
            @"(args) => {
                const storage = args.scope === 'Local' ? window.localStorage : window.sessionStorage;
                if (args.remove === true) storage.removeItem(args.key);
                else storage.setItem(args.key, args.value ?? '');
            }",
            new {
                scope = normalizedScope,
                key,
                value,
                remove
            });
    }

    /// <summary>
    /// Saves rendered session content to a file.
    /// </summary>
    public static async Task SaveContentAsync(HtmlBrowserSession session, string path, string? selector = null, bool innerHtml = false, bool asText = false, int? timeout = null, CancellationToken cancellationToken = default) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        string fullPath = path.ToFullPath();
        string? directory = System.IO.Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory)) {
            System.IO.Directory.CreateDirectory(directory);
        }

        string content = await GetContentAsync(session.Page, selector, innerHtml, asText, timeout, cancellationToken).ConfigureAwait(false);
#if NETSTANDARD2_0 || NETFRAMEWORK
        System.IO.File.WriteAllText(fullPath, content);
#else
        await System.IO.File.WriteAllTextAsync(fullPath, content, cancellationToken).ConfigureAwait(false);
#endif
    }

    /// <summary>
    /// Opens a page, saves rendered content to a file, and closes the temporary browser session.
    /// </summary>
    /// <param name="url">URL or file URI to render.</param>
    /// <param name="path">Destination file path.</param>
    /// <param name="launchOptions">Browser launch and context options.</param>
    /// <param name="selector">Optional selector to save.</param>
    /// <param name="innerHtml">Save inner HTML instead of outer HTML.</param>
    /// <param name="asText">Save visible text instead of HTML.</param>
    /// <param name="timeout">Timeout in milliseconds while waiting for the selector.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public static async Task SaveContentAsync(
        string url,
        string path,
        HtmlBrowserLaunchOptions launchOptions,
        string? selector = null,
        bool innerHtml = false,
        bool asText = false,
        int? timeout = null,
        CancellationToken cancellationToken = default) {
        if (launchOptions == null) {
            throw new ArgumentNullException(nameof(launchOptions));
        }

        await using HtmlBrowserSession session = await OpenSessionAsync(url, launchOptions, cancellationToken).ConfigureAwait(false);
        await SaveContentAsync(session, path, selector, innerHtml, asText, timeout, cancellationToken).ConfigureAwait(false);
    }

    private static HtmlBrowserElementInfo ParseElementInfo(string json) {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Dictionary<string, string> attributes = new(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("attributes", out JsonElement attrElement) && attrElement.ValueKind == JsonValueKind.Object) {
            foreach (JsonProperty property in attrElement.EnumerateObject()) {
                attributes[property.Name] = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() ?? string.Empty : property.Value.ToString();
            }
        }

        return new HtmlBrowserElementInfo {
            Selector = GetJsonString(root, "selector") ?? string.Empty,
            Tag = GetJsonString(root, "tag") ?? string.Empty,
            Text = GetJsonString(root, "text") ?? string.Empty,
            InnerHtml = GetJsonString(root, "innerHtml"),
            OuterHtml = GetJsonString(root, "outerHtml"),
            Id = GetJsonString(root, "id"),
            Class = GetJsonString(root, "className"),
            Name = GetJsonString(root, "name"),
            Type = GetJsonString(root, "type"),
            Role = GetJsonString(root, "role"),
            Href = GetJsonString(root, "href"),
            Value = GetJsonString(root, "value"),
            Attributes = attributes,
            Visible = GetJsonBool(root, "visible"),
            Enabled = GetJsonBool(root, "enabled"),
            Editable = GetJsonBool(root, "editable"),
            Checked = GetJsonNullableBool(root, "checked"),
            Selected = GetJsonNullableBool(root, "selected"),
            InViewport = GetJsonBool(root, "inViewport"),
            X = GetJsonDouble(root, "x"),
            Y = GetJsonDouble(root, "y"),
            Width = GetJsonDouble(root, "width"),
            Height = GetJsonDouble(root, "height")
        };
    }

    private static string NormalizeStorageScope(string scope, bool allowAll = true) {
        if (string.Equals(scope, "Local", StringComparison.OrdinalIgnoreCase)) return "Local";
        if (string.Equals(scope, "Session", StringComparison.OrdinalIgnoreCase)) return "Session";
        if (allowAll && string.Equals(scope, "All", StringComparison.OrdinalIgnoreCase)) return "All";
        throw new ArgumentException(allowAll ? "Scope must be Local, Session, or All." : "Scope must be Local or Session.", nameof(scope));
    }

    private static string? GetJsonString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool GetJsonBool(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.True;

    private static bool? GetJsonNullableBool(JsonElement element, string propertyName) {
        if (!element.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind == JsonValueKind.Null) return null;
        return value.ValueKind == JsonValueKind.True;
    }

    private static double GetJsonDouble(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : 0;
}
