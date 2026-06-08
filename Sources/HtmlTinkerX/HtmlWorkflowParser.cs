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
            int sourceIndex = index++;
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
                Index = sourceIndex,
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

        foreach (IElement element in document.QuerySelectorAll("script, link[href], link[imagesrcset], img[src], img[srcset], input[type=image][src], picture source[srcset], audio[src], video[src], video[poster], audio source[src], video source[src], track[src], style")) {
            foreach (HtmlAssetReference asset in CreateAssetsForElement(element, effectiveBaseUri, includeInline)) {
                asset.Index = index++;
                assets.Add(asset);
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
            if (!image.HasAttribute("alt")) {
                AddFinding(findings, "missing-img-alt", "Warning", "Image is missing an alt attribute.", image, "alt", image.GetAttribute("src"));
            }
        }

        foreach (IElement label in document.QuerySelectorAll("label")) {
            if (!HasReadableLabel(label)) {
                AddFinding(findings, "empty-label", "Warning", "Label has no readable text.", label, null, null);
            }

            string? target = label.GetAttribute("for");
            if (!string.IsNullOrWhiteSpace(target)) {
                IElement? targetElement = document.GetElementById(target!);
                if (targetElement == null) {
                    AddFinding(findings, "label-target-missing", "Error", $"Label references missing id '{target}'.", label, "for", target);
                } else if (!IsLabelableElement(targetElement)) {
                    AddFinding(findings, "label-target-invalid", "Error", $"Label references non-labelable id '{target}'.", label, "for", target);
                }
            }
        }

        foreach (IElement field in document.QuerySelectorAll("button, input, select, textarea")) {
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

    private static IEnumerable<HtmlAssetReference> CreateAssetsForElement(IElement element, Uri? baseUri, bool includeInline) {
        if (element.LocalName.Equals("script", StringComparison.OrdinalIgnoreCase)) {
            string src = element.GetAttribute("src") ?? string.Empty;
            if (element.HasAttribute("src")) {
                yield return CreateAsset(0, "Script", element, "src", src, baseUri);
            } else if (includeInline && HtmlJavaScriptVariableSelector.IsJavaScriptScriptType(element.GetAttribute("type")) && !string.IsNullOrWhiteSpace(element.TextContent)) {
                yield return CreateInlineAsset(0, "InlineScript", element);
            }

            yield break;
        }

        if (element.LocalName.Equals("link", StringComparison.OrdinalIgnoreCase)) {
            string rel = element.GetAttribute("rel") ?? string.Empty;
            string? kind = GetLinkAssetKind(rel);
            if (kind != null && element.HasAttribute("href")) {
                yield return CreateAsset(0, kind, element, "href", element.GetAttribute("href") ?? string.Empty, baseUri);
            }

            if (IsImagePreload(element)) {
                foreach (string candidate in SplitSrcSet(element.GetAttribute("imagesrcset") ?? string.Empty)) {
                    yield return CreateAsset(0, "ImageCandidate", element, "imagesrcset", candidate, baseUri);
                }
            }

            yield break;
        }

        if (element.LocalName.Equals("img", StringComparison.OrdinalIgnoreCase)) {
            if (element.HasAttribute("src")) {
                yield return CreateAsset(0, "Image", element, "src", element.GetAttribute("src") ?? string.Empty, baseUri);
            }

            if (element.HasAttribute("srcset")) {
                foreach (string candidate in SplitSrcSet(element.GetAttribute("srcset") ?? string.Empty)) {
                    yield return CreateAsset(0, "ImageCandidate", element, "srcset", candidate, baseUri);
                }
            }

            yield break;
        }

        if (element.LocalName.Equals("input", StringComparison.OrdinalIgnoreCase)
            && (element.GetAttribute("type") ?? string.Empty).Trim().Equals("image", StringComparison.OrdinalIgnoreCase)
            && element.HasAttribute("src")) {
            yield return CreateAsset(0, "Image", element, "src", element.GetAttribute("src") ?? string.Empty, baseUri);
            yield break;
        }

        if (element.LocalName.Equals("source", StringComparison.OrdinalIgnoreCase) && element.ParentElement?.LocalName.Equals("picture", StringComparison.OrdinalIgnoreCase) == true) {
            foreach (string candidate in SplitSrcSet(element.GetAttribute("srcset") ?? string.Empty)) {
                yield return CreateAsset(0, "ImageCandidate", element, "srcset", candidate, baseUri);
            }

            yield break;
        }

        if (element.LocalName.Equals("audio", StringComparison.OrdinalIgnoreCase) ||
            element.LocalName.Equals("video", StringComparison.OrdinalIgnoreCase) ||
            element.LocalName.Equals("track", StringComparison.OrdinalIgnoreCase) ||
            element.LocalName.Equals("source", StringComparison.OrdinalIgnoreCase)) {
            if (element.LocalName.Equals("video", StringComparison.OrdinalIgnoreCase) && element.HasAttribute("poster")) {
                yield return CreateAsset(0, "Image", element, "poster", element.GetAttribute("poster") ?? string.Empty, baseUri);
            }

            if (element.HasAttribute("src")) {
                yield return CreateAsset(0, "Media", element, "src", element.GetAttribute("src") ?? string.Empty, baseUri);
            }

            yield break;
        }

        if (includeInline && element.LocalName.Equals("style", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(element.TextContent)) {
            yield return CreateInlineAsset(0, "InlineStyle", element);
        }
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
        string srcset = value ?? string.Empty;
        int start = 0;
        for (int index = 0; index < srcset.Length; index++) {
            if (srcset[index] != ',') {
                continue;
            }

            string candidate = srcset.Substring(start, index - start).TrimStart();
            if (candidate.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                && !candidate.Any(char.IsWhiteSpace)
                && !IsSeparatorAfterDataUrl(srcset, index)) {
                continue;
            }

            string url = GetSrcSetUrl(candidate);
            if (url.Length > 0) {
                yield return url;
            }

            start = index + 1;
        }

        string finalUrl = GetSrcSetUrl(srcset.Substring(start).TrimStart());
        if (finalUrl.Length > 0) {
            yield return finalUrl;
        }
    }

    private static string GetSrcSetUrl(string candidate) {
        string trimmed = candidate.Trim();
        if (trimmed.Length == 0) {
            return string.Empty;
        }

        if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) {
            int descriptorIndex = trimmed.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
            return descriptorIndex >= 0 ? trimmed.Substring(0, descriptorIndex) : trimmed;
        }

        return trimmed.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
    }

    private static bool IsSeparatorAfterDataUrl(string srcset, int commaIndex) {
        return commaIndex + 1 >= srcset.Length || char.IsWhiteSpace(srcset[commaIndex + 1]);
    }

    private static bool IsImagePreload(IElement link) {
        string rel = link.GetAttribute("rel") ?? string.Empty;
        string asValue = link.GetAttribute("as") ?? string.Empty;
        return asValue.Equals("image", StringComparison.OrdinalIgnoreCase)
            && rel.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(static token => token.Equals("preload", StringComparison.OrdinalIgnoreCase));
    }

    private static Uri? GetEffectiveBaseUri(IDocument document, Uri? baseUri) {
        string? baseHref = document.QuerySelector("base[href]")?.GetAttribute("href");
        if (!string.IsNullOrWhiteSpace(baseHref) && Uri.TryCreate(baseHref, UriKind.RelativeOrAbsolute, out Uri? parsedBase)) {
            if (parsedBase!.IsAbsoluteUri) {
                return parsedBase;
            }

            if (baseUri != null) {
                return new Uri(baseUri, parsedBase);
            }
        }

        return baseUri;
    }

    private static string? ResolveUrl(string url, Uri? baseUri, out bool isValid) {
        isValid = false;
        string normalized = (url ?? string.Empty).Trim();
        if (normalized.Length == 0 || normalized.Any(char.IsWhiteSpace)) {
            return null;
        }

        if (!Uri.TryCreate(normalized, UriKind.RelativeOrAbsolute, out Uri? parsed)) {
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
            "button" => true,
            "input" => !new[] { "hidden", "submit", "reset" }.Contains(type, StringComparer.OrdinalIgnoreCase),
            "select" or "textarea" => true,
            _ => false
        };
    }

    private static bool HasAccessibleLabel(IDocument document, IElement field) {
        if (!string.IsNullOrWhiteSpace(field.GetAttribute("aria-label")) ||
            !string.IsNullOrWhiteSpace(field.GetAttribute("title")) ||
            HasAltAccessibleName(field) ||
            HasValueAccessibleName(field) ||
            HasOwnReadableText(field) ||
            HasReadableLabelledByTarget(document, field.GetAttribute("aria-labelledby"))) {
            return true;
        }

        string? id = field.GetAttribute("id");
        if (!string.IsNullOrWhiteSpace(id) && document.QuerySelectorAll("label[for]").Any(label => string.Equals(label.GetAttribute("for"), id, StringComparison.Ordinal) && HasReadableLabel(label))) {
            return true;
        }

        for (INode? parent = field.Parent; parent != null; parent = parent.Parent) {
            if (parent is IElement element && element.LocalName.Equals("label", StringComparison.OrdinalIgnoreCase) && !element.HasAttribute("for") && HasReadableLabel(element)) {
                return true;
            }
        }

        return false;
    }

    private static bool IsLabelableElement(IElement element) {
        if (element.LocalName.Equals("input", StringComparison.OrdinalIgnoreCase)) {
            string type = (element.GetAttribute("type") ?? string.Empty).Trim();
            return !type.Equals("hidden", StringComparison.OrdinalIgnoreCase);
        }

        return new[] { "button", "meter", "output", "progress", "select", "textarea" }
            .Contains(element.LocalName, StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasReadableLabel(IElement label) {
        return !string.IsNullOrWhiteSpace(label.TextContent) ||
            !string.IsNullOrWhiteSpace(label.GetAttribute("aria-label")) ||
            !string.IsNullOrWhiteSpace(label.GetAttribute("title")) ||
            label.QuerySelectorAll("img[alt]").Any(static image => !string.IsNullOrWhiteSpace(image.GetAttribute("alt")));
    }

    private static bool HasAltAccessibleName(IElement field) {
        string type = (field.GetAttribute("type") ?? string.Empty).Trim();
        return field.LocalName.Equals("input", StringComparison.OrdinalIgnoreCase) &&
            type.Equals("image", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(field.GetAttribute("alt"));
    }

    private static bool HasValueAccessibleName(IElement field) {
        string type = (field.GetAttribute("type") ?? string.Empty).Trim();
        return field.LocalName.Equals("input", StringComparison.OrdinalIgnoreCase) &&
            type.Equals("button", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(field.GetAttribute("value"));
    }

    private static bool HasOwnReadableText(IElement field) {
        return field.LocalName.Equals("button", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(field.TextContent);
    }

    private static bool HasReadableLabelledByTarget(IDocument document, string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return false;
        }

        string labelledBy = value!;
        foreach (string id in labelledBy.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
            IElement? target = document.GetElementById(id);
            if (!string.IsNullOrWhiteSpace(target?.TextContent)) {
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
