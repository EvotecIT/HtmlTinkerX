using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HtmlTinkerX;

/// <summary>Provides HTML script, asset, and compatibility workflow extraction helpers.</summary>
public static class HtmlWorkflowParser {
    /// <summary>Selects script elements from HTML.</summary>
    public static IReadOnlyList<HtmlScriptReference> SelectScripts(string html, Uri? baseUri = null, bool javaScriptOnly = false, bool includeInline = true) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        Uri? effectiveBaseUri = GetEffectiveBaseUri(document, baseUri);
        List<HtmlScriptReference> scripts = new();
        int index = 0;
        foreach (IElement script in document.QuerySelectorAll("script")) {
            string type = script.GetAttribute("type") ?? string.Empty;
            bool isJavaScript = HtmlJavaScriptVariableSelector.IsJavaScriptScriptType(type);
            bool isModule = NormalizeScriptType(type).Equals("module", StringComparison.OrdinalIgnoreCase);
            string src = script.GetAttribute("src") ?? string.Empty;
            string content = script.TextContent ?? string.Empty;

            if (javaScriptOnly && !isJavaScript) {
                continue;
            }

            if (!includeInline && string.IsNullOrWhiteSpace(src)) {
                continue;
            }

            scripts.Add(new HtmlScriptReference {
                Index = index++,
                Type = type,
                Source = src,
                ResolvedUrl = ResolveUrl(src, effectiveBaseUri, out _),
                Content = string.IsNullOrWhiteSpace(src) ? content : string.Empty,
                IsJavaScript = isJavaScript,
                IsModule = isModule,
                IsExternal = !string.IsNullOrWhiteSpace(src)
            });
        }

        return scripts;
    }

    /// <summary>Selects common asset references from HTML.</summary>
    public static IReadOnlyList<HtmlAssetReference> SelectAssets(string html, Uri? baseUri = null, bool includeInline = true) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        Uri? effectiveBaseUri = GetEffectiveBaseUri(document, baseUri);
        List<HtmlAssetReference> assets = new();
        int index = 0;

        foreach (IElement script in document.QuerySelectorAll("script")) {
            string src = script.GetAttribute("src") ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(src)) {
                assets.Add(CreateAsset(index++, "Script", script, "src", src, effectiveBaseUri));
            } else if (includeInline && HtmlJavaScriptVariableSelector.IsJavaScriptScriptType(script.GetAttribute("type")) && !string.IsNullOrWhiteSpace(script.TextContent)) {
                assets.Add(CreateInlineAsset(index++, "InlineScript", script));
            }
        }

        foreach (IElement link in document.QuerySelectorAll("link[href]")) {
            string rel = link.GetAttribute("rel") ?? string.Empty;
            string href = link.GetAttribute("href") ?? string.Empty;
            string? kind = GetLinkAssetKind(rel);
            if (kind != null) {
                assets.Add(CreateAsset(index++, kind, link, "href", href, effectiveBaseUri));
            }
        }

        foreach (IElement image in document.QuerySelectorAll("img[src]")) {
            assets.Add(CreateAsset(index++, "Image", image, "src", image.GetAttribute("src") ?? string.Empty, effectiveBaseUri));
        }

        foreach (IElement source in document.QuerySelectorAll("source[src], source[srcset], img[srcset]")) {
            string attribute = !string.IsNullOrWhiteSpace(source.GetAttribute("srcset")) ? "srcset" : "src";
            string value = source.GetAttribute(attribute) ?? string.Empty;
            foreach (string candidate in SplitSrcSet(value)) {
                assets.Add(CreateAsset(index++, "ImageCandidate", source, attribute, candidate, effectiveBaseUri));
            }
        }

        if (includeInline) {
            foreach (IElement style in document.QuerySelectorAll("style")) {
                if (!string.IsNullOrWhiteSpace(style.TextContent)) {
                    assets.Add(CreateInlineAsset(index++, "InlineStyle", style));
                }
            }
        }

        return assets;
    }

    /// <summary>Finds common generated-page compatibility and accessibility issues.</summary>
    public static IReadOnlyList<HtmlCompatibilityFinding> MeasureCompatibility(string html, Uri? baseUri = null) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        Uri? effectiveBaseUri = GetEffectiveBaseUri(document, baseUri);
        List<HtmlCompatibilityFinding> findings = new();

        foreach (IGrouping<string, IElement> duplicate in document.QuerySelectorAll("[id]")
                     .Where(static element => !string.IsNullOrWhiteSpace(element.Id))
                     .GroupBy(static element => element.Id!, StringComparer.Ordinal)
                     .Where(static group => group.Count() > 1)) {
            foreach (IElement element in duplicate) {
                AddFinding(findings, "duplicate-id", "Error", $"Duplicate id '{duplicate.Key}'.", element, "id", duplicate.Key);
            }
        }

        foreach (IElement image in document.QuerySelectorAll("img")) {
            if (string.IsNullOrWhiteSpace(image.GetAttribute("alt"))) {
                AddFinding(findings, "missing-img-alt", "Warning", "Image is missing non-empty alt text.", image, "alt", image.GetAttribute("src"));
            }
        }

        foreach (IElement label in document.QuerySelectorAll("label")) {
            string text = (label.TextContent ?? string.Empty).Trim();
            if (text.Length == 0 && string.IsNullOrWhiteSpace(label.GetAttribute("aria-label")) && string.IsNullOrWhiteSpace(label.GetAttribute("title"))) {
                AddFinding(findings, "empty-label", "Warning", "Label has no readable text.", label, null, null);
            }

            string? target = label.GetAttribute("for");
            if (!string.IsNullOrWhiteSpace(target) && document.GetElementById(target!) == null) {
                AddFinding(findings, "label-target-missing", "Error", $"Label references missing id '{target}'.", label, "for", target);
            }
        }

        foreach (IElement field in document.QuerySelectorAll("input, select, textarea")) {
            if (IsLabelableFieldNeedingLabel(field) && !HasAccessibleLabel(document, field)) {
                AddFinding(findings, "form-field-missing-label", "Warning", "Form field has no associated label or accessible name.", field, null, field.GetAttribute("name"));
            }
        }

        foreach (IElement element in document.QuerySelectorAll("[style]")) {
            if (string.IsNullOrWhiteSpace(element.GetAttribute("style"))) {
                AddFinding(findings, "empty-inline-style", "Info", "Element has an empty inline style attribute.", element, "style", null);
            }
        }

        foreach (HtmlAssetReference asset in SelectAssets(html, effectiveBaseUri, includeInline: false)) {
            if (!asset.IsValidUrl || string.IsNullOrWhiteSpace(asset.ResolvedUrl)) {
                findings.Add(new HtmlCompatibilityFinding {
                    RuleId = "invalid-resource-url",
                    Severity = "Error",
                    Message = $"Asset URL '{asset.Url}' could not be resolved.",
                    Selector = asset.Element,
                    Element = asset.Element,
                    Attribute = asset.Attribute,
                    Value = asset.Url
                });
            }
        }

        return findings;
    }

    private static HtmlAssetReference CreateAsset(int index, string kind, IElement element, string attribute, string url, Uri? baseUri) {
        string? resolved = ResolveUrl(url, baseUri, out bool isValid);
        return new HtmlAssetReference {
            Index = index,
            Kind = kind,
            Element = element.LocalName,
            Attribute = attribute,
            Url = url,
            ResolvedUrl = resolved,
            Rel = element.GetAttribute("rel"),
            Type = element.GetAttribute("type"),
            Media = element.GetAttribute("media"),
            IsValidUrl = isValid,
            IsInline = false
        };
    }

    private static HtmlAssetReference CreateInlineAsset(int index, string kind, IElement element) {
        return new HtmlAssetReference {
            Index = index,
            Kind = kind,
            Element = element.LocalName,
            Type = element.GetAttribute("type"),
            Media = element.GetAttribute("media"),
            IsValidUrl = true,
            IsInline = true,
            Content = element.TextContent ?? string.Empty
        };
    }

    private static string? GetLinkAssetKind(string rel) {
        string[] tokens = rel.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Any(static token => token.Equals("stylesheet", StringComparison.OrdinalIgnoreCase))) {
            return "Stylesheet";
        }

        if (tokens.Any(static token => token.Equals("modulepreload", StringComparison.OrdinalIgnoreCase))) {
            return "ModulePreload";
        }

        if (tokens.Any(static token => token.Equals("preload", StringComparison.OrdinalIgnoreCase) || token.Equals("preconnect", StringComparison.OrdinalIgnoreCase) || token.Equals("dns-prefetch", StringComparison.OrdinalIgnoreCase))) {
            return "Preload";
        }

        if (tokens.Any(static token => token.Equals("manifest", StringComparison.OrdinalIgnoreCase))) {
            return "Manifest";
        }

        if (tokens.Any(static token => token.Equals("icon", StringComparison.OrdinalIgnoreCase) || token.Equals("apple-touch-icon", StringComparison.OrdinalIgnoreCase) || token.Equals("mask-icon", StringComparison.OrdinalIgnoreCase))) {
            return "Icon";
        }

        return null;
    }

    private static IEnumerable<string> SplitSrcSet(string value) {
        foreach (string candidate in (value ?? string.Empty).Split(',')) {
            string trimmed = candidate.Trim();
            if (trimmed.Length == 0) {
                continue;
            }

            string url = trimmed.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            if (url.Length > 0) {
                yield return url;
            }
        }
    }

    private static Uri? GetEffectiveBaseUri(IDocument document, Uri? baseUri) {
        if (baseUri != null) {
            return baseUri;
        }

        string? baseHref = document.QuerySelector("base[href]")?.GetAttribute("href");
        return Uri.TryCreate(baseHref, UriKind.Absolute, out Uri? parsed) ? parsed : null;
    }

    private static string? ResolveUrl(string url, Uri? baseUri, out bool isValid) {
        isValid = false;
        if (string.IsNullOrWhiteSpace(url) || url.Any(char.IsWhiteSpace)) {
            return null;
        }

        if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out Uri? parsed)) {
            return null;
        }

        isValid = true;
        if (parsed!.IsAbsoluteUri) {
            return parsed.ToString();
        }

        return baseUri != null ? new Uri(baseUri, parsed).ToString() : parsed.ToString();
    }

    private static bool IsLabelableFieldNeedingLabel(IElement field) {
        string type = (field.GetAttribute("type") ?? string.Empty).Trim();
        return field.LocalName switch {
            "input" => !new[] { "hidden", "submit", "button", "reset", "image" }.Contains(type, StringComparer.OrdinalIgnoreCase),
            "select" or "textarea" => true,
            _ => false
        };
    }

    private static bool HasAccessibleLabel(IDocument document, IElement field) {
        if (!string.IsNullOrWhiteSpace(field.GetAttribute("aria-label")) ||
            !string.IsNullOrWhiteSpace(field.GetAttribute("aria-labelledby")) ||
            !string.IsNullOrWhiteSpace(field.GetAttribute("title"))) {
            return true;
        }

        string? id = field.GetAttribute("id");
        if (!string.IsNullOrWhiteSpace(id) && document.QuerySelectorAll("label[for]").Any(label => string.Equals(label.GetAttribute("for"), id, StringComparison.Ordinal))) {
            return true;
        }

        for (INode? parent = field.Parent; parent != null; parent = parent.Parent) {
            if (parent is IElement element && element.LocalName.Equals("label", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }

    private static void AddFinding(List<HtmlCompatibilityFinding> findings, string ruleId, string severity, string message, IElement element, string? attribute, string? value) {
        findings.Add(new HtmlCompatibilityFinding {
            RuleId = ruleId,
            Severity = severity,
            Message = message,
            Selector = CreateSelectorHint(element),
            Element = element.LocalName,
            Attribute = attribute,
            Value = value
        });
    }

    private static string CreateSelectorHint(IElement element) {
        if (!string.IsNullOrWhiteSpace(element.Id)) {
            return $"{element.LocalName}#{element.Id}";
        }

        string className = element.GetAttribute("class") ?? string.Empty;
        string firstClass = className.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return firstClass.Length > 0 ? $"{element.LocalName}.{firstClass}" : element.LocalName;
    }

    private static string NormalizeScriptType(string? type) {
        return (type ?? string.Empty).Split(';')[0].Trim();
    }
}
