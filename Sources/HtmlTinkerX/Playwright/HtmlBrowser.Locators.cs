using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for discovering resilient browser locators.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Finds ranked locator candidates for elements on the current browser page.
    /// </summary>
    /// <param name="session">Browser session to inspect.</param>
    /// <param name="query">Optional text, label, id, name, placeholder, href, or selector fragment to filter candidates.</param>
    /// <param name="visibleOnly">Return only visible elements.</param>
    /// <param name="limit">Maximum number of candidates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ranked locator candidates.</returns>
    public static async Task<IReadOnlyList<HtmlBrowserLocatorCandidate>> FindLocatorCandidatesAsync(
        HtmlBrowserSession session,
        string? query = null,
        bool visibleOnly = true,
        int limit = 25,
        CancellationToken cancellationToken = default) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        if (limit <= 0) {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than zero.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        string json = await session.Page.EvaluateAsync<string>(
            LocatorCandidateScript,
            new {
                query = query ?? string.Empty,
                visibleOnly,
                limit
            }).ConfigureAwait(false);

        List<HtmlBrowserLocatorCandidate> candidates = new();
        using JsonDocument document = JsonDocument.Parse(json);
        foreach (JsonElement element in document.RootElement.EnumerateArray()) {
            HtmlBrowserLocatorCandidate candidate = new() {
                Index = candidates.Count,
                Strategy = GetJsonString(element, "strategy") ?? string.Empty,
                Selector = GetJsonString(element, "selector") ?? string.Empty,
                Locator = GetJsonString(element, "locator") ?? string.Empty,
                Score = GetJsonInt(element, "score"),
                Reason = GetJsonString(element, "reason") ?? string.Empty,
                Text = GetJsonString(element, "text") ?? string.Empty,
                Tag = GetJsonString(element, "tag") ?? string.Empty,
                Visible = GetJsonBool(element, "visible"),
                Enabled = GetJsonBool(element, "enabled"),
                Editable = GetJsonBool(element, "editable"),
                InViewport = GetJsonBool(element, "inViewport")
            };
            PopulateLocatorGuidance(candidate);
            candidates.Add(candidate);
        }

        IReadOnlyList<HtmlBrowserLocatorCandidate> result = candidates
            .OrderByDescending(static item => item.Score)
            .ThenByDescending(static item => item.Visible)
            .ThenByDescending(static item => item.Enabled)
            .Take(limit)
            .Select((item, index) => {
                item.Index = index;
                return item;
            })
            .ToArray();
        RecordRecipeStep(session, new HtmlBrowserRecipeStep {
            Action = HtmlBrowserRecipeAction.Locator,
            Text = query,
            IncludeHidden = !visibleOnly,
            Limit = limit
        });
        return result;
    }

    /// <summary>
    /// Finds stable CSS selector alternates for the first element matched by an existing selector.
    /// </summary>
    /// <param name="session">Browser session to inspect.</param>
    /// <param name="selector">Existing selector whose first matched element should be analyzed.</param>
    /// <param name="limit">Maximum alternate selectors to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="nth">Optional zero-based match index for selectors that intentionally target a later occurrence.</param>
    /// <returns>Stable selector alternates that point at the same element and do not contain recognized sensitive values.</returns>
    public static async Task<IReadOnlyList<string>> FindSelectorAlternatesAsync(
        HtmlBrowserSession session,
        string selector,
        int limit = 5,
        CancellationToken cancellationToken = default,
        int? nth = null) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        if (string.IsNullOrWhiteSpace(selector)) {
            throw new ArgumentException("Selector cannot be empty.", nameof(selector));
        }

        if (limit <= 0) {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than zero.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        string[] selectors = await session.Page.EvaluateAsync<string[]>(
            SelectorAlternateScript,
            new {
                selector,
                limit,
                nth
            }).ConfigureAwait(false);

        return selectors
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item.Trim())
            .Where(item => !string.Equals(item, selector.Trim(), StringComparison.Ordinal))
            .Where(static item => !SelectorContainsSensitiveValue(item))
            .Distinct(StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
    }

    private const string LocatorCandidateScript = @"(args) => {
        const esc = (globalThis.CSS && CSS.escape) ? CSS.escape : (s) => String(s).replace(/[^a-zA-Z0-9_-]/g, '\\$&');
        const cssValue = value => String(value).replace(/\\/g, '\\\\').replace(/'/g, ""\\'"");
        const normalize = value => String(value || '').replace(/\s+/g, ' ').trim();
        const query = normalize(args.query).toLowerCase();
        const visibleOnly = args.visibleOnly === true;
        const limit = Number(args.limit || 25);
        const seen = new Set();
        const output = [];
        const roleSelector = (element, tag, role, aria, href) => {
            if (element.hasAttribute('role')) return `[role='${cssValue(role)}']`;
            if (aria) return `${tag}[aria-label='${cssValue(aria)}']`;
            if (element.id) return `${tag}#${esc(element.id)}`;
            const name = element.getAttribute('name');
            if (name) return `${tag}[name='${cssValue(name)}']`;
            if (tag === 'a' && href) return `${tag}[href='${cssValue(href)}']`;
            return tag;
        };

        const add = (element, strategy, selector, locator, score, reason, textOverride) => {
            if (!selector || !locator) return;
            const rect = element.getBoundingClientRect();
            const style = window.getComputedStyle(element);
            const visible = !!(rect.width || rect.height) &&
                style.display !== 'none' &&
                style.visibility !== 'hidden' &&
                parseFloat(style.opacity || '1') !== 0;
            if (visibleOnly && !visible) return;
            const tag = element.tagName ? element.tagName.toLowerCase() : '';
            const disabled = (element.matches && element.matches(':disabled')) || element.disabled === true || element.getAttribute('aria-disabled') === 'true';
            const editable = !disabled && (element.isContentEditable || ['input', 'textarea', 'select'].includes(tag));
            const text = normalize(textOverride || element.innerText || element.textContent || element.value || '');
            const haystack = [
                strategy,
                selector,
                locator,
                text,
                tag,
                element.id,
                element.getAttribute('name'),
                element.getAttribute('aria-label'),
                element.getAttribute('placeholder'),
                element.getAttribute('href'),
                element.getAttribute('data-testid'),
                element.getAttribute('data-test')
            ].map(normalize).join(' ').toLowerCase();
            if (query && !haystack.includes(query)) return;
            const key = `${strategy}|${selector}|${locator}`;
            if (seen.has(key)) return;
            seen.add(key);
            output.push({
                strategy,
                selector,
                locator,
                score,
                reason,
                text,
                tag,
                visible,
                enabled: !disabled,
                editable,
                inViewport: rect.bottom >= 0 &&
                    rect.right >= 0 &&
                    rect.top <= (window.innerHeight || document.documentElement.clientHeight) &&
                    rect.left <= (window.innerWidth || document.documentElement.clientWidth)
            });
        };

        const elements = Array.from(document.querySelectorAll(
            'a,button,input,textarea,select,label,[role],[aria-label],[placeholder],[data-testid],[data-test],[name],[id]'
        ));

        for (const element of elements) {
            const tag = element.tagName ? element.tagName.toLowerCase() : '';
            const text = normalize(element.innerText || element.textContent || element.value || '');
            const testId = element.getAttribute('data-testid') || element.getAttribute('data-test');
            const href = element.getAttribute('href');
            if (testId) add(element, 'TestId', `[data-testid='${cssValue(testId)}'],[data-test='${cssValue(testId)}']`, `GetByTestId('${cssValue(testId)}')`, 100, 'test id attributes are usually the most stable automation hook', text);

            if (element.id) add(element, 'Id', `${tag}#${esc(element.id)}`, `${tag}#${esc(element.id)}`, 95, 'id selector is concise and usually stable', text);

            const role = element.getAttribute('role') || (tag === 'button' ? 'button' : tag === 'a' ? 'link' : '');
            const aria = element.getAttribute('aria-label');
            const accessibleName = normalize(aria || text || element.getAttribute('value'));
            if (role && accessibleName) add(element, 'Role', roleSelector(element, tag, role, aria, href), `GetByRole('${cssValue(role)}', Name='${cssValue(accessibleName)}')`, 92, 'role plus accessible name follows the user-visible UI contract', accessibleName);

            const name = element.getAttribute('name');
            if (name) add(element, 'Name', `${tag}[name='${cssValue(name)}']`, `${tag}[name='${cssValue(name)}']`, 88, 'name attribute is stable for forms', text);

            if (aria) add(element, 'AriaLabel', `${tag}[aria-label='${cssValue(aria)}']`, `${tag}[aria-label='${cssValue(aria)}']`, 86, 'aria-label is explicit and user-facing', aria);

            const placeholder = element.getAttribute('placeholder');
            if (placeholder) add(element, 'Placeholder', `${tag}[placeholder='${cssValue(placeholder)}']`, `GetByPlaceholder('${cssValue(placeholder)}')`, 84, 'placeholder is useful for search and form fields', placeholder);

            const label = element.labels && element.labels.length ? normalize(element.labels[0].innerText || element.labels[0].textContent) : '';
            if (label) {
                const labelSelector = name ? `${tag}[name='${cssValue(name)}']` : element.id ? `${tag}#${esc(element.id)}` : '';
                if (labelSelector) add(element, 'Label', labelSelector, `GetByLabel('${cssValue(label)}')`, 83, 'associated label matches what a user sees', label);
            }

            if (href) add(element, 'Href', `${tag}[href='${cssValue(href)}']`, `${tag}[href='${cssValue(href)}']`, 80, 'href selector is useful for navigation links', text);

            if (text) add(element, 'Text', `text=${text}`, `GetByText('${cssValue(text)}')`, 74, 'visible text is readable but may change with localization or content edits', text);

            const cls = typeof element.className === 'string' ? normalize(element.className) : '';
            if (cls) {
                const classes = cls.split(/\s+/).filter(Boolean).slice(0, 3);
                if (classes.length) add(element, 'Css', `${tag}.${classes.map(esc).join('.')}`, `${tag}.${classes.map(esc).join('.')}`, 60, 'class selector is a fallback when semantic locators are unavailable', text);
            }
        }

        return JSON.stringify(output
            .sort((a, b) => b.score - a.score || Number(b.visible) - Number(a.visible) || Number(b.enabled) - Number(a.enabled))
            .slice(0, limit));
    }";

    private const string SelectorAlternateScript = @"(args) => {
        const esc = (globalThis.CSS && CSS.escape) ? CSS.escape : (s) => String(s).replace(/[^a-zA-Z0-9_-]/g, '\\$&');
        const cssValue = value => String(value).replace(/\\/g, '\\\\').replace(/'/g, ""\\'"");
        const normalize = value => String(value || '').replace(/\s+/g, ' ').trim();
        const primary = normalize(args.selector);
        const limit = Number(args.limit || 5);
        const primaryMatches = Array.from(document.querySelectorAll(primary));
        const nth = args.nth === null || args.nth === undefined ? 0 : Number(args.nth);
        if (!Number.isInteger(nth) || nth < 0 || nth >= primaryMatches.length) return [];
        const element = primaryMatches[nth];

        const tag = element.tagName ? element.tagName.toLowerCase() : '';
        const output = [];
        const seen = new Set();
        const add = (selector) => {
            selector = normalize(selector);
            if (!selector || selector === primary || seen.has(selector)) return;
            try {
                const matches = Array.from(document.querySelectorAll(selector));
                if (matches.length !== 1 || matches[0] !== element) return;
            } catch {
                return;
            }

            seen.add(selector);
            output.push(selector);
        };

        const testId = element.getAttribute('data-testid');
        if (testId) add(`[data-testid='${cssValue(testId)}']`);
        const dataTest = element.getAttribute('data-test');
        if (dataTest) add(`[data-test='${cssValue(dataTest)}']`);
        if (element.id) add(`${tag}#${esc(element.id)}`);

        const name = element.getAttribute('name');
        if (name) add(`${tag}[name='${cssValue(name)}']`);

        const aria = element.getAttribute('aria-label');
        if (aria) add(`${tag}[aria-label='${cssValue(aria)}']`);

        const placeholder = element.getAttribute('placeholder');
        if (placeholder) add(`${tag}[placeholder='${cssValue(placeholder)}']`);

        const href = element.getAttribute('href');
        if (href) add(`${tag}[href='${cssValue(href)}']`);

        const cls = typeof element.className === 'string' ? normalize(element.className) : '';
        if (cls) {
            const classes = cls.split(/\s+/).filter(Boolean).slice(0, 3);
            if (classes.length) add(`${tag}.${classes.map(esc).join('.')}`);
        }

        return output.slice(0, limit);
    }";

    private static int GetJsonInt(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : 0;

    private static void PopulateLocatorGuidance(HtmlBrowserLocatorCandidate candidate) {
        List<string> warnings = BuildLocatorWarnings(candidate);
        candidate.Warnings = warnings;
        candidate.SuggestedAction = ChooseLocatorAction(candidate);
        candidate.TestCommand = BuildSelectorCommand("Test-HtmlBrowserElement -Session $session -Selector", candidate.Selector, " -Visible");
        candidate.SuggestedCommand = warnings.Any(IsSensitiveLocatorWarning)
            ? "$candidate | Format-List Strategy,Selector,Reason,Warnings"
            : candidate.SuggestedAction switch {
                "SetInput" => BuildSelectorCommand("Set-HtmlBrowserInput -Session $session -Selector", candidate.Selector, " -Value '<value>'"),
                "Click" => BuildSelectorCommand("Invoke-HtmlBrowserClick -Session $session -Selector", candidate.Selector, string.Empty),
                _ => BuildSelectorCommand("Get-HtmlBrowserElement -Session $session -Selector", candidate.Selector, string.Empty)
            };
    }

    private static string ChooseLocatorAction(HtmlBrowserLocatorCandidate candidate) {
        if (candidate.Editable) {
            return "SetInput";
        }

        if (string.Equals(candidate.Tag, "button", StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Tag, "a", StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Strategy, "Role", StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Strategy, "TestId", StringComparison.OrdinalIgnoreCase)) {
            return "Click";
        }

        return "Inspect";
    }

    private static List<string> BuildLocatorWarnings(HtmlBrowserLocatorCandidate candidate) {
        List<string> warnings = new();
        if (!candidate.Visible) {
            warnings.Add("Candidate is hidden; use -IncludeHidden only when you intentionally target hidden elements.");
        }

        if (!candidate.Enabled) {
            warnings.Add("Candidate is disabled and may not accept click or input actions yet.");
        }

        if (!candidate.InViewport) {
            warnings.Add("Candidate is outside the viewport; scroll or wait for layout before acting.");
        }

        if (SelectorContainsSensitiveValue(candidate.Selector)) {
            warnings.Add("Candidate selector appears to contain sensitive values. Review it before copying into scripts or logs.");
        }

        if (string.Equals(candidate.Strategy, "Text", StringComparison.OrdinalIgnoreCase)) {
            warnings.Add("Text locators are readable but can change with localization or content edits.");
        } else if (string.Equals(candidate.Strategy, "Css", StringComparison.OrdinalIgnoreCase)) {
            warnings.Add("Class-based CSS locators are a fallback and may break when the page is restyled.");
        }

        return warnings;
    }

    private static bool SelectorContainsSensitiveValue(string selector) {
        if (string.IsNullOrWhiteSpace(selector)) {
            return false;
        }

        return HtmlSensitiveValueRedactor.IsSensitiveName(selector)
            || !string.Equals(selector, HtmlSensitiveValueRedactor.RedactSensitiveQueryValues(selector), StringComparison.Ordinal)
            || !string.Equals(selector, HtmlSensitiveValueRedactor.RedactSensitiveEvidenceText(selector), StringComparison.Ordinal);
    }

    private static bool IsSensitiveLocatorWarning(string warning) =>
        warning.IndexOf("sensitive", StringComparison.OrdinalIgnoreCase) >= 0;

    private static string BuildSelectorCommand(string prefix, string selector, string suffix) =>
        $"{prefix} '{EscapePowerShellSingleQuotedString(selector)}'{suffix}";

    private static string EscapePowerShellSingleQuotedString(string value) =>
        (value ?? string.Empty).Replace("'", "''");
}
