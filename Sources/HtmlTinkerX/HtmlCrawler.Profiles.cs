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
    private static ProfileSelectionDecision ResolveInitialProfileDecision(
        string? profileName,
        Uri startUri,
        bool autoProfile,
        IReadOnlyList<HtmlCrawlProfile> customProfiles) {
        if (!string.IsNullOrWhiteSpace(profileName)) {
            HtmlCrawlProfile? explicitProfile = HtmlCrawlProfiles.ResolveByName(profileName, customProfiles);
            return new ProfileSelectionDecision {
                Profile = explicitProfile,
                ReasonCode = explicitProfile == null ? HtmlCrawlProfileSelectionReasonCode.None : HtmlCrawlProfileSelectionReasonCode.ExplicitProfileName,
                Reason = explicitProfile == null ? null : $"Applied profile '{explicitProfile.Name}' because it was explicitly requested."
            };
        }

        if (autoProfile) {
            HtmlCrawlProfile? hostMatchedProfile = HtmlCrawlProfiles.Resolve(null, startUri, autoProfile: true, customProfiles);
            if (hostMatchedProfile != null) {
                return new ProfileSelectionDecision {
                    Profile = hostMatchedProfile,
                    ReasonCode = HtmlCrawlProfileSelectionReasonCode.AutoProfileHostMatch,
                    Reason = $"Auto-profile matched '{hostMatchedProfile.Name}' from the starting host '{startUri.Host}'."
                };
            }
        }

        return new ProfileSelectionDecision();
    }

    private static void ApplyRunMetadata(HtmlCrawlPage page, HtmlCrawlResult result) {
        page.AppliedScenario = result.AppliedScenario;
        page.AppliedProfileName = result.AppliedProfileName;
        page.AppliedProfileReasonCode = result.AppliedProfileReasonCode;
        page.AppliedProfileReason = result.AppliedProfileReason;
    }

    private static ProfileSelectionDecision InferAutoProfile(Uri startUri, string? html, HtmlCrawlPage page, IReadOnlyList<HtmlCrawlProfile> customProfiles) {
        if (LooksLikeWordPressSite(html, page)) {
            HtmlCrawlProfile? wordpressProfile = HtmlCrawlProfiles.ResolveByName("wordpress-content", customProfiles);
            if (wordpressProfile != null) {
                return new ProfileSelectionDecision {
                    Profile = wordpressProfile,
                    ReasonCode = HtmlCrawlProfileSelectionReasonCode.AutoProfileWordPressMarkers,
                    Reason = "Auto-profile inferred WordPress content markers from the fetched page."
                };
            }
        }

        if (LooksLikeApiDocumentationSite(html, page)) {
            HtmlCrawlProfile? apiDocsProfile = HtmlCrawlProfiles.ResolveByName("api-docs-content", customProfiles);
            if (apiDocsProfile != null) {
                return new ProfileSelectionDecision {
                    Profile = apiDocsProfile,
                    ReasonCode = HtmlCrawlProfileSelectionReasonCode.AutoProfileApiDocumentationMarkers,
                    Reason = "Auto-profile inferred API documentation markers such as Swagger/ReDoc or OpenAPI links."
                };
            }
        }

        if (LooksLikeDocumentationSite(html, page)) {
            HtmlCrawlProfile? docsProfile = HtmlCrawlProfiles.ResolveByName("docs-content", customProfiles);
            if (docsProfile != null) {
                return new ProfileSelectionDecision {
                    Profile = docsProfile,
                    ReasonCode = HtmlCrawlProfileSelectionReasonCode.AutoProfileDocumentationMarkers,
                    Reason = "Auto-profile inferred documentation markers such as TOC/sidebar chrome around article content."
                };
            }
        }

        return ResolveInitialProfileDecision(null, startUri, autoProfile: true, customProfiles);
    }

    private static bool LooksLikeWordPressSite(string? html, HtmlCrawlPage page) {
        if (string.IsNullOrWhiteSpace(html)) {
            return false;
        }

        string normalizedHtml = html!.ToLowerInvariant();
        if (normalizedHtml.Contains("content=\"wordpress", StringComparison.Ordinal)
            || normalizedHtml.Contains("content='wordpress", StringComparison.Ordinal)
            || normalizedHtml.Contains("/wp-content/", StringComparison.Ordinal)
            || normalizedHtml.Contains("/wp-includes/", StringComparison.Ordinal)
            || normalizedHtml.Contains("wp-block-", StringComparison.Ordinal)
            || normalizedHtml.Contains("wpml-ls", StringComparison.Ordinal)
            || normalizedHtml.Contains("class=\"site-main", StringComparison.Ordinal)
            || normalizedHtml.Contains("class='site-main", StringComparison.Ordinal)) {
            return true;
        }

        if (page.AssetUrls.Any(url => url.Contains("/wp-content/", StringComparison.OrdinalIgnoreCase) || url.Contains("/wp-includes/", StringComparison.OrdinalIgnoreCase))) {
            return true;
        }

        if (page.Links.Any(url => url.Contains("/wp-json/", StringComparison.OrdinalIgnoreCase))) {
            return true;
        }

        return false;
    }

    private static bool LooksLikeApiDocumentationSite(string? html, HtmlCrawlPage page) {
        if (string.IsNullOrWhiteSpace(html)) {
            return false;
        }

        string normalizedHtml = html!.ToLowerInvariant();
        bool hasApiUiMarkers = normalizedHtml.Contains("swagger-ui", StringComparison.Ordinal)
            || normalizedHtml.Contains("redoc-wrap", StringComparison.Ordinal)
            || normalizedHtml.Contains("<rapi-doc", StringComparison.Ordinal)
            || normalizedHtml.Contains("data-spec-url", StringComparison.Ordinal)
            || normalizedHtml.Contains("scalar-api-reference", StringComparison.Ordinal)
            || normalizedHtml.Contains("try it out", StringComparison.Ordinal)
            || normalizedHtml.Contains("openapi", StringComparison.Ordinal);
        bool hasApiSpecLinks = page.Links.Any(url => url.Contains("swagger.json", StringComparison.OrdinalIgnoreCase)
            || url.Contains("openapi.json", StringComparison.OrdinalIgnoreCase)
            || url.Contains("openapi.yaml", StringComparison.OrdinalIgnoreCase)
            || url.Contains("openapi.yml", StringComparison.OrdinalIgnoreCase))
            || page.AssetUrls.Any(url => url.Contains("swagger.json", StringComparison.OrdinalIgnoreCase)
                || url.Contains("openapi.json", StringComparison.OrdinalIgnoreCase)
                || url.Contains("openapi.yaml", StringComparison.OrdinalIgnoreCase)
                || url.Contains("openapi.yml", StringComparison.OrdinalIgnoreCase));

        return hasApiUiMarkers || hasApiSpecLinks;
    }

    private static bool LooksLikeDocumentationSite(string? html, HtmlCrawlPage page) {
        if (string.IsNullOrWhiteSpace(html)) {
            return false;
        }

        string normalizedHtml = html!.ToLowerInvariant();
        bool hasArticle = normalizedHtml.Contains("<article", StringComparison.Ordinal)
            || normalizedHtml.Contains("role=\"main\"", StringComparison.Ordinal)
            || normalizedHtml.Contains("role='main'", StringComparison.Ordinal);
        bool hasDocsChrome = normalizedHtml.Contains("table of contents", StringComparison.Ordinal)
            || normalizedHtml.Contains("on this page", StringComparison.Ordinal)
            || normalizedHtml.Contains("edit this page", StringComparison.Ordinal)
            || normalizedHtml.Contains("theme-doc-", StringComparison.Ordinal)
            || normalizedHtml.Contains("docs-sidebar", StringComparison.Ordinal)
            || normalizedHtml.Contains("docs-nav", StringComparison.Ordinal)
            || normalizedHtml.Contains("class=\"sidebar", StringComparison.Ordinal)
            || normalizedHtml.Contains("class='sidebar", StringComparison.Ordinal)
            || normalizedHtml.Contains("class=\"toc", StringComparison.Ordinal)
            || normalizedHtml.Contains("class='toc", StringComparison.Ordinal)
            || normalizedHtml.Contains("class=\"table-of-contents", StringComparison.Ordinal)
            || normalizedHtml.Contains("class='table-of-contents", StringComparison.Ordinal);
        bool hasDocsLinks = page.Links.Any(url => url.Contains("/docs/", StringComparison.OrdinalIgnoreCase)
            || url.Contains("/documentation/", StringComparison.OrdinalIgnoreCase)
            || url.Contains("#", StringComparison.Ordinal));

        return hasArticle && (hasDocsChrome || hasDocsLinks);
    }

    private static async Task AutoScrollPageAsync(IPage page, HtmlCrawlOptions options, CancellationToken cancellationToken) {
        for (int i = 0; i < options.AutoScrollSteps; i++) {
            cancellationToken.ThrowIfCancellationRequested();
            await page.EvaluateAsync("() => window.scrollTo(0, document.body.scrollHeight)").ConfigureAwait(false);
            if (options.AutoScrollDelayMs > 0) {
                await page.WaitForTimeoutAsync(options.AutoScrollDelayMs).ConfigureAwait(false);
            }
        }

        await page.EvaluateAsync("() => window.scrollTo(0, 0)").ConfigureAwait(false);
    }

    internal static AutoRenderDecision EvaluateAutoRender(HtmlCrawlPage page, HtmlCrawlOptions options) {
        if (page == null) {
            throw new ArgumentNullException(nameof(page));
        }
        if (options == null) {
            throw new ArgumentNullException(nameof(options));
        }
        if (page.Status != HtmlCrawlPageStatus.Success) {
            return new AutoRenderDecision {
                ShouldRender = false,
                ReasonCode = HtmlCrawlRenderReasonCode.StaticStatusNotSuccess,
                Reason = $"Kept static because page status was {page.Status}."
            };
        }

        bool selectorMissed = !string.IsNullOrWhiteSpace(options.Selector)
                              && string.IsNullOrWhiteSpace(page.Html)
                              && string.IsNullOrWhiteSpace(page.Text);
        if (selectorMissed) {
            return new AutoRenderDecision {
                ShouldRender = true,
                ReasonCode = HtmlCrawlRenderReasonCode.AutoRenderSelectorMiss,
                Reason = $"Auto-render triggered because selector '{options.Selector}' produced no stored HTML or text in static mode."
            };
        }

        int wordCount = CountWords(page.Text);
        if (!string.IsNullOrWhiteSpace(options.WaitForSelector) && wordCount < options.AutoRenderTextWordThreshold) {
            return new AutoRenderDecision {
                ShouldRender = true,
                ReasonCode = HtmlCrawlRenderReasonCode.AutoRenderWaitForSelectorThin,
                Reason = $"Auto-render triggered because WaitForSelector '{options.WaitForSelector}' was configured and static text stayed below {options.AutoRenderTextWordThreshold} words."
            };
        }

        if (wordCount >= options.AutoRenderTextWordThreshold) {
            return new AutoRenderDecision {
                ShouldRender = false,
                ReasonCode = HtmlCrawlRenderReasonCode.StaticThresholdMet,
                Reason = $"Kept static because extracted text reached {wordCount} words, meeting the {options.AutoRenderTextWordThreshold}-word threshold."
            };
        }

        string html = page.Html ?? string.Empty;
        if (string.IsNullOrWhiteSpace(html)) {
            return new AutoRenderDecision {
                ShouldRender = true,
                ReasonCode = HtmlCrawlRenderReasonCode.AutoRenderNoHtml,
                Reason = "Auto-render triggered because static extraction produced no stored HTML."
            };
        }

        string normalizedHtml = html.ToLowerInvariant();
        bool hasShellMarker =
            Regex.IsMatch(normalizedHtml, "id\\s*=\\s*[\"']__next[\"']", RegexOptions.CultureInvariant)
            || Regex.IsMatch(normalizedHtml, "id\\s*=\\s*[\"']app[\"']", RegexOptions.CultureInvariant)
            || Regex.IsMatch(normalizedHtml, "id\\s*=\\s*[\"']root[\"']", RegexOptions.CultureInvariant)
            || normalizedHtml.Contains("data-reactroot", StringComparison.Ordinal)
            || normalizedHtml.Contains("ng-version", StringComparison.Ordinal)
            || normalizedHtml.Contains("window.__next_data__", StringComparison.Ordinal)
            || Regex.IsMatch(normalizedHtml, "type\\s*=\\s*[\"']module[\"']", RegexOptions.CultureInvariant);
        int scriptCount = Regex.Matches(html, "<script\\b", RegexOptions.IgnoreCase).Count;
        int headingCount = Regex.Matches(html, "<h[1-6]\\b", RegexOptions.IgnoreCase).Count;

        if (hasShellMarker) {
            return new AutoRenderDecision {
                ShouldRender = true,
                ReasonCode = HtmlCrawlRenderReasonCode.AutoRenderJavaScriptShell,
                Reason = "Auto-render triggered because the static HTML looked like a JavaScript shell container."
            };
        }

        if (scriptCount >= 6) {
            return new AutoRenderDecision {
                ShouldRender = true,
                ReasonCode = HtmlCrawlRenderReasonCode.AutoRenderManyScripts,
                Reason = $"Auto-render triggered because the static page contained {scriptCount} script tags but only {wordCount} extracted words."
            };
        }

        if (headingCount == 0) {
            return new AutoRenderDecision {
                ShouldRender = true,
                ReasonCode = HtmlCrawlRenderReasonCode.AutoRenderNoHeadings,
                Reason = "Auto-render triggered because the static page had no headings and very little extracted text."
            };
        }

        return new AutoRenderDecision {
            ShouldRender = false,
            ReasonCode = HtmlCrawlRenderReasonCode.StaticHeuristicsNotTriggered,
            Reason = $"Kept static because auto-render heuristics did not trigger, even though extracted text stayed at {wordCount} words."
        };
    }

    internal static bool ShouldRetryWithRendering(HtmlCrawlPage page, HtmlCrawlOptions options) {
        return EvaluateAutoRender(page, options).ShouldRender;
    }


    private static bool IsAllowedPageContent(string? contentType, string content, HtmlCrawlOptions options) {
        if (!options.RestrictToAllowedContentTypes) {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(contentType) && MatchesParameterPattern(contentType!.Trim(), options.AllowedContentTypePatterns)) {
            return true;
        }

        return LooksLikeHtml(content);
    }

    private static string? TryGetResponseContentType(IResponse? response) {
        if (response?.Headers == null) {
            return null;
        }

        if (response.Headers.TryGetValue("content-type", out string? value) && !string.IsNullOrWhiteSpace(value)) {
            int separatorIndex = value.IndexOf(';');
            return separatorIndex >= 0 ? value.Substring(0, separatorIndex).Trim() : value.Trim();
        }

        return null;
    }

    private static bool LooksLikeHtml(string? content) {
        if (string.IsNullOrWhiteSpace(content)) {
            return false;
        }

        string sample = content!.TrimStart();
        return sample.StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase)
               || sample.StartsWith("<html", StringComparison.OrdinalIgnoreCase)
               || sample.StartsWith("<head", StringComparison.OrdinalIgnoreCase)
               || sample.StartsWith("<body", StringComparison.OrdinalIgnoreCase)
               || sample.StartsWith("<main", StringComparison.OrdinalIgnoreCase)
               || sample.StartsWith("<article", StringComparison.OrdinalIgnoreCase)
               || sample.StartsWith("<section", StringComparison.OrdinalIgnoreCase)
               || sample.StartsWith("<div", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TrySkipDuplicateContent(
        HtmlCrawlPage page,
        HtmlCrawlOptions options,
        IDictionary<string, string> contentFingerprints,
        out HtmlCrawlPage? duplicatePage) {
        duplicatePage = null;
        if (!options.DeduplicatePages || page.Status != HtmlCrawlPageStatus.Success) {
            return false;
        }

        string fingerprintSource = !string.IsNullOrWhiteSpace(page.Text)
            ? page.Text
            : page.Html;
        if (string.IsNullOrWhiteSpace(fingerprintSource)) {
            return false;
        }

        string fingerprint = ComputeContentFingerprint(fingerprintSource);
        page.ContentFingerprint = fingerprint;
        if (!contentFingerprints.TryGetValue(fingerprint, out string? originalUrl)) {
            contentFingerprints[fingerprint] = page.Url;
            return false;
        }

        duplicatePage = new HtmlCrawlPage {
            Url = page.Url,
            RequestedUrl = page.RequestedUrl,
            ParentUrl = page.ParentUrl,
            CanonicalUrl = page.CanonicalUrl,
            ContentFingerprint = fingerprint,
            DuplicateOfUrl = originalUrl,
            Status = HtmlCrawlPageStatus.Skipped,
            SkipReason = HtmlCrawlSkipReason.DuplicateContent,
            Depth = page.Depth,
            StatusCode = page.StatusCode,
            Title = page.Title,
            Error = $"Duplicate of {originalUrl}",
            Rendered = page.Rendered,
            RenderMode = page.RenderMode,
            RenderReasonCode = page.RenderReasonCode,
            RenderReason = page.RenderReason,
            AppliedScenario = page.AppliedScenario,
            AppliedProfileName = page.AppliedProfileName,
            AppliedProfileReasonCode = page.AppliedProfileReasonCode,
            AppliedProfileReason = page.AppliedProfileReason,
            ContentModeUsed = page.ContentModeUsed,
            ContentSelectionReasonCode = page.ContentSelectionReasonCode,
            ContentSelectionReason = page.ContentSelectionReason,
            ContentElementTag = page.ContentElementTag,
            ContentElementId = page.ContentElementId,
            ContentElementClasses = page.ContentElementClasses.ToList(),
            ContentElementSelectorHint = page.ContentElementSelectorHint,
            ContentSelectionScore = page.ContentSelectionScore,
            ReaderCandidateCount = page.ReaderCandidateCount,
            ReaderRootElementSelectorHint = page.ReaderRootElementSelectorHint,
            ContentComparisons = CloneContentComparisons(page.ContentComparisons),
            BestContentComparisonMode = page.BestContentComparisonMode,
            BestContentComparisonReasonCode = page.BestContentComparisonReasonCode,
            BestContentComparisonWordCount = page.BestContentComparisonWordCount,
            RunnerUpContentComparisonMode = page.RunnerUpContentComparisonMode,
            BestContentComparisonWordDelta = page.BestContentComparisonWordDelta,
            ContentComparisonDeltaSummary = page.ContentComparisonDeltaSummary,
            ContentComparisonPreviewSummary = page.ContentComparisonPreviewSummary,
            Started = page.Started,
            Finished = page.Finished
        };
        return true;
    }

    private static string ComputeContentFingerprint(string content) {
        string normalized = content.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        byte[] bytes = Encoding.UTF8.GetBytes(normalized);
        using SHA256 sha = SHA256.Create();
        byte[] hashBytes = sha.ComputeHash(bytes);
        return BitConverter.ToString(hashBytes).Replace("-", string.Empty);
    }

    private static string DecodeResponse(byte[] bytes, string? charset) {
        if (!string.IsNullOrEmpty(charset)) {
            try {
                string normalizedCharset = charset!.Trim().Trim('"').Trim('\'');
                return Encoding.GetEncoding(normalizedCharset).GetString(bytes);
            } catch {
            }
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        string asciiContent = Encoding.ASCII.GetString(bytes);
        Match metaMatch = Regex.Match(
            asciiContent,
            @"<meta[^>]+charset\s*=\s*[""']?(?<charset>[^""'>\s]+)",
            RegexOptions.IgnoreCase);

        if (metaMatch.Success) {
            try {
                return Encoding.GetEncoding(metaMatch.Groups["charset"].Value).GetString(bytes);
            } catch {
            }
        }

        return Encoding.UTF8.GetString(bytes);
    }

}
