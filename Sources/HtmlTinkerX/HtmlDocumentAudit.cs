using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HtmlTinkerX;

/// <summary>
/// Audits static or rendered HTML with one reusable correctness, safety, and accessibility contract.
/// </summary>
public static class HtmlDocumentAudit {
    private static readonly string[] UrlAttributes = { "href", "xlink:href", "src", "action", "formaction" };

    /// <summary>
    /// Audits HTML markup using the supplied checks.
    /// </summary>
    /// <param name="html">HTML markup to audit.</param>
    /// <param name="options">Optional audit settings.</param>
    /// <returns>Structured issues discovered in the document.</returns>
    public static HtmlDocumentAuditResult Analyze(string html, HtmlDocumentAuditOptions? options = null) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        HtmlDocumentAuditOptions effectiveOptions = options ?? new HtmlDocumentAuditOptions();
        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        List<HtmlDocumentAuditIssue> issues = new();

        if (effectiveOptions.CheckDocumentMetadata) {
            AuditDocumentMetadata(document, issues);
        }

        if (effectiveOptions.CheckDuplicateIds) {
            AuditDuplicateIds(document, issues);
        }

        if (effectiveOptions.CheckAccessibleNames) {
            AuditAccessibleNames(document, issues);
        }

        if (effectiveOptions.CheckUnsafeUrls) {
            AuditUnsafeUrls(document, issues);
        }

        if (effectiveOptions.CheckHeadingOrder) {
            AuditHeadingOrder(document, issues);
        }

        return new HtmlDocumentAuditResult { Issues = issues };
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

    private static void AuditDuplicateIds(IDocument document, ICollection<HtmlDocumentAuditIssue> issues) {
        foreach (IGrouping<string, IElement> group in document.All
                     .Where(static element => !string.IsNullOrWhiteSpace(element.Id))
                     .GroupBy(static element => element.Id!, StringComparer.Ordinal)
                     .Where(static group => group.Count() > 1)) {
            Add(
                issues,
                "duplicate-id",
                HtmlDocumentAuditSeverity.Error,
                $"The id '{group.Key}' is used by {group.Count()} elements.",
                $"#{group.Key}");
        }
    }

    private static void AuditAccessibleNames(IDocument document, ICollection<HtmlDocumentAuditIssue> issues) {
        IReadOnlyDictionary<string, IElement> elementsById = document.All
            .Where(static element => !string.IsNullOrWhiteSpace(element.Id))
            .GroupBy(static element => element.Id!, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);
        Dictionary<string, string> explicitLabels = document.QuerySelectorAll("label[for]")
            .Where(static label => !string.IsNullOrWhiteSpace(label.GetAttribute("for")))
            .GroupBy(static label => label.GetAttribute("for")!, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => NormalizeText(group.Select(static label => label.TextContent)), StringComparer.Ordinal);

        foreach (IElement image in document.QuerySelectorAll("img")) {
            if (!image.HasAttribute("alt")) {
                Add(issues, "image-alt-missing", HtmlDocumentAuditSeverity.Error, "The image does not define an alt attribute. Use an empty alt value for decorative images.", Describe(image));
            }
        }

        foreach (IElement element in document.QuerySelectorAll("button, a[href], [role=button], [role=link]")) {
            if (!HasAccessibleName(element, explicitLabels, elementsById, allowElementText: true)) {
                Add(issues, "interactive-name-missing", HtmlDocumentAuditSeverity.Error, "The interactive element has no accessible name.", Describe(element));
            }
        }

        foreach (IElement control in document.QuerySelectorAll("input:not([type=hidden]), select, textarea")) {
            if (!HasAccessibleName(control, explicitLabels, elementsById, allowElementText: false)) {
                Add(issues, "form-label-missing", HtmlDocumentAuditSeverity.Error, "The form control has no associated label or accessible name.", Describe(control));
            }
        }
    }

    private static bool HasAccessibleName(
        IElement element,
        IReadOnlyDictionary<string, string> explicitLabels,
        IReadOnlyDictionary<string, IElement> elementsById,
        bool allowElementText) {
        if (!string.IsNullOrWhiteSpace(element.GetAttribute("aria-label")) ||
            !string.IsNullOrWhiteSpace(element.GetAttribute("title")) ||
            (allowElementText && !string.IsNullOrWhiteSpace(element.TextContent))) {
            return true;
        }

        if (element.LocalName.Equals("input", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(element.GetAttribute("type"), "image", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(element.GetAttribute("alt"))) {
            return true;
        }

        if (element.QuerySelectorAll("img[alt]").Any(static image => !string.IsNullOrWhiteSpace(image.GetAttribute("alt")))) {
            return true;
        }

        string? labelledBy = element.GetAttribute("aria-labelledby");
        if (labelledBy is string labelledByValue && !string.IsNullOrWhiteSpace(labelledByValue)) {
            foreach (string id in labelledByValue.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
                if (elementsById.TryGetValue(id, out IElement? label) && !string.IsNullOrWhiteSpace(label.TextContent)) {
                    return true;
                }
            }
        }

        string? idValue = element.Id;
        if (idValue is string controlId && !string.IsNullOrWhiteSpace(controlId) && explicitLabels.TryGetValue(controlId, out string? labelText) && !string.IsNullOrWhiteSpace(labelText)) {
            return true;
        }

        for (IElement? ancestor = element.ParentElement; ancestor != null; ancestor = ancestor.ParentElement) {
            if (ancestor.LocalName.Equals("label", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(ancestor.TextContent)) {
                return true;
            }
        }

        string? type = element.GetAttribute("type");
        if (string.Equals(type, "submit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "reset", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        return string.Equals(type, "button", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(element.GetAttribute("value"));
    }

    private static void AuditUnsafeUrls(IDocument document, ICollection<HtmlDocumentAuditIssue> issues) {
        foreach (IElement element in document.All) {
            foreach (string attribute in UrlAttributes) {
                string? value = element.GetAttribute(attribute);
                if (!IsUnsafeUrl(element, attribute, value)) {
                    continue;
                }

                Add(
                    issues,
                    "unsafe-url-scheme",
                    HtmlDocumentAuditSeverity.Error,
                    $"The {attribute} attribute uses an executable URL scheme.",
                    Describe(element));
            }

            AuditElementSpecificUrl(element, "object", "data", issues);
            AuditElementSpecificUrl(element, "video", "poster", issues);
        }
    }

    private static void AuditElementSpecificUrl(
        IElement element,
        string elementName,
        string attribute,
        ICollection<HtmlDocumentAuditIssue> issues) {
        if (!element.LocalName.Equals(elementName, StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        string? value = element.GetAttribute(attribute);
        if (!IsUnsafeUrl(element, attribute, value)) {
            return;
        }

        Add(
            issues,
            "unsafe-url-scheme",
            HtmlDocumentAuditSeverity.Error,
            $"The {attribute} attribute uses an executable URL scheme.",
            Describe(element));
    }

    private static bool IsUnsafeUrl(IElement element, string attribute, string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return false;
        }

        string normalized = new(value.Where(static character => !char.IsControl(character) && !char.IsWhiteSpace(character)).ToArray());
        if (!Uri.TryCreate(normalized, UriKind.RelativeOrAbsolute, out Uri? uri)) {
            return true;
        }

        if (!uri.IsAbsoluteUri) {
            return false;
        }

        if (IsImageSourceContext(element, attribute) && IsSafeEmbeddedImage(normalized)) {
            return false;
        }

        bool isAsset = attribute.Equals("src", StringComparison.OrdinalIgnoreCase) ||
                       (element.LocalName.Equals("link", StringComparison.OrdinalIgnoreCase) && attribute.Equals("href", StringComparison.OrdinalIgnoreCase)) ||
                       (element.LocalName.Equals("object", StringComparison.OrdinalIgnoreCase) && attribute.Equals("data", StringComparison.OrdinalIgnoreCase)) ||
                       (element.LocalName.Equals("video", StringComparison.OrdinalIgnoreCase) && attribute.Equals("poster", StringComparison.OrdinalIgnoreCase));
        if (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

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

        if (!attribute.Equals("src", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        return element.LocalName.Equals("img", StringComparison.OrdinalIgnoreCase) ||
               element.LocalName.Equals("image", StringComparison.OrdinalIgnoreCase) ||
               element.LocalName.Equals("source", StringComparison.OrdinalIgnoreCase) ||
               element.LocalName.Equals("input", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSafeEmbeddedImage(string normalized) {
        return normalized.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("data:image/jpeg;base64,", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("data:image/jpg;base64,", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("data:image/gif;base64,", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("data:image/webp;base64,", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("data:image/avif;base64,", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("data:image/bmp;base64,", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("data:image/x-icon;base64,", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("data:image/svg+xml,", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("data:image/svg+xml;base64,", StringComparison.OrdinalIgnoreCase);
    }

    private static void AuditHeadingOrder(IDocument document, ICollection<HtmlDocumentAuditIssue> issues) {
        int previousLevel = 0;
        foreach (IElement heading in document.QuerySelectorAll("h1, h2, h3, h4, h5, h6")) {
            int level = heading.LocalName[1] - '0';
            if (previousLevel > 0 && level > previousLevel + 1) {
                Add(
                    issues,
                    "heading-level-skipped",
                    HtmlDocumentAuditSeverity.Warning,
                    $"Heading hierarchy jumps from h{previousLevel} to h{level}.",
                    Describe(heading));
            }

            previousLevel = level;
        }
    }

    private static string Describe(IElement element) {
        if (!string.IsNullOrWhiteSpace(element.Id)) {
            return $"{element.LocalName}#{element.Id}";
        }

        string? className = element.ClassList.FirstOrDefault();
        return string.IsNullOrWhiteSpace(className) ? element.LocalName : $"{element.LocalName}.{className}";
    }

    private static string NormalizeText(IEnumerable<string> values) =>
        string.Join(" ", values.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(static value => value.Trim()));

    private static void Add(
        ICollection<HtmlDocumentAuditIssue> issues,
        string code,
        HtmlDocumentAuditSeverity severity,
        string message,
        string element) =>
        issues.Add(new HtmlDocumentAuditIssue {
            Code = code,
            Severity = severity,
            Message = message,
            Element = element
        });
}
