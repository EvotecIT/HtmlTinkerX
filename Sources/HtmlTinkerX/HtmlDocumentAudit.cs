using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Audits static or rendered HTML with one reusable correctness, safety, and accessibility contract.
/// </summary>
public static class HtmlDocumentAudit {
    private static readonly string[] UrlAttributes = { "href", "xlink:href", "src", "action", "formaction" };

    /// <summary>
    /// Audits HTML markup using the supplied checks.
    /// </summary>
    public static HtmlDocumentAuditResult Analyze(
        string html,
        HtmlDocumentAuditOptions? options = null,
        CancellationToken cancellationToken = default) {
        if (html == null) throw new ArgumentNullException(nameof(html));
        cancellationToken.ThrowIfCancellationRequested();
        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        cancellationToken.ThrowIfCancellationRequested();
        return Analyze(document, options, cancellationToken);
    }

    /// <summary>
    /// Audits HTML markup asynchronously, including cancellable AngleSharp parsing.
    /// </summary>
    public static async Task<HtmlDocumentAuditResult> AnalyzeAsync(
        string html,
        HtmlDocumentAuditOptions? options = null,
        CancellationToken cancellationToken = default) {
        if (html == null) throw new ArgumentNullException(nameof(html));
        IDocument document = await HtmlParser.ParseWithAngleSharpAsync(html, cancellationToken).ConfigureAwait(false);
        return Analyze(document, options, cancellationToken);
    }

    internal static HtmlDocumentAuditResult Analyze(
        IDocument document,
        HtmlDocumentAuditOptions? options,
        CancellationToken cancellationToken) {
        if (document == null) throw new ArgumentNullException(nameof(document));

        HtmlDocumentAuditOptions effectiveOptions = options ?? new HtmlDocumentAuditOptions();
        List<IElement> elements = MaterializeElements(document, cancellationToken);
        IReadOnlyDictionary<string, List<IElement>> elementsById = IndexElementsById(elements, cancellationToken);
        List<HtmlDocumentAuditIssue> issues = new();

        if (effectiveOptions.CheckDocumentMetadata) AuditDocumentMetadata(document, issues);
        if (effectiveOptions.CheckDuplicateIds) AuditDuplicateIds(elementsById, issues, cancellationToken);
        if (effectiveOptions.CheckAccessibleNames) AuditAccessibleNames(elements, elementsById, issues, cancellationToken);
        if (effectiveOptions.CheckUnsafeUrls) AuditUnsafeUrls(elements, issues, cancellationToken);
        if (effectiveOptions.CheckHeadingOrder) AuditHeadingOrder(elements, issues, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        return new HtmlDocumentAuditResult { Issues = issues };
    }

    private static List<IElement> MaterializeElements(IDocument document, CancellationToken cancellationToken) {
        List<IElement> elements = new();
        foreach (IElement element in document.All) {
            cancellationToken.ThrowIfCancellationRequested();
            elements.Add(element);
        }
        return elements;
    }

    private static IReadOnlyDictionary<string, List<IElement>> IndexElementsById(
        IEnumerable<IElement> elements,
        CancellationToken cancellationToken) {
        Dictionary<string, List<IElement>> index = new(StringComparer.Ordinal);
        foreach (IElement element in elements) {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(element.Id)) continue;
            if (!index.TryGetValue(element.Id!, out List<IElement>? matches)) {
                matches = new List<IElement>();
                index[element.Id!] = matches;
            }
            matches.Add(element);
        }
        return index;
    }

    private static void AuditDocumentMetadata(IDocument document, ICollection<HtmlDocumentAuditIssue> issues) {
        if (string.IsNullOrWhiteSpace(document.Title)) {
            Add(issues, "document-title-missing", HtmlDocumentAuditSeverity.Warning, "The document does not define a non-empty title.", "head");
        }
        IElement? root = document.DocumentElement;
        if (root == null || string.IsNullOrWhiteSpace(root.GetAttribute("lang"))) {
            Add(issues, "document-language-missing", HtmlDocumentAuditSeverity.Warning, "The document root does not declare a language.", "html");
        }
    }

    private static void AuditDuplicateIds(
        IReadOnlyDictionary<string, List<IElement>> elementsById,
        ICollection<HtmlDocumentAuditIssue> issues,
        CancellationToken cancellationToken) {
        foreach (KeyValuePair<string, List<IElement>> entry in elementsById) {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.Value.Count <= 1) continue;
            Add(issues, "duplicate-id", HtmlDocumentAuditSeverity.Error,
                $"The id '{entry.Key}' is used by {entry.Value.Count} elements.", $"#{entry.Key}");
        }
    }

    private static void AuditAccessibleNames(
        IReadOnlyList<IElement> elements,
        IReadOnlyDictionary<string, List<IElement>> elementsById,
        ICollection<HtmlDocumentAuditIssue> issues,
        CancellationToken cancellationToken) {
        Dictionary<string, string> explicitLabels = new(StringComparer.Ordinal);
        foreach (IElement label in elements) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!label.LocalName.Equals("label", StringComparison.OrdinalIgnoreCase)) continue;
            string? target = label.GetAttribute("for");
            if (string.IsNullOrWhiteSpace(target)) continue;
            string text = NormalizeText(label.TextContent);
            if (explicitLabels.TryGetValue(target!, out string? current)) text = NormalizeText(current, text);
            explicitLabels[target!] = text;
        }

        foreach (IElement element in elements) {
            cancellationToken.ThrowIfCancellationRequested();
            if (element.LocalName.Equals("img", StringComparison.OrdinalIgnoreCase) && !element.HasAttribute("alt")) {
                Add(issues, "image-alt-missing", HtmlDocumentAuditSeverity.Error,
                    "The image does not define an alt attribute. Use an empty alt value for decorative images.", Describe(element));
            }

            if (IsInteractive(element) &&
                !HasAccessibleName(element, explicitLabels, elementsById, allowElementText: true, cancellationToken)) {
                Add(issues, "interactive-name-missing", HtmlDocumentAuditSeverity.Error,
                    "The interactive element has no accessible name.", Describe(element));
            }

            if (IsFormControl(element) &&
                !HasAccessibleName(element, explicitLabels, elementsById, allowElementText: false, cancellationToken)) {
                Add(issues, "form-label-missing", HtmlDocumentAuditSeverity.Error,
                    "The form control has no associated label or accessible name.", Describe(element));
            }
        }
    }

    private static bool IsInteractive(IElement element) {
        if (element.LocalName.Equals("button", StringComparison.OrdinalIgnoreCase)) return true;
        if (element.LocalName.Equals("a", StringComparison.OrdinalIgnoreCase) && element.HasAttribute("href")) return true;
        string? role = element.GetAttribute("role");
        return string.Equals(role, "button", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(role, "link", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFormControl(IElement element) {
        if (element.LocalName.Equals("select", StringComparison.OrdinalIgnoreCase) ||
            element.LocalName.Equals("textarea", StringComparison.OrdinalIgnoreCase)) return true;
        return element.LocalName.Equals("input", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(element.GetAttribute("type"), "hidden", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasAccessibleName(
        IElement element,
        IReadOnlyDictionary<string, string> explicitLabels,
        IReadOnlyDictionary<string, List<IElement>> elementsById,
        bool allowElementText,
        CancellationToken cancellationToken) {
        if (!string.IsNullOrWhiteSpace(element.GetAttribute("aria-label")) ||
            !string.IsNullOrWhiteSpace(element.GetAttribute("title")) ||
            (allowElementText && !string.IsNullOrWhiteSpace(element.TextContent))) return true;

        if (element.LocalName.Equals("input", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(element.GetAttribute("type"), "image", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(element.GetAttribute("alt"))) return true;

        foreach (IElement image in element.QuerySelectorAll("img[alt]")) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(image.GetAttribute("alt"))) return true;
        }

        string? labelledBy = element.GetAttribute("aria-labelledby");
        if (!string.IsNullOrWhiteSpace(labelledBy)) {
            foreach (string id in labelledBy!.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
                cancellationToken.ThrowIfCancellationRequested();
                if (elementsById.TryGetValue(id, out List<IElement>? labels) &&
                    labels.Any(static label => !string.IsNullOrWhiteSpace(label.TextContent))) return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(element.Id) &&
            explicitLabels.TryGetValue(element.Id!, out string? labelText) &&
            !string.IsNullOrWhiteSpace(labelText)) return true;

        for (IElement? ancestor = element.ParentElement; ancestor != null; ancestor = ancestor.ParentElement) {
            cancellationToken.ThrowIfCancellationRequested();
            if (ancestor.LocalName.Equals("label", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(ancestor.TextContent)) return true;
        }

        string? type = element.GetAttribute("type");
        if (string.Equals(type, "submit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "reset", StringComparison.OrdinalIgnoreCase)) return true;
        return string.Equals(type, "button", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(element.GetAttribute("value"));
    }

    private static void AuditUnsafeUrls(
        IReadOnlyList<IElement> elements,
        ICollection<HtmlDocumentAuditIssue> issues,
        CancellationToken cancellationToken) {
        foreach (IElement element in elements) {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (string attribute in UrlAttributes) {
                if (!IsUnsafeUrl(element, attribute, element.GetAttribute(attribute))) continue;
                Add(issues, "unsafe-url-scheme", HtmlDocumentAuditSeverity.Error,
                    $"The {attribute} attribute uses an executable URL scheme.", Describe(element));
            }

            AuditElementSpecificUrl(element, "object", "data", issues);
            AuditElementSpecificUrl(element, "video", "poster", issues);
            string? refreshUrl = GetMetaRefreshUrl(element);
            if (IsUnsafeUrl(element, "href", refreshUrl)) {
                Add(issues, "unsafe-url-scheme", HtmlDocumentAuditSeverity.Error,
                    "The meta refresh target uses an executable URL scheme.", Describe(element));
            }
        }
    }

    private static string? GetMetaRefreshUrl(IElement element) {
        if (!element.LocalName.Equals("meta", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(element.GetAttribute("http-equiv")?.Trim(), "refresh", StringComparison.OrdinalIgnoreCase)) return null;
        string? content = element.GetAttribute("content");
        if (string.IsNullOrWhiteSpace(content)) return null;
        int separator = content!.IndexOf(';');
        if (separator < 0 || separator == content.Length - 1) return null;
        string target = content.Substring(separator + 1).Trim();
        int equals = target.IndexOf('=');
        if (equals < 0 || !target.Substring(0, equals).Trim().Equals("url", StringComparison.OrdinalIgnoreCase)) return null;
        target = target.Substring(equals + 1).Trim();
        if (target.Length >= 2 &&
            ((target[0] == '"' && target[target.Length - 1] == '"') ||
             (target[0] == '\'' && target[target.Length - 1] == '\''))) {
            target = target.Substring(1, target.Length - 2).Trim();
        }
        return target;
    }

    private static void AuditElementSpecificUrl(
        IElement element,
        string elementName,
        string attribute,
        ICollection<HtmlDocumentAuditIssue> issues) {
        if (!element.LocalName.Equals(elementName, StringComparison.OrdinalIgnoreCase) ||
            !IsUnsafeUrl(element, attribute, element.GetAttribute(attribute))) return;
        Add(issues, "unsafe-url-scheme", HtmlDocumentAuditSeverity.Error,
            $"The {attribute} attribute uses an executable URL scheme.", Describe(element));
    }

    private static bool IsUnsafeUrl(IElement element, string attribute, string? value) {
        if (string.IsNullOrWhiteSpace(value)) return false;
        string normalized = new(value.Where(static character => !char.IsControl(character) && !char.IsWhiteSpace(character)).ToArray());
        if (!Uri.TryCreate(normalized, UriKind.RelativeOrAbsolute, out Uri? uri)) return true;
        if (!uri.IsAbsoluteUri) return false;
        if (IsImageSourceContext(element, attribute) && IsSafeEmbeddedImage(normalized)) return false;

        bool isAsset = attribute.Equals("src", StringComparison.OrdinalIgnoreCase) ||
                       (element.LocalName.Equals("link", StringComparison.OrdinalIgnoreCase) && attribute.Equals("href", StringComparison.OrdinalIgnoreCase)) ||
                       (element.LocalName.Equals("object", StringComparison.OrdinalIgnoreCase) && attribute.Equals("data", StringComparison.OrdinalIgnoreCase)) ||
                       (element.LocalName.Equals("video", StringComparison.OrdinalIgnoreCase) && attribute.Equals("poster", StringComparison.OrdinalIgnoreCase));
        if (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return false;
        return isAsset ||
               (!uri.Scheme.Equals(Uri.UriSchemeMailto, StringComparison.OrdinalIgnoreCase) &&
                !uri.Scheme.Equals("tel", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsImageSourceContext(IElement element, string attribute) {
        if (attribute.Equals("href", StringComparison.OrdinalIgnoreCase) &&
            element.LocalName.Equals("link", StringComparison.OrdinalIgnoreCase)) {
            string? relation = element.GetAttribute("rel");
            return !string.IsNullOrWhiteSpace(relation) && relation!
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(static token => token.EndsWith("icon", StringComparison.OrdinalIgnoreCase));
        }
        if (attribute.Equals("poster", StringComparison.OrdinalIgnoreCase)) {
            return element.LocalName.Equals("video", StringComparison.OrdinalIgnoreCase);
        }
        if (!attribute.Equals("src", StringComparison.OrdinalIgnoreCase)) return false;
        return element.LocalName.Equals("img", StringComparison.OrdinalIgnoreCase) ||
               element.LocalName.Equals("image", StringComparison.OrdinalIgnoreCase) ||
               element.LocalName.Equals("source", StringComparison.OrdinalIgnoreCase) ||
               element.LocalName.Equals("input", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSafeEmbeddedImage(string normalized) =>
        normalized.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase) ||
        normalized.StartsWith("data:image/jpeg;base64,", StringComparison.OrdinalIgnoreCase) ||
        normalized.StartsWith("data:image/jpg;base64,", StringComparison.OrdinalIgnoreCase) ||
        normalized.StartsWith("data:image/gif;base64,", StringComparison.OrdinalIgnoreCase) ||
        normalized.StartsWith("data:image/webp;base64,", StringComparison.OrdinalIgnoreCase) ||
        normalized.StartsWith("data:image/avif;base64,", StringComparison.OrdinalIgnoreCase) ||
        normalized.StartsWith("data:image/bmp;base64,", StringComparison.OrdinalIgnoreCase) ||
        normalized.StartsWith("data:image/x-icon;base64,", StringComparison.OrdinalIgnoreCase) ||
        normalized.StartsWith("data:image/svg+xml,", StringComparison.OrdinalIgnoreCase) ||
        normalized.StartsWith("data:image/svg+xml;base64,", StringComparison.OrdinalIgnoreCase);

    private static void AuditHeadingOrder(
        IReadOnlyList<IElement> elements,
        ICollection<HtmlDocumentAuditIssue> issues,
        CancellationToken cancellationToken) {
        int previousLevel = 0;
        foreach (IElement heading in elements) {
            cancellationToken.ThrowIfCancellationRequested();
            if (heading.LocalName.Length != 2 || heading.LocalName[0] != 'h' ||
                heading.LocalName[1] < '1' || heading.LocalName[1] > '6') continue;
            int level = heading.LocalName[1] - '0';
            if (previousLevel > 0 && level > previousLevel + 1) {
                Add(issues, "heading-level-skipped", HtmlDocumentAuditSeverity.Warning,
                    $"Heading hierarchy jumps from h{previousLevel} to h{level}.", Describe(heading));
            }
            previousLevel = level;
        }
    }

    private static string Describe(IElement element) {
        if (!string.IsNullOrWhiteSpace(element.Id)) return $"{element.LocalName}#{element.Id}";
        string? className = element.ClassList.FirstOrDefault();
        return string.IsNullOrWhiteSpace(className) ? element.LocalName : $"{element.LocalName}.{className}";
    }

    private static string NormalizeText(params string?[] values) =>
        string.Join(" ", values.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(static value => value!.Trim()));

    private static void Add(
        ICollection<HtmlDocumentAuditIssue> issues,
        string code,
        HtmlDocumentAuditSeverity severity,
        string message,
        string element) =>
        issues.Add(new HtmlDocumentAuditIssue { Code = code, Severity = severity, Message = message, Element = element });
}
