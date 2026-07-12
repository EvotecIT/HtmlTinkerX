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
    private static async Task<FetchedPageData> FetchHttpPageAsync(HttpClient client, CrawlRequest request, HtmlCrawlOptions options, IReadOnlyDictionary<string, HtmlCrawlJsonSchemaField> structuredSchema, CancellationToken cancellationToken) {
        HtmlCrawlPage page = new() {
            Url = NormalizeUrl(request.Uri, options),
            RequestedUrl = request.Uri.AbsoluteUri,
            ParentUrl = request.ParentUrl,
            Depth = request.Depth,
            Rendered = false,
            RenderMode = HtmlCrawlRenderMode.Static,
            Status = HtmlCrawlPageStatus.Success,
            Started = DateTimeOffset.UtcNow
        };

        try {
            using HttpResponseMessage response = await client.GetAsync(request.Uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            page.StatusCode = (int)response.StatusCode;
            page.ContentType = response.Content.Headers.ContentType?.MediaType ?? response.Content.Headers.ContentType?.ToString();
            response.EnsureSuccessStatusCode();

            byte[] bytes = await HtmlUtilities.ReadResponseBytesAsync(response, options.MaximumPageResponseBytes, cancellationToken).ConfigureAwait(false);
            string html = DecodeResponse(bytes, response.Content.Headers.ContentType?.CharSet);
            if (!IsAllowedPageContent(page.ContentType, html, options)) {
                page.Status = HtmlCrawlPageStatus.Skipped;
                page.SkipReason = HtmlCrawlSkipReason.UnsupportedContentType;
                page.Error = $"Skipped content type '{page.ContentType ?? "unknown"}'.";
                return new FetchedPageData {
                    Page = page,
                    RawHtml = html
                };
            }

            PopulatePageFromHtml(page, html, request.Uri, options, structuredSchema);
            return new FetchedPageData {
                Page = page,
                RawHtml = html
            };
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception ex) {
            page.Status = HtmlCrawlPageStatus.Failed;
            page.Error = ex.Message;
        } finally {
            page.Finished = DateTimeOffset.UtcNow;
        }

        return new FetchedPageData {
            Page = page
        };
    }

    private static async Task<FetchedPageData> FetchRenderedPageAsync(HtmlBrowserSession session, CrawlRequest request, HtmlCrawlOptions options, IReadOnlyDictionary<string, HtmlCrawlJsonSchemaField> structuredSchema, CancellationToken cancellationToken) {
        HtmlCrawlPage page = new() {
            Url = NormalizeUrl(request.Uri, options),
            RequestedUrl = request.Uri.AbsoluteUri,
            ParentUrl = request.ParentUrl,
            Depth = request.Depth,
            Rendered = true,
            RenderMode = options.Render ? HtmlCrawlRenderMode.Rendered : HtmlCrawlRenderMode.AutoRendered,
            Status = HtmlCrawlPageStatus.Success,
            Started = DateTimeOffset.UtcNow
        };

        try {
            cancellationToken.ThrowIfCancellationRequested();
            int networkLogStart = session.NetworkLog.Count();
            IResponse? response = await session.Page.GotoAsync(request.Uri.AbsoluteUri, new PageGotoOptions {
                Timeout = options.Timeout,
                WaitUntil = WaitUntilState.NetworkIdle
            }).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(options.WaitForSelector)) {
                await session.Page.WaitForSelectorAsync(options.WaitForSelector!, new PageWaitForSelectorOptions {
                    Timeout = options.Timeout
                }).ConfigureAwait(false);
            }

            if (options.WaitAfterLoadMs > 0) {
                await session.Page.WaitForTimeoutAsync(options.WaitAfterLoadMs).ConfigureAwait(false);
            }

            await ApplyRenderedInteractionsAsync(session.Page, page, options, cancellationToken).ConfigureAwait(false);

            if (options.AutoScroll) {
                await AutoScrollPageAsync(session.Page, options, cancellationToken).ConfigureAwait(false);
            }

            if (options.HiddenContentMode == HtmlCrawlHiddenContentMode.RespectHidden) {
                await MarkRenderedHiddenElementsAsync(session.Page).ConfigureAwait(false);
            }

            string fullHtml = await session.Page.ContentAsync().ConfigureAwait(false);
            page.StatusCode = response?.Status;
            page.ContentType = TryGetResponseContentType(response);

            if (!IsAllowedPageContent(page.ContentType, fullHtml, options)) {
                page.Status = HtmlCrawlPageStatus.Skipped;
                page.SkipReason = HtmlCrawlSkipReason.UnsupportedContentType;
                page.Error = $"Skipped content type '{page.ContentType ?? "unknown"}'.";
                return new FetchedPageData {
                    Page = page,
                    RawHtml = fullHtml
                };
            }

            string? title = await session.Page.TitleAsync().ConfigureAwait(false);
            PopulatePageFromHtml(page, fullHtml, request.Uri, options, structuredSchema, title);
            Uri runtimeDiagnosticsUri = TryGetAbsoluteUri(session.Page.Url, out Uri? renderedUri) ? renderedUri! : request.Uri;
            MergeOfflineDependencyDiagnostics(page.OfflineDependencyDiagnostics, DetectRenderedNetworkDependencyDiagnostics(session.NetworkLog.Skip(networkLogStart), runtimeDiagnosticsUri));
            return new FetchedPageData {
                Page = page,
                RawHtml = fullHtml
            };
        } catch (Exception ex) {
            page.Status = HtmlCrawlPageStatus.Failed;
            page.Error = ex.Message;
        } finally {
            page.Finished = DateTimeOffset.UtcNow;
        }

        return new FetchedPageData {
            Page = page
        };
    }

    private static async Task ApplyRenderedInteractionsAsync(IPage page, HtmlCrawlPage crawlPage, HtmlCrawlOptions options, CancellationToken cancellationToken) {
        foreach (string text in options.DismissTexts.Where(text => !string.IsNullOrWhiteSpace(text)).Select(text => text.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)) {
            cancellationToken.ThrowIfCancellationRequested();
            if (await TryClickRenderedTextAsync(page, text, options, cancellationToken).ConfigureAwait(false)) {
                crawlPage.AppliedInteractions.Add($"Dismissed text: {text}");
            }
        }

        foreach (string selector in options.DismissSelectors.Where(selector => !string.IsNullOrWhiteSpace(selector)).Select(selector => selector.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)) {
            cancellationToken.ThrowIfCancellationRequested();
            if (await TryClickRenderedSelectorAsync(page, selector, options, cancellationToken).ConfigureAwait(false)) {
                crawlPage.AppliedInteractions.Add($"Dismissed: {selector}");
            }
        }

        for (int i = 0; i < options.InteractionRepeatCount; i++) {
            foreach (string text in options.ClickTexts.Where(text => !string.IsNullOrWhiteSpace(text)).Select(text => text.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)) {
                cancellationToken.ThrowIfCancellationRequested();
                if (await TryClickRenderedTextAsync(page, text, options, cancellationToken).ConfigureAwait(false)) {
                    crawlPage.AppliedInteractions.Add(options.InteractionRepeatCount > 1
                        ? $"Clicked text [{i + 1}]: {text}"
                        : $"Clicked text: {text}");
                }
            }

            foreach (string selector in options.ClickSelectors.Where(selector => !string.IsNullOrWhiteSpace(selector)).Select(selector => selector.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)) {
                cancellationToken.ThrowIfCancellationRequested();
                if (await TryClickRenderedSelectorAsync(page, selector, options, cancellationToken).ConfigureAwait(false)) {
                    crawlPage.AppliedInteractions.Add(options.InteractionRepeatCount > 1
                        ? $"Clicked [{i + 1}]: {selector}"
                        : $"Clicked: {selector}");
                }
            }
        }
    }

    private static async Task<bool> TryClickRenderedSelectorAsync(IPage page, string selector, HtmlCrawlOptions options, CancellationToken cancellationToken) {
        try {
            ILocator locator = page.Locator(selector).First;
            if (await locator.CountAsync().ConfigureAwait(false) == 0) {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await locator.ClickAsync(new LocatorClickOptions {
                Timeout = Math.Min(options.Timeout, 3000)
            }).ConfigureAwait(false);
            if (options.InteractionDelayMs > 0) {
                await page.WaitForTimeoutAsync(options.InteractionDelayMs).ConfigureAwait(false);
            }
            return true;
        } catch {
            return false;
        }
    }

    private static async Task<bool> TryClickRenderedTextAsync(IPage page, string text, HtmlCrawlOptions options, CancellationToken cancellationToken) {
        try {
            ILocator locator = page.GetByText(text).First;
            if (await locator.CountAsync().ConfigureAwait(false) == 0) {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await locator.ClickAsync(new LocatorClickOptions {
                Timeout = Math.Min(options.Timeout, 3000)
            }).ConfigureAwait(false);
            if (options.InteractionDelayMs > 0) {
                await page.WaitForTimeoutAsync(options.InteractionDelayMs).ConfigureAwait(false);
            }
            return true;
        } catch {
            return false;
        }
    }

    private static void PopulatePageFromHtml(HtmlCrawlPage page, string html, Uri requestUri, HtmlCrawlOptions options, IReadOnlyDictionary<string, HtmlCrawlJsonSchemaField> structuredSchema, string? titleOverride = null) {
        page.Title = string.IsNullOrWhiteSpace(titleOverride) ? ExtractTitle(html) : titleOverride;
        page.CanonicalUrl = ExtractCanonicalUrl(html, requestUri, options);
        page.Links = ExtractLinks(html, requestUri, options);
        page.AssetUrls = ExtractAssetUrls(html, requestUri, options);
        page.OfflineDependencyDiagnostics = DetectOfflineDependencyDiagnostics(html);
        ContentSelectionResult contentSelection = SelectContent(html, options);
        string selectedHtml = ApplyContentCleanup(contentSelection.Html, options);
        page.ContentModeUsed = contentSelection.ModeUsed;
        page.ContentSelectionReasonCode = contentSelection.ReasonCode;
        page.ContentSelectionReason = contentSelection.Reason;
        page.ContentElementTag = contentSelection.Element?.LocalName;
        page.ContentElementId = contentSelection.Element?.Id;
        page.ContentElementClasses = GetElementClassNames(contentSelection.Element);
        page.ContentElementSelectorHint = BuildElementSelectorHint(contentSelection.Element);
        page.ContentSelectionScore = contentSelection.Score;
        page.ReaderCandidateCount = contentSelection.ReaderCandidateCount;
        page.ReaderRootElementSelectorHint = contentSelection.ReaderRootElementSelectorHint;
        page.ContentComparisons = options.CompareContentModes
            ? BuildContentComparisons(html, options)
            : new List<HtmlCrawlContentComparison>();
        HtmlCrawlContentComparison? bestComparison = GetBestContentComparison(page.ContentComparisons);
        HtmlCrawlContentComparison? runnerUpComparison = GetRunnerUpContentComparison(page.ContentComparisons, bestComparison);
        page.BestContentComparisonMode = bestComparison?.Mode;
        page.BestContentComparisonReasonCode = bestComparison?.ReasonCode;
        page.BestContentComparisonWordCount = bestComparison?.WordCount;
        page.RunnerUpContentComparisonMode = runnerUpComparison?.Mode;
        page.BestContentComparisonWordDelta = bestComparison != null && runnerUpComparison != null
            ? bestComparison.WordCount - runnerUpComparison.WordCount
            : null;
        page.ContentComparisonDeltaSummary = BuildContentComparisonDeltaSummary(page.ContentComparisons, bestComparison);
        page.ContentComparisonPreviewSummary = BuildContentComparisonPreviewSummary(page.ContentComparisons, bestComparison);
        string markdownBaseUrl = ResolveMarkdownBaseUrl(html, requestUri);
        string selectedText = HtmlParserToText.ConvertToText(PrepareHtmlForTextExtraction(selectedHtml, options));
        string selectedMarkdown = options.IncludeMarkdown || options.IncludeStructuredJson
            ? ConvertSelectedHtmlToMarkdown(selectedHtml, markdownBaseUrl, options)
            : string.Empty;
        page.Html = options.IncludeHtml ? selectedHtml : string.Empty;
        page.Text = options.IncludeText ? selectedText : string.Empty;
        page.Markdown = options.IncludeMarkdown ? selectedMarkdown : string.Empty;
        page.StructuredJson = options.IncludeStructuredJson ? BuildStructuredJson(page, html, selectedHtml, selectedText, selectedMarkdown, structuredSchema, options.StructuredJsonPreset) : null;
    }

    private static string ResolveMarkdownBaseUrl(string html, Uri requestUri) {
        if (string.IsNullOrWhiteSpace(html)) {
            return requestUri.AbsoluteUri;
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        return GetDocumentBaseUri(document, requestUri).AbsoluteUri;
    }

    private static string ConvertSelectedHtmlToMarkdown(string html, string? pageUrl, HtmlCrawlOptions? options = null) {
        return HtmlMarkdownConverterAdapter.ConvertToMarkdown(
            html,
            pageUrl,
            options?.MarkdownImageMode ?? MarkdownImageRenderingMode.PortableMarkdown,
            options?.ListingCardMetadataMode ?? HtmlListingCardMetadataMode.SuppressInRepeatedCards,
            options?.MarkdownProfile ?? HtmlMarkdownProfile.Portable);
    }

    private static IList<HtmlCrawlOfflineDependencyDiagnostic> DetectOfflineDependencyDiagnostics(string html) {
        if (string.IsNullOrWhiteSpace(html)) {
            return new List<HtmlCrawlOfflineDependencyDiagnostic>();
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        List<string> scriptBodies = document.QuerySelectorAll("script")
            .Where(script => string.IsNullOrWhiteSpace(script.GetAttribute("src")))
            .Select(script => script.TextContent ?? string.Empty)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();
        List<string> handlerBodies = document.QuerySelectorAll("*")
            .SelectMany(element => element.Attributes
                .Where(attribute => attribute != null
                    && attribute.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(attribute.Value))
                .Select(attribute => attribute.Value))
            .ToList();
        List<string> sources = new(scriptBodies.Count + handlerBodies.Count);
        sources.AddRange(scriptBodies);
        sources.AddRange(handlerBodies);
        if (sources.Count == 0) {
            return new List<HtmlCrawlOfflineDependencyDiagnostic>();
        }

        List<HtmlCrawlOfflineDependencyDiagnostic> diagnostics = new();
        AddOfflineDependencyDiagnosticIfMatched(diagnostics, sources, "fetch-api", "Inline JavaScript calls fetch(), so the saved page may still require live API responses.", @"(?<![\w$])fetch\s*\(");
        AddOfflineDependencyDiagnosticIfMatched(diagnostics, sources, "xml-http-request", "Inline JavaScript uses XMLHttpRequest, so the saved page may still request live network data.", @"\bXMLHttpRequest\b");
        AddOfflineDependencyDiagnosticIfMatched(diagnostics, sources, "axios", "Inline JavaScript references axios, which often means the saved page still depends on live HTTP requests.", @"(?<![\w$])axios(?:\s*\(|\s*\.)");
        AddOfflineDependencyDiagnosticIfMatched(diagnostics, sources, "websocket", "Inline JavaScript opens a WebSocket connection, so the saved page may still require a live server session.", @"\bWebSocket\s*\(");
        AddOfflineDependencyDiagnosticIfMatched(diagnostics, sources, "event-source", "Inline JavaScript opens an EventSource stream, so the saved page may still require live server-sent events.", @"\bEventSource\s*\(");
        AddOfflineDependencyDiagnosticIfMatched(diagnostics, sources, "service-worker", "Inline JavaScript references navigator.serviceWorker, which can keep runtime behavior tied to a live browsing context.", @"navigator\s*\.\s*serviceWorker");
        AddOfflineDependencyDiagnosticIfMatched(diagnostics, sources, "dynamic-import", "Inline JavaScript uses dynamic import(), which can keep additional modules or routes loaded at runtime.", @"(?<![\w$])import\s*\(");
        return diagnostics;
    }

    internal static IList<HtmlCrawlOfflineDependencyDiagnostic> DetectRenderedNetworkDependencyDiagnostics(IEnumerable<HtmlNetworkEntry>? entries, Uri pageUri) {
        List<HtmlCrawlOfflineDependencyDiagnostic> diagnostics = new();
        if (entries == null) {
            return diagnostics;
        }

        bool observedCrossOriginRuntime = false;
        foreach (HtmlNetworkEntry entry in entries.Where(static entry => entry != null && !string.IsNullOrWhiteSpace(entry.Url))) {
            string? kind = entry.ResourceType switch {
                HtmlNetworkResourceType.Fetch => "observed-fetch-api",
                HtmlNetworkResourceType.XHR => "observed-xml-http-request",
                HtmlNetworkResourceType.WebSocket => "observed-websocket",
                HtmlNetworkResourceType.EventSource => "observed-event-source",
                _ => null
            };
            if (kind == null) {
                continue;
            }

            string summary = entry.ResourceType switch {
                HtmlNetworkResourceType.Fetch => "Rendered browsing observed a fetch() request, so the saved page may still depend on live API responses.",
                HtmlNetworkResourceType.XHR => "Rendered browsing observed an XMLHttpRequest call, so the saved page may still request live network data.",
                HtmlNetworkResourceType.WebSocket => "Rendered browsing observed a WebSocket connection, so the saved page may still require a live server session.",
                HtmlNetworkResourceType.EventSource => "Rendered browsing observed a server-sent events stream, so the saved page may still require a live event feed.",
                _ => string.Empty
            };
            AddOfflineDependencyDiagnosticIfMissing(diagnostics, new HtmlCrawlOfflineDependencyDiagnostic {
                Kind = kind,
                Severity = GetOfflineDependencySeverity(kind),
                Summary = summary,
                Evidence = entry.Url
            });

            if (!observedCrossOriginRuntime
                && TryGetAbsoluteUri(entry.Url, out Uri? resourceUri)
                && !string.Equals(resourceUri!.Host, pageUri.Host, StringComparison.OrdinalIgnoreCase)) {
                observedCrossOriginRuntime = true;
                AddOfflineDependencyDiagnosticIfMissing(diagnostics, new HtmlCrawlOfflineDependencyDiagnostic {
                    Kind = "observed-cross-origin-runtime",
                    Severity = GetOfflineDependencySeverity("observed-cross-origin-runtime"),
                    Summary = "Rendered browsing observed runtime network traffic to another host, so the saved page may still depend on live cross-origin services.",
                    Evidence = entry.Url
                });
            }
        }

        return diagnostics;
    }

    private static void AddOfflineDependencyDiagnosticIfMatched(
        ICollection<HtmlCrawlOfflineDependencyDiagnostic> diagnostics,
        IEnumerable<string> sources,
        string kind,
        string summary,
        string pattern) {
        Regex regex = new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        foreach (string source in sources) {
            Match match = regex.Match(source);
            if (!match.Success) {
                continue;
            }

            diagnostics.Add(new HtmlCrawlOfflineDependencyDiagnostic {
                Kind = kind,
                Severity = GetOfflineDependencySeverity(kind),
                Summary = summary,
                Evidence = ExtractOfflineDependencyEvidence(source, match)
            });
            return;
        }
    }

    internal static string GetOfflineDependencySeverity(string? kind) => kind?.ToLowerInvariant() switch {
        "websocket" => "high",
        "event-source" => "high",
        "observed-websocket" => "high",
        "observed-event-source" => "high",
        "observed-cross-origin-runtime" => "high",
        _ => "warning"
    };

    private static void MergeOfflineDependencyDiagnostics(
        IList<HtmlCrawlOfflineDependencyDiagnostic> target,
        IEnumerable<HtmlCrawlOfflineDependencyDiagnostic> additions) {
        if (target == null) {
            throw new ArgumentNullException(nameof(target));
        }

        foreach (HtmlCrawlOfflineDependencyDiagnostic diagnostic in additions ?? Enumerable.Empty<HtmlCrawlOfflineDependencyDiagnostic>()) {
            AddOfflineDependencyDiagnosticIfMissing(target, diagnostic);
        }
    }

    private static void AddOfflineDependencyDiagnosticIfMissing(
        ICollection<HtmlCrawlOfflineDependencyDiagnostic> diagnostics,
        HtmlCrawlOfflineDependencyDiagnostic? diagnostic) {
        if (diagnostics == null) {
            throw new ArgumentNullException(nameof(diagnostics));
        }

        if (diagnostic == null || string.IsNullOrWhiteSpace(diagnostic.Kind)) {
            return;
        }

        if (diagnostics.Any(existing =>
                string.Equals(existing.Kind, diagnostic.Kind, StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.Evidence, diagnostic.Evidence, StringComparison.OrdinalIgnoreCase))) {
            return;
        }

        diagnostics.Add(diagnostic);
    }

    private static string ExtractOfflineDependencyEvidence(string source, Match match) {
        if (string.IsNullOrWhiteSpace(source) || match == null || !match.Success) {
            return string.Empty;
        }

        int start = Math.Max(0, match.Index - 30);
        int end = Math.Min(source.Length, match.Index + match.Length + 50);
        string snippet = source.Substring(start, end - start).Replace("\r", " ").Replace("\n", " ").Trim();
        return Regex.Replace(snippet, @"\s+", " ");
    }

    private static bool TryGetAbsoluteUri(string? value, out Uri? uri) {
        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? absoluteUri)
            && (absoluteUri.Scheme == Uri.UriSchemeHttp
                || absoluteUri.Scheme == Uri.UriSchemeHttps
                || string.Equals(absoluteUri.Scheme, "ws", StringComparison.OrdinalIgnoreCase)
                || string.Equals(absoluteUri.Scheme, "wss", StringComparison.OrdinalIgnoreCase))) {
            uri = absoluteUri;
            return true;
        }

        uri = null;
        return false;
    }

}
