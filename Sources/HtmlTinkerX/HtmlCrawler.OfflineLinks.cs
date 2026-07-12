using AngleSharp.Dom;
using Microsoft.Playwright;
using OfficeIMO.Markdown;
using OfficeIMO.Markdown.Html;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace HtmlTinkerX;

public static partial class HtmlCrawler {
    private static List<string> ExtractLinks(string html, Uri baseUri, HtmlCrawlOptions options) {
        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        Uri effectiveBaseUri = GetDocumentBaseUri(document, baseUri);
        HashSet<string> links = new(StringComparer.OrdinalIgnoreCase);

        foreach (IElement anchor in document.QuerySelectorAll("a[href]")) {
            string? href = anchor.GetAttribute("href");
            if (string.IsNullOrWhiteSpace(href)) {
                continue;
            }

            string safeHref = href!;
            if (safeHref.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
                safeHref.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                safeHref.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            if (!Uri.TryCreate(effectiveBaseUri, safeHref, out Uri? resolved)) {
                continue;
            }

            if (resolved.Scheme != Uri.UriSchemeHttp && resolved.Scheme != Uri.UriSchemeHttps) {
                continue;
            }

            links.Add(NormalizeUrl(resolved, options));
        }

        return links.ToList();
    }

    private static List<string> ExtractAssetUrls(string html, Uri baseUri, HtmlCrawlOptions options) {
        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        Uri effectiveBaseUri = GetDocumentBaseUri(document, baseUri);
        HashSet<string> assets = new(StringComparer.OrdinalIgnoreCase);

        CollectAssetUrlsFromContainer(document, effectiveBaseUri, options, assets);
        foreach (IElement noscript in document.QuerySelectorAll("noscript")) {
            if (!TryGetNoscriptFallbackWrapper(noscript, out IElement wrapper)) {
                continue;
            }

            CollectAssetUrlsFromContainer(wrapper, effectiveBaseUri, options, assets);
        }

        return assets.ToList();
    }

    private static void CollectAssetUrlsFromContainer(
        IParentNode container,
        Uri effectiveBaseUri,
        HtmlCrawlOptions options,
        ISet<string> assets) {
        foreach (IElement element in container.QuerySelectorAll("img, source, video, audio, track, script, iframe, embed, object[data], link[href], a[href], style, [style]")) {
            switch (element.TagName.ToUpperInvariant()) {
                case "IMG":
                case "SOURCE":
                case "VIDEO":
                case "AUDIO":
                case "TRACK":
                case "SCRIPT":
                case "IFRAME":
                case "EMBED":
                    AddAssetCandidate(element.GetAttribute("src"), effectiveBaseUri, options, assets);
                    AddAssetCandidate(element.GetAttribute("data-src"), effectiveBaseUri, options, assets);
                    AddAssetCandidate(element.GetAttribute("data-lazy-src"), effectiveBaseUri, options, assets);
                    AddAssetCandidate(element.GetAttribute("data-original-src"), effectiveBaseUri, options, assets);
                    if (element.TagName.Equals("VIDEO", StringComparison.OrdinalIgnoreCase)) {
                        AddAssetCandidate(element.GetAttribute("poster"), effectiveBaseUri, options, assets);
                    }
                    foreach (string srcSetCandidate in ExtractSrcSetUrls(
                                 element.GetAttribute("srcset"),
                                 element.GetAttribute("data-srcset"),
                                 element.GetAttribute("data-lazy-srcset"),
                                 element.GetAttribute("data-original-srcset"))) {
                        AddAssetCandidate(srcSetCandidate, effectiveBaseUri, options, assets);
                    }
                    break;
                case "OBJECT":
                    AddAssetCandidate(element.GetAttribute("data"), effectiveBaseUri, options, assets);
                    break;
                case "LINK":
                    if (ShouldTreatLinkAsOfflineAsset(element)) {
                        AddAssetCandidate(element.GetAttribute("href"), effectiveBaseUri, options, assets);
                        foreach (string srcSetCandidate in ExtractSrcSetUrls(element.GetAttribute("imagesrcset"))) {
                            AddAssetCandidate(srcSetCandidate, effectiveBaseUri, options, assets);
                        }
                    }
                    break;
                case "A":
                    string? href = element.GetAttribute("href");
                    if (LooksLikeAssetPath(href, options)) {
                        AddAssetCandidate(href, effectiveBaseUri, options, assets);
                    }
                    break;
                case "STYLE":
                    foreach (string cssUrl in ExtractCssUrls(element.TextContent)) {
                        AddAssetCandidate(cssUrl, effectiveBaseUri, options, assets);
                    }
                    break;
                default:
                    string? inlineStyle = element.GetAttribute("style");
                    if (!string.IsNullOrWhiteSpace(inlineStyle)) {
                        foreach (string cssUrl in ExtractCssUrls(inlineStyle)) {
                            AddAssetCandidate(cssUrl, effectiveBaseUri, options, assets);
                        }
                    }
                    break;
            }
        }
    }

    private static Dictionary<string, string> BuildLocalPageMap(IEnumerable<HtmlCrawlPage> pages) {
        Dictionary<string, string> pageMap = new(StringComparer.OrdinalIgnoreCase);
        foreach (HtmlCrawlPage page in pages) {
            if (string.IsNullOrWhiteSpace(page.HtmlPath)) {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(page.Url)) {
                pageMap[page.Url] = page.HtmlPath!;
            }

            if (!string.IsNullOrWhiteSpace(page.RequestedUrl)) {
                pageMap[page.RequestedUrl!] = page.HtmlPath!;
            }

            if (!string.IsNullOrWhiteSpace(page.CanonicalUrl)) {
                pageMap[page.CanonicalUrl!] = page.HtmlPath!;
            }
        }

        return pageMap;
    }

    private static bool ShouldRewriteStoredHtml(HtmlCrawlOptions? options) {
        return options?.IncludeHtml == true
               && ((options.DownloadAssets && options.RewriteAssetReferencesToLocal)
                   || options.RewritePageLinksToLocal);
    }

    private static string RewriteStoredHtmlToLocalPaths(
        string html,
        string pageUrl,
        string pageHtmlPath,
        IEnumerable<HtmlCrawlAsset> assets,
        IDictionary<string, string> localPageMap,
        HtmlCrawlOptions options) {
        if (string.IsNullOrWhiteSpace(html)
            || string.IsNullOrWhiteSpace(pageUrl)
            || string.IsNullOrWhiteSpace(pageHtmlPath)) {
            return html;
        }

        Dictionary<string, string> assetMap = assets
            .Where(asset => !string.IsNullOrWhiteSpace(asset.Url) && !string.IsNullOrWhiteSpace(asset.FilePath) && string.IsNullOrWhiteSpace(asset.Error))
            .GroupBy(asset => asset.Url, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().FilePath!, StringComparer.OrdinalIgnoreCase);
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out Uri? pageUri)) {
            return html;
        }

        if (LooksLikeFullHtmlDocument(html)) {
            IDocument document = HtmlParser.ParseWithAngleSharp(html);
            Uri effectiveBaseUri = GetDocumentBaseUri(document, pageUri);
            RewriteStoredReferencesInContainer(document, effectiveBaseUri, pageHtmlPath, assetMap, localPageMap, options);
            RemoveBaseElements(document);
            return document.DocumentElement?.OuterHtml ?? html;
        }

        IDocument fragmentDocument = HtmlParser.ParseWithAngleSharp($"<div id=\"__htmltinkerx_assetwrap\">{html}</div>");
        IElement? wrapper = fragmentDocument.QuerySelector("#__htmltinkerx_assetwrap");
        if (wrapper == null) {
            return html;
        }

        Uri effectiveFragmentBaseUri = GetDocumentBaseUri(fragmentDocument, pageUri);
        RewriteStoredReferencesInContainer(wrapper, effectiveFragmentBaseUri, pageHtmlPath, assetMap, localPageMap, options);
        RemoveBaseElements(wrapper);
        return wrapper.InnerHtml;
    }

    private static bool LooksLikeFullHtmlDocument(string html) {
        string sample = html.TrimStart();
        return sample.StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase)
               || sample.StartsWith("<html", StringComparison.OrdinalIgnoreCase)
               || sample.StartsWith("<head", StringComparison.OrdinalIgnoreCase)
               || sample.StartsWith("<body", StringComparison.OrdinalIgnoreCase);
    }

    private static void RewriteStoredReferencesInContainer(
        IParentNode container,
        Uri resolutionBaseUri,
        string pageHtmlPath,
        IDictionary<string, string> assetMap,
        IDictionary<string, string> localPageMap,
        HtmlCrawlOptions options) {
        foreach (IElement element in container.QuerySelectorAll("img, source, video, audio, track, script, iframe, embed, object[data], link[href], a[href]")) {
            switch (element.TagName.ToUpperInvariant()) {
                case "IMG":
                case "SOURCE":
                case "VIDEO":
                case "AUDIO":
                case "TRACK":
                case "SCRIPT":
                case "IFRAME":
                case "EMBED":
                    RewriteAssetAttribute(element, "src", resolutionBaseUri, pageHtmlPath, assetMap, options);
                    RewriteAssetAttribute(element, "data-src", resolutionBaseUri, pageHtmlPath, assetMap, options);
                    RewriteAssetAttribute(element, "data-lazy-src", resolutionBaseUri, pageHtmlPath, assetMap, options);
                    RewriteAssetAttribute(element, "data-original-src", resolutionBaseUri, pageHtmlPath, assetMap, options);
                    if (element.TagName.Equals("VIDEO", StringComparison.OrdinalIgnoreCase)) {
                        RewriteAssetAttribute(element, "poster", resolutionBaseUri, pageHtmlPath, assetMap, options);
                    }
                    break;
                case "OBJECT":
                    RewriteAssetAttribute(element, "data", resolutionBaseUri, pageHtmlPath, assetMap, options);
                    break;
                case "LINK":
                    if (ShouldTreatLinkAsOfflineAsset(element)) {
                        RewriteAssetAttribute(element, "href", resolutionBaseUri, pageHtmlPath, assetMap, options);
                        RewriteSrcSetAttribute(element, "imagesrcset", resolutionBaseUri, pageHtmlPath, assetMap, options);
                    }
                    break;
                case "A":
                    string? href = element.GetAttribute("href");
                    if (options.RewritePageLinksToLocal) {
                        RewritePageAttribute(element, "href", resolutionBaseUri, pageHtmlPath, localPageMap, options);
                    }
                    if (options.DownloadAssets && options.RewriteAssetReferencesToLocal && LooksLikeAssetPath(href, options)) {
                        RewriteAssetAttribute(element, "href", resolutionBaseUri, pageHtmlPath, assetMap, options);
                    }
                    break;
            }
        }

        foreach (IElement element in container.QuerySelectorAll("style")) {
            string css = element.TextContent ?? string.Empty;
            string rewrittenCss = RewriteCssUrlsToLocal(css, resolutionBaseUri, pageHtmlPath, assetMap, options);
            if (!string.Equals(css, rewrittenCss, StringComparison.Ordinal)) {
                element.TextContent = rewrittenCss;
            }
        }

        foreach (IElement element in container.QuerySelectorAll("[style]")) {
            string? style = element.GetAttribute("style");
            if (string.IsNullOrWhiteSpace(style)) {
                continue;
            }

            string rewrittenStyle = RewriteCssUrlsToLocal(style!, resolutionBaseUri, pageHtmlPath, assetMap, options);
            if (!string.Equals(style, rewrittenStyle, StringComparison.Ordinal)) {
                element.SetAttribute("style", rewrittenStyle);
            }
        }

        foreach (IElement element in container.QuerySelectorAll("img, source")) {
            RewriteSrcSetAttribute(element, "srcset", resolutionBaseUri, pageHtmlPath, assetMap, options);
            RewriteSrcSetAttribute(element, "data-srcset", resolutionBaseUri, pageHtmlPath, assetMap, options);
            RewriteSrcSetAttribute(element, "data-lazy-srcset", resolutionBaseUri, pageHtmlPath, assetMap, options);
            RewriteSrcSetAttribute(element, "data-original-srcset", resolutionBaseUri, pageHtmlPath, assetMap, options);
        }

        RewriteNoscriptFallbackReferences(container, resolutionBaseUri, pageHtmlPath, assetMap, localPageMap, options);
    }

    private static Uri GetDocumentBaseUri(IParentNode container, Uri fallbackBaseUri) {
        IElement? baseElement = container.QuerySelector("base[href]");
        string? href = baseElement?.GetAttribute("href");
        if (string.IsNullOrWhiteSpace(href)) {
            return fallbackBaseUri;
        }

        return TryResolveAbsoluteUri(fallbackBaseUri, href!, out Uri? resolved) ? resolved! : fallbackBaseUri;
    }

    private static void RemoveBaseElements(IParentNode container) {
        foreach (IElement baseElement in container.QuerySelectorAll("base[href]").ToArray()) {
            baseElement.Remove();
        }
    }

    private static void RewriteNoscriptFallbackReferences(
        IParentNode container,
        Uri resolutionBaseUri,
        string pageHtmlPath,
        IDictionary<string, string> assetMap,
        IDictionary<string, string> localPageMap,
        HtmlCrawlOptions options) {
        foreach (IElement noscript in container.QuerySelectorAll("noscript")) {
            if (!TryGetNoscriptFallbackWrapper(noscript, out IElement wrapper)) {
                continue;
            }

            RewriteStoredReferencesInContainer(wrapper, resolutionBaseUri, pageHtmlPath, assetMap, localPageMap, options);
            RemoveBaseElements(wrapper);
            noscript.InnerHtml = wrapper.InnerHtml;
        }
    }

    private static bool TryGetNoscriptFallbackWrapper(IElement element, out IElement wrapper) {
        wrapper = null!;
        if (element == null || !element.TagName.Equals("NOSCRIPT", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        foreach (string html in EnumerateNoscriptHtmlCandidates(element)) {
            IDocument document = HtmlParser.ParseWithAngleSharp($"<div id=\"__htmltinkerx_noscript_media\">{html}</div>");
            IElement? parsedWrapper = document.QuerySelector("#__htmltinkerx_noscript_media");
            if (parsedWrapper?.QuerySelector("img,picture,source,video,audio") == null) {
                continue;
            }

            wrapper = parsedWrapper;
            return true;
        }

        return false;
    }

    private static bool ShouldTreatLinkAsOfflineAsset(IElement element) {
        if (element == null || !element.TagName.Equals("LINK", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        string rel = element.GetAttribute("rel") ?? string.Empty;
        return rel.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0
               || rel.IndexOf("image", StringComparison.OrdinalIgnoreCase) >= 0
               || rel.IndexOf("stylesheet", StringComparison.OrdinalIgnoreCase) >= 0
               || rel.IndexOf("preload", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void RewriteAssetAttribute(
        IElement element,
        string attributeName,
        Uri pageUri,
        string pageHtmlPath,
        IDictionary<string, string> assetMap,
        HtmlCrawlOptions options) {
        string? value = element.GetAttribute(attributeName);
        if (string.IsNullOrWhiteSpace(value)) {
            return;
        }

        if (!TryResolveAbsoluteUri(pageUri, value!, out Uri? resolved)) {
            return;
        }

        string normalized = NormalizeUrl(resolved!, options);
        if (!assetMap.TryGetValue(normalized, out string? localPath) || string.IsNullOrWhiteSpace(localPath)) {
            return;
        }

        element.SetAttribute(attributeName, BuildRelativePath(pageHtmlPath, localPath));
    }

    private static void RewriteSrcSetAttribute(
        IElement element,
        string attributeName,
        Uri pageUri,
        string pageHtmlPath,
        IDictionary<string, string> assetMap,
        HtmlCrawlOptions options) {
        string? value = element.GetAttribute(attributeName);
        if (string.IsNullOrWhiteSpace(value)) {
            return;
        }

        string rewritten = RewriteSrcSetToLocal(value!, pageUri, pageHtmlPath, assetMap, options);
        if (!string.Equals(rewritten, value, StringComparison.Ordinal)) {
            element.SetAttribute(attributeName, rewritten);
        }
    }

    private static void RewritePageAttribute(
        IElement element,
        string attributeName,
        Uri pageUri,
        string pageHtmlPath,
        IDictionary<string, string> localPageMap,
        HtmlCrawlOptions options) {
        string? value = element.GetAttribute(attributeName);
        if (string.IsNullOrWhiteSpace(value)) {
            return;
        }

        if (value!.StartsWith("#", StringComparison.Ordinal)) {
            return;
        }

        if (!TryResolveAbsoluteUri(pageUri, value, out Uri? resolved)) {
            return;
        }

        string normalized = NormalizeUrl(resolved!, options);
        if (!localPageMap.TryGetValue(normalized, out string? localPath) || string.IsNullOrWhiteSpace(localPath)) {
            return;
        }

        string relative = BuildRelativePath(pageHtmlPath, localPath);
        if (resolved!.Fragment.Length > 0) {
            relative += resolved.Fragment;
        }

        element.SetAttribute(attributeName, relative);
    }

    private static string RewriteSrcSetToLocal(
        string srcSet,
        Uri pageUri,
        string pageHtmlPath,
        IDictionary<string, string> assetMap,
        HtmlCrawlOptions options) {
        List<string> rewritten = new();
        foreach (string entry in srcSet!.Split(',')) {
            string trimmed = entry.Trim();
            if (trimmed.Length == 0) {
                continue;
            }

            int separatorIndex = trimmed.IndexOf(' ');
            string candidate = separatorIndex > 0 ? trimmed.Substring(0, separatorIndex) : trimmed;
            string descriptor = separatorIndex > 0 ? trimmed.Substring(separatorIndex).Trim() : string.Empty;
            string finalCandidate = candidate;

            if (TryResolveAbsoluteUri(pageUri, candidate, out Uri? resolved)) {
                string normalized = NormalizeUrl(resolved!, options);
                if (assetMap.TryGetValue(normalized, out string? localPath) && !string.IsNullOrWhiteSpace(localPath)) {
                    finalCandidate = BuildRelativePath(pageHtmlPath, localPath);
                }
            }

            rewritten.Add(string.IsNullOrEmpty(descriptor) ? finalCandidate : $"{finalCandidate} {descriptor}");
        }

        return string.Join(", ", rewritten);
    }

    private static string RewriteCssUrlsToLocal(
        string css,
        Uri pageUri,
        string pageHtmlPath,
        IDictionary<string, string> assetMap,
        HtmlCrawlOptions options) {
        if (string.IsNullOrWhiteSpace(css)) {
            return css;
        }

        string rewritten = Regex.Replace(
            css,
            @"url\(\s*(?:""(?<value>[^""]+)""|'(?<value>[^']+)'|(?<value>[^)\s]+))\s*\)",
            match => {
                string original = match.Groups["value"].Value;
                string replaced = RewriteCssUrlCandidate(original, pageUri, pageHtmlPath, assetMap, options);
                if (string.Equals(original, replaced, StringComparison.Ordinal)) {
                    return match.Value;
                }

                if (match.Value.Contains("\"", StringComparison.Ordinal)) {
                    return $"url(\"{replaced}\")";
                }

                if (match.Value.Contains("'", StringComparison.Ordinal)) {
                    return $"url('{replaced}')";
                }

                return $"url({replaced})";
            },
            RegexOptions.IgnoreCase);

        rewritten = Regex.Replace(
            rewritten,
            @"@import\s+(?:""(?<value>[^""]+)""|'(?<value>[^']+)')",
            match => {
                string original = match.Groups["value"].Value;
                string replaced = RewriteCssUrlCandidate(original, pageUri, pageHtmlPath, assetMap, options);
                if (string.Equals(original, replaced, StringComparison.Ordinal)) {
                    return match.Value;
                }

                if (match.Value.Contains("\"", StringComparison.Ordinal)) {
                    return $"@import \"{replaced}\"";
                }

                return $"@import '{replaced}'";
            },
            RegexOptions.IgnoreCase);

        return rewritten;
    }

    private static string RewriteCssUrlCandidate(
        string candidate,
        Uri pageUri,
        string pageHtmlPath,
        IDictionary<string, string> assetMap,
        HtmlCrawlOptions options) {
        if (string.IsNullOrWhiteSpace(candidate)) {
            return candidate;
        }

        if (!TryResolveAbsoluteUri(pageUri, candidate, out Uri? resolved)) {
            return candidate;
        }

        string normalized = NormalizeUrl(resolved!, options);
        if (!assetMap.TryGetValue(normalized, out string? localPath) || string.IsNullOrWhiteSpace(localPath)) {
            return candidate;
        }

        return BuildRelativePath(pageHtmlPath, localPath);
    }

    private static IEnumerable<string> ExtractSrcSetUrls(params string?[] srcSets) {
        foreach (string? srcSet in srcSets) {
            if (string.IsNullOrWhiteSpace(srcSet)) {
                continue;
            }

            string normalizedSrcSet = srcSet!;
            foreach (string entry in normalizedSrcSet.Split(',')) {
                string trimmed = entry.Trim();
                if (trimmed.Length == 0) {
                    continue;
                }

                int separatorIndex = trimmed.IndexOf(' ');
                yield return separatorIndex > 0 ? trimmed.Substring(0, separatorIndex) : trimmed;
            }
        }
    }

    private static IEnumerable<string> ExtractCssUrls(string? cssText) {
        if (string.IsNullOrWhiteSpace(cssText)) {
            yield break;
        }

        foreach (Match match in Regex.Matches(cssText!, @"url\(\s*(?:""(?<value>[^""]+)""|'(?<value>[^']+)'|(?<value>[^)\s]+))\s*\)", RegexOptions.IgnoreCase)) {
            string value = match.Groups["value"].Value;
            if (!string.IsNullOrWhiteSpace(value)) {
                yield return value;
            }
        }

        foreach (Match match in Regex.Matches(cssText!, @"@import\s+(?:""(?<value>[^""]+)""|'(?<value>[^']+)')", RegexOptions.IgnoreCase)) {
            string value = match.Groups["value"].Value;
            if (!string.IsNullOrWhiteSpace(value)) {
                yield return value;
            }
        }
    }

    private static void AddAssetCandidate(string? candidate, Uri baseUri, HtmlCrawlOptions options, ISet<string> assets) {
        if (string.IsNullOrWhiteSpace(candidate)) {
            return;
        }

        string safeCandidate = candidate!;
        if (safeCandidate.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            || safeCandidate.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
            || safeCandidate.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            || safeCandidate.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        if (!TryResolveAbsoluteUri(baseUri, safeCandidate, out Uri? resolved)) {
            return;
        }

        if (!IsAssetUrlAllowed(resolved!, baseUri, options)) {
            return;
        }

        assets.Add(NormalizeUrl(resolved!, options));
    }

    private static bool LooksLikeAssetPath(string? candidate, HtmlCrawlOptions options) {
        if (string.IsNullOrWhiteSpace(candidate)) {
            return false;
        }

        string value = candidate!;
        int queryIndex = value.IndexOfAny(new[] { '?', '#' });
        string path = queryIndex >= 0 ? value.Substring(0, queryIndex) : value;
        return MatchesAny(path, options.IgnoredAssetPathPatterns)
               || (options.AssetIncludePatterns.Count > 0 && MatchesAny(candidate!, options.AssetIncludePatterns));
    }

    private static bool IsAssetUrlAllowed(Uri assetUri, Uri pageUri, HtmlCrawlOptions options) {
        if (options.RestrictToHost && !IsHostInScope(assetUri.Host, pageUri.Host, options.IncludeSubdomains)) {
            return false;
        }

        string normalized = NormalizeUrl(assetUri, options);
        if (options.AssetIncludePatterns.Count > 0 && !MatchesAny(normalized, options.AssetIncludePatterns)) {
            return false;
        }

        if (options.AssetExcludePatterns.Count > 0 && MatchesAny(normalized, options.AssetExcludePatterns)) {
            return false;
        }

        return true;
    }

}
