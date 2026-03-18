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

/// <summary>
/// Provides a simple offline-first website crawler for .NET and PowerShell.
/// </summary>
public static class HtmlCrawler {
    internal sealed class AutoRenderDecision {
        public bool ShouldRender { get; set; }
        public string Reason { get; set; } = string.Empty;
        public HtmlCrawlRenderReasonCode ReasonCode { get; set; }
    }

    internal sealed class ProfileSelectionDecision {
        public HtmlCrawlProfile? Profile { get; set; }
        public HtmlCrawlProfileSelectionReasonCode ReasonCode { get; set; }
        public string? Reason { get; set; }
    }

    private sealed class ContentSelectionResult {
        public HtmlCrawlContentMode ModeUsed { get; set; } = HtmlCrawlContentMode.Focused;
        public HtmlCrawlContentSelectionReasonCode ReasonCode { get; set; }
        public string Reason { get; set; } = string.Empty;
        public IElement? Element { get; set; }
        public string Html { get; set; } = string.Empty;
        public double? Score { get; set; }
        public int ReaderCandidateCount { get; set; }
        public string? ReaderRootElementSelectorHint { get; set; }
    }

    private sealed class PageSearchMetadata {
        public int WordCount { get; set; }
        public int CharacterCount { get; set; }
        public int ChunkCount { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string[] Headings { get; set; } = Array.Empty<string>();
        public string[] Keywords { get; set; } = Array.Empty<string>();
    }

    private sealed class PageChunkRecord {
        public string ChunkId { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? Title { get; set; }
        public int Depth { get; set; }
        public int ChunkIndex { get; set; }
        public int WordCount { get; set; }
        public int CharacterCount { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string[] Headings { get; set; } = Array.Empty<string>();
        public string[] Keywords { get; set; } = Array.Empty<string>();
        public string Text { get; set; } = string.Empty;
        public string? HtmlPath { get; set; }
        public string? TextPath { get; set; }
        public string? ManifestPath { get; set; }
        public string OfflineReadinessGrade { get; set; } = "ready";
        public string HighestOfflineRiskSeverity { get; set; } = "none";
        public int OfflineDependencyDiagnosticCount { get; set; }
        public string OfflineDependencyKindsSummary { get; set; } = string.Empty;
        public string Fingerprint { get; set; } = string.Empty;
    }

    private sealed class GraphNodeRecord {
        public string Url { get; set; } = string.Empty;
        public string? Title { get; set; }
        public int Depth { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? SkipReason { get; set; }
        public string OfflineReadinessGrade { get; set; } = "not-assessed";
        public string HighestOfflineRiskSeverity { get; set; } = "none";
        public int OfflineDependencyDiagnosticCount { get; set; }
        public string OfflineDependencyKindsSummary { get; set; } = string.Empty;
        public int InDegree { get; set; }
        public int OutDegree { get; set; }
        public int InternalOutDegree { get; set; }
        public string? HtmlPath { get; set; }
        public string? ManifestPath { get; set; }
    }

    private sealed class GraphEdgeRecord {
        public string SourceUrl { get; set; } = string.Empty;
        public string TargetUrl { get; set; } = string.Empty;
        public bool TargetKnown { get; set; }
        public bool Internal { get; set; }
        public string Relation { get; set; } = string.Empty;
    }

    private sealed class CrawlRequest {
        public Uri Uri { get; set; } = null!;
        public string? ParentUrl { get; set; }
        public int Depth { get; set; }
    }

    private sealed class FetchedPageData {
        public HtmlCrawlPage Page { get; set; } = null!;
        public string? RawHtml { get; set; }
    }

    private sealed class CrawlArtifactPaths {
        public string ManifestPath { get; set; } = string.Empty;
        public string PagesDirectory { get; set; } = string.Empty;
        public string AssetsDirectory { get; set; } = string.Empty;
        public string PagesJsonlPath { get; set; } = string.Empty;
        public string PagesCsvPath { get; set; } = string.Empty;
        public string SkippedPagesJsonlPath { get; set; } = string.Empty;
        public string SkippedAssetsJsonlPath { get; set; } = string.Empty;
        public string LinksJsonlPath { get; set; } = string.Empty;
        public string AssetsJsonlPath { get; set; } = string.Empty;
        public string StructuredJsonPagesJsonlPath { get; set; } = string.Empty;
        public string OpenApiLikeJsonPath { get; set; } = string.Empty;
        public string OpenApiJsonPath { get; set; } = string.Empty;
        public string ChunksJsonlPath { get; set; } = string.Empty;
        public string GraphJsonPath { get; set; } = string.Empty;
        public string SummaryJsonPath { get; set; } = string.Empty;
        public string SummaryTextPath { get; set; } = string.Empty;
        public string IndexHtmlPath { get; set; } = string.Empty;
    }

    private sealed class RobotsDocument {
        public List<RobotsRule> Rules { get; } = new();
        public List<string> SitemapUrls { get; } = new();
        public int? CrawlDelayMs { get; set; }
    }

    private sealed class RobotsGroup {
        public List<string> UserAgents { get; } = new();
        public List<RobotsRule> Rules { get; } = new();
        public int? CrawlDelayMs { get; set; }
    }

    private sealed class RobotsRule {
        public bool Allow { get; set; }
        public string Path { get; set; } = string.Empty;
    }

    private static readonly HashSet<string> SearchStopWords = new(StringComparer.OrdinalIgnoreCase) {
        "a", "an", "and", "are", "as", "at", "be", "been", "but", "by", "for", "from", "had", "has", "have",
        "he", "her", "him", "his", "i", "if", "in", "into", "is", "it", "its", "me", "more", "most", "my",
        "no", "not", "of", "on", "or", "our", "ours", "she", "so", "some", "than", "that", "the", "their",
        "them", "then", "there", "these", "they", "this", "those", "to", "too", "up", "us", "was", "we",
        "were", "what", "when", "where", "which", "who", "will", "with", "you", "your"
    };

    private static readonly string[] ContentFallbackSelectors = {
        "main",
        "[role='main']",
        "#main",
        "#main-content",
        ".main-content",
        ".site-main",
        "#content",
        ".content",
        ".entry-content",
        ".post-content",
        "article"
    };

    private static readonly string[] BoilerplateSignalTokens = {
        "site-header",
        "primary-navigation",
        "nav-menu",
        "menu-item-search",
        "footer-nav",
        "footer-site-info",
        "wpml-ls",
        "sharing-popup",
        "post-footer-sharing",
        "socials-sharing",
        "gem-pagination",
        "skip-link",
        "language-switcher",
        "locale-switcher",
        "related-post",
        "related-articles",
        "related-content",
        "breadcrumbs",
        "breadcrumb",
        "comment-respond",
        "comments-area",
        "newsletter",
        "subscribe",
        "promo",
        "cookie-banner",
        "table-of-contents",
        "toc",
        "sidebar",
        "share",
        "social",
        "pagination",
        "pager"
    };

    private static readonly (string Kind, string Selector)[] StructuredRegionSelectors = {
        ("Header", "header,[role='banner'],.site-header,.page-header,#header"),
        ("Navigation", "nav,[role='navigation'],.navbar,.navigation,.site-nav,.site-navigation,.menu,#nav,#menu"),
        ("Main", "main,[role='main'],#main,#main-content,.main-content,.site-main,#content,.content"),
        ("Article", "article,[itemtype*='Article'],[itemtype*='BlogPosting']"),
        ("Aside", "aside,[role='complementary'],.sidebar,.side-nav,.toc,.table-of-contents,.on-this-page"),
        ("Footer", "footer,[role='contentinfo'],.site-footer,.page-footer,#footer")
    };

    private const int StrictOpenApiPromotionThreshold = 45;

    /// <summary>
    /// Crawls a site starting from the supplied URL.
    /// </summary>
    /// <param name="startUrl">URL to begin crawling from.</param>
    /// <param name="options">Crawler options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A crawl result containing pages and extracted content.</returns>
    public static async Task<HtmlCrawlResult> CrawlAsync(string startUrl, HtmlCrawlOptions? options = null, CancellationToken cancellationToken = default) {
        if (startUrl == null) {
            throw new ArgumentNullException(nameof(startUrl));
        }

        if (!Uri.TryCreate(startUrl, UriKind.Absolute, out Uri? startUri) ||
            (startUri.Scheme != Uri.UriSchemeHttp && startUri.Scheme != Uri.UriSchemeHttps)) {
            throw new ArgumentException("The start URL must be an absolute http or https URL.", nameof(startUrl));
        }

        HtmlCrawlOptions resolvedOptions = options?.Clone() ?? new HtmlCrawlOptions();
        HtmlCrawlScenarios.Apply(resolvedOptions, resolvedOptions.Scenario);
        IReadOnlyDictionary<string, HtmlCrawlJsonSchemaField> structuredSchema = await LoadStructuredSchemaAsync(resolvedOptions, cancellationToken).ConfigureAwait(false);
        if (structuredSchema.Count > 0 || resolvedOptions.StructuredJsonPreset != HtmlCrawlStructuredJsonPreset.None) {
            resolvedOptions.IncludeStructuredJson = true;
        }
        IReadOnlyList<HtmlCrawlProfile> customProfiles = string.IsNullOrWhiteSpace(resolvedOptions.ProfilePath)
            ? Array.Empty<HtmlCrawlProfile>()
            : await HtmlCrawlProfiles.LoadFromPathAsync(resolvedOptions.ProfilePath!, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(resolvedOptions.ProfileName) && HtmlCrawlProfiles.ResolveByName(resolvedOptions.ProfileName, customProfiles) == null) {
            string availableProfiles = string.Join(", ", HtmlCrawlProfiles.GetNames(customProfiles));
            throw new ArgumentException($"Unknown crawl profile '{resolvedOptions.ProfileName}'. Available profiles: {availableProfiles}.", nameof(HtmlCrawlOptions.ProfileName));
        }

        ProfileSelectionDecision profileDecision = ResolveInitialProfileDecision(resolvedOptions.ProfileName, startUri, resolvedOptions.AutoProfile, customProfiles);
        HtmlCrawlProfile? appliedProfile = profileDecision.Profile;
        if (appliedProfile != null) {
            HtmlCrawlProfiles.Apply(resolvedOptions, appliedProfile);
        }
        ValidateOptions(resolvedOptions);

        try {
            string persistencePath = resolvedOptions.OutputPath ?? resolvedOptions.ResumePath ?? string.Empty;
            bool persistSnapshots = !string.IsNullOrEmpty(persistencePath);
            HtmlCrawlResult result;
            if (!string.IsNullOrEmpty(resolvedOptions.ResumePath)) {
                result = await LoadResultAsync(resolvedOptions.ResumePath!, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(result.StartUrl, startUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase)) {
                    throw new InvalidOperationException($"Resume data was created for '{result.StartUrl}', but the current crawl starts from '{startUri.AbsoluteUri}'.");
                }
            } else {
                result = new HtmlCrawlResult {
                    StartUrl = startUri.AbsoluteUri,
                    AppliedScenario = resolvedOptions.Scenario,
                    AppliedProfileName = appliedProfile?.Name,
                    AppliedProfileReasonCode = profileDecision.ReasonCode,
                    AppliedProfileReason = profileDecision.Reason,
                    RenderEnabled = resolvedOptions.Render,
                    AutoRenderEnabled = resolvedOptions.AutoRender,
                    HiddenContentMode = resolvedOptions.HiddenContentMode,
                    MarkdownImageMode = resolvedOptions.MarkdownImageMode,
                    ListingCardMetadataMode = resolvedOptions.ListingCardMetadataMode,
                    Started = DateTimeOffset.UtcNow
                };
            }

            if (string.IsNullOrWhiteSpace(result.AppliedProfileName) && appliedProfile != null) {
                result.AppliedProfileName = appliedProfile.Name;
            }
            if (result.AppliedScenario == HtmlCrawlScenario.Custom && resolvedOptions.Scenario != HtmlCrawlScenario.Custom) {
                result.AppliedScenario = resolvedOptions.Scenario;
            }
            if (result.AppliedProfileReasonCode == HtmlCrawlProfileSelectionReasonCode.None && profileDecision.ReasonCode != HtmlCrawlProfileSelectionReasonCode.None) {
                result.AppliedProfileReasonCode = profileDecision.ReasonCode;
            }
            if (string.IsNullOrWhiteSpace(result.AppliedProfileReason) && !string.IsNullOrWhiteSpace(profileDecision.Reason)) {
                result.AppliedProfileReason = profileDecision.Reason;
            }
            result.RenderEnabled = resolvedOptions.Render;
            result.AutoRenderEnabled = resolvedOptions.AutoRender;
            result.HiddenContentMode = resolvedOptions.HiddenContentMode;
            result.MarkdownImageMode = resolvedOptions.MarkdownImageMode;
            result.ListingCardMetadataMode = resolvedOptions.ListingCardMetadataMode;

            Queue<CrawlRequest> pending = new();
            HashSet<string> queued = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, RobotsDocument?> robotsCache = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> contentFingerprints = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> downloadedAssets = new(StringComparer.OrdinalIgnoreCase);

            foreach (HtmlCrawlPage page in result.Pages) {
                if (!string.IsNullOrEmpty(page.Url)) {
                    visited.Add(page.Url);
                }

                if (!string.IsNullOrWhiteSpace(page.ContentFingerprint) && !string.IsNullOrWhiteSpace(page.Url) && !contentFingerprints.ContainsKey(page.ContentFingerprint!)) {
                    contentFingerprints[page.ContentFingerprint!] = page.Url;
                }
            }

            foreach (HtmlCrawlAsset asset in result.Assets) {
                if (!string.IsNullOrWhiteSpace(asset.Url)) {
                    downloadedAssets.Add(asset.Url);
                }
            }

            foreach (HtmlCrawlPage page in result.SkippedPages) {
                if (!string.IsNullOrEmpty(page.Url)) {
                    visited.Add(page.Url);
                }
            }

            foreach (HtmlCrawlPendingItem item in result.PendingPages) {
                if (TryResolveAbsoluteUri(startUri, item.Url, out Uri? pendingUri)) {
                    EnqueuePage(pendingUri!, item.ParentUrl, item.Depth, pending, queued, resolvedOptions);
                }
            }

            using HttpClient client = CreateClient(resolvedOptions);
            await using HtmlBrowserSession? session = (resolvedOptions.Render || resolvedOptions.AutoRender)
                ? await CreateRenderSessionAsync(resolvedOptions, cancellationToken).ConfigureAwait(false)
                : null;

            if (result.PageCount == 0 && result.PendingPages.Count == 0) {
                EnqueuePage(startUri, null, 0, pending, queued, resolvedOptions);
            }

            await DiscoverSitemapCandidatesAsync(startUri, client, resolvedOptions, robotsCache, result, pending, queued, visited, cancellationToken).ConfigureAwait(false);
            if (persistSnapshots) {
                await PersistSnapshotAsync(result, persistencePath, pending, cancellationToken, resolvedOptions).ConfigureAwait(false);
            }

            while (pending.Count > 0 && result.Pages.Count < resolvedOptions.MaxPages) {
                cancellationToken.ThrowIfCancellationRequested();
                CrawlRequest next = pending.Dequeue();
                string normalizedUrl = NormalizeUrl(next.Uri, resolvedOptions);
                if (!visited.Add(normalizedUrl)) {
                    continue;
                }

                if (resolvedOptions.RespectRobotsTxt) {
                    RobotsDocument? robots = await GetRobotsDocumentAsync(next.Uri, client, resolvedOptions, robotsCache, cancellationToken).ConfigureAwait(false);
                    if (robots != null && !IsAllowedByRobots(robots, next.Uri)) {
                        result.SkippedPages.Add(CreateSkippedPage(next, HtmlCrawlSkipReason.DisallowedByRobots));
                        if (persistSnapshots) {
                            await PersistSnapshotAsync(result, persistencePath, pending, cancellationToken, resolvedOptions).ConfigureAwait(false);
                        }
                        continue;
                    }
                }

                HtmlCrawlPage page;
                if (resolvedOptions.Render) {
                    FetchedPageData fetchedPage = await FetchRenderedPageAsync(session!, next, resolvedOptions, structuredSchema, cancellationToken).ConfigureAwait(false);
                    page = fetchedPage.Page;
                    page.RenderMode = HtmlCrawlRenderMode.Rendered;
                    page.RenderReasonCode = HtmlCrawlRenderReasonCode.ExplicitRender;
                    page.RenderReason = "Rendered because browser mode was explicitly requested.";
                } else {
                    FetchedPageData fetchedPage = await FetchHttpPageAsync(client, next, resolvedOptions, structuredSchema, cancellationToken).ConfigureAwait(false);
                    page = fetchedPage.Page;
                    if (appliedProfile == null && string.IsNullOrWhiteSpace(resolvedOptions.ProfileName) && resolvedOptions.AutoProfile) {
                        ProfileSelectionDecision inferredProfileDecision = InferAutoProfile(startUri, fetchedPage.RawHtml, page, customProfiles);
                        if (inferredProfileDecision.Profile != null) {
                            appliedProfile = inferredProfileDecision.Profile;
                            HtmlCrawlProfiles.Apply(resolvedOptions, appliedProfile);
                            result.AppliedProfileName = appliedProfile.Name;
                            result.AppliedProfileReasonCode = inferredProfileDecision.ReasonCode;
                            result.AppliedProfileReason = inferredProfileDecision.Reason;
                            if (!string.IsNullOrWhiteSpace(fetchedPage.RawHtml)) {
                                PopulatePageFromHtml(page, fetchedPage.RawHtml!, next.Uri, resolvedOptions, structuredSchema);
                            }
                        }
                    }

                    if (resolvedOptions.AutoRender) {
                        AutoRenderDecision decision = EvaluateAutoRender(page, resolvedOptions);
                        page.RenderReasonCode = decision.ReasonCode;
                        page.RenderReason = decision.Reason;
                        if (decision.ShouldRender) {
                            fetchedPage = await FetchRenderedPageAsync(session!, next, resolvedOptions, structuredSchema, cancellationToken).ConfigureAwait(false);
                            page = fetchedPage.Page;
                            page.RenderMode = HtmlCrawlRenderMode.AutoRendered;
                            page.RenderReasonCode = decision.ReasonCode;
                            page.RenderReason = decision.Reason;
                        }
                    } else {
                        page.RenderReasonCode = HtmlCrawlRenderReasonCode.StaticRenderDisabled;
                        page.RenderReason = "Kept static because browser rendering was not enabled.";
                    }
                }

                ApplyRunMetadata(page, result);

                if (page.Status == HtmlCrawlPageStatus.Skipped) {
                    result.SkippedPages.Add(page);
                    if (persistSnapshots) {
                        await PersistSnapshotAsync(result, persistencePath, pending, cancellationToken, resolvedOptions).ConfigureAwait(false);
                    }
                    continue;
                }

                ApplyCanonicalUrlIfAllowed(page, startUri, resolvedOptions, visited);

                if (TrySkipDuplicateContent(page, resolvedOptions, contentFingerprints, out HtmlCrawlPage? duplicatePage)) {
                    result.SkippedPages.Add(duplicatePage!);
                    if (persistSnapshots) {
                        await PersistSnapshotAsync(result, persistencePath, pending, cancellationToken, resolvedOptions).ConfigureAwait(false);
                    }
                    continue;
                }

                result.Pages.Add(page);

                if (resolvedOptions.DownloadAssets && page.AssetUrls.Count > 0) {
                    string? assetsDirectory = persistSnapshots ? ResolveArtifactPaths(persistencePath).AssetsDirectory : null;
                    await DownloadAssetsForPageAsync(client, page, resolvedOptions, result, downloadedAssets, assetsDirectory, cancellationToken).ConfigureAwait(false);
                }

                if (page.Status == HtmlCrawlPageStatus.Success && next.Depth < resolvedOptions.MaxDepth) {
                    foreach (string link in page.Links) {
                        cancellationToken.ThrowIfCancellationRequested();
                        QueueCandidate(link, page.Url, next.Depth + 1, startUri, resolvedOptions, pending, queued, visited, result);
                    }
                }

                int crawlDelay = await GetEffectiveDelayAsync(next.Uri, client, resolvedOptions, robotsCache, cancellationToken).ConfigureAwait(false);
                if (crawlDelay > 0 && pending.Count > 0 && result.Pages.Count < resolvedOptions.MaxPages) {
                    await Task.Delay(crawlDelay, cancellationToken).ConfigureAwait(false);
                }

                if (persistSnapshots) {
                    await PersistSnapshotAsync(result, persistencePath, pending, cancellationToken, resolvedOptions).ConfigureAwait(false);
                }
            }

            result.PendingPages = SnapshotPendingPages(pending);
            result.Finished = DateTimeOffset.UtcNow;
            UpdateDerivedResultData(result);
            if (persistSnapshots) {
                await PersistSnapshotAsync(result, persistencePath, pending, cancellationToken, resolvedOptions).ConfigureAwait(false);
            }
            return result;
        } finally {
            resolvedOptions.ClearSensitiveData();
        }
    }

    private static void ValidateOptions(HtmlCrawlOptions options) {
        if (options.MaxDepth < 0) {
            throw new ArgumentOutOfRangeException(nameof(options.MaxDepth), "MaxDepth must be zero or greater.");
        }
        if (options.MaxPages <= 0) {
            throw new ArgumentOutOfRangeException(nameof(options.MaxPages), "MaxPages must be greater than zero.");
        }
        if (options.Timeout <= 0) {
            throw new ArgumentOutOfRangeException(nameof(options.Timeout), "Timeout must be greater than zero.");
        }
        if (options.DelayMs < 0) {
            throw new ArgumentOutOfRangeException(nameof(options.DelayMs), "DelayMs must be zero or greater.");
        }
        if (options.WaitAfterLoadMs < 0) {
            throw new ArgumentOutOfRangeException(nameof(options.WaitAfterLoadMs), "WaitAfterLoadMs must be zero or greater.");
        }
        if (options.AutoScrollSteps <= 0) {
            throw new ArgumentOutOfRangeException(nameof(options.AutoScrollSteps), "AutoScrollSteps must be greater than zero.");
        }
        if (options.AutoScrollDelayMs < 0) {
            throw new ArgumentOutOfRangeException(nameof(options.AutoScrollDelayMs), "AutoScrollDelayMs must be zero or greater.");
        }
        if (options.InteractionDelayMs < 0) {
            throw new ArgumentOutOfRangeException(nameof(options.InteractionDelayMs), "InteractionDelayMs must be zero or greater.");
        }
        if (options.InteractionRepeatCount <= 0) {
            throw new ArgumentOutOfRangeException(nameof(options.InteractionRepeatCount), "InteractionRepeatCount must be greater than zero.");
        }
        if (options.AutoRenderTextWordThreshold <= 0) {
            throw new ArgumentOutOfRangeException(nameof(options.AutoRenderTextWordThreshold), "AutoRenderTextWordThreshold must be greater than zero.");
        }
        if (!string.IsNullOrEmpty(options.PathPrefix) && !options.PathPrefix!.StartsWith("/", StringComparison.Ordinal)) {
            throw new ArgumentException("PathPrefix must start with '/'.", nameof(options.PathPrefix));
        }
    }

    private static HttpClient CreateClient(HtmlCrawlOptions options) {
        NetworkCredential? proxyCredential = null;
        if (!string.IsNullOrEmpty(options.ProxyUsername) || !string.IsNullOrEmpty(options.ProxyPassword)) {
            proxyCredential = new NetworkCredential(options.ProxyUsername, options.ProxyPassword);
        }

        HttpClient client = HtmlHttpClientFactory.Create(options.Proxy, proxyCredential);
        client.Timeout = TimeSpan.FromMilliseconds(options.Timeout);

        if (!string.IsNullOrEmpty(options.UserAgent)) {
            client.DefaultRequestHeaders.UserAgent.Clear();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
        }

        foreach (var header in options.Headers) {
            client.DefaultRequestHeaders.Remove(header.Key);
            client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (!string.IsNullOrEmpty(options.Username) && options.Password != null) {
            string basicToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Username}:{options.Password}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicToken);
        }

        return client;
    }

    private static async Task<HtmlBrowserSession> CreateRenderSessionAsync(HtmlCrawlOptions options, CancellationToken cancellationToken) {
        string bootstrapUrl = "about:blank";
        HtmlBrowserSession session = !string.IsNullOrEmpty(options.StorageStatePath)
            ? await HtmlBrowser.ImportSessionAsync(
                bootstrapUrl,
                options.StorageStatePath!,
                browser: options.Browser,
                clean: options.CleanBrowserInstall,
                headless: options.Headless,
                userAgent: options.UserAgent,
                proxy: options.Proxy,
                proxyUsername: options.ProxyUsername,
                proxyPassword: options.ProxyPassword,
                timeout: options.Timeout,
                cancellationToken: cancellationToken).ConfigureAwait(false)
            : await HtmlBrowser.OpenSessionAsync(
                bootstrapUrl,
                browser: options.Browser,
                clean: options.CleanBrowserInstall,
                username: options.Username,
                password: options.Password,
                formLogin: options.FormLogin,
                headless: options.Headless,
                userAgent: options.UserAgent,
                proxy: options.Proxy,
                proxyUsername: options.ProxyUsername,
                proxyPassword: options.ProxyPassword,
                timeout: options.Timeout,
                cancellationToken: cancellationToken).ConfigureAwait(false);

        session.Page.SetDefaultTimeout(options.Timeout);

        if (options.Headers.Count > 0) {
            await session.Context.SetExtraHTTPHeadersAsync(new Dictionary<string, string>(options.Headers, StringComparer.OrdinalIgnoreCase)).ConfigureAwait(false);
        }

        foreach (string pattern in options.BlockResourcePatterns) {
            await HtmlBrowser.RegisterRouteAsync(session, pattern, route => route.AbortAsync(), cancellationToken).ConfigureAwait(false);
        }

        return session;
    }

    private static async Task DiscoverSitemapCandidatesAsync(
        Uri startUri,
        HttpClient client,
        HtmlCrawlOptions options,
        IDictionary<string, RobotsDocument?> robotsCache,
        HtmlCrawlResult result,
        Queue<CrawlRequest> pending,
        HashSet<string> queued,
        HashSet<string> visited,
        CancellationToken cancellationToken) {
        if (!options.UseSitemaps && options.SitemapUrls.Count == 0) {
            return;
        }

        HashSet<string> initialSitemaps = new(StringComparer.OrdinalIgnoreCase);
        foreach (string sitemap in options.SitemapUrls) {
            if (TryResolveAbsoluteUri(startUri, sitemap, out Uri? resolved)) {
                initialSitemaps.Add(NormalizeUrl(resolved!, options));
            }
        }

        if (options.UseSitemaps) {
            RobotsDocument? robots = await GetRobotsDocumentAsync(startUri, client, options, robotsCache, cancellationToken).ConfigureAwait(false);
            if (robots != null) {
                foreach (string sitemap in robots.SitemapUrls) {
                    if (TryResolveAbsoluteUri(startUri, sitemap, out Uri? resolved)) {
                        initialSitemaps.Add(NormalizeUrl(resolved!, options));
                    }
                }
            }

            if (initialSitemaps.Count == 0) {
                initialSitemaps.Add(NormalizeUrl(new Uri(startUri, "/sitemap.xml"), options));
            }
        }

        Queue<Uri> sitemapQueue = new();
        HashSet<string> processedSitemaps = new(StringComparer.OrdinalIgnoreCase);
        foreach (string sitemap in initialSitemaps) {
            if (Uri.TryCreate(sitemap, UriKind.Absolute, out Uri? sitemapUri) && processedSitemaps.Add(sitemap)) {
                sitemapQueue.Enqueue(sitemapUri);
            }
        }

        while (sitemapQueue.Count > 0) {
            cancellationToken.ThrowIfCancellationRequested();
            Uri sitemapUri = sitemapQueue.Dequeue();
            result.SitemapUrls.Add(sitemapUri.AbsoluteUri);

            string xml;
            try {
                xml = await HtmlUtilities.GetStringWithProperEncodingAsync(client, sitemapUri.AbsoluteUri, cancellationToken).ConfigureAwait(false);
            } catch {
                continue;
            }

            XDocument document;
            try {
                document = XDocument.Parse(xml);
            } catch {
                continue;
            }

            XElement? root = document.Root;
            if (root == null) {
                continue;
            }

            if (root.Name.LocalName.Equals("sitemapindex", StringComparison.OrdinalIgnoreCase)) {
                foreach (string nested in document.Descendants().Where(x => x.Name.LocalName == "loc").Select(x => x.Value.Trim())) {
                    if (!TryResolveAbsoluteUri(sitemapUri, nested, out Uri? nestedUri)) {
                        continue;
                    }

                    string normalizedNested = NormalizeUrl(nestedUri!, options);
                    if (processedSitemaps.Add(normalizedNested)) {
                        sitemapQueue.Enqueue(nestedUri!);
                    }
                }
                continue;
            }

            if (!root.Name.LocalName.Equals("urlset", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            foreach (string location in document.Descendants().Where(x => x.Name.LocalName == "loc").Select(x => x.Value.Trim())) {
                QueueCandidate(location, sitemapUri.AbsoluteUri, 0, startUri, options, pending, queued, visited, result);
            }
        }
    }

    private static void QueueCandidate(
        string candidateUrl,
        string? parentUrl,
        int depth,
        Uri startUri,
        HtmlCrawlOptions options,
        Queue<CrawlRequest> pending,
        HashSet<string> queued,
        HashSet<string> visited,
        HtmlCrawlResult result) {
        if (!TryResolveAbsoluteUri(startUri, candidateUrl, out Uri? candidateUri)) {
            result.SkippedPages.Add(CreateSkippedPage(candidateUrl, parentUrl, depth, HtmlCrawlSkipReason.InvalidUrl));
            return;
        }

        HtmlCrawlSkipReason skipReason = GetSkipReasonForCandidate(candidateUri!, startUri, options);
        if (skipReason != HtmlCrawlSkipReason.None) {
            result.SkippedPages.Add(CreateSkippedPage(candidateUri!.AbsoluteUri, parentUrl, depth, skipReason));
            return;
        }

        string normalized = NormalizeUrl(candidateUri!, options);
        if (queued.Contains(normalized) || visited.Contains(normalized)) {
            return;
        }

        EnqueuePage(candidateUri!, parentUrl, depth, pending, queued, options);
    }

    private static void EnqueuePage(Uri uri, string? parentUrl, int depth, Queue<CrawlRequest> pending, HashSet<string> queued, HtmlCrawlOptions options) {
        string normalized = NormalizeUrl(uri, options);
        pending.Enqueue(new CrawlRequest {
            Uri = uri,
            ParentUrl = parentUrl,
            Depth = depth
        });
        queued.Add(normalized);
    }

    private static HtmlCrawlSkipReason GetSkipReasonForCandidate(Uri candidate, Uri startUri, HtmlCrawlOptions options) {
        if ((candidate.Scheme != Uri.UriSchemeHttp && candidate.Scheme != Uri.UriSchemeHttps) ||
            !Uri.IsWellFormedUriString(candidate.AbsoluteUri, UriKind.Absolute)) {
            return HtmlCrawlSkipReason.InvalidUrl;
        }

        if (options.RestrictToHost && !IsHostInScope(candidate.Host, startUri.Host, options.IncludeSubdomains)) {
            return HtmlCrawlSkipReason.OutsideHost;
        }

        string pathPrefix = NormalizePathPrefix(options.PathPrefix);
        if (!string.IsNullOrEmpty(pathPrefix) &&
            !candidate.AbsolutePath.StartsWith(pathPrefix, StringComparison.OrdinalIgnoreCase)) {
            return HtmlCrawlSkipReason.OutsidePathScope;
        }

        if (options.SkipKnownAssetUrls && MatchesAny(candidate.AbsolutePath, options.IgnoredAssetPathPatterns)) {
            return HtmlCrawlSkipReason.AssetPath;
        }

        string url = NormalizeUrl(candidate, options);
        if (options.IncludePatterns.Count > 0 && !MatchesAny(url, options.IncludePatterns)) {
            return HtmlCrawlSkipReason.NotIncludedByPattern;
        }

        if (options.ExcludePatterns.Count > 0 && MatchesAny(url, options.ExcludePatterns)) {
            return HtmlCrawlSkipReason.ExcludedByPattern;
        }

        return HtmlCrawlSkipReason.None;
    }

    private static async Task<RobotsDocument?> GetRobotsDocumentAsync(
        Uri uri,
        HttpClient client,
        HtmlCrawlOptions options,
        IDictionary<string, RobotsDocument?> cache,
        CancellationToken cancellationToken) {
        string hostKey = GetHostKey(uri);
        if (cache.TryGetValue(hostKey, out RobotsDocument? cached)) {
            return cached;
        }

        Uri robotsUri = new UriBuilder(uri.Scheme, uri.Host, uri.Port) {
            Path = "/robots.txt"
        }.Uri;

        try {
            using HttpResponseMessage response = await client.GetAsync(robotsUri, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) {
                cache[hostKey] = new RobotsDocument();
                return cache[hostKey];
            }

            string text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            RobotsDocument robots = ParseRobots(text, options.RobotsUserAgent);
            cache[hostKey] = robots;
            return robots;
        } catch {
            cache[hostKey] = new RobotsDocument();
            return cache[hostKey];
        }
    }

    private static RobotsDocument ParseRobots(string content, string crawlerUserAgent) {
        List<RobotsGroup> groups = new();
        List<string> sitemapUrls = new();
        RobotsGroup currentGroup = new();
        bool currentGroupHasDirectives = false;

        foreach (string rawLine in content.Replace("\r", string.Empty).Split('\n')) {
            string line = StripComment(rawLine).Trim();
            if (string.IsNullOrWhiteSpace(line)) {
                continue;
            }

            int separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0) {
                continue;
            }

            string key = line.Substring(0, separatorIndex).Trim();
            string value = line.Substring(separatorIndex + 1).Trim();

            if (key.Equals("User-agent", StringComparison.OrdinalIgnoreCase)) {
                if (currentGroup.UserAgents.Count > 0 && currentGroupHasDirectives) {
                    groups.Add(currentGroup);
                    currentGroup = new RobotsGroup();
                    currentGroupHasDirectives = false;
                }

                currentGroup.UserAgents.Add(value);
                continue;
            }

            if (key.Equals("Sitemap", StringComparison.OrdinalIgnoreCase)) {
                sitemapUrls.Add(value);
                continue;
            }

            if (currentGroup.UserAgents.Count == 0) {
                currentGroup.UserAgents.Add("*");
            }

            if (key.Equals("Allow", StringComparison.OrdinalIgnoreCase) || key.Equals("Disallow", StringComparison.OrdinalIgnoreCase)) {
                if (!string.IsNullOrEmpty(value)) {
                    currentGroup.Rules.Add(new RobotsRule {
                        Allow = key.Equals("Allow", StringComparison.OrdinalIgnoreCase),
                        Path = value
                    });
                }
                currentGroupHasDirectives = true;
                continue;
            }

            if (key.Equals("Crawl-delay", StringComparison.OrdinalIgnoreCase) && double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double delaySeconds)) {
                currentGroup.CrawlDelayMs = (int)Math.Round(delaySeconds * 1000);
                currentGroupHasDirectives = true;
            }
        }

        if (currentGroup.UserAgents.Count > 0 || currentGroupHasDirectives) {
            groups.Add(currentGroup);
        }

        RobotsGroup? selected = SelectRobotsGroup(groups, crawlerUserAgent);
        RobotsDocument document = new();
        document.SitemapUrls.AddRange(sitemapUrls);
        if (selected != null) {
            document.Rules.AddRange(selected.Rules);
            document.CrawlDelayMs = selected.CrawlDelayMs;
        }

        return document;
    }

    private static RobotsGroup? SelectRobotsGroup(IEnumerable<RobotsGroup> groups, string crawlerUserAgent) {
        RobotsGroup? wildcard = null;
        RobotsGroup? bestMatch = null;
        int bestLength = -1;

        foreach (RobotsGroup group in groups) {
            foreach (string token in group.UserAgents) {
                if (token == "*") {
                    wildcard ??= group;
                    continue;
                }

                if (UserAgentMatches(crawlerUserAgent, token) && token.Length > bestLength) {
                    bestMatch = group;
                    bestLength = token.Length;
                }
            }
        }

        return bestMatch ?? wildcard;
    }

    private static bool UserAgentMatches(string crawlerUserAgent, string token) {
        if (token == "*") {
            return true;
        }

        if (string.IsNullOrWhiteSpace(crawlerUserAgent)) {
            return false;
        }

        return crawlerUserAgent.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string StripComment(string line) {
        int commentIndex = line.IndexOf('#');
        return commentIndex >= 0 ? line.Substring(0, commentIndex) : line;
    }

    private static bool IsAllowedByRobots(RobotsDocument robots, Uri uri) {
        if (robots.Rules.Count == 0) {
            return true;
        }

        string target = string.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery;
        int bestLength = -1;
        bool allowed = true;

        foreach (RobotsRule rule in robots.Rules) {
            if (string.IsNullOrEmpty(rule.Path) || !target.StartsWith(rule.Path, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            if (rule.Path.Length > bestLength || (rule.Path.Length == bestLength && rule.Allow)) {
                bestLength = rule.Path.Length;
                allowed = rule.Allow;
            }
        }

        return allowed;
    }

    private static async Task<int> GetEffectiveDelayAsync(
        Uri currentUri,
        HttpClient client,
        HtmlCrawlOptions options,
        IDictionary<string, RobotsDocument?> cache,
        CancellationToken cancellationToken) {
        int delay = options.DelayMs;
        if (!options.RespectRobotsTxt) {
            return delay;
        }

        RobotsDocument? robots = await GetRobotsDocumentAsync(currentUri, client, options, cache, cancellationToken).ConfigureAwait(false);
        if (robots?.CrawlDelayMs is int robotsDelay) {
            return Math.Max(delay, robotsDelay);
        }

        return delay;
    }

    /// <summary>
    /// Saves a crawl result and any referenced page content to disk.
    /// </summary>
    /// <param name="result">Crawl result to save.</param>
    /// <param name="path">Directory or manifest path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task SaveResultAsync(HtmlCrawlResult result, string path, CancellationToken cancellationToken = default) =>
        PersistSnapshotAsync(result ?? throw new ArgumentNullException(nameof(result)), path, result.PendingPages, cancellationToken, null);

    /// <summary>
    /// Loads a previously saved crawl result from disk.
    /// </summary>
    /// <param name="path">Directory or manifest path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized crawl result.</returns>
    public static async Task<HtmlCrawlResult> LoadResultAsync(string path, CancellationToken cancellationToken = default) {
        if (path == null) {
            throw new ArgumentNullException(nameof(path));
        }

        string manifestPath = ResolveManifestPath(path);
        if (!File.Exists(manifestPath)) {
            throw new FileNotFoundException($"Crawl manifest not found: {manifestPath}", manifestPath);
        }

        string json;
#if NETSTANDARD2_0 || NETFRAMEWORK
        json = await Task.Run(() => File.ReadAllText(manifestPath), cancellationToken).ConfigureAwait(false);
#else
        json = await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false);
#endif

        JsonSerializerOptions options = CreateJsonOptions();
        HtmlCrawlResult? result = JsonSerializer.Deserialize<HtmlCrawlResult>(json, options);
        if (result == null) {
            throw new InvalidOperationException($"Unable to deserialize crawl result from '{manifestPath}'.");
        }

        return result;
    }

    private static async Task PersistSnapshotAsync(
        HtmlCrawlResult result,
        string path,
        Queue<CrawlRequest> pending,
        CancellationToken cancellationToken,
        HtmlCrawlOptions? options) =>
        await PersistSnapshotAsync(result, path, SnapshotPendingPages(pending), cancellationToken, options).ConfigureAwait(false);

    private static async Task PersistSnapshotAsync(
        HtmlCrawlResult result,
        string path,
        IEnumerable<HtmlCrawlPendingItem> pendingItems,
        CancellationToken cancellationToken,
        HtmlCrawlOptions? options) {
        if (string.IsNullOrWhiteSpace(path)) {
            return;
        }

        CrawlArtifactPaths artifactPaths = ResolveArtifactPaths(path);
        result.PendingPages = pendingItems.ToList();
        result.ManifestPath = artifactPaths.ManifestPath;
        result.PagesDirectoryPath = artifactPaths.PagesDirectory;
        result.AssetsDirectoryPath = artifactPaths.AssetsDirectory;
        result.PagesJsonlPath = artifactPaths.PagesJsonlPath;
        result.PagesCsvPath = artifactPaths.PagesCsvPath;
        result.SkippedPagesJsonlPath = artifactPaths.SkippedPagesJsonlPath;
        result.SkippedAssetsJsonlPath = artifactPaths.SkippedAssetsJsonlPath;
        result.LinksJsonlPath = artifactPaths.LinksJsonlPath;
        result.AssetsJsonlPath = artifactPaths.AssetsJsonlPath;
        result.StructuredJsonPagesJsonlPath = artifactPaths.StructuredJsonPagesJsonlPath;
        result.OpenApiLikePath = artifactPaths.OpenApiLikeJsonPath;
        result.OpenApiPath = artifactPaths.OpenApiJsonPath;
        result.ChunksJsonlPath = artifactPaths.ChunksJsonlPath;
        result.GraphJsonPath = artifactPaths.GraphJsonPath;
        result.SummaryPath = artifactPaths.SummaryJsonPath;
        result.SummaryTextPath = artifactPaths.SummaryTextPath;
        result.IndexHtmlPath = artifactPaths.IndexHtmlPath;
        UpdateDerivedResultData(result);

        for (int i = 0; i < result.Pages.Count; i++) {
            cancellationToken.ThrowIfCancellationRequested();
            HtmlCrawlPage page = result.Pages[i];
            string prefix = (i + 1).ToString("D4");
            string slug = BuildPageSlug(page, prefix);

            if (!string.IsNullOrEmpty(page.Html)) {
                page.HtmlPath = CombinePathWithinDirectory(artifactPaths.PagesDirectory, $"{slug}.html");
            }

            if (!string.IsNullOrEmpty(page.Text)) {
                page.TextPath = CombinePathWithinDirectory(artifactPaths.PagesDirectory, $"{slug}.txt");
            }

            if (!string.IsNullOrEmpty(page.Markdown)) {
                page.MarkdownPath = CombinePathWithinDirectory(artifactPaths.PagesDirectory, $"{slug}.md");
            }

            if (page.StructuredJson != null) {
                page.StructuredJsonPath = CombinePathWithinDirectory(artifactPaths.PagesDirectory, $"{slug}.structured.json");
            }

            page.ManifestPath = CombinePathWithinDirectory(artifactPaths.PagesDirectory, $"{slug}.json");
        }

        Dictionary<string, string> localPageMap = BuildLocalPageMap(result.Pages);
        Dictionary<string, string> assetMap = result.Assets
            .Where(asset => !string.IsNullOrWhiteSpace(asset.Url) && !string.IsNullOrWhiteSpace(asset.FilePath) && string.IsNullOrWhiteSpace(asset.Error))
            .GroupBy(asset => asset.Url, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().FilePath!, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < result.Pages.Count; i++) {
            cancellationToken.ThrowIfCancellationRequested();
            HtmlCrawlPage page = result.Pages[i];

            if (!string.IsNullOrEmpty(page.Html)) {
                string htmlToWrite = ShouldRewriteStoredHtml(options)
                    ? RewriteStoredHtmlToLocalPaths(page.Html, page.Url, page.HtmlPath!, result.Assets, localPageMap, options!)
                    : page.Html;
                await WriteTextAsync(page.HtmlPath!, htmlToWrite, cancellationToken).ConfigureAwait(false);
            }

            if (!string.IsNullOrEmpty(page.Text)) {
                await WriteTextAsync(page.TextPath!, page.Text, cancellationToken).ConfigureAwait(false);
            }

            if (!string.IsNullOrEmpty(page.Markdown)) {
                await WriteTextAsync(page.MarkdownPath!, page.Markdown, cancellationToken).ConfigureAwait(false);
            }

            if (page.StructuredJson != null) {
                await WriteTextAsync(page.StructuredJsonPath!, JsonSerializer.Serialize(page.StructuredJson, CreateJsonOptions()), cancellationToken).ConfigureAwait(false);
            }

            await WriteTextAsync(page.ManifestPath!, BuildPageManifestJson(page, result.Assets, localPageMap, assetMap), cancellationToken).ConfigureAwait(false);
        }

        await RewriteDownloadedCssAssetsAsync(result.Assets, options, cancellationToken).ConfigureAwait(false);

        StringBuilder pagesJsonl = new();
        StringBuilder pagesCsv = new();
        pagesCsv.AppendLine("Url,RequestedUrl,CanonicalUrl,ParentUrl,Depth,Status,StatusCode,ContentType,Title,HtmlPath,TextPath,MarkdownPath,StructuredJsonPath,ManifestPath,ContentFingerprint,DuplicateOfUrl,Rendered,RenderMode,RenderReasonCode,RenderReason,AppliedScenario,AppliedProfileName,AppliedProfileReasonCode,AppliedProfileReason,ContentModeUsed,ContentSelectionReasonCode,ContentSelectionReason,ContentElementTag,ContentElementId,ContentElementClasses,ContentElementSelectorHint,ContentSelectionScore,ReaderCandidateCount,ReaderRootElementSelectorHint,ContentComparisonCount,BestContentComparisonMode,BestContentComparisonReasonCode,BestContentComparisonWordCount,RunnerUpContentComparisonMode,BestContentComparisonWordDelta,ContentComparisonDeltaSummary,ContentComparisonPreviewSummary,Started,Finished,DurationMs,LinkCount,AssetCount,InteractionCount,StructuredTableCount,StructuredListCount,StructuredFormCount,StructuredMicrodataCount,StructuredMetaTagCount,StructuredCodeBlockCount,StructuredCodeSampleCount,StructuredApiEndpointCount,StructuredAuthenticatedApiEndpointCount,StructuredRateLimitedApiEndpointCount,StructuredApiErrorResponseCount,StructuredBreadcrumbCount,StructuredFaqCount,StructuredSpecTableCount,StructuredCalloutCount,StructuredPrimaryActionCount,StructuredHeaderCount,StructuredNavigationCount,StructuredMainCount,StructuredArticleCount,StructuredAsideCount,StructuredFooterCount,OfflineReadinessGrade,HighestOfflineRiskSeverity,OfflineDependencyDiagnosticCount,OfflineDependencyKindsSummary,Error");
        foreach (HtmlCrawlPage page in result.Pages) {
            cancellationToken.ThrowIfCancellationRequested();
            pagesJsonl.AppendLine(JsonSerializer.Serialize(new {
                page.Url,
                page.RequestedUrl,
                page.CanonicalUrl,
                page.ParentUrl,
                page.Depth,
                page.Status,
                page.StatusCode,
                page.ContentType,
                page.Title,
                page.HtmlPath,
                page.TextPath,
                page.MarkdownPath,
                page.StructuredJsonPath,
                page.ManifestPath,
                page.ContentFingerprint,
                page.DuplicateOfUrl,
                page.Rendered,
                page.RenderMode,
                page.RenderReasonCode,
                page.RenderReason,
                page.AppliedScenario,
                page.AppliedProfileName,
                page.AppliedProfileReasonCode,
                page.AppliedProfileReason,
                page.ContentModeUsed,
                page.ContentSelectionReasonCode,
                page.ContentSelectionReason,
                page.ContentElementTag,
                page.ContentElementId,
                page.ContentElementClasses,
                page.ContentElementSelectorHint,
                page.ContentSelectionScore,
                page.ReaderCandidateCount,
                page.ReaderRootElementSelectorHint,
                ContentComparisonCount = page.ContentComparisons.Count,
                page.BestContentComparisonMode,
                page.BestContentComparisonReasonCode,
                page.BestContentComparisonWordCount,
                page.RunnerUpContentComparisonMode,
                page.BestContentComparisonWordDelta,
                page.ContentComparisonDeltaSummary,
                page.ContentComparisonPreviewSummary,
                page.AppliedInteractions,
                page.Started,
                page.Finished,
                DurationMs = (long)page.Duration.TotalMilliseconds,
                LinkCount = page.Links.Count,
                AssetCount = page.AssetUrls.Count,
                StructuredTableCount = page.StructuredJson?.Tables.Count ?? 0,
                StructuredListCount = page.StructuredJson?.Lists.Count ?? 0,
                StructuredFormCount = page.StructuredJson?.Forms.Count ?? 0,
                StructuredMicrodataCount = page.StructuredJson?.MicrodataItems.Count ?? 0,
                StructuredMetaTagCount = page.StructuredJson?.MetaTags.Count ?? 0,
                StructuredCodeBlockCount = page.StructuredJson?.CodeBlocks.Count ?? 0,
                StructuredCodeSampleCount = page.StructuredJson?.CodeSamples.Count ?? 0,
                StructuredApiEndpointCount = page.StructuredJson?.ApiEndpoints.Count ?? 0,
                StructuredAuthenticatedApiEndpointCount = GetStructuredAuthenticatedApiEndpointCount(page.StructuredJson),
                StructuredRateLimitedApiEndpointCount = GetStructuredRateLimitedApiEndpointCount(page.StructuredJson),
                StructuredApiErrorResponseCount = GetStructuredApiErrorResponseCount(page.StructuredJson),
                StructuredBreadcrumbCount = page.StructuredJson?.Breadcrumbs.Count ?? 0,
                StructuredFaqCount = page.StructuredJson?.FaqItems.Count ?? 0,
                StructuredSpecTableCount = page.StructuredJson?.SpecTables.Count ?? 0,
                StructuredCalloutCount = page.StructuredJson?.Callouts.Count ?? 0,
                StructuredPrimaryActionCount = page.StructuredJson?.PrimaryActions.Count ?? 0,
                StructuredHeaderCount = page.StructuredJson?.Layout.HeaderCount ?? 0,
                StructuredNavigationCount = page.StructuredJson?.Layout.NavigationCount ?? 0,
                StructuredMainCount = page.StructuredJson?.Layout.MainCount ?? 0,
                StructuredArticleCount = page.StructuredJson?.Layout.ArticleCount ?? 0,
                StructuredAsideCount = page.StructuredJson?.Layout.AsideCount ?? 0,
                StructuredFooterCount = page.StructuredJson?.Layout.FooterCount ?? 0,
                page.OfflineReadinessGrade,
                page.HighestOfflineRiskSeverity,
                OfflineDependencyDiagnosticCount = page.OfflineDependencyDiagnosticCount,
                page.OfflineDependencyKinds,
                page.OfflineDependencyKindsSummary,
                OfflineDependencyDiagnostics = page.OfflineDependencyDiagnostics,
                page.Error
            }));

            pagesCsv.AppendLine(string.Join(",",
                EscapeCsv(page.Url),
                EscapeCsv(page.RequestedUrl),
                EscapeCsv(page.CanonicalUrl),
                EscapeCsv(page.ParentUrl),
                EscapeCsv(page.Depth.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.Status.ToString()),
                EscapeCsv(page.StatusCode?.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.ContentType),
                EscapeCsv(page.Title),
                EscapeCsv(page.HtmlPath),
                EscapeCsv(page.TextPath),
                EscapeCsv(page.MarkdownPath),
                EscapeCsv(page.StructuredJsonPath),
                EscapeCsv(page.ManifestPath),
                EscapeCsv(page.ContentFingerprint),
                EscapeCsv(page.DuplicateOfUrl),
                EscapeCsv(page.Rendered.ToString()),
                EscapeCsv(page.RenderMode.ToString()),
                EscapeCsv(page.RenderReasonCode.ToString()),
                EscapeCsv(page.RenderReason),
                EscapeCsv(page.AppliedScenario.ToString()),
                EscapeCsv(page.AppliedProfileName),
                EscapeCsv(page.AppliedProfileReasonCode.ToString()),
                EscapeCsv(page.AppliedProfileReason),
                EscapeCsv(page.ContentModeUsed.ToString()),
                EscapeCsv(page.ContentSelectionReasonCode.ToString()),
                EscapeCsv(page.ContentSelectionReason),
                EscapeCsv(page.ContentElementTag),
                EscapeCsv(page.ContentElementId),
                EscapeCsv(string.Join("|", page.ContentElementClasses)),
                EscapeCsv(page.ContentElementSelectorHint),
                EscapeCsv(page.ContentSelectionScore?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.ReaderCandidateCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.ReaderRootElementSelectorHint),
                EscapeCsv(page.ContentComparisons.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.BestContentComparisonMode?.ToString()),
                EscapeCsv(page.BestContentComparisonReasonCode?.ToString()),
                EscapeCsv(page.BestContentComparisonWordCount?.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.RunnerUpContentComparisonMode?.ToString()),
                EscapeCsv(page.BestContentComparisonWordDelta?.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.ContentComparisonDeltaSummary),
                EscapeCsv(page.ContentComparisonPreviewSummary),
                EscapeCsv(page.Started.ToString("O", System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.Finished.ToString("O", System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(((long)page.Duration.TotalMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.Links.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.AssetUrls.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.AppliedInteractions.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.Tables.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.Lists.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.Forms.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.MicrodataItems.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.MetaTags.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.CodeBlocks.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.CodeSamples.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.ApiEndpoints.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(GetStructuredAuthenticatedApiEndpointCount(page.StructuredJson).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(GetStructuredRateLimitedApiEndpointCount(page.StructuredJson).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(GetStructuredApiErrorResponseCount(page.StructuredJson).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.Breadcrumbs.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.FaqItems.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.SpecTables.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.Callouts.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.PrimaryActions.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.Layout.HeaderCount ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.Layout.NavigationCount ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.Layout.MainCount ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.Layout.ArticleCount ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.Layout.AsideCount ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv((page.StructuredJson?.Layout.FooterCount ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.OfflineReadinessGrade),
                EscapeCsv(page.HighestOfflineRiskSeverity),
                EscapeCsv(page.OfflineDependencyDiagnosticCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.OfflineDependencyKindsSummary),
                EscapeCsv(page.Error)));
        }

        List<HtmlCrawlPage> skippedContentPages = result.SkippedPages
            .Where(page => page.SkipReason != HtmlCrawlSkipReason.AssetPath)
            .ToList();
        List<HtmlCrawlPage> skippedAssetPages = result.SkippedPages
            .Where(page => page.SkipReason == HtmlCrawlSkipReason.AssetPath)
            .ToList();

        StringBuilder skippedPagesJsonl = new();
        foreach (HtmlCrawlPage page in skippedContentPages) {
            cancellationToken.ThrowIfCancellationRequested();
            skippedPagesJsonl.AppendLine(JsonSerializer.Serialize(new {
                page.Url,
                page.RequestedUrl,
                page.CanonicalUrl,
                page.ParentUrl,
                page.Depth,
                page.Status,
                page.SkipReason,
                page.ContentType,
                page.ContentFingerprint,
                page.DuplicateOfUrl,
                page.OfflineReadinessGrade,
                page.HighestOfflineRiskSeverity,
                page.OfflineDependencyDiagnosticCount,
                page.OfflineDependencyKindsSummary,
                page.Error
            }));
        }

        StringBuilder skippedAssetsJsonl = new();
        foreach (HtmlCrawlPage page in skippedAssetPages) {
            cancellationToken.ThrowIfCancellationRequested();
            skippedAssetsJsonl.AppendLine(JsonSerializer.Serialize(new {
                page.Url,
                page.RequestedUrl,
                page.CanonicalUrl,
                page.ParentUrl,
                page.Depth,
                page.Status,
                page.SkipReason,
                page.ContentType,
                page.ContentFingerprint,
                page.DuplicateOfUrl,
                page.OfflineReadinessGrade,
                page.HighestOfflineRiskSeverity,
                page.OfflineDependencyDiagnosticCount,
                page.OfflineDependencyKindsSummary,
                page.Error
            }));
        }

        StringBuilder linksJsonl = new();
        foreach (HtmlCrawlPage page in result.Pages.Where(page => !string.IsNullOrWhiteSpace(page.Url))) {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (string link in page.Links.Where(link => !string.IsNullOrWhiteSpace(link)).Distinct(StringComparer.OrdinalIgnoreCase)) {
                linksJsonl.AppendLine(JsonSerializer.Serialize(new {
                    SourceUrl = page.Url,
                    TargetUrl = link,
                    page.Depth,
                    page.Rendered
                }));
            }
        }

        StringBuilder assetsJsonl = new();
        foreach (HtmlCrawlAsset asset in result.Assets) {
            cancellationToken.ThrowIfCancellationRequested();
            assetsJsonl.AppendLine(JsonSerializer.Serialize(new {
                asset.Url,
                asset.PageUrl,
                asset.Source,
                asset.ContentType,
                asset.StatusCode,
                asset.FilePath,
                asset.ContentLength,
                asset.Error,
                asset.Started,
                asset.Finished,
                DurationMs = (long)asset.Duration.TotalMilliseconds
            }));
        }

        StringBuilder structuredPagesJsonl = new();
        foreach (HtmlCrawlPage page in result.Pages.Where(page => page.StructuredJson != null)) {
            cancellationToken.ThrowIfCancellationRequested();
            structuredPagesJsonl.AppendLine(JsonSerializer.Serialize(new {
                page.Url,
                page.Title,
                page.Depth,
                page.StructuredJsonPath,
                page.StructuredJson
            }, CreateJsonOptions()));
        }

        List<PageChunkRecord> chunkRecords = BuildChunkRecords(result.Pages);
        result.ChunkCount = chunkRecords.Count;
        StringBuilder chunksJsonl = new();
        foreach (PageChunkRecord chunk in chunkRecords) {
            cancellationToken.ThrowIfCancellationRequested();
            chunksJsonl.AppendLine(JsonSerializer.Serialize(new {
                chunk.ChunkId,
                chunk.Url,
                chunk.Title,
                chunk.Depth,
                chunk.ChunkIndex,
                chunk.WordCount,
                chunk.CharacterCount,
                chunk.Summary,
                chunk.Headings,
                chunk.Keywords,
                chunk.Text,
                HtmlPath = BuildRelativeOptionalPath(artifactPaths.ChunksJsonlPath, chunk.HtmlPath),
                TextPath = BuildRelativeOptionalPath(artifactPaths.ChunksJsonlPath, chunk.TextPath),
                ManifestPath = BuildRelativeOptionalPath(artifactPaths.ChunksJsonlPath, chunk.ManifestPath),
                chunk.OfflineReadinessGrade,
                chunk.HighestOfflineRiskSeverity,
                chunk.OfflineDependencyDiagnosticCount,
                chunk.OfflineDependencyKindsSummary,
                chunk.Fingerprint
            }));
        }

        (object graphDocument, int graphNodeCount, int graphEdgeCount, int fetchedNodeCount, int skippedNodeCount, int externalNodeCount, Dictionary<string, int> nodeCategories, Dictionary<string, int> edgeRelations, Dictionary<string, int> skippedNodeReasons) =
            BuildGraphDocument(result.Pages, result.SkippedPages, artifactPaths.GraphJsonPath);
        result.GraphNodeCount = graphNodeCount;
        result.GraphEdgeCount = graphEdgeCount;
        result.GraphFetchedNodeCount = fetchedNodeCount;
        result.GraphSkippedNodeCount = skippedNodeCount;
        result.GraphExternalNodeCount = externalNodeCount;
        result.GraphNodeCategories = nodeCategories;
        result.GraphEdgeRelations = edgeRelations;
        result.GraphSkippedNodeReasons = skippedNodeReasons;

        HtmlCrawlSummary summary = result.Summary;
        await WriteTextAsync(artifactPaths.PagesJsonlPath, pagesJsonl.ToString(), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.PagesCsvPath, pagesCsv.ToString(), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.SkippedPagesJsonlPath, skippedPagesJsonl.ToString(), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.SkippedAssetsJsonlPath, skippedAssetsJsonl.ToString(), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.LinksJsonlPath, linksJsonl.ToString(), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.AssetsJsonlPath, assetsJsonl.ToString(), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.StructuredJsonPagesJsonlPath, structuredPagesJsonl.ToString(), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.OpenApiLikeJsonPath, JsonSerializer.Serialize(result.OpenApiLike, CreateJsonOptions()), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.OpenApiJsonPath, JsonSerializer.Serialize(result.OpenApiDocument, CreateJsonOptions()), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.ChunksJsonlPath, chunksJsonl.ToString(), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.GraphJsonPath, JsonSerializer.Serialize(graphDocument, CreateJsonOptions()), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.SummaryJsonPath, JsonSerializer.Serialize(summary, CreateJsonOptions()), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.SummaryTextPath, summary.ToReportText(result.SitemapUrls), cancellationToken).ConfigureAwait(false);
        await WriteTextAsync(artifactPaths.IndexHtmlPath, BuildIndexHtml(result, summary, artifactPaths.IndexHtmlPath), cancellationToken).ConfigureAwait(false);

        string json = JsonSerializer.Serialize(result, CreateJsonOptions());
        await WriteTextAsync(artifactPaths.ManifestPath, json, cancellationToken).ConfigureAwait(false);
    }

    private static List<HtmlCrawlPendingItem> SnapshotPendingPages(IEnumerable<CrawlRequest> pending) {
        List<HtmlCrawlPendingItem> snapshot = new();
        foreach (CrawlRequest item in pending) {
            snapshot.Add(new HtmlCrawlPendingItem {
                Url = item.Uri.AbsoluteUri,
                ParentUrl = item.ParentUrl,
                Depth = item.Depth
            });
        }

        return snapshot;
    }

    private static void UpdateDerivedResultData(HtmlCrawlResult result) {
        result.OpenApiLike = BuildResultOpenApiLike(result);
        result.OpenApiDocument = BuildResultOpenApiDocument(result.OpenApiLike, result);
    }

    private static Dictionary<string, object?> BuildResultOpenApiDocument(HtmlCrawlStructuredOpenApiLike openApiLike, HtmlCrawlResult result) {
        Dictionary<string, object?> paths = BuildStrictOpenApiPaths(openApiLike);
        Dictionary<string, object?> document = new(StringComparer.OrdinalIgnoreCase) {
            ["openapi"] = "3.1.0",
            ["info"] = new Dictionary<string, object?> {
                ["title"] = openApiLike.Title ?? "Offline API",
                ["description"] = openApiLike.Description,
                ["version"] = "0.0.0-offline"
            },
            ["servers"] = openApiLike.Servers.Select(server => new Dictionary<string, object?> {
                ["url"] = server
            }).ToList(),
            ["paths"] = paths
        };

        Dictionary<string, object?> components = BuildStrictOpenApiComponents(openApiLike);
        if (components.Count > 0) {
            document["components"] = components;
        }

        document["x-htmltinkerx-openApiLikePath"] = result.OpenApiLikePath;
        document["x-htmltinkerx-startUrl"] = result.StartUrl;
        document["x-htmltinkerx-operationCount"] = openApiLike.Paths.Values.Sum(path => path.Operations.Count);
        document["x-htmltinkerx-promotion"] = BuildStrictOpenApiPromotionMetadata(openApiLike, paths);
        return document;
    }

    private static Dictionary<string, object?> BuildStrictOpenApiPaths(HtmlCrawlStructuredOpenApiLike openApiLike) {
        Dictionary<string, object?> paths = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, HtmlCrawlStructuredOpenApiPathItem> pathItem in openApiLike.Paths.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
            Dictionary<string, object?> operations = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, HtmlCrawlStructuredOpenApiOperation> operationItem in pathItem.Value.Operations.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                if (!operationItem.Value.StrictOpenApiEligible) {
                    continue;
                }

                operations[operationItem.Key] = BuildStrictOpenApiOperation(operationItem.Value);
            }

            if (operations.Count > 0) {
                paths[pathItem.Key] = operations;
            }
        }

        return paths;
    }

    private static Dictionary<string, object?> BuildStrictOpenApiOperation(HtmlCrawlStructuredOpenApiOperation operation) {
        Dictionary<string, object?> value = new(StringComparer.OrdinalIgnoreCase) {
            ["operationId"] = operation.OperationId,
            ["summary"] = operation.Summary,
            ["description"] = operation.Description,
            ["tags"] = operation.Tags
        };

        List<object> parameters = BuildStrictOpenApiParameters(operation);
        if (parameters.Count > 0) {
            value["parameters"] = parameters;
        }

        object? requestBody = BuildStrictOpenApiRequestBody(operation);
        if (requestBody != null) {
            value["requestBody"] = requestBody;
        }

        value["responses"] = BuildStrictOpenApiResponses(operation);

        if (operation.Authentication.Required != false && !string.IsNullOrWhiteSpace(operation.AuthenticationRef)) {
            value["security"] = new List<object> {
                new Dictionary<string, object?> {
                    [operation.AuthenticationRef!] = Array.Empty<string>()
                }
            };
        }

        AddStrictOpenApiExtension(value, "x-htmltinkerx-resource", operation.Resource);
        AddStrictOpenApiExtension(value, "x-htmltinkerx-rateLimitRef", operation.RateLimitRef);
        AddStrictOpenApiExtension(value, "x-htmltinkerx-parametersRef", operation.ParametersRef);
        AddStrictOpenApiExtension(value, "x-htmltinkerx-requestHeadersRef", operation.RequestHeadersRef);
        AddStrictOpenApiExtension(value, "x-htmltinkerx-responseHeadersRef", operation.ResponseHeadersRef);
        AddStrictOpenApiExtension(value, "x-htmltinkerx-requestExamplesRef", operation.RequestExamplesRef);
        AddStrictOpenApiExtension(value, "x-htmltinkerx-responseExamplesRef", operation.ResponseExamplesRef);
        AddStrictOpenApiExtension(value, "x-htmltinkerx-errorCatalogRef", operation.ErrorCatalogRef);
        AddStrictOpenApiExtension(value, "x-htmltinkerx-promotionScore", operation.StrictOpenApiScore);
        if (operation.StrictOpenApiWarnings.Count > 0) {
            value["x-htmltinkerx-promotionWarnings"] = operation.StrictOpenApiWarnings.ToList();
        }
        if (operation.Provenance.PageUrls.Count > 0) {
            value["x-htmltinkerx-sourcePages"] = operation.Provenance.PageUrls.ToList();
        }
        if (operation.Provenance.Entries.Count > 0) {
            value["x-htmltinkerx-provenance"] = operation.Provenance.Entries
                .Select(entry => new Dictionary<string, object?> {
                    ["pageUrl"] = entry.PageUrl,
                    ["kind"] = entry.Kind,
                    ["selectorHint"] = entry.SelectorHint,
                    ["label"] = entry.Label
                })
                .ToList();
        }
        return value;
    }

    private static Dictionary<string, object?> BuildStrictOpenApiPromotionMetadata(
        HtmlCrawlStructuredOpenApiLike openApiLike,
        IReadOnlyDictionary<string, object?> strictPaths) {
        List<Dictionary<string, object?>> skippedOperations = openApiLike.Paths
            .SelectMany(path => path.Value.Operations.Values)
            .Where(operation => !operation.StrictOpenApiEligible)
            .OrderByDescending(operation => operation.StrictOpenApiScore)
            .ThenBy(operation => operation.Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(operation => operation.Method, StringComparer.OrdinalIgnoreCase)
            .Select(operation => new Dictionary<string, object?> {
                ["operationId"] = operation.OperationId,
                ["method"] = operation.Method,
                ["path"] = operation.Path,
                ["score"] = operation.StrictOpenApiScore,
                ["warnings"] = operation.StrictOpenApiWarnings.ToList()
            })
            .ToList();

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) {
            ["threshold"] = openApiLike.StrictOpenApiPromotionThreshold,
            ["eligibleOperationCount"] = openApiLike.StrictOpenApiEligibleOperationCount,
            ["skippedOperationCount"] = openApiLike.StrictOpenApiSkippedOperationCount,
            ["promotedPathCount"] = strictPaths.Count,
            ["averageScore"] = openApiLike.StrictOpenApiAverageScore,
            ["skippedOperations"] = skippedOperations
        };
    }

    private static List<object> BuildStrictOpenApiParameters(HtmlCrawlStructuredOpenApiOperation operation) {
        return operation.Parameters
            .Where(parameter => !string.Equals(ResolveStructuredApiParameterLocation(operation.Path, parameter), "body", StringComparison.OrdinalIgnoreCase))
            .OrderBy(parameter => ResolveStructuredApiParameterLocation(operation.Path, parameter), StringComparer.OrdinalIgnoreCase)
            .ThenBy(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase)
            .Select(parameter => {
                string location = ResolveStructuredApiParameterLocation(operation.Path, parameter);
                Dictionary<string, object?> value = new(StringComparer.OrdinalIgnoreCase) {
                    ["name"] = parameter.Name,
                    ["in"] = NormalizeStrictOpenApiParameterLocation(location),
                    ["required"] = string.Equals(location, "path", StringComparison.OrdinalIgnoreCase) || parameter.Required == true,
                    ["description"] = parameter.Description,
                    ["schema"] = BuildStrictOpenApiParameterSchema(parameter)
                };

                if (!string.IsNullOrWhiteSpace(parameter.ExampleValue)) {
                    value["example"] = ParseStrictOpenApiExampleValue(parameter.ExampleValue);
                }
                if (!string.IsNullOrWhiteSpace(parameter.DefaultValue)) {
                    value["x-htmltinkerx-default"] = parameter.DefaultValue;
                }
                return (object)value;
            })
            .ToList();
    }

    private static object BuildStrictOpenApiParameterSchema(HtmlCrawlStructuredApiParameter parameter) {
        Dictionary<string, object?> schema = new(StringComparer.OrdinalIgnoreCase);
        ApplyStrictOpenApiType(schema, parameter.Type, parameter.Format);
        if (parameter.Nullable == true) {
            schema["nullable"] = true;
        }
        if (!string.IsNullOrWhiteSpace(parameter.Pattern)) {
            schema["pattern"] = parameter.Pattern;
        }
        if (parameter.EnumValues.Count > 0) {
            schema["enum"] = parameter.EnumValues.Cast<object>().ToList();
        }

        return schema;
    }

    private static object? BuildStrictOpenApiRequestBody(HtmlCrawlStructuredOpenApiOperation operation) {
        List<HtmlCrawlStructuredRequestExample> bodyExamples = operation.RequestExamples
            .Where(example => !string.IsNullOrWhiteSpace(example.Body))
            .ToList();
        bool hasSchema = !string.IsNullOrWhiteSpace(operation.RequestBodyFieldsRef) || !string.IsNullOrWhiteSpace(operation.RequestBodySchemaRef);
        bool hasExamples = bodyExamples.Count > 0;
        if (!hasSchema && !hasExamples) {
            return null;
        }

        string contentType = operation.RequestHeaders
            .FirstOrDefault(header => string.Equals(header.Name, "Content-Type", StringComparison.OrdinalIgnoreCase))
            ?.Value
            ?? bodyExamples.Select(example => example.ContentType).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? "application/json";

        Dictionary<string, object?> mediaType = new(StringComparer.OrdinalIgnoreCase);
        object? schema = BuildStrictOpenApiSchemaReference(operation.RequestBodyFieldsRef, operation.RequestBodySchemaRef);
        if (schema != null) {
            mediaType["schema"] = schema;
        }

        Dictionary<string, object?> examples = BuildStrictOpenApiRequestExamples(bodyExamples);
        if (examples.Count > 0) {
            mediaType["examples"] = examples;
        }

        return new Dictionary<string, object?> {
            ["required"] = operation.Parameters.Any(parameter => string.Equals(ResolveStructuredApiParameterLocation(operation.Path, parameter), "body", StringComparison.OrdinalIgnoreCase) && parameter.Required == true),
            ["content"] = new Dictionary<string, object?> {
                [contentType] = mediaType
            }
        };
    }

    private static Dictionary<string, object?> BuildStrictOpenApiResponses(HtmlCrawlStructuredOpenApiOperation operation) {
        Dictionary<string, object?> responses = new(StringComparer.OrdinalIgnoreCase);

        foreach (IGrouping<string, HtmlCrawlStructuredResponseExample> group in operation.ResponseExamples
                     .GroupBy(example => GetStrictOpenApiResponseCode(example.StatusCode), StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)) {
            bool isError = group.Any(example => example.IsError);
            responses[group.Key] = BuildStrictOpenApiResponse(group.ToList(), isError, operation);
        }

        if (responses.Count == 0) {
            responses["default"] = new Dictionary<string, object?> {
                ["description"] = operation.Description ?? "Documented response"
            };
        }

        return responses;
    }

    private static Dictionary<string, object?> BuildStrictOpenApiResponse(
        IReadOnlyList<HtmlCrawlStructuredResponseExample> examples,
        bool isError,
        HtmlCrawlStructuredOpenApiOperation operation) {
        HtmlCrawlStructuredResponseExample primary = examples[0];
        Dictionary<string, object?> response = new(StringComparer.OrdinalIgnoreCase) {
            ["description"] = primary.StatusText ?? primary.Title ?? primary.Description ?? (isError ? "Error response" : "Successful response")
        };

        Dictionary<string, object?> headers = BuildStrictOpenApiHeaderDefinitions(examples.SelectMany(example => example.Headers));
        if (headers.Count > 0) {
            response["headers"] = headers;
        }

        string? contentType = examples.Select(example => example.ContentType).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        object? schema = BuildStrictOpenApiSchemaReference(
            isError ? operation.ErrorResponseFieldsRef : operation.SuccessResponseFieldsRef,
            isError ? operation.ErrorResponseSchemaRef : operation.SuccessResponseSchemaRef);
        Dictionary<string, object?> exampleEntries = BuildStrictOpenApiResponseExamples(examples);
        if (schema != null || exampleEntries.Count > 0) {
            Dictionary<string, object?> mediaType = new(StringComparer.OrdinalIgnoreCase);
            if (schema != null) {
                mediaType["schema"] = schema;
            }
            if (exampleEntries.Count > 0) {
                mediaType["examples"] = exampleEntries;
            }

            response["content"] = new Dictionary<string, object?> {
                [contentType ?? "application/json"] = mediaType
            };
        }

        return response;
    }

    private static Dictionary<string, object?> BuildStrictOpenApiComponents(HtmlCrawlStructuredOpenApiLike openApiLike) {
        Dictionary<string, object?> components = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, object?> securitySchemes = BuildStrictOpenApiSecuritySchemes(openApiLike.Components.AuthProfiles);
        if (securitySchemes.Count > 0) {
            components["securitySchemes"] = securitySchemes;
        }

        Dictionary<string, object?> schemas = BuildStrictOpenApiSchemas(openApiLike.Components);
        if (schemas.Count > 0) {
            components["schemas"] = schemas;
        }

        AddStrictOpenApiComponentExtension(components, "x-htmltinkerx-rateLimitProfiles", openApiLike.Components.RateLimitProfiles);
        AddStrictOpenApiComponentExtension(components, "x-htmltinkerx-parameterSets", openApiLike.Components.ParameterSets);
        AddStrictOpenApiComponentExtension(components, "x-htmltinkerx-requestHeaderSets", openApiLike.Components.RequestHeaderSets);
        AddStrictOpenApiComponentExtension(components, "x-htmltinkerx-responseHeaderSets", openApiLike.Components.ResponseHeaderSets);
        AddStrictOpenApiComponentExtension(components, "x-htmltinkerx-requestExampleSets", openApiLike.Components.RequestExampleSets);
        AddStrictOpenApiComponentExtension(components, "x-htmltinkerx-responseExampleSets", openApiLike.Components.ResponseExampleSets);
        AddStrictOpenApiComponentExtension(components, "x-htmltinkerx-errorCatalogs", openApiLike.Components.ErrorCatalogs);
        AddStrictOpenApiComponentExtension(components, "x-htmltinkerx-schemaProvenance", BuildStrictOpenApiSchemaComponentProvenance(openApiLike.Components.FieldSets));
        return components;
    }

    private static Dictionary<string, object?> BuildStrictOpenApiSecuritySchemes(IDictionary<string, HtmlCrawlStructuredApiAuthentication> authProfiles) {
        Dictionary<string, object?> securitySchemes = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, HtmlCrawlStructuredApiAuthentication> item in authProfiles.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
            Dictionary<string, object?> scheme = new(StringComparer.OrdinalIgnoreCase);
            string? primaryHeader = item.Value.Headers.FirstOrDefault();
            if (item.Value.Schemes.Any(schemeName => string.Equals(schemeName, "oauth2", StringComparison.OrdinalIgnoreCase))) {
                scheme["type"] = "oauth2";
                scheme["flows"] = new Dictionary<string, object?> {
                    ["clientCredentials"] = new Dictionary<string, object?> {
                        ["tokenUrl"] = "https://example.invalid/token",
                        ["scopes"] = new Dictionary<string, object?>()
                    }
                };
                scheme["x-htmltinkerx-oauth2FlowPlaceholder"] = true;
            } else if (item.Value.Schemes.Any(schemeName => string.Equals(schemeName, "bearer", StringComparison.OrdinalIgnoreCase))) {
                scheme["type"] = "http";
                scheme["scheme"] = "bearer";
            } else if (item.Value.Schemes.Any(schemeName => string.Equals(schemeName, "basic", StringComparison.OrdinalIgnoreCase))) {
                scheme["type"] = "http";
                scheme["scheme"] = "basic";
            } else {
                scheme["type"] = "apiKey";
                scheme["in"] = "header";
                scheme["name"] = primaryHeader ?? "Authorization";
            }

            if (!string.IsNullOrWhiteSpace(item.Value.Summary)) {
                scheme["description"] = item.Value.Summary;
            }
            securitySchemes[item.Key] = scheme;
        }

        return securitySchemes;
    }

    private static Dictionary<string, object?> BuildStrictOpenApiSchemas(HtmlCrawlStructuredOpenApiComponents components) {
        Dictionary<string, object?> schemas = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, IList<HtmlCrawlStructuredField>> item in components.FieldSets.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
            schemas[item.Key] = BuildStrictOpenApiSchemaFromFields(item.Value);
        }

        foreach (KeyValuePair<string, IDictionary<string, string?>> item in components.Schemas.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
            if (!schemas.ContainsKey(item.Key)) {
                schemas[item.Key] = BuildStrictOpenApiSchemaFromFlatMap(item.Value);
            }
        }

        return schemas;
    }

    private static Dictionary<string, object?> BuildStrictOpenApiSchemaComponentProvenance(IDictionary<string, IList<HtmlCrawlStructuredField>> fieldSets) {
        Dictionary<string, object?> provenance = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, IList<HtmlCrawlStructuredField>> item in fieldSets.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
            List<Dictionary<string, object?>> entries = BuildStrictOpenApiFieldProvenance(item.Value.SelectMany(field => field.Provenance));
            if (entries.Count > 0) {
                provenance[item.Key] = entries;
            }
        }

        return provenance;
    }

    private static string BuildPageManifestJson(
        HtmlCrawlPage page,
        IEnumerable<HtmlCrawlAsset> assets,
        IDictionary<string, string> localPageMap,
        IDictionary<string, string> assetMap) {
        string? manifestPath = page.ManifestPath;
        PageSearchMetadata searchMetadata = BuildPageSearchMetadata(page);
        object manifest = new {
            page.Url,
            page.RequestedUrl,
            page.ParentUrl,
            page.CanonicalUrl,
            page.Depth,
            page.Status,
            page.SkipReason,
            page.StatusCode,
            page.ContentType,
            page.Title,
            page.Rendered,
            page.RenderMode,
            page.RenderReasonCode,
            page.RenderReason,
            page.AppliedScenario,
            page.AppliedProfileName,
            page.AppliedProfileReasonCode,
            page.AppliedProfileReason,
            Extraction = new {
                page.ContentModeUsed,
                page.ContentSelectionReasonCode,
                page.ContentSelectionReason,
                page.ContentElementTag,
                page.ContentElementId,
                page.ContentElementClasses,
                page.ContentElementSelectorHint,
                page.ContentSelectionScore,
                page.ReaderCandidateCount,
                page.ReaderRootElementSelectorHint
            },
            BestContentComparison = page.BestContentComparisonMode == null ? null : new {
                page.BestContentComparisonMode,
                page.BestContentComparisonReasonCode,
                page.BestContentComparisonWordCount,
                page.RunnerUpContentComparisonMode,
                page.BestContentComparisonWordDelta,
                page.ContentComparisonDeltaSummary
            },
            page.ContentComparisonPreviewSummary,
            ContentComparisons = page.ContentComparisons
                .OrderBy(comparison => comparison.Mode.ToString(), StringComparer.OrdinalIgnoreCase)
                .Select(comparison => new {
                    comparison.Mode,
                    comparison.ReasonCode,
                    comparison.Reason,
                    comparison.ElementSelectorHint,
                    comparison.WordCount,
                    comparison.CharacterCount,
                    comparison.Summary,
                    comparison.Score,
                    comparison.ReaderCandidateCount,
                    comparison.ReaderRootElementSelectorHint
                })
                .ToArray(),
            page.AppliedInteractions,
            page.Started,
            page.Finished,
            DurationMs = (long)page.Duration.TotalMilliseconds,
            PageFiles = new {
                HtmlPath = BuildRelativeOptionalPath(manifestPath, page.HtmlPath),
                TextPath = BuildRelativeOptionalPath(manifestPath, page.TextPath),
                MarkdownPath = BuildRelativeOptionalPath(manifestPath, page.MarkdownPath),
                StructuredJsonPath = BuildRelativeOptionalPath(manifestPath, page.StructuredJsonPath)
            },
            page.StructuredJson,
            Search = new {
                searchMetadata.WordCount,
                searchMetadata.CharacterCount,
                searchMetadata.ChunkCount,
                searchMetadata.Summary,
                searchMetadata.Headings,
                searchMetadata.Keywords
            },
            Links = page.Links
                .Where(link => !string.IsNullOrWhiteSpace(link))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(link => link, StringComparer.OrdinalIgnoreCase)
                .Select(link => new {
                    Url = link,
                    LocalPagePath = localPageMap.TryGetValue(link, out string? localPagePath) ? BuildRelativeOptionalPath(manifestPath, localPagePath) : null
                })
                .ToArray(),
            ReferencedAssets = page.AssetUrls
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(url => url, StringComparer.OrdinalIgnoreCase)
                .Select(url => new {
                    Url = url,
                    LocalFilePath = assetMap.TryGetValue(url, out string? localAssetPath) ? BuildRelativeOptionalPath(manifestPath, localAssetPath) : null
                })
                .ToArray(),
            DownloadedAssets = assets
                .Where(asset => string.Equals(asset.PageUrl, page.Url, StringComparison.OrdinalIgnoreCase))
                .Where(asset => !string.IsNullOrWhiteSpace(asset.Url))
                .OrderBy(asset => asset.Url, StringComparer.OrdinalIgnoreCase)
                .Select(asset => new {
                    asset.Url,
                    asset.ContentType,
                    asset.StatusCode,
                    asset.Error,
                    LocalFilePath = BuildRelativeOptionalPath(manifestPath, asset.FilePath),
                    asset.ContentLength
                })
                .ToArray(),
            page.OfflineReadinessGrade,
            page.HighestOfflineRiskSeverity,
            page.OfflineDependencyDiagnosticCount,
            page.OfflineDependencyKinds,
            page.OfflineDependencyKindsSummary,
            OfflineDependencyDiagnostics = page.OfflineDependencyDiagnostics
                .OrderBy(diagnostic => diagnostic.Kind, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            page.Error
        };

        return JsonSerializer.Serialize(manifest, CreateJsonOptions());
    }

    private static HtmlCrawlStructuredJson BuildStructuredJson(
        HtmlCrawlPage page,
        string fullHtml,
        string selectedHtml,
        string structuredText,
        string structuredMarkdown,
        IReadOnlyDictionary<string, HtmlCrawlJsonSchemaField> structuredSchema,
        HtmlCrawlStructuredJsonPreset structuredPreset) {
        IDocument document = HtmlParser.ParseWithAngleSharp(fullHtml);
        IDocument selectedDocument = HtmlParser.ParseWithAngleSharp($"<div id=\"__htmltinkerx_structured_selected\">{selectedHtml}</div>");
        List<HtmlMetaTag> metaTags = HtmlParser.ParseMetaTags(fullHtml);
        HtmlOpenGraph openGraph = HtmlParser.ParseOpenGraph(fullHtml);
        HtmlCrawlStructuredMetadata metadata = BuildStructuredMetadata(page, document, metaTags, openGraph);
        PageSearchMetadata searchMetadata = BuildPageSearchMetadata(selectedHtml, structuredText, structuredMarkdown);
        List<HtmlTableResult> tables = HtmlParser.ParseTablesWithAngleSharpDetailed(selectedHtml);
        List<HtmlCrawlStructuredCodeBlock> codeBlocks = BuildStructuredCodeBlocks(selectedDocument);
        List<HtmlCrawlStructuredCodeSample> codeSamples = BuildStructuredCodeSamples(selectedDocument);
        List<HtmlCrawlStructuredBreadcrumbTrail> breadcrumbs = BuildStructuredBreadcrumbs(document, page.Url);
        List<HtmlCrawlStructuredFaqItem> faqItems = BuildStructuredFaqItems(selectedDocument);
        List<HtmlCrawlStructuredSpecTable> specTables = BuildStructuredSpecTables(selectedDocument, tables);
        List<HtmlCrawlStructuredCallout> callouts = BuildStructuredCallouts(selectedDocument);
        List<HtmlCrawlStructuredPrimaryAction> primaryActions = BuildStructuredPrimaryActions(selectedDocument, page.Url);
        List<HtmlCrawlStructuredApiEndpoint> apiEndpoints = BuildStructuredApiEndpoints(selectedDocument, codeSamples, page.Url);
        HtmlCrawlStructuredJson structuredJson = new() {
            Document = BuildStructuredDocument(page, searchMetadata, structuredText, structuredMarkdown),
            Content = new HtmlCrawlStructuredContent {
                WordCount = searchMetadata.WordCount,
                CharacterCount = searchMetadata.CharacterCount,
                ChunkCount = searchMetadata.ChunkCount,
                Summary = searchMetadata.Summary,
                Headings = new List<string>(searchMetadata.Headings),
                Keywords = new List<string>(searchMetadata.Keywords)
            },
            Metadata = metadata,
            Layout = BuildStructuredLayout(document),
            MetaTags = metaTags,
            OpenGraph = openGraph,
            MicrodataItems = HtmlParser.ParseMicrodataItems(fullHtml),
            Forms = HtmlParser.ParseFormsWithAngleSharp(fullHtml),
            Lists = HtmlParser.ParseListsWithAngleSharpDetailed(selectedHtml),
            Tables = tables,
            CodeBlocks = codeBlocks,
            CodeSamples = codeSamples,
            Breadcrumbs = breadcrumbs,
            FaqItems = faqItems,
            SpecTables = specTables,
            Callouts = callouts,
            PrimaryActions = primaryActions,
            ApiEndpoints = apiEndpoints,
            ApiCatalog = BuildStructuredApiCatalog(metadata, apiEndpoints),
            OpenApiLike = BuildStructuredOpenApiLike(page, metadata, apiEndpoints)
        };

        HtmlCrawlStructuredJsonPreset resolvedPreset = ResolveStructuredJsonPreset(structuredJson, document, selectedDocument, structuredPreset);
        structuredJson.ResolvedPreset = resolvedPreset;
        IReadOnlyDictionary<string, HtmlCrawlJsonSchemaField> effectiveStructuredSchema = BuildEffectiveStructuredSchema(resolvedPreset, structuredSchema);
        if (effectiveStructuredSchema.Count > 0) {
            structuredJson.Extracted = BuildStructuredSchemaExtraction(structuredJson, document, selectedDocument, effectiveStructuredSchema);
        }

        return structuredJson;
    }

    private static IReadOnlyDictionary<string, HtmlCrawlJsonSchemaField> BuildEffectiveStructuredSchema(
        HtmlCrawlStructuredJsonPreset structuredPreset,
        IReadOnlyDictionary<string, HtmlCrawlJsonSchemaField> structuredSchema) {
        Dictionary<string, HtmlCrawlJsonSchemaField> effectiveSchema = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, HtmlCrawlJsonSchemaField> field in GetPresetStructuredSchema(structuredPreset)) {
            effectiveSchema[field.Key] = field.Value;
        }

        foreach (KeyValuePair<string, HtmlCrawlJsonSchemaField> field in structuredSchema) {
            effectiveSchema[field.Key] = field.Value;
        }

        return effectiveSchema;
    }

    private static IReadOnlyDictionary<string, HtmlCrawlJsonSchemaField> GetPresetStructuredSchema(HtmlCrawlStructuredJsonPreset structuredPreset) {
        Dictionary<string, HtmlCrawlJsonSchemaField> schema = new(StringComparer.OrdinalIgnoreCase);
        if (structuredPreset == HtmlCrawlStructuredJsonPreset.None) {
            return schema;
        }

        void AddPath(string name, string path) {
            schema[name] = new HtmlCrawlJsonSchemaField {
                Path = path
            };
        }

        void AddSelector(string name, string selector, string source = "selected", string mode = "Text", string? attribute = null, bool all = false) {
            schema[name] = new HtmlCrawlJsonSchemaField {
                Selector = selector,
                Source = source,
                Mode = mode,
                Attribute = attribute,
                All = all
            };
        }

        switch (structuredPreset) {
            case HtmlCrawlStructuredJsonPreset.Docs:
                AddPath("title", "Metadata.Title");
                AddPath("description", "Metadata.Description");
                AddPath("canonicalUrl", "Metadata.CanonicalUrl");
                AddPath("language", "Metadata.Language");
                AddPath("siteName", "Metadata.SiteName");
                AddPath("summary", "Document.Summary");
                AddPath("content", "Document.Text");
                AddPath("markdown", "Document.Markdown");
                AddPath("headings", "Document.Headings");
                AddPath("keywords", "Document.Keywords");
                AddPath("codeBlocks", "CodeBlocks");
                AddPath("codeSamples", "CodeSamples");
                AddPath("apiEndpoints", "ApiEndpoints");
                AddPath("apiCatalog", "ApiCatalog");
                AddPath("apiTags", "ApiCatalog.Tags");
                AddPath("apiResources", "ApiCatalog.Resources");
                AddPath("operationIds", "ApiCatalog.OperationIds");
                AddPath("openApiLike", "OpenApiLike");
                AddPath("openApiPaths", "OpenApiLike.Paths");
                AddPath("openApiServers", "OpenApiLike.Servers");
                AddPath("authentication", "ApiEndpoints.0.Authentication");
                AddPath("authRequired", "ApiEndpoints.0.Authentication.Required");
                AddPath("authenticationSchemes", "ApiEndpoints.0.Authentication.Schemes");
                AddPath("authenticationHeaders", "ApiEndpoints.0.Authentication.Headers");
                AddPath("rateLimit", "ApiEndpoints.0.RateLimit");
                AddPath("rateLimitHeaders", "ApiEndpoints.0.RateLimit.Headers");
                AddPath("rateLimitStatusCode", "ApiEndpoints.0.RateLimit.StatusCode");
                AddPath("operationId", "ApiEndpoints.0.OperationId");
                AddPath("resource", "ApiEndpoints.0.Resource");
                AddPath("tags", "ApiEndpoints.0.Tags");
                AddPath("requestExamples", "ApiEndpoints.0.RequestExamples");
                AddPath("requestExampleCount", "ApiEndpoints.0.RequestExamples.Count");
                AddPath("requestHeaders", "ApiEndpoints.0.RequestHeaders");
                AddPath("responseHeaders", "ApiEndpoints.0.ResponseHeaders");
                AddPath("errorResponses", "ApiEndpoints.0.ErrorResponses");
                AddPath("errorResponseCount", "ApiEndpoints.0.ErrorResponses.Count");
                AddPath("errorCatalog", "ApiEndpoints.0.ErrorCatalog");
                AddPath("errorCatalogCount", "ApiEndpoints.0.ErrorCatalog.Count");
                AddPath("successResponseSchema", "ApiEndpoints.0.SuccessResponseSchema");
                AddPath("errorResponseSchema", "ApiEndpoints.0.ErrorResponseSchema");
                AddPath("requestBodyFields", "ApiEndpoints.0.RequestBodyFields");
                AddPath("successResponseFields", "ApiEndpoints.0.SuccessResponseFields");
                AddPath("errorResponseFields", "ApiEndpoints.0.ErrorResponseFields");
                AddPath("faqItems", "FaqItems");
                AddPath("callouts", "Callouts");
                AddPath("primaryActions", "PrimaryActions");
                AddSelector("mainHeading", "h1, h2");
                AddSelector("sectionHeadings", "h2, h3", all: true);
                AddSelector("navigationLinks", "header nav a, nav a, [role='navigation'] a", source: "page", all: true);
                AddSelector("navigationHrefs", "header nav a, nav a, [role='navigation'] a", source: "page", mode: "Attribute", attribute: "href", all: true);
                AddPath("breadcrumbs", "Breadcrumbs.0.Labels");
                AddPath("codeBlockCount", "CodeBlocks.Count");
                AddPath("codeSampleCount", "CodeSamples.Count");
                AddPath("apiEndpointCount", "ApiEndpoints.Count");
                AddSelector("faqCount", "details, [itemscope][itemtype*='Question' i], [itemprop='mainEntity'][itemscope]", mode: "Count");
                AddPath("calloutCount", "Callouts.Count");
                AddPath("primaryActionLabels", "PrimaryActions");
                AddSelector("tableCount", "table", mode: "Count");
                break;

            case HtmlCrawlStructuredJsonPreset.Article:
                AddPath("title", "Metadata.Title");
                AddPath("description", "Metadata.Description");
                AddPath("canonicalUrl", "Metadata.CanonicalUrl");
                AddPath("language", "Metadata.Language");
                AddPath("author", "Metadata.Author");
                AddPath("publishedTime", "Metadata.PublishedTime");
                AddPath("modifiedTime", "Metadata.ModifiedTime");
                AddPath("imageUrl", "Metadata.ImageUrl");
                AddPath("summary", "Document.Summary");
                AddPath("content", "Document.Text");
                AddPath("markdown", "Document.Markdown");
                AddPath("headings", "Document.Headings");
                AddPath("keywords", "Document.Keywords");
                AddPath("links", "Document.Links");
                AddPath("faqItems", "FaqItems");
                AddPath("callouts", "Callouts");
                AddPath("primaryActions", "PrimaryActions");
                AddSelector("mainHeading", "h1");
                AddSelector("lead", "p");
                AddSelector("sectionHeadings", "h2, h3", all: true);
                AddSelector("imageUrls", "article img[src], main img[src], img[src]", source: "page", mode: "Attribute", attribute: "src", all: true);
                break;

            case HtmlCrawlStructuredJsonPreset.Product:
                AddPath("title", "Metadata.Title");
                AddPath("description", "Metadata.Description");
                AddPath("canonicalUrl", "Metadata.CanonicalUrl");
                AddPath("language", "Metadata.Language");
                AddPath("siteName", "Metadata.SiteName");
                AddPath("imageUrl", "Metadata.ImageUrl");
                AddPath("summary", "Document.Summary");
                AddPath("content", "Document.Text");
                AddPath("markdown", "Document.Markdown");
                AddPath("headings", "Document.Headings");
                AddPath("breadcrumbs", "Breadcrumbs.0.Labels");
                AddPath("faqItems", "FaqItems");
                AddPath("specTables", "SpecTables");
                AddPath("primaryActions", "PrimaryActions");
                AddSelector("name", "[itemprop='name'], h1", source: "page");
                AddSelector("price", "[itemprop='price'], .price, .product-price, [data-price], [class*='price']", source: "page");
                AddSelector("priceMeta", "meta[property='product:price:amount'], meta[itemprop='price']", source: "page", mode: "Attribute", attribute: "content");
                AddSelector("currency", "meta[property='product:price:currency'], meta[itemprop='priceCurrency']", source: "page", mode: "Attribute", attribute: "content");
                AddSelector("sku", "[itemprop='sku'], [data-sku], .sku, [class*='sku']", source: "page");
                AddSelector("availability", "[itemprop='availability'], .availability, [data-stock-status], [class*='stock']", source: "page");
                AddSelector("imageUrls", "img[src]", source: "page", mode: "Attribute", attribute: "src", all: true);
                AddSelector("specTableCount", "table", mode: "Count");
                break;
        }

        return schema;
    }

    private static HtmlCrawlStructuredJsonPreset ResolveStructuredJsonPreset(
        HtmlCrawlStructuredJson structuredJson,
        IDocument document,
        IDocument selectedDocument,
        HtmlCrawlStructuredJsonPreset structuredPreset) {
        if (structuredPreset == HtmlCrawlStructuredJsonPreset.None) {
            return HtmlCrawlStructuredJsonPreset.None;
        }

        if (structuredPreset != HtmlCrawlStructuredJsonPreset.Auto) {
            return structuredPreset;
        }

        if (LooksLikeProductPage(document, structuredJson)) {
            return HtmlCrawlStructuredJsonPreset.Product;
        }

        if (LooksLikeDocsPage(document, selectedDocument, structuredJson)) {
            return HtmlCrawlStructuredJsonPreset.Docs;
        }

        return HtmlCrawlStructuredJsonPreset.Article;
    }

    private static bool LooksLikeDocsPage(IDocument document, IDocument selectedDocument, HtmlCrawlStructuredJson structuredJson) {
        string url = structuredJson.Document.Url ?? string.Empty;
        string title = structuredJson.Metadata.Title ?? structuredJson.Document.Title ?? string.Empty;
        bool docsUrl = url.IndexOf("/docs", StringComparison.OrdinalIgnoreCase) >= 0
            || url.IndexOf("/documentation", StringComparison.OrdinalIgnoreCase) >= 0
            || url.IndexOf("/reference", StringComparison.OrdinalIgnoreCase) >= 0
            || url.IndexOf("/api", StringComparison.OrdinalIgnoreCase) >= 0
            || url.IndexOf("/manual", StringComparison.OrdinalIgnoreCase) >= 0
            || url.IndexOf("/guide", StringComparison.OrdinalIgnoreCase) >= 0;
        bool docsTitle = title.IndexOf("docs", StringComparison.OrdinalIgnoreCase) >= 0
            || title.IndexOf("documentation", StringComparison.OrdinalIgnoreCase) >= 0
            || title.IndexOf("reference", StringComparison.OrdinalIgnoreCase) >= 0
            || title.IndexOf("api", StringComparison.OrdinalIgnoreCase) >= 0;
        int codeBlockCount = selectedDocument.QuerySelectorAll("pre, code, samp, kbd").Length;
        int tocCount = document.QuerySelectorAll("[aria-label*='table of contents' i], nav.toc, .toc, .table-of-contents, [data-toc]").Length;
        bool strongNavigation = structuredJson.Layout.Regions.Any(region =>
            string.Equals(region.Kind, "Navigation", StringComparison.OrdinalIgnoreCase) &&
            region.LinkCount >= 4);
        return docsUrl || docsTitle || codeBlockCount > 0 || tocCount > 0 || (strongNavigation && structuredJson.Document.Headings.Count >= 2);
    }

    private static bool LooksLikeProductPage(IDocument document, HtmlCrawlStructuredJson structuredJson) {
        bool hasProductMicrodata = structuredJson.MicrodataItems.Any(item =>
            !string.IsNullOrWhiteSpace(item.Type) &&
            item.Type!.IndexOf("Product", StringComparison.OrdinalIgnoreCase) >= 0);
        bool hasPrice = document.QuerySelectorAll("[itemprop='price'], meta[property='product:price:amount'], .price, .product-price, [data-price], [class*='price']").Length > 0;
        bool hasSku = document.QuerySelectorAll("[itemprop='sku'], [data-sku], .sku, [class*='sku']").Length > 0;
        bool hasAvailability = document.QuerySelectorAll("[itemprop='availability'], .availability, [data-stock-status], [class*='stock']").Length > 0;
        bool hasCommerceAction = document.QuerySelectorAll("button, a")
            .Select(element => NormalizeWhitespace(element.TextContent))
            .Any(text =>
                text.IndexOf("add to cart", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("buy now", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("checkout", StringComparison.OrdinalIgnoreCase) >= 0);
        return hasProductMicrodata || (hasPrice && (hasSku || hasAvailability || hasCommerceAction));
    }

    private static PageSearchMetadata BuildPageSearchMetadata(HtmlCrawlPage page) {
        return BuildPageSearchMetadata(page.Html, page.Text, page.Markdown);
    }

    private static PageSearchMetadata BuildPageSearchMetadata(string html, string text, string markdown) {
        string sourceText = GetSearchText(text, markdown, html);
        string[] headings = ExtractHeadings(html);
        List<string> chunkTexts = BuildPageChunkTexts(sourceText);
        return new PageSearchMetadata {
            WordCount = CountWords(sourceText),
            CharacterCount = sourceText.Length,
            ChunkCount = chunkTexts.Count,
            Summary = BuildSummary(sourceText),
            Headings = headings,
            Keywords = ExtractKeywords(sourceText)
        };
    }

    internal static int GetChunkCountForSummary(IEnumerable<HtmlCrawlPage> pages) =>
        BuildChunkRecords(pages).Count;

    internal static int GetStructuredAuthenticatedApiEndpointCount(HtmlCrawlStructuredJson? structuredJson) =>
        structuredJson?.ApiEndpoints.Count(endpoint =>
            endpoint.Authentication.Required.HasValue
            || endpoint.Authentication.Schemes.Count > 0
            || endpoint.Authentication.Headers.Count > 0
            || !string.IsNullOrWhiteSpace(endpoint.Authentication.Summary)) ?? 0;

    internal static int GetStructuredRateLimitedApiEndpointCount(HtmlCrawlStructuredJson? structuredJson) =>
        structuredJson?.ApiEndpoints.Count(endpoint =>
            endpoint.RateLimit.Mentioned
            || endpoint.RateLimit.StatusCode.HasValue
            || endpoint.RateLimit.Headers.Count > 0
            || !string.IsNullOrWhiteSpace(endpoint.RateLimit.Limit)
            || !string.IsNullOrWhiteSpace(endpoint.RateLimit.Summary)) ?? 0;

    internal static int GetStructuredApiErrorResponseCount(HtmlCrawlStructuredJson? structuredJson) =>
        structuredJson?.ApiEndpoints.Sum(endpoint => endpoint.ErrorResponses.Count) ?? 0;

    private static (object GraphDocument, int NodeCount, int EdgeCount, int FetchedNodeCount, int SkippedNodeCount, int ExternalNodeCount, Dictionary<string, int> NodeCategories, Dictionary<string, int> EdgeRelations, Dictionary<string, int> SkippedNodeReasons) BuildGraphDocument(
        IEnumerable<HtmlCrawlPage> pages,
        IEnumerable<HtmlCrawlPage> skippedPages,
        string graphJsonPath) {
        List<HtmlCrawlPage> pageList = pages
            .Where(page => !string.IsNullOrWhiteSpace(page.Url))
            .OrderBy(page => page.Depth)
            .ThenBy(page => page.Url, StringComparer.OrdinalIgnoreCase)
            .ToList();
        List<HtmlCrawlPage> skippedPageList = skippedPages
            .Where(page => !string.IsNullOrWhiteSpace(page.Url))
            .OrderBy(page => page.Depth)
            .ThenBy(page => page.Url, StringComparer.OrdinalIgnoreCase)
            .ToList();
        List<HtmlCrawlPage> skippedGraphPages = skippedPageList
            .Where(page => !IsExternalSkipReason(page.SkipReason))
            .ToList();
        List<HtmlCrawlPage> externalGraphPages = skippedPageList
            .Where(page => IsExternalSkipReason(page.SkipReason))
            .ToList();
        HashSet<string> fetchedUrls = new(pageList.Select(page => page.Url), StringComparer.OrdinalIgnoreCase);
        HashSet<string> skippedUrls = new(skippedGraphPages.Select(page => page.Url), StringComparer.OrdinalIgnoreCase);
        HashSet<string> externalSkippedUrls = new(externalGraphPages.Select(page => page.Url), StringComparer.OrdinalIgnoreCase);
        HashSet<string> edgeKeys = new(StringComparer.OrdinalIgnoreCase);
        List<GraphEdgeRecord> edges = new();
        Dictionary<string, int> incomingTotal = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> outgoingTotal = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> outgoingInternal = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> externalUrls = new(StringComparer.OrdinalIgnoreCase);

        foreach (HtmlCrawlPage page in pageList) {
            IEnumerable<string> pageLinks = page.Links
                .Where(link => !string.IsNullOrWhiteSpace(link))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (string link in pageLinks) {
                string edgeKey = page.Url + "->" + link;
                if (!edgeKeys.Add(edgeKey)) {
                    continue;
                }

                bool targetKnown = fetchedUrls.Contains(link);
                bool targetSkipped = skippedUrls.Contains(link);
                bool targetExternal = externalSkippedUrls.Contains(link);
                string relation = targetKnown
                    ? "fetched"
                    : targetSkipped
                        ? "skipped"
                        : targetExternal
                            ? "external"
                            : "external";
                edges.Add(new GraphEdgeRecord {
                    SourceUrl = page.Url,
                    TargetUrl = link,
                    TargetKnown = targetKnown,
                    Internal = targetKnown,
                    Relation = relation
                });

                outgoingTotal[page.Url] = outgoingTotal.TryGetValue(page.Url, out int total) ? total + 1 : 1;
                incomingTotal[link] = incomingTotal.TryGetValue(link, out int incomingCount) ? incomingCount + 1 : 1;
                if (targetKnown) {
                    outgoingInternal[page.Url] = outgoingInternal.TryGetValue(page.Url, out int internalCount) ? internalCount + 1 : 1;
                } else if (!targetSkipped) {
                    externalUrls.Add(link);
                }
            }
        }

        List<GraphNodeRecord> nodes = pageList.Select(page => new GraphNodeRecord {
            Url = page.Url,
            Title = page.Title,
            Depth = page.Depth,
            Status = page.Status.ToString(),
            Category = "Fetched",
            OfflineReadinessGrade = page.OfflineReadinessGrade,
            HighestOfflineRiskSeverity = page.HighestOfflineRiskSeverity,
            OfflineDependencyDiagnosticCount = page.OfflineDependencyDiagnosticCount,
            OfflineDependencyKindsSummary = page.OfflineDependencyKindsSummary,
            InDegree = incomingTotal.TryGetValue(page.Url, out int inDegree) ? inDegree : 0,
            OutDegree = outgoingTotal.TryGetValue(page.Url, out int outDegree) ? outDegree : 0,
            InternalOutDegree = outgoingInternal.TryGetValue(page.Url, out int internalOutDegree) ? internalOutDegree : 0,
            HtmlPath = BuildRelativeOptionalPath(graphJsonPath, page.HtmlPath),
            ManifestPath = BuildRelativeOptionalPath(graphJsonPath, page.ManifestPath)
        }).ToList();

        nodes.AddRange(skippedGraphPages
            .Where(page => !fetchedUrls.Contains(page.Url))
            .Select(page => new GraphNodeRecord {
                Url = page.Url,
                Title = page.Title,
                Depth = page.Depth,
                Status = page.Status.ToString(),
                Category = "Skipped",
                SkipReason = page.SkipReason.ToString(),
                OfflineReadinessGrade = page.OfflineReadinessGrade,
                HighestOfflineRiskSeverity = page.HighestOfflineRiskSeverity,
                OfflineDependencyDiagnosticCount = page.OfflineDependencyDiagnosticCount,
                OfflineDependencyKindsSummary = page.OfflineDependencyKindsSummary,
                InDegree = incomingTotal.TryGetValue(page.Url, out int inDegree) ? inDegree : 0,
                OutDegree = 0,
                InternalOutDegree = 0
            }));

        nodes.AddRange(externalGraphPages
            .Where(page => !fetchedUrls.Contains(page.Url))
            .Select(page => new GraphNodeRecord {
                Url = page.Url,
                Title = page.Title,
                Depth = page.Depth,
                Status = page.Status.ToString(),
                Category = "External",
                SkipReason = page.SkipReason.ToString(),
                OfflineReadinessGrade = page.OfflineReadinessGrade,
                HighestOfflineRiskSeverity = page.HighestOfflineRiskSeverity,
                OfflineDependencyDiagnosticCount = page.OfflineDependencyDiagnosticCount,
                OfflineDependencyKindsSummary = page.OfflineDependencyKindsSummary,
                InDegree = incomingTotal.TryGetValue(page.Url, out int inDegree) ? inDegree : 0,
                OutDegree = 0,
                InternalOutDegree = 0
            }));

        nodes.AddRange(externalUrls
            .Where(url => !externalSkippedUrls.Contains(url))
            .OrderBy(url => url, StringComparer.OrdinalIgnoreCase)
            .Select(url => new GraphNodeRecord {
                Url = url,
                Depth = -1,
                Status = "Discovered",
                Category = "External",
                InDegree = incomingTotal.TryGetValue(url, out int inDegree) ? inDegree : 0,
                OutDegree = 0,
                InternalOutDegree = 0
            }));

        int fetchedNodeCount = nodes.Count(node => string.Equals(node.Category, "Fetched", StringComparison.OrdinalIgnoreCase));
        int skippedNodeCount = nodes.Count(node => string.Equals(node.Category, "Skipped", StringComparison.OrdinalIgnoreCase));
        int externalNodeCount = nodes.Count(node => string.Equals(node.Category, "External", StringComparison.OrdinalIgnoreCase));
        Dictionary<string, int> nodeCategories = nodes
            .GroupBy(node => node.Category, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> edgeRelations = edges
            .GroupBy(edge => edge.Relation, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> skippedNodeReasons = nodes
            .Where(node => string.Equals(node.Category, "Skipped", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(node.SkipReason))
            .GroupBy(node => node.SkipReason!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> offlineReadinessCounts = nodes
            .GroupBy(node => node.OfflineReadinessGrade, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> offlineSeverityCounts = nodes
            .GroupBy(node => node.HighestOfflineRiskSeverity, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> offlineDependencyKindCounts = nodes
            .SelectMany(node => string.IsNullOrWhiteSpace(node.OfflineDependencyKindsSummary)
                ? Array.Empty<string>()
                : node.OfflineDependencyKindsSummary.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(kind => kind.Trim())
                    .Where(kind => !string.IsNullOrWhiteSpace(kind)))
            .GroupBy(kind => kind, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        int offlineRiskNodeCount = nodes.Count(node => node.OfflineDependencyDiagnosticCount > 0);
        int highOfflineRiskNodeCount = nodes.Count(node => string.Equals(node.HighestOfflineRiskSeverity, "high", StringComparison.OrdinalIgnoreCase));

        object graphDocument = new {
            Summary = new {
                NodeCount = nodes.Count,
                EdgeCount = edges.Count,
                FetchedNodeCount = fetchedNodeCount,
                SkippedNodeCount = skippedNodeCount,
                ExternalNodeCount = externalNodeCount,
                OfflineRiskNodeCount = offlineRiskNodeCount,
                HighOfflineRiskNodeCount = highOfflineRiskNodeCount,
                OfflineReadinessCounts = offlineReadinessCounts,
                OfflineSeverityCounts = offlineSeverityCounts,
                OfflineDependencyKindCounts = offlineDependencyKindCounts,
                NodeCategories = nodeCategories,
                EdgeRelations = edgeRelations,
                SkippedNodeReasons = skippedNodeReasons
            },
            Nodes = nodes.OrderBy(node => node.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(node => node.Depth)
                .ThenBy(node => node.Url, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Edges = edges.OrderBy(edge => edge.SourceUrl, StringComparer.OrdinalIgnoreCase)
                .ThenBy(edge => edge.TargetUrl, StringComparer.OrdinalIgnoreCase)
                .Select(edge => new {
                    edge.SourceUrl,
                    edge.TargetUrl,
                    edge.TargetKnown,
                    edge.Internal,
                    edge.Relation
                })
                .ToArray()
        };
        return (graphDocument, nodes.Count, edges.Count, fetchedNodeCount, skippedNodeCount, externalNodeCount, nodeCategories, edgeRelations, skippedNodeReasons);
    }

    private static bool IsExternalSkipReason(HtmlCrawlSkipReason reason) =>
        reason == HtmlCrawlSkipReason.OutsideHost;

    private static List<PageChunkRecord> BuildChunkRecords(IEnumerable<HtmlCrawlPage> pages) {
        List<PageChunkRecord> chunks = new();
        HashSet<string> seenFingerprints = new(StringComparer.OrdinalIgnoreCase);
        int nextChunkId = 1;

        foreach (HtmlCrawlPage page in pages.Where(page => page.Status == HtmlCrawlPageStatus.Success)) {
            PageSearchMetadata searchMetadata = BuildPageSearchMetadata(page);
            List<string> pageChunks = BuildPageChunkTexts(page.Text, page.Markdown, page.Html);
            int chunkIndex = 1;
            foreach (string chunkText in pageChunks) {
                string fingerprint = ComputeContentFingerprint(chunkText);
                if (!seenFingerprints.Add(fingerprint)) {
                    continue;
                }

                chunks.Add(new PageChunkRecord {
                    ChunkId = $"chunk-{nextChunkId:D5}",
                    Url = page.Url,
                Title = page.Title,
                Depth = page.Depth,
                ChunkIndex = chunkIndex++,
                WordCount = CountWords(chunkText),
                CharacterCount = chunkText.Length,
                    Summary = BuildSummary(chunkText),
                    Headings = searchMetadata.Headings,
                    Keywords = ExtractKeywords(chunkText),
                Text = chunkText,
                HtmlPath = page.HtmlPath,
                TextPath = page.TextPath,
                ManifestPath = page.ManifestPath,
                OfflineReadinessGrade = page.OfflineReadinessGrade,
                HighestOfflineRiskSeverity = page.HighestOfflineRiskSeverity,
                OfflineDependencyDiagnosticCount = page.OfflineDependencyDiagnosticCount,
                OfflineDependencyKindsSummary = page.OfflineDependencyKindsSummary,
                Fingerprint = fingerprint
            });
                nextChunkId++;
            }
        }

        return chunks;
    }

    private static List<string> BuildPageChunkTexts(HtmlCrawlPage page) =>
        BuildPageChunkTexts(page.Text, page.Markdown, page.Html);

    private static List<string> BuildPageChunkTexts(string text, string markdown, string html) {
        string sourceText = GetSearchText(text, markdown, html);
        return BuildPageChunkTexts(sourceText);
    }

    private static List<string> BuildPageChunkTexts(string sourceText) {
        if (string.IsNullOrWhiteSpace(sourceText)) {
            return new List<string>();
        }

        const int targetWords = 140;
        const int overlapWords = 30;
        string[] words = sourceText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) {
            return new List<string>();
        }

        if (words.Length <= targetWords) {
            return new List<string> { sourceText };
        }

        List<string> chunks = new();
        int start = 0;
        while (start < words.Length) {
            int length = Math.Min(targetWords, words.Length - start);
            string chunk = string.Join(" ", words.Skip(start).Take(length)).Trim();
            if (!string.IsNullOrWhiteSpace(chunk)) {
                chunks.Add(chunk);
            }

            if (start + length >= words.Length) {
                break;
            }

            start += Math.Max(1, targetWords - overlapWords);
        }

        return chunks;
    }

    private static string GetSearchText(HtmlCrawlPage page) =>
        GetSearchText(page.Text, page.Markdown, page.Html);

    private static string GetSearchText(string text, string markdown, string html) {
        if (!string.IsNullOrWhiteSpace(text)) {
            return NormalizeWhitespace(text);
        }

        if (!string.IsNullOrWhiteSpace(markdown)) {
            return NormalizeWhitespace(markdown);
        }

        if (!string.IsNullOrWhiteSpace(html)) {
            return NormalizeWhitespace(HtmlParserToText.ConvertToText(html));
        }

        return string.Empty;
    }

    private static HtmlCrawlStructuredDocument BuildStructuredDocument(
        HtmlCrawlPage page,
        PageSearchMetadata searchMetadata,
        string text,
        string markdown) {
        return new HtmlCrawlStructuredDocument {
            Url = page.Url,
            RequestedUrl = page.RequestedUrl,
            ParentUrl = page.ParentUrl,
            CanonicalUrl = page.CanonicalUrl,
            Depth = page.Depth,
            Title = page.Title,
            ContentType = page.ContentType,
            Rendered = page.Rendered,
            RenderMode = page.RenderMode,
            RenderReasonCode = page.RenderReasonCode,
            ContentModeUsed = page.ContentModeUsed,
            ContentSelectionReasonCode = page.ContentSelectionReasonCode,
            ContentSelectionReason = page.ContentSelectionReason,
            ContentElementSelectorHint = page.ContentElementSelectorHint,
            WordCount = searchMetadata.WordCount,
            CharacterCount = searchMetadata.CharacterCount,
            ChunkCount = searchMetadata.ChunkCount,
            LinkCount = page.Links.Count,
            AssetCount = page.AssetUrls.Count,
            InteractionCount = page.AppliedInteractions.Count,
            Summary = searchMetadata.Summary,
            Headings = new List<string>(searchMetadata.Headings),
            Keywords = new List<string>(searchMetadata.Keywords),
            Links = page.Links
                .Where(link => !string.IsNullOrWhiteSpace(link))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(link => link, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            AppliedInteractions = new List<string>(page.AppliedInteractions),
            Text = text,
            Markdown = markdown
        };
    }

    private static HtmlCrawlStructuredMetadata BuildStructuredMetadata(
        HtmlCrawlPage page,
        IDocument document,
        IReadOnlyCollection<HtmlMetaTag> metaTags,
        HtmlOpenGraph openGraph) {
        return new HtmlCrawlStructuredMetadata {
            Title = page.Title ?? FindOpenGraphValue(openGraph, "title"),
            Description = FindMetaContent(metaTags, "description")
                ?? FindOpenGraphValue(openGraph, "description"),
            CanonicalUrl = page.CanonicalUrl,
            Language = document.DocumentElement?.GetAttribute("lang"),
            SiteName = FindOpenGraphValue(openGraph, "site_name")
                ?? FindMetaContent(metaTags, "application-name"),
            Type = FindOpenGraphValue(openGraph, "type"),
            Author = FindMetaContent(metaTags, "author", "article:author"),
            PublishedTime = FindMetaContent(metaTags, "article:published_time", "published_time", "pubdate"),
            ModifiedTime = FindMetaContent(metaTags, "article:modified_time", "last-modified", "modified_time"),
            Robots = FindMetaContent(metaTags, "robots"),
            Generator = FindMetaContent(metaTags, "generator"),
            ImageUrl = FindOpenGraphValue(openGraph, "image"),
            Keywords = SplitMetadataKeywords(FindMetaContent(metaTags, "keywords")),
            MetaTagCount = metaTags.Count,
            OpenGraphPropertyCount = openGraph.Properties.Count
        };
    }

    private static HtmlCrawlStructuredLayout BuildStructuredLayout(IDocument document) {
        List<HtmlCrawlStructuredRegion> regions = new();
        HashSet<IElement> seenElements = new();

        foreach ((string kind, string selector) in StructuredRegionSelectors) {
            IEnumerable<IElement> matches = document.QuerySelectorAll(selector)
                .Where(element => element != null && seenElements.Add(element))
                .Where(element => !string.Equals(kind, "Navigation", StringComparison.OrdinalIgnoreCase) || !LooksLikeBreadcrumbElement(element))
                .Where(ShouldIncludeStructuredRegion)
                .OrderByDescending(element => CountWords(element.TextContent))
                .ThenBy(element => BuildElementSelectorHint(element), StringComparer.OrdinalIgnoreCase)
                .Take(4);

            foreach (IElement element in matches) {
                regions.Add(BuildStructuredRegion(kind, element));
            }
        }

        return new HtmlCrawlStructuredLayout {
            Regions = regions
                .OrderBy(region => GetStructuredRegionKindOrder(region.Kind))
                .ThenByDescending(region => region.WordCount)
                .ThenBy(region => region.SelectorHint, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            HeaderCount = regions.Count(region => string.Equals(region.Kind, "Header", StringComparison.OrdinalIgnoreCase)),
            NavigationCount = regions.Count(region => string.Equals(region.Kind, "Navigation", StringComparison.OrdinalIgnoreCase)),
            MainCount = regions.Count(region => string.Equals(region.Kind, "Main", StringComparison.OrdinalIgnoreCase)),
            ArticleCount = regions.Count(region => string.Equals(region.Kind, "Article", StringComparison.OrdinalIgnoreCase)),
            AsideCount = regions.Count(region => string.Equals(region.Kind, "Aside", StringComparison.OrdinalIgnoreCase)),
            FooterCount = regions.Count(region => string.Equals(region.Kind, "Footer", StringComparison.OrdinalIgnoreCase))
        };
    }

    private static HtmlCrawlStructuredRegion BuildStructuredRegion(string kind, IElement element) {
        string text = NormalizeWhitespace(element.TextContent);
        return new HtmlCrawlStructuredRegion {
            Kind = kind,
            Tag = element.LocalName,
            Id = string.IsNullOrWhiteSpace(element.Id) ? null : element.Id,
            Classes = GetElementClassNames(element),
            SelectorHint = BuildElementSelectorHint(element),
            Role = element.GetAttribute("role"),
            AriaLabel = element.GetAttribute("aria-label"),
            Summary = BuildSummary(text),
            WordCount = CountWords(text),
            LinkCount = element.QuerySelectorAll("a[href]").Length,
            HeadingCount = element.QuerySelectorAll("h1, h2, h3, h4, h5, h6").Length,
            LinkLabels = element.QuerySelectorAll("a[href]")
                .Select(anchor => NormalizeWhitespace(anchor.TextContent))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .Take(12)
                .ToList(),
            IsLikelyBoilerplate = ContainsBoilerplateSignals(element) || IsLinkDenseBoilerplateBlock(element)
        };
    }

    private static bool ShouldIncludeStructuredRegion(IElement element) {
        if (element == null) {
            return false;
        }

        string text = NormalizeWhitespace(element.TextContent);
        int wordCount = CountWords(text);
        int linkCount = element.QuerySelectorAll("a[href]").Length;
        int headingCount = element.QuerySelectorAll("h1, h2, h3, h4, h5, h6").Length;
        return wordCount > 0 || linkCount > 0 || headingCount > 0;
    }

    private static bool LooksLikeBreadcrumbElement(IElement element) {
        if (element == null) {
            return false;
        }

        string selectorHint = BuildElementSelectorHint(element) ?? string.Empty;
        string ariaLabel = element.GetAttribute("aria-label") ?? string.Empty;
        string itemType = element.GetAttribute("itemtype") ?? string.Empty;
        return selectorHint.IndexOf("breadcrumb", StringComparison.OrdinalIgnoreCase) >= 0
            || ariaLabel.IndexOf("breadcrumb", StringComparison.OrdinalIgnoreCase) >= 0
            || itemType.IndexOf("BreadcrumbList", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int GetStructuredRegionKindOrder(string? kind) {
        return kind?.ToLowerInvariant() switch {
            "header" => 0,
            "navigation" => 1,
            "main" => 2,
            "article" => 3,
            "aside" => 4,
            "footer" => 5,
            _ => 6
        };
    }

    private static List<HtmlCrawlStructuredCodeBlock> BuildStructuredCodeBlocks(IDocument selectedDocument) {
        List<HtmlCrawlStructuredCodeBlock> blocks = new();
        HashSet<IElement> seen = new();
        IEnumerable<IElement> elements = selectedDocument.QuerySelectorAll("pre")
            .Concat(selectedDocument.QuerySelectorAll("code[class*='language'], code[class*='lang-'], code[data-language], code[lang], code[type], samp, kbd")
                .Where(element => element.Closest("pre") == null));

        foreach (IElement element in elements) {
            if (element == null || !seen.Add(element)) {
                continue;
            }

            IElement sourceElement = string.Equals(element.LocalName, "pre", StringComparison.OrdinalIgnoreCase)
                ? element.QuerySelector("code") ?? element
                : element;
            string code = NormalizeCodeBlockText(sourceElement.TextContent);
            if (string.IsNullOrWhiteSpace(code)) {
                continue;
            }

            string[] lines = code.Split(new[] { '\n' }, StringSplitOptions.None);
            blocks.Add(new HtmlCrawlStructuredCodeBlock {
                Language = DetectCodeBlockLanguage(sourceElement),
                Code = code,
                LineCount = lines.Length,
                CharacterCount = code.Length,
                SelectorHint = BuildElementSelectorHint(element)
            });
        }

        return blocks;
    }

    private static List<HtmlCrawlStructuredCodeSample> BuildStructuredCodeSamples(IDocument selectedDocument) {
        List<HtmlCrawlStructuredCodeSample> samples = new();
        HashSet<IElement> seen = new();
        foreach (IElement element in selectedDocument.QuerySelectorAll("pre")
                     .Concat(selectedDocument.QuerySelectorAll("code[class*='language'], code[class*='lang-'], code[data-language], code[lang], code[type], samp, kbd")
                         .Where(item => item.Closest("pre") == null))) {
            if (element == null || !seen.Add(element)) {
                continue;
            }

            IElement sourceElement = string.Equals(element.LocalName, "pre", StringComparison.OrdinalIgnoreCase)
                ? element.QuerySelector("code") ?? element
                : element;
            string code = NormalizeCodeBlockText(sourceElement.TextContent);
            if (string.IsNullOrWhiteSpace(code)) {
                continue;
            }

            string? heading = FindNearbyHeadingText(element);
            string? language = DetectCodeBlockLanguage(sourceElement);
            string kind = DetectStructuredCodeSampleKind(code, language);
            string? method = null;
            string? path = null;
            TryParseApiMethodAndPath(code, out method, out path);
            if (string.IsNullOrWhiteSpace(method)
                && string.IsNullOrWhiteSpace(path)
                && LooksLikeRequestPayloadHeading(heading)) {
                string? apiHeading = FindNearbyApiHeadingText(element);
                if (!string.IsNullOrWhiteSpace(apiHeading)) {
                    TryParseApiMethodAndPath(apiHeading!, out method, out path);
                }
            }

            samples.Add(new HtmlCrawlStructuredCodeSample {
                Title = BuildStructuredCodeSampleTitle(heading, kind, method, path, language),
                Heading = heading,
                Language = language,
                Kind = kind,
                Code = code,
                Method = method,
                Path = path,
                SelectorHint = BuildElementSelectorHint(element)
            });
        }

        return samples;
    }

    private static List<HtmlCrawlStructuredBreadcrumbTrail> BuildStructuredBreadcrumbs(IDocument document, string? pageUrl) {
        List<HtmlCrawlStructuredBreadcrumbTrail> trails = new();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        Uri? baseUri = Uri.TryCreate(pageUrl, UriKind.Absolute, out Uri? resolvedBaseUri) ? resolvedBaseUri : null;

        foreach (IElement container in document.QuerySelectorAll("[aria-label*='breadcrumb' i], nav.breadcrumb, .breadcrumb, .breadcrumbs, [data-breadcrumb], [data-breadcrumbs], ol[itemtype*='BreadcrumbList' i], ul[itemtype*='BreadcrumbList' i]")) {
            HtmlCrawlStructuredBreadcrumbTrail? trail = BuildStructuredBreadcrumbTrail(container, baseUri);
            if (trail == null || trail.Items.Count == 0) {
                continue;
            }

            string key = string.Join(">", trail.Labels);
            if (!seen.Add(key)) {
                continue;
            }

            trails.Add(trail);
        }

        return trails;
    }

    private static HtmlCrawlStructuredBreadcrumbTrail? BuildStructuredBreadcrumbTrail(IElement container, Uri? baseUri) {
        IEnumerable<IElement> candidates = container.QuerySelectorAll("li").Length > 0
            ? container.QuerySelectorAll("li")
            : container.QuerySelectorAll("a[href], [aria-current='page'], span[itemprop='name'], strong, span");

        List<HtmlCrawlStructuredBreadcrumbItem> items = new();
        foreach (IElement candidate in candidates) {
            IElement? anchor = string.Equals(candidate.LocalName, "a", StringComparison.OrdinalIgnoreCase)
                ? candidate
                : candidate.QuerySelector("a[href]");
            string label = NormalizeWhitespace(anchor?.TextContent ?? candidate.TextContent);
            if (string.IsNullOrWhiteSpace(label)) {
                continue;
            }

            if (items.Count > 0 && string.Equals(items[items.Count - 1].Label, label, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            string? url = TryResolveStructuredHref(baseUri, anchor?.GetAttribute("href"));
            bool isCurrent = string.Equals(candidate.GetAttribute("aria-current"), "page", StringComparison.OrdinalIgnoreCase)
                || (anchor == null && candidates.Count() > 1)
                || (string.IsNullOrWhiteSpace(url) && items.Count > 0);
            items.Add(new HtmlCrawlStructuredBreadcrumbItem {
                Label = label,
                Url = url,
                IsCurrent = isCurrent
            });
        }

        if (items.Count < 2) {
            return null;
        }

        return new HtmlCrawlStructuredBreadcrumbTrail {
            Items = items,
            Labels = items.Select(item => item.Label).ToList(),
            Urls = items.Where(item => !string.IsNullOrWhiteSpace(item.Url))
                .Select(item => item.Url!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            CurrentLabel = items.LastOrDefault(item => item.IsCurrent)?.Label ?? items[items.Count - 1].Label,
            SelectorHint = BuildElementSelectorHint(container)
        };
    }

    private static List<HtmlCrawlStructuredFaqItem> BuildStructuredFaqItems(IDocument selectedDocument) {
        List<HtmlCrawlStructuredFaqItem> items = new();
        HashSet<string> seenQuestions = new(StringComparer.OrdinalIgnoreCase);

        foreach (IElement questionElement in selectedDocument.QuerySelectorAll("[itemscope][itemtype*='Question' i], [itemprop='mainEntity'][itemscope]")) {
            string question = NormalizeWhitespace(questionElement.QuerySelector("[itemprop='name']")?.TextContent);
            IElement? answerElement = questionElement.QuerySelector("[itemprop='acceptedAnswer'] [itemprop='text'], [itemprop='acceptedAnswer']");
            string answer = NormalizeWhitespace(answerElement?.TextContent);
            string answerMarkdown = answerElement == null ? string.Empty : ConvertSelectedHtmlToMarkdown(answerElement.InnerHtml, null);
            AddStructuredFaqItem(items, seenQuestions, question, answer, answerMarkdown, "Microdata", questionElement);
        }

        foreach (IElement details in selectedDocument.QuerySelectorAll("details")) {
            string question = NormalizeWhitespace(details.QuerySelector("summary")?.TextContent);
            IElement clone = (IElement)details.Clone(true);
            foreach (IElement summary in clone.QuerySelectorAll("summary").ToArray()) {
                summary.Remove();
            }

            string answer = NormalizeWhitespace(clone.TextContent);
            string answerMarkdown = string.IsNullOrWhiteSpace(clone.InnerHtml) ? string.Empty : ConvertSelectedHtmlToMarkdown(clone.InnerHtml, null);
            AddStructuredFaqItem(items, seenQuestions, question, answer, answerMarkdown, "Details", details);
        }

        return items;
    }

    private static void AddStructuredFaqItem(
        IList<HtmlCrawlStructuredFaqItem> items,
        ISet<string> seenQuestions,
        string question,
        string answer,
        string answerMarkdown,
        string source,
        IElement element) {
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer) || !seenQuestions.Add(question)) {
            return;
        }

        items.Add(new HtmlCrawlStructuredFaqItem {
            Question = question,
            Answer = answer,
            AnswerMarkdown = answerMarkdown,
            Source = source,
            SelectorHint = BuildElementSelectorHint(element)
        });
    }

    private static List<HtmlCrawlStructuredSpecTable> BuildStructuredSpecTables(IDocument selectedDocument, IReadOnlyList<HtmlTableResult> tables) {
        List<HtmlCrawlStructuredSpecTable> specTables = new();
        IHtmlCollection<IElement> tableElements = selectedDocument.QuerySelectorAll("table");
        for (int index = 0; index < Math.Min(tableElements.Length, tables.Count); index++) {
            IElement tableElement = tableElements[index];
            HtmlTableResult table = tables[index];
            if (!LooksLikeStructuredSpecTable(tableElement, table)) {
                continue;
            }

            string? title = NormalizeWhitespace(tableElement.QuerySelector("caption")?.TextContent);
            if (string.IsNullOrWhiteSpace(title)) {
                title = FindNearbyHeadingText(tableElement);
            }

            List<HtmlCrawlStructuredSpecItem> entries = BuildStructuredSpecEntries(table);
            if (entries.Count == 0) {
                continue;
            }

            HtmlCrawlStructuredSpecTable specTable = new() {
                TableIndex = index,
                Title = title,
                Headers = new List<string>(table.Metadata.Headers),
                Entries = entries,
                SelectorHint = BuildElementSelectorHint(tableElement)
            };
            foreach (HtmlCrawlStructuredSpecItem entry in entries) {
                if (!specTable.Properties.ContainsKey(entry.Name)) {
                    specTable.Properties[entry.Name] = entry.Value;
                }
            }

            specTables.Add(specTable);
        }

        return specTables;
    }

    private static List<HtmlCrawlStructuredSpecItem> BuildStructuredSpecEntries(HtmlTableResult table) {
        List<HtmlCrawlStructuredSpecItem> entries = new();
        foreach (Dictionary<string, string?> row in table.Data) {
            List<KeyValuePair<string, string?>> populated = row
                .Where(item => !string.IsNullOrWhiteSpace(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
                .ToList();
            if (populated.Count < 2) {
                continue;
            }

            HtmlCrawlStructuredSpecItem item = new() {
                Name = NormalizeWhitespace(populated[0].Value ?? populated[0].Key),
                Value = NormalizeWhitespace(populated[1].Value)
            };
            if (string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.Value)) {
                continue;
            }

            entries.Add(item);
        }

        return entries;
    }

    private static bool LooksLikeStructuredSpecTable(IElement tableElement, HtmlTableResult table) {
        string selectorHint = BuildElementSelectorHint(tableElement) ?? string.Empty;
        string classes = table.Metadata.Classes ?? string.Empty;
        string headers = string.Join(" ", table.Metadata.Headers);
        string nearbyHeading = FindNearbyHeadingText(tableElement) ?? string.Empty;
        bool looksLikeParameterTable = ContainsAnyToken(headers, "parameter", "required", "description", "default", "location")
            || ContainsAnyToken(nearbyHeading, "parameter", "request body", "query parameter", "path parameter", "header");
        if (looksLikeParameterTable) {
            return false;
        }

        bool classSignal = ContainsAnyToken(selectorHint, "spec", "feature", "attribute", "property", "parameter", "option", "config")
            || ContainsAnyToken(classes, "spec", "feature", "attribute", "property", "parameter", "option", "config");
        bool headerSignal = ContainsAnyToken(headers, "name", "property", "attribute", "setting", "option", "parameter", "value", "description", "details");
        bool twoColumn = table.Metadata.ColumnCount > 0 && table.Metadata.ColumnCount <= 2;
        bool rowShapeLooksLikePairs = table.Data.Count > 0 && table.Data.All(row => row.Count(item => !string.IsNullOrWhiteSpace(item.Value)) <= 2);
        return classSignal || (twoColumn && (headerSignal || rowShapeLooksLikePairs));
    }

    private static List<HtmlCrawlStructuredCallout> BuildStructuredCallouts(IDocument selectedDocument) {
        List<HtmlCrawlStructuredCallout> callouts = new();
        HashSet<IElement> seen = new();
        foreach (IElement element in selectedDocument.QuerySelectorAll("aside, blockquote, div, section").Where(LooksLikeStructuredCalloutElement)) {
            if (element == null || !seen.Add(element) || element.Closest("details") != null) {
                continue;
            }

            string text = NormalizeWhitespace(element.TextContent);
            if (string.IsNullOrWhiteSpace(text)) {
                continue;
            }

            string? title = NormalizeWhitespace(element.QuerySelector("strong, b, h1, h2, h3, h4, h5, h6, .title, .heading")?.TextContent);
            callouts.Add(new HtmlCrawlStructuredCallout {
                Kind = DetectStructuredCalloutKind(element),
                Title = title,
                Text = text,
                Markdown = string.IsNullOrWhiteSpace(element.InnerHtml) ? string.Empty : ConvertSelectedHtmlToMarkdown(element.InnerHtml, null),
                SelectorHint = BuildElementSelectorHint(element)
            });
        }

        return callouts;
    }

    private static bool LooksLikeStructuredCalloutElement(IElement element) {
        if (element == null) {
            return false;
        }

        string role = element.GetAttribute("role") ?? string.Empty;
        string hint = BuildElementSelectorHint(element) ?? string.Empty;
        return role.Equals("alert", StringComparison.OrdinalIgnoreCase)
            || role.Equals("note", StringComparison.OrdinalIgnoreCase)
            || role.Equals("status", StringComparison.OrdinalIgnoreCase)
            || ContainsAnyToken(hint, "note", "warning", "warn", "tip", "info", "danger", "error", "success", "important", "callout", "admonition", "caution");
    }

    private static string DetectStructuredCalloutKind(IElement element) {
        string hint = BuildElementSelectorHint(element) ?? string.Empty;
        string role = element.GetAttribute("role") ?? string.Empty;
        if (ContainsAnyToken(hint, "warning", "warn", "caution")) {
            return "warning";
        }
        if (ContainsAnyToken(hint, "danger", "error")) {
            return "danger";
        }
        if (ContainsAnyToken(hint, "tip", "success")) {
            return "tip";
        }
        if (ContainsAnyToken(hint, "important")) {
            return "important";
        }
        if (role.Equals("alert", StringComparison.OrdinalIgnoreCase)) {
            return "warning";
        }
        return "note";
    }

    private static List<HtmlCrawlStructuredPrimaryAction> BuildStructuredPrimaryActions(IDocument selectedDocument, string? pageUrl) {
        List<(int Score, HtmlCrawlStructuredPrimaryAction Action)> scored = new();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        Uri? baseUri = Uri.TryCreate(pageUrl, UriKind.Absolute, out Uri? baseUriResolved) ? baseUriResolved : null;

        foreach (IElement element in selectedDocument.QuerySelectorAll("a[href], button, input[type='submit'], input[type='button'], [role='button']")) {
            string label = GetStructuredActionLabel(element);
            if (string.IsNullOrWhiteSpace(label)) {
                continue;
            }

            int score = ScoreStructuredPrimaryAction(element, label);
            if (score <= 0) {
                continue;
            }

            string? url = string.Equals(element.LocalName, "a", StringComparison.OrdinalIgnoreCase)
                ? TryResolveStructuredHref(baseUri, element.GetAttribute("href"))
                : null;
            string key = $"{label}|{url}|{element.LocalName}";
            if (!seen.Add(key)) {
                continue;
            }

            scored.Add((score, new HtmlCrawlStructuredPrimaryAction {
                Label = label,
                Url = url,
                Type = DetectStructuredActionType(element),
                Intent = DetectStructuredActionIntent(label, element),
                SelectorHint = BuildElementSelectorHint(element)
            }));
        }

        return scored
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Action.Label, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Action)
            .Take(8)
            .ToList();
    }

    private static string GetStructuredActionLabel(IElement element) {
        string? inputValue = element.GetAttribute("value");
        return NormalizeWhitespace(inputValue ?? element.TextContent);
    }

    private static int ScoreStructuredPrimaryAction(IElement element, string label) {
        string hint = BuildElementSelectorHint(element) ?? string.Empty;
        int score = 0;
        if (ContainsAnyToken(hint, "primary", "cta", "button", "btn", "action")) {
            score += 3;
        }
        if (element.GetAttribute("role")?.Equals("button", StringComparison.OrdinalIgnoreCase) == true
            || string.Equals(element.LocalName, "button", StringComparison.OrdinalIgnoreCase)) {
            score += 2;
        }
        if (ContainsAnyToken(label, "install", "download", "start", "get started", "try", "buy", "add to cart", "checkout", "contact sales", "sign up", "create account")) {
            score += 4;
        }
        if (label.Length <= 40) {
            score += 1;
        }
        if (ContainsAnyToken(label, "learn more", "read more", "home", "docs", "privacy")) {
            score -= 2;
        }
        if (element.Closest("nav, header, footer") != null) {
            score -= 2;
        }
        return score;
    }

    private static string DetectStructuredActionType(IElement element) {
        if (string.Equals(element.LocalName, "a", StringComparison.OrdinalIgnoreCase)) {
            return "link";
        }
        if (string.Equals(element.LocalName, "button", StringComparison.OrdinalIgnoreCase)) {
            return "button";
        }

        return string.Equals(element.GetAttribute("type"), "submit", StringComparison.OrdinalIgnoreCase)
            ? "submit"
            : "button";
    }

    private static string DetectStructuredActionIntent(string label, IElement element) {
        if (ContainsAnyToken(label, "install")) {
            return "install";
        }
        if (ContainsAnyToken(label, "download")) {
            return "download";
        }
        if (ContainsAnyToken(label, "buy", "add to cart", "checkout")) {
            return "buy";
        }
        if (ContainsAnyToken(label, "start", "get started", "try")) {
            return "start";
        }
        if (ContainsAnyToken(label, "contact sales")) {
            return "contact-sales";
        }
        return string.Equals(element.LocalName, "a", StringComparison.OrdinalIgnoreCase) ? "navigate" : "action";
    }

    private static List<HtmlCrawlStructuredApiEndpoint> BuildStructuredApiEndpoints(
        IDocument selectedDocument,
        IReadOnlyList<HtmlCrawlStructuredCodeSample> codeSamples,
        string pageUrl) {
        Dictionary<string, HtmlCrawlStructuredApiEndpoint> endpoints = new(StringComparer.OrdinalIgnoreCase);

        foreach (IElement heading in selectedDocument.QuerySelectorAll("h1, h2, h3, h4, h5, h6")) {
            string text = NormalizeWhitespace(heading.TextContent);
            if (!TryParseApiMethodAndPath(text, out string? method, out string? path)) {
                continue;
            }

            HtmlCrawlStructuredApiEndpoint endpoint = GetOrCreateStructuredApiEndpoint(endpoints, method!, path!);
            endpoint.Title ??= text;
            endpoint.Description ??= FindFollowingParagraphText(heading);
            endpoint.SelectorHint ??= BuildElementSelectorHint(heading);
            AppendDistinct(endpoint.Sources, "Heading");

            IDocument sectionDocument = BuildStructuredSectionDocument(heading);
            List<HtmlCrawlStructuredCodeSample> sectionCodeSamples = BuildStructuredCodeSamples(sectionDocument);
            foreach (HtmlCrawlStructuredApiParameter parameter in BuildStructuredApiParameters(sectionDocument)) {
                if (!endpoint.Parameters.Any(existing =>
                        string.Equals(existing.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(existing.Location, parameter.Location, StringComparison.OrdinalIgnoreCase))) {
                    endpoint.Parameters.Add(parameter);
                }
            }

            foreach (HtmlCrawlStructuredRequestExample requestExample in BuildStructuredRequestExamples(sectionCodeSamples)) {
                if (!endpoint.RequestExamples.Any(existing =>
                        string.Equals(existing.Method, requestExample.Method, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(existing.Path, requestExample.Path, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(existing.Body, requestExample.Body, StringComparison.Ordinal)
                        && string.Equals(existing.Title, requestExample.Title, StringComparison.OrdinalIgnoreCase))) {
                    endpoint.RequestExamples.Add(requestExample);
                }
            }

            foreach (HtmlCrawlStructuredResponseExample responseExample in BuildStructuredResponseExamples(sectionDocument, sectionCodeSamples, pageUrl)) {
                if (!endpoint.ResponseExamples.Any(existing =>
                        string.Equals(existing.Body, responseExample.Body, StringComparison.Ordinal)
                        && string.Equals(existing.Title, responseExample.Title, StringComparison.OrdinalIgnoreCase)
                        && existing.StatusCode == responseExample.StatusCode)) {
                    endpoint.ResponseExamples.Add(responseExample);
                }
            }

            MergeStructuredApiAuthentication(endpoint.Authentication, BuildStructuredApiAuthentication(sectionDocument, sectionCodeSamples, endpoint.Parameters));
            MergeStructuredApiRateLimit(endpoint.RateLimit, BuildStructuredApiRateLimit(sectionDocument, sectionCodeSamples, endpoint.ResponseExamples));
        }

        foreach (HtmlCrawlStructuredCodeSample sample in codeSamples.Where(sample => !string.IsNullOrWhiteSpace(sample.Method) && !string.IsNullOrWhiteSpace(sample.Path))) {
            HtmlCrawlStructuredApiEndpoint endpoint = GetOrCreateStructuredApiEndpoint(endpoints, sample.Method!, sample.Path!);
            endpoint.Title ??= sample.Title;
            endpoint.SelectorHint ??= sample.SelectorHint;
            if (!string.IsNullOrWhiteSpace(sample.Language)) {
                AppendDistinct(endpoint.ExampleLanguages, sample.Language!);
            }
            AppendDistinct(endpoint.Sources, "CodeSample");

            HtmlCrawlStructuredRequestExample? requestExample = BuildStructuredRequestExample(sample);
            if (requestExample != null
                && !endpoint.RequestExamples.Any(existing =>
                    string.Equals(existing.Method, requestExample.Method, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.Path, requestExample.Path, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.Body, requestExample.Body, StringComparison.Ordinal)
                    && string.Equals(existing.Title, requestExample.Title, StringComparison.OrdinalIgnoreCase))) {
                endpoint.RequestExamples.Add(requestExample);
            }
        }

        foreach (HtmlCrawlStructuredApiEndpoint endpoint in endpoints.Values) {
            endpoint.Resource ??= BuildStructuredApiPrimaryResource(endpoint.Path);
            foreach (string tag in BuildStructuredApiTags(endpoint.Path, endpoint.Title, endpoint.Description)) {
                AppendDistinct(endpoint.Tags, tag);
            }
            endpoint.OperationId ??= BuildStructuredApiOperationId(endpoint.Method, endpoint.Path, endpoint.Title);
            ApplyStructuredApiParameterGrouping(endpoint, pageUrl);
            if (!endpoint.Authentication.Required.HasValue
                && (endpoint.Authentication.Schemes.Count > 0 || endpoint.Authentication.Headers.Count > 0)) {
                endpoint.Authentication.Required = true;
            }
            endpoint.RequestHeaders = BuildStructuredEndpointRequestHeaders(endpoint);
            endpoint.ResponseHeaders = BuildStructuredEndpointResponseHeaders(endpoint);
            endpoint.ErrorResponses = endpoint.ResponseExamples
                .Where(response => response.IsError)
                .OrderBy(response => response.StatusCode ?? int.MaxValue)
                .ThenBy(response => response.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
            endpoint.ErrorCatalog = BuildStructuredEndpointErrorCatalog(endpoint.ErrorResponses);
            endpoint.SuccessResponseSchema = BuildStructuredEndpointResponseSchema(endpoint.ResponseExamples.Where(response => !response.IsError));
            endpoint.ErrorResponseSchema = BuildStructuredEndpointResponseSchema(endpoint.ErrorResponses);
            endpoint.SuccessResponseFields = BuildStructuredEndpointResponseFields(endpoint.ResponseExamples.Where(response => !response.IsError));
            endpoint.ErrorResponseFields = BuildStructuredEndpointResponseFields(endpoint.ErrorResponses);
        }

        return endpoints.Values
            .OrderBy(endpoint => endpoint.Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(endpoint => endpoint.Method, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HtmlCrawlStructuredApiCatalog BuildStructuredApiCatalog(
        HtmlCrawlStructuredMetadata metadata,
        IReadOnlyList<HtmlCrawlStructuredApiEndpoint> endpoints) {
        List<HtmlCrawlStructuredApiEndpoint> endpointList = endpoints
            .Where(endpoint => !string.IsNullOrWhiteSpace(endpoint.Method) && !string.IsNullOrWhiteSpace(endpoint.Path))
            .ToList();
        return new HtmlCrawlStructuredApiCatalog {
            Title = metadata.Title,
            Description = metadata.Description,
            OperationCount = endpointList.Count,
            PathCount = endpointList.Select(endpoint => endpoint.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            AuthenticatedOperationCount = endpointList.Count(endpoint =>
                endpoint.Authentication.Required == true
                || endpoint.Authentication.Schemes.Count > 0
                || endpoint.Authentication.Headers.Count > 0),
            RateLimitedOperationCount = endpointList.Count(endpoint =>
                endpoint.RateLimit.Mentioned
                || endpoint.RateLimit.StatusCode.HasValue
                || endpoint.RateLimit.Headers.Count > 0
                || !string.IsNullOrWhiteSpace(endpoint.RateLimit.Limit)),
            ErrorCatalogCount = endpointList.Sum(endpoint => endpoint.ErrorCatalog.Count),
            Resources = endpointList.Select(endpoint => endpoint.Resource)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToList(),
            Tags = endpointList.SelectMany(endpoint => endpoint.Tags)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            OperationIds = endpointList.Select(endpoint => endpoint.OperationId)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToList(),
            Paths = endpointList.Select(endpoint => endpoint.Path)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static HtmlCrawlStructuredOpenApiLike BuildStructuredOpenApiLike(
        HtmlCrawlPage page,
        HtmlCrawlStructuredMetadata metadata,
        IReadOnlyList<HtmlCrawlStructuredApiEndpoint> endpoints) {
        Dictionary<string, HtmlCrawlStructuredOpenApiPathItem> paths = new(StringComparer.OrdinalIgnoreCase);
        foreach (HtmlCrawlStructuredApiEndpoint endpoint in endpoints
                     .Where(endpoint => !string.IsNullOrWhiteSpace(endpoint.Method) && !string.IsNullOrWhiteSpace(endpoint.Path))
                     .OrderBy(endpoint => endpoint.Path, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(endpoint => endpoint.Method, StringComparer.OrdinalIgnoreCase)) {
            if (!paths.TryGetValue(endpoint.Path, out HtmlCrawlStructuredOpenApiPathItem? pathItem)) {
                pathItem = new HtmlCrawlStructuredOpenApiPathItem {
                    Path = endpoint.Path
                };
                paths[endpoint.Path] = pathItem;
            }

            if (!string.IsNullOrWhiteSpace(endpoint.Resource)) {
                AppendDistinct(pathItem.Resources, endpoint.Resource!);
            }

            pathItem.Operations[endpoint.Method.ToLowerInvariant()] = BuildStructuredOpenApiOperation(endpoint, page.Url);
        }

        HtmlCrawlStructuredOpenApiLike openApiLike = new() {
            Title = metadata.Title,
            Description = metadata.Description,
            Servers = BuildStructuredOpenApiServers(page, metadata),
            Tags = endpoints.SelectMany(endpoint => endpoint.Tags)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Resources = endpoints.Select(endpoint => endpoint.Resource)
                .Where(resource => !string.IsNullOrWhiteSpace(resource))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(resource => resource, StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToList(),
            Paths = paths
        };
        ApplyStructuredOpenApiComponents(openApiLike);
        AnnotateStructuredOpenApiPromotion(openApiLike);
        return openApiLike;
    }

    private static HtmlCrawlStructuredOpenApiLike BuildResultOpenApiLike(HtmlCrawlResult result) {
        List<(HtmlCrawlPage Page, HtmlCrawlStructuredApiEndpoint Endpoint)> endpointEntries = result.Pages
            .Where(page => page.StructuredJson != null)
            .SelectMany(page => (page.StructuredJson?.ApiEndpoints ?? Array.Empty<HtmlCrawlStructuredApiEndpoint>())
                .Where(endpoint => !string.IsNullOrWhiteSpace(endpoint.Method) && !string.IsNullOrWhiteSpace(endpoint.Path))
                .Select(endpoint => (Page: page, Endpoint: endpoint)))
            .ToList();

        Dictionary<string, HtmlCrawlStructuredOpenApiPathItem> paths = new(StringComparer.OrdinalIgnoreCase);
        foreach ((HtmlCrawlPage Page, HtmlCrawlStructuredApiEndpoint Endpoint) entry in endpointEntries
                     .OrderBy(item => item.Endpoint.Path, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Endpoint.Method, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Page.Url, StringComparer.OrdinalIgnoreCase)) {
            if (!paths.TryGetValue(entry.Endpoint.Path, out HtmlCrawlStructuredOpenApiPathItem? pathItem)) {
                pathItem = new HtmlCrawlStructuredOpenApiPathItem {
                    Path = entry.Endpoint.Path
                };
                paths[entry.Endpoint.Path] = pathItem;
            }

            if (!string.IsNullOrWhiteSpace(entry.Endpoint.Resource)) {
                AppendDistinct(pathItem.Resources, entry.Endpoint.Resource!);
            }

            string methodKey = entry.Endpoint.Method.ToLowerInvariant();
            if (!pathItem.Operations.TryGetValue(methodKey, out HtmlCrawlStructuredOpenApiOperation? operation)) {
                operation = BuildStructuredOpenApiOperation(entry.Endpoint, entry.Page.Url);
                pathItem.Operations[methodKey] = operation;
                continue;
            }

            MergeStructuredOpenApiOperation(operation, entry.Endpoint, entry.Page.Url);
        }

        HtmlCrawlStructuredMetadata? primaryMetadata = result.Pages
            .Select(page => page.StructuredJson?.Metadata)
            .FirstOrDefault(metadata => metadata != null && (!string.IsNullOrWhiteSpace(metadata.Title) || !string.IsNullOrWhiteSpace(metadata.Description)));

        HtmlCrawlStructuredOpenApiLike openApiLike = new() {
            Title = primaryMetadata?.Title,
            Description = primaryMetadata?.Description,
            Servers = BuildStructuredOpenApiServers(result.Pages.Select(page => page.Url)),
            Tags = endpointEntries.SelectMany(item => item.Endpoint.Tags)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Resources = endpointEntries.Select(item => item.Endpoint.Resource)
                .Where(resource => !string.IsNullOrWhiteSpace(resource))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(resource => resource, StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToList(),
            Paths = paths
        };
        ApplyStructuredOpenApiComponents(openApiLike);
        AnnotateStructuredOpenApiPromotion(openApiLike);
        return openApiLike;
    }

    private static HtmlCrawlStructuredOpenApiOperation BuildStructuredOpenApiOperation(HtmlCrawlStructuredApiEndpoint endpoint, string pageUrl) {
        return new HtmlCrawlStructuredOpenApiOperation {
            OperationId = endpoint.OperationId,
            Method = endpoint.Method.ToLowerInvariant(),
            Path = endpoint.Path,
            Summary = endpoint.Title,
            Description = endpoint.Description,
            Resource = endpoint.Resource,
            Tags = new List<string>(endpoint.Tags),
            Authentication = CloneStructuredApiAuthentication(endpoint.Authentication),
            RateLimit = CloneStructuredApiRateLimit(endpoint.RateLimit),
            Parameters = endpoint.Parameters.Select(CloneStructuredApiParameter).ToList(),
            RequestHeaders = endpoint.RequestHeaders.Select(CloneStructuredHttpHeader).ToList(),
            ResponseHeaders = endpoint.ResponseHeaders.Select(CloneStructuredHttpHeader).ToList(),
            RequestExamples = endpoint.RequestExamples.Select(CloneStructuredRequestExample).ToList(),
            ResponseExamples = endpoint.ResponseExamples.Select(CloneStructuredResponseExample).ToList(),
            ErrorCatalog = endpoint.ErrorCatalog.Select(CloneStructuredApiError).ToList(),
            RequestBodySchema = new Dictionary<string, string?>(endpoint.RequestBodySchema, StringComparer.OrdinalIgnoreCase),
            RequestBodyFields = endpoint.RequestBodyFields.Select(CloneStructuredField).ToList(),
            SuccessResponseSchema = new Dictionary<string, string?>(endpoint.SuccessResponseSchema, StringComparer.OrdinalIgnoreCase),
            SuccessResponseFields = endpoint.SuccessResponseFields.Select(CloneStructuredField).ToList(),
            ErrorResponseSchema = new Dictionary<string, string?>(endpoint.ErrorResponseSchema, StringComparer.OrdinalIgnoreCase),
            ErrorResponseFields = endpoint.ErrorResponseFields.Select(CloneStructuredField).ToList(),
            Provenance = BuildStructuredOpenApiProvenance(endpoint, pageUrl)
        };
    }

    private static void AnnotateStructuredOpenApiPromotion(HtmlCrawlStructuredOpenApiLike openApiLike) {
        List<HtmlCrawlStructuredOpenApiOperation> operations = openApiLike.Paths.Values
            .SelectMany(path => path.Operations.Values)
            .ToList();

        foreach (HtmlCrawlStructuredOpenApiOperation operation in operations) {
            AnnotateStructuredOpenApiPromotion(operation);
        }

        openApiLike.StrictOpenApiPromotionThreshold = StrictOpenApiPromotionThreshold;
        openApiLike.StrictOpenApiEligibleOperationCount = operations.Count(operation => operation.StrictOpenApiEligible);
        openApiLike.StrictOpenApiSkippedOperationCount = operations.Count - openApiLike.StrictOpenApiEligibleOperationCount;
        openApiLike.StrictOpenApiAverageScore = operations.Count == 0
            ? 0
            : Math.Round(operations.Average(operation => operation.StrictOpenApiScore), 2, MidpointRounding.AwayFromZero);
    }

    private static void AnnotateStructuredOpenApiPromotion(HtmlCrawlStructuredOpenApiOperation operation) {
        List<string> warnings = new();
        int score = 0;

        bool hasMethod = !string.IsNullOrWhiteSpace(operation.Method);
        bool hasPath = !string.IsNullOrWhiteSpace(operation.Path);
        bool hasOperationId = !string.IsNullOrWhiteSpace(operation.OperationId);
        bool hasSummary = !string.IsNullOrWhiteSpace(operation.Summary);
        bool hasDescription = !string.IsNullOrWhiteSpace(operation.Description);
        bool hasGrouping = !string.IsNullOrWhiteSpace(operation.Resource) || operation.Tags.Count > 0;
        bool hasRequestContract = operation.Parameters.Count > 0
            || operation.RequestBodyFields.Count > 0
            || operation.RequestBodySchema.Count > 0
            || operation.RequestExamples.Count > 0;
        bool hasResponseContract = operation.ResponseExamples.Count > 0
            || operation.SuccessResponseFields.Count > 0
            || operation.SuccessResponseSchema.Count > 0
            || operation.ErrorResponseFields.Count > 0
            || operation.ErrorResponseSchema.Count > 0;
        bool hasSuccessfulResponse = operation.ResponseExamples.Any(example => !example.IsError && example.StatusCode.GetValueOrDefault() < 400)
            || operation.SuccessResponseFields.Count > 0
            || operation.SuccessResponseSchema.Count > 0;
        bool hasErrorCoverage = operation.ResponseExamples.Any(example => example.IsError)
            || operation.ErrorCatalog.Count > 0
            || operation.ErrorResponseFields.Count > 0
            || operation.ErrorResponseSchema.Count > 0;
        bool hasAuthentication = operation.Authentication.Required != false
            || operation.Authentication.Headers.Count > 0
            || operation.Authentication.Schemes.Count > 0;
        bool hasRateLimit = operation.RateLimit.Mentioned
            || operation.RateLimit.StatusCode != null
            || operation.RateLimit.Headers.Count > 0;
        bool hasHeaders = operation.RequestHeaders.Count > 0 || operation.ResponseHeaders.Count > 0;

        if (hasMethod) {
            score += 10;
        } else {
            warnings.Add("missing method");
        }

        if (hasPath) {
            score += 10;
        } else {
            warnings.Add("missing path");
        }

        if (hasOperationId) {
            score += 10;
        } else {
            warnings.Add("missing operationId");
        }

        if (hasSummary) {
            score += 10;
        } else {
            warnings.Add("missing summary");
        }

        if (hasDescription) {
            score += 5;
        } else {
            warnings.Add("missing description");
        }

        if (hasGrouping) {
            score += 5;
        }

        if (operation.Parameters.Count > 0) {
            score += 8;
        }
        if (operation.RequestBodyFields.Count > 0 || operation.RequestBodySchema.Count > 0) {
            score += 8;
        }
        if (operation.RequestExamples.Count > 0) {
            score += 8;
        }
        if (!hasRequestContract) {
            warnings.Add("missing request contract");
        }

        if (hasSuccessfulResponse) {
            score += 20;
        } else {
            warnings.Add("missing success response contract");
        }

        if (hasResponseContract) {
            score += 8;
        } else {
            warnings.Add("missing response contract");
        }

        if (hasErrorCoverage) {
            score += 4;
        }
        if (hasAuthentication) {
            score += 4;
        }
        if (hasRateLimit) {
            score += 2;
        }
        if (hasHeaders) {
            score += 2;
        }

        operation.StrictOpenApiScore = Math.Min(score, 100);
        operation.StrictOpenApiEligible = hasMethod
            && hasPath
            && hasSummary
            && hasSuccessfulResponse
            && operation.StrictOpenApiScore >= StrictOpenApiPromotionThreshold;

        if (!operation.StrictOpenApiEligible && operation.StrictOpenApiScore < StrictOpenApiPromotionThreshold) {
            warnings.Add("promotion score below threshold");
        }

        operation.StrictOpenApiWarnings = warnings
            .Where(warning => !string.IsNullOrWhiteSpace(warning))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(warning => warning, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HtmlCrawlStructuredOpenApiProvenance BuildStructuredOpenApiProvenance(HtmlCrawlStructuredApiEndpoint endpoint, string pageUrl) {
        HtmlCrawlStructuredOpenApiProvenance provenance = new();
        AppendStructuredOpenApiProvenanceEntry(provenance, pageUrl, "Endpoint", endpoint.SelectorHint, endpoint.Title);

        foreach (string sourceKind in endpoint.Sources) {
            AppendDistinct(provenance.SourceKinds, sourceKind);
            AppendStructuredOpenApiProvenanceEntry(provenance, pageUrl, sourceKind, endpoint.SelectorHint, endpoint.Title);
        }

        foreach (HtmlCrawlStructuredRequestExample example in endpoint.RequestExamples) {
            AppendStructuredOpenApiProvenanceEntry(provenance, pageUrl, "RequestExample", example.SelectorHint, example.Title ?? example.Method ?? example.Path);
        }

        foreach (HtmlCrawlStructuredResponseExample example in endpoint.ResponseExamples) {
            string? label = example.Title
                ?? (example.StatusCode.HasValue ? $"Response {example.StatusCode.Value}" : null)
                ?? example.Description;
            AppendStructuredOpenApiProvenanceEntry(provenance, pageUrl, "ResponseExample", example.SelectorHint, label);
        }

        foreach (HtmlCrawlStructuredApiError error in endpoint.ErrorCatalog) {
            string? label = error.Summary
                ?? (error.StatusCode.HasValue ? $"Error {error.StatusCode.Value}" : null)
                ?? error.StatusText;
            AppendStructuredOpenApiProvenanceEntry(provenance, pageUrl, "ErrorCatalog", error.SelectorHint, label);
        }

        if (endpoint.Parameters.Count > 0) {
            string? parameterSource = endpoint.Parameters
                .Select(parameter => parameter.Location)
                .FirstOrDefault(location => !string.IsNullOrWhiteSpace(location));
            AppendStructuredOpenApiProvenanceEntry(provenance, pageUrl, "ParameterTable", endpoint.SelectorHint, parameterSource);
        }

        return provenance;
    }

    private static void MergeStructuredOpenApiProvenance(HtmlCrawlStructuredOpenApiProvenance target, HtmlCrawlStructuredOpenApiProvenance source) {
        foreach (string pageUrl in source.PageUrls) {
            AppendDistinct(target.PageUrls, pageUrl);
        }

        foreach (string kind in source.SourceKinds) {
            AppendDistinct(target.SourceKinds, kind);
        }

        foreach (HtmlCrawlStructuredOpenApiProvenanceEntry entry in source.Entries) {
            if (target.Entries.Any(existing =>
                    string.Equals(existing.PageUrl, entry.PageUrl, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.Kind, entry.Kind, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.SelectorHint, entry.SelectorHint, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.Label, entry.Label, StringComparison.OrdinalIgnoreCase))) {
                continue;
            }

            target.Entries.Add(CloneStructuredOpenApiProvenanceEntry(entry));
        }
    }

    private static void AppendStructuredOpenApiProvenanceEntry(
        HtmlCrawlStructuredOpenApiProvenance provenance,
        string pageUrl,
        string kind,
        string? selectorHint,
        string? label) {
        if (string.IsNullOrWhiteSpace(pageUrl) || string.IsNullOrWhiteSpace(kind)) {
            return;
        }

        AppendDistinct(provenance.PageUrls, pageUrl);
        AppendDistinct(provenance.SourceKinds, kind);

        if (provenance.Entries.Any(existing =>
                string.Equals(existing.PageUrl, pageUrl, StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.Kind, kind, StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.SelectorHint, selectorHint, StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.Label, label, StringComparison.OrdinalIgnoreCase))) {
            return;
        }

        provenance.Entries.Add(new HtmlCrawlStructuredOpenApiProvenanceEntry {
            PageUrl = pageUrl,
            Kind = kind,
            SelectorHint = selectorHint,
            Label = label
        });
    }

    private static void MergeStructuredOpenApiOperation(HtmlCrawlStructuredOpenApiOperation target, HtmlCrawlStructuredApiEndpoint source, string pageUrl) {
        target.OperationId ??= source.OperationId;
        target.Summary ??= source.Title;
        target.Description ??= source.Description;
        target.Resource ??= source.Resource;
        foreach (string tag in source.Tags) {
            AppendDistinct(target.Tags, tag);
        }

        MergeStructuredApiAuthentication(target.Authentication, source.Authentication);
        MergeStructuredApiRateLimit(target.RateLimit, source.RateLimit);

        foreach (HtmlCrawlStructuredApiParameter parameter in source.Parameters) {
            string incomingLocation = ResolveStructuredApiParameterLocation(target.Path, parameter);
            HtmlCrawlStructuredApiParameter? existing = target.Parameters.FirstOrDefault(current =>
                string.Equals(current.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(ResolveStructuredApiParameterLocation(target.Path, current), incomingLocation, StringComparison.OrdinalIgnoreCase));
            if (existing == null) {
                target.Parameters.Add(CloneStructuredApiParameter(parameter));
                continue;
            }

            MergeStructuredApiParameter(existing, parameter);
        }

        foreach (HtmlCrawlStructuredHttpHeader header in source.RequestHeaders) {
            AppendStructuredHeader(target.RequestHeaders, header.Name, header.Value);
        }
        foreach (HtmlCrawlStructuredHttpHeader header in source.ResponseHeaders) {
            AppendStructuredHeader(target.ResponseHeaders, header.Name, header.Value);
        }

        foreach (HtmlCrawlStructuredRequestExample example in source.RequestExamples) {
            if (!target.RequestExamples.Any(existing =>
                    string.Equals(existing.Method, example.Method, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.Path, example.Path, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.Body, example.Body, StringComparison.Ordinal)
                    && string.Equals(existing.Title, example.Title, StringComparison.OrdinalIgnoreCase))) {
                target.RequestExamples.Add(CloneStructuredRequestExample(example));
            }
        }

        foreach (HtmlCrawlStructuredResponseExample example in source.ResponseExamples) {
            if (!target.ResponseExamples.Any(existing =>
                    existing.StatusCode == example.StatusCode
                    && string.Equals(existing.Body, example.Body, StringComparison.Ordinal)
                    && string.Equals(existing.Title, example.Title, StringComparison.OrdinalIgnoreCase))) {
                target.ResponseExamples.Add(CloneStructuredResponseExample(example));
            }
        }

        foreach (HtmlCrawlStructuredApiError error in source.ErrorCatalog) {
            HtmlCrawlStructuredApiError? existing = target.ErrorCatalog.FirstOrDefault(item =>
                item.StatusCode == error.StatusCode
                && string.Equals(item.StatusText, error.StatusText, StringComparison.OrdinalIgnoreCase));
            if (existing == null) {
                target.ErrorCatalog.Add(CloneStructuredApiError(error));
                continue;
            }

            existing.Summary ??= error.Summary;
            existing.ContentType ??= error.ContentType;
            existing.SelectorHint ??= error.SelectorHint;
            existing.SampleCount += error.SampleCount;
            foreach (HtmlCrawlStructuredHttpHeader header in error.Headers) {
                AppendStructuredHeader(existing.Headers, header.Name, header.Value);
            }
            MergeStructuredSchemaMaps(existing.Schema, error.Schema);
            existing.Fields = MergeStructuredFieldCollections(existing.Fields, error.Fields);
        }

        MergeStructuredSchemaMaps(target.RequestBodySchema, source.RequestBodySchema);
        MergeStructuredSchemaMaps(target.SuccessResponseSchema, source.SuccessResponseSchema);
        MergeStructuredSchemaMaps(target.ErrorResponseSchema, source.ErrorResponseSchema);
        target.RequestBodyFields = MergeStructuredFieldCollections(target.RequestBodyFields, source.RequestBodyFields);
        target.SuccessResponseFields = MergeStructuredFieldCollections(target.SuccessResponseFields, source.SuccessResponseFields);
        target.ErrorResponseFields = MergeStructuredFieldCollections(target.ErrorResponseFields, source.ErrorResponseFields);
        MergeStructuredOpenApiProvenance(target.Provenance, BuildStructuredOpenApiProvenance(source, pageUrl));
    }

    private static IList<HtmlCrawlStructuredField> MergeStructuredFieldCollections(
        IEnumerable<HtmlCrawlStructuredField> first,
        IEnumerable<HtmlCrawlStructuredField> second) {
        Dictionary<string, HtmlCrawlStructuredField> fields = new(StringComparer.OrdinalIgnoreCase);
        foreach (HtmlCrawlStructuredField field in first.Concat(second)) {
            if (!fields.TryGetValue(field.Path, out HtmlCrawlStructuredField? existing)) {
                fields[field.Path] = CloneStructuredField(field);
                continue;
            }

            if (string.IsNullOrWhiteSpace(existing.Name) && !string.IsNullOrWhiteSpace(field.Name)) {
                existing.Name = field.Name;
            }
            existing.ParentPath ??= field.ParentPath;
            existing.Kind = MergeStructuredFieldKinds(existing.Kind, field.Kind);
            existing.Depth = Math.Min(existing.Depth, field.Depth);
            existing.Type = MergeStructuredTypeValues(existing.Type, field.Type);
            existing.Format ??= field.Format;
            existing.Required = existing.Required == true && field.Required == true
                ? true
                : existing.Required ?? field.Required;
            existing.Nullable = existing.Nullable == true || field.Nullable == true
                ? true
                : existing.Nullable ?? field.Nullable;
            existing.ExampleValue ??= field.ExampleValue;
            existing.Source ??= field.Source;
            MergeStructuredFieldProvenance(existing, field);
            existing.EvidenceCount = Math.Max(existing.EvidenceCount, field.EvidenceCount);
            existing.ConfidenceScore = Math.Max(existing.ConfidenceScore, field.ConfidenceScore);
            foreach (string enumValue in field.EnumValues) {
                AppendDistinct(existing.EnumValues, enumValue);
            }
            foreach (string childPath in field.ChildPaths) {
                AppendDistinct(existing.ChildPaths, childPath);
            }
        }

        return FinalizeStructuredFieldConfidence(FinalizeStructuredFieldRelationships(fields.Values))
            .OrderBy(field => field.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AppendStructuredFieldProvenance(
        HtmlCrawlStructuredField field,
        string pageUrl,
        string kind,
        string? selectorHint,
        string? label) {
        if (field == null || string.IsNullOrWhiteSpace(pageUrl) || string.IsNullOrWhiteSpace(kind)) {
            return;
        }

        if (field.Provenance.Any(existing =>
                string.Equals(existing.PageUrl, pageUrl, StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.Kind, kind, StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.SelectorHint, selectorHint, StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.Label, label, StringComparison.OrdinalIgnoreCase))) {
            return;
        }

        field.Provenance.Add(new HtmlCrawlStructuredFieldProvenanceEntry {
            PageUrl = pageUrl,
            Kind = kind,
            SelectorHint = selectorHint,
            Label = label
        });
    }

    private static void MergeStructuredFieldProvenance(HtmlCrawlStructuredField target, HtmlCrawlStructuredField source) {
        foreach (HtmlCrawlStructuredFieldProvenanceEntry provenance in source.Provenance) {
            AppendStructuredFieldProvenance(target, provenance.PageUrl, provenance.Kind, provenance.SelectorHint, provenance.Label);
        }
    }

    private static IList<HtmlCrawlStructuredField> FinalizeStructuredFieldConfidence(IEnumerable<HtmlCrawlStructuredField> fields) {
        List<HtmlCrawlStructuredField> fieldList = fields.ToList();
        foreach (HtmlCrawlStructuredField field in fieldList) {
            field.EvidenceCount = field.Provenance
                .Select(entry => string.Join("|",
                    entry.PageUrl ?? string.Empty,
                    entry.Kind ?? string.Empty,
                    entry.SelectorHint ?? string.Empty,
                    entry.Label ?? string.Empty))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            field.ConfidenceScore = ComputeStructuredFieldConfidence(field);
        }

        return fieldList;
    }

    private static int ComputeStructuredFieldConfidence(HtmlCrawlStructuredField field) {
        int score = 20;
        int evidenceCount = field.EvidenceCount > 0 ? field.EvidenceCount : field.Provenance.Count;
        int sourceKindCount = field.Provenance
            .Select(entry => entry.Kind)
            .Where(kind => !string.IsNullOrWhiteSpace(kind))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        if (field.Provenance.Any(entry => string.Equals(entry.Kind, "ParameterTable", StringComparison.OrdinalIgnoreCase))) {
            score += 40;
        }
        if (field.Provenance.Any(entry => string.Equals(entry.Kind, "JsonResponse", StringComparison.OrdinalIgnoreCase))) {
            score += 25;
        }
        if (field.Provenance.Any(entry => string.Equals(entry.Kind, "JsonSchemaMap", StringComparison.OrdinalIgnoreCase))) {
            score += 10;
        }
        if (field.Required == true) {
            score += 10;
        }
        if (!string.IsNullOrWhiteSpace(field.Type)) {
            score += 5;
        }
        if (!string.IsNullOrWhiteSpace(field.Format)) {
            score += 5;
        }
        if (!string.IsNullOrWhiteSpace(field.ExampleValue)) {
            score += 5;
        }
        if (field.EnumValues.Count > 0) {
            score += 5;
        }
        if (field.ChildPaths.Count > 0) {
            score += 5;
        }

        score += Math.Min(evidenceCount * 5, 15);
        score += Math.Min(sourceKindCount * 5, 10);

        return Math.Min(score, 100);
    }

    private static void ApplyStructuredOpenApiComponents(HtmlCrawlStructuredOpenApiLike openApiLike) {
        HtmlCrawlStructuredOpenApiComponents components = new();
        Dictionary<string, string> schemaRefs = new(StringComparer.Ordinal);
        Dictionary<string, string> fieldSetRefs = new(StringComparer.Ordinal);
        Dictionary<string, string> authRefs = new(StringComparer.Ordinal);
        Dictionary<string, string> rateLimitRefs = new(StringComparer.Ordinal);
        Dictionary<string, string> parameterSetRefs = new(StringComparer.Ordinal);
        Dictionary<string, string> requestHeaderSetRefs = new(StringComparer.Ordinal);
        Dictionary<string, string> responseHeaderSetRefs = new(StringComparer.Ordinal);
        Dictionary<string, string> requestExampleSetRefs = new(StringComparer.Ordinal);
        Dictionary<string, string> responseExampleSetRefs = new(StringComparer.Ordinal);
        Dictionary<string, string> errorCatalogRefs = new(StringComparer.Ordinal);

        foreach (HtmlCrawlStructuredOpenApiOperation operation in openApiLike.Paths.Values
                     .SelectMany(path => path.Operations.Values)
                     .OrderBy(operation => operation.Path, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(operation => operation.Method, StringComparer.OrdinalIgnoreCase)) {
            if (HasStructuredAuthProfile(operation.Authentication)) {
                operation.AuthenticationRef = GetOrAddStructuredAuthProfileComponent(components, authRefs, operation.Authentication);
            }
            if (HasStructuredRateLimitProfile(operation.RateLimit)) {
                operation.RateLimitRef = GetOrAddStructuredRateLimitProfileComponent(components, rateLimitRefs, operation.RateLimit);
            }
            if (operation.Parameters.Count > 0) {
                operation.ParametersRef = GetOrAddStructuredParameterSetComponent(components, parameterSetRefs, operation.Parameters);
            }
            if (operation.RequestHeaders.Count > 0) {
                operation.RequestHeadersRef = GetOrAddStructuredHeaderSetComponent(components, requestHeaderSetRefs, "requestHeaderSet", operation.RequestHeaders);
            }
            if (operation.ResponseHeaders.Count > 0) {
                operation.ResponseHeadersRef = GetOrAddStructuredHeaderSetComponent(components, responseHeaderSetRefs, "responseHeaderSet", operation.ResponseHeaders);
            }
            if (operation.RequestExamples.Count > 0) {
                operation.RequestExamplesRef = GetOrAddStructuredRequestExampleSetComponent(components, requestExampleSetRefs, operation.RequestExamples);
            }
            if (operation.ResponseExamples.Count > 0) {
                operation.ResponseExamplesRef = GetOrAddStructuredResponseExampleSetComponent(components, responseExampleSetRefs, operation.ResponseExamples);
            }
            if (operation.ErrorCatalog.Count > 0) {
                operation.ErrorCatalogRef = GetOrAddStructuredErrorCatalogComponent(components, errorCatalogRefs, operation.ErrorCatalog);
            }
            if (operation.RequestBodySchema.Count > 0) {
                operation.RequestBodySchemaRef = GetOrAddStructuredSchemaComponent(components, schemaRefs, "requestBodySchema", operation.RequestBodySchema);
            }
            if (operation.SuccessResponseSchema.Count > 0) {
                operation.SuccessResponseSchemaRef = GetOrAddStructuredSchemaComponent(components, schemaRefs, "successResponseSchema", operation.SuccessResponseSchema);
            }
            if (operation.ErrorResponseSchema.Count > 0) {
                operation.ErrorResponseSchemaRef = GetOrAddStructuredSchemaComponent(components, schemaRefs, "errorResponseSchema", operation.ErrorResponseSchema);
            }
            if (operation.RequestBodyFields.Count > 0) {
                operation.RequestBodyFieldsRef = GetOrAddStructuredFieldSetComponent(components, fieldSetRefs, "requestBodyFields", operation.RequestBodyFields);
            }
            if (operation.SuccessResponseFields.Count > 0) {
                operation.SuccessResponseFieldsRef = GetOrAddStructuredFieldSetComponent(components, fieldSetRefs, "successResponseFields", operation.SuccessResponseFields);
            }
            if (operation.ErrorResponseFields.Count > 0) {
                operation.ErrorResponseFieldsRef = GetOrAddStructuredFieldSetComponent(components, fieldSetRefs, "errorResponseFields", operation.ErrorResponseFields);
            }
        }

        openApiLike.Components = components;
    }

    private static bool HasStructuredAuthProfile(HtmlCrawlStructuredApiAuthentication value) =>
        value.Required.HasValue
        || value.Schemes.Count > 0
        || value.Headers.Count > 0
        || !string.IsNullOrWhiteSpace(value.Summary);

    private static bool HasStructuredRateLimitProfile(HtmlCrawlStructuredApiRateLimit value) =>
        value.Mentioned
        || value.StatusCode.HasValue
        || value.Headers.Count > 0
        || !string.IsNullOrWhiteSpace(value.Limit)
        || !string.IsNullOrWhiteSpace(value.Window)
        || !string.IsNullOrWhiteSpace(value.Summary);

    private static string GetOrAddStructuredSchemaComponent(
        HtmlCrawlStructuredOpenApiComponents components,
        IDictionary<string, string> refs,
        string prefix,
        IDictionary<string, string?> schema) {
        string signature = BuildStructuredSchemaSignature(schema);
        if (refs.TryGetValue(signature, out string? existingRef)) {
            return existingRef;
        }

        string key = BuildStructuredComponentKey(prefix, components.Schemas.Keys);
        components.Schemas[key] = new Dictionary<string, string?>(schema, StringComparer.OrdinalIgnoreCase);
        refs[signature] = key;
        return key;
    }

    private static string GetOrAddStructuredFieldSetComponent(
        HtmlCrawlStructuredOpenApiComponents components,
        IDictionary<string, string> refs,
        string prefix,
        IEnumerable<HtmlCrawlStructuredField> fields) {
        List<HtmlCrawlStructuredField> clonedFields = fields.Select(CloneStructuredField).ToList();
        string signature = BuildStructuredFieldSetSignature(clonedFields);
        if (refs.TryGetValue(signature, out string? existingRef)) {
            return existingRef;
        }

        string key = BuildStructuredComponentKey(prefix, components.FieldSets.Keys);
        components.FieldSets[key] = clonedFields;
        refs[signature] = key;
        return key;
    }

    private static string GetOrAddStructuredAuthProfileComponent(
        HtmlCrawlStructuredOpenApiComponents components,
        IDictionary<string, string> refs,
        HtmlCrawlStructuredApiAuthentication auth) {
        string signature = BuildStructuredAuthProfileSignature(auth);
        if (refs.TryGetValue(signature, out string? existingRef)) {
            return existingRef;
        }

        string key = BuildStructuredComponentKey("authProfile", components.AuthProfiles.Keys);
        components.AuthProfiles[key] = CloneStructuredApiAuthentication(auth);
        refs[signature] = key;
        return key;
    }

    private static string GetOrAddStructuredRateLimitProfileComponent(
        HtmlCrawlStructuredOpenApiComponents components,
        IDictionary<string, string> refs,
        HtmlCrawlStructuredApiRateLimit rateLimit) {
        string signature = BuildStructuredRateLimitProfileSignature(rateLimit);
        if (refs.TryGetValue(signature, out string? existingRef)) {
            return existingRef;
        }

        string key = BuildStructuredComponentKey("rateLimitProfile", components.RateLimitProfiles.Keys);
        components.RateLimitProfiles[key] = CloneStructuredApiRateLimit(rateLimit);
        refs[signature] = key;
        return key;
    }

    private static string GetOrAddStructuredParameterSetComponent(
        HtmlCrawlStructuredOpenApiComponents components,
        IDictionary<string, string> refs,
        IEnumerable<HtmlCrawlStructuredApiParameter> parameters) {
        List<HtmlCrawlStructuredApiParameter> clonedParameters = parameters.Select(CloneStructuredApiParameter).ToList();
        string signature = BuildStructuredParameterSetSignature(clonedParameters);
        if (refs.TryGetValue(signature, out string? existingRef)) {
            return existingRef;
        }

        string key = BuildStructuredComponentKey("parameterSet", components.ParameterSets.Keys);
        components.ParameterSets[key] = clonedParameters;
        refs[signature] = key;
        return key;
    }

    private static string GetOrAddStructuredHeaderSetComponent(
        HtmlCrawlStructuredOpenApiComponents components,
        IDictionary<string, string> refs,
        string prefix,
        IEnumerable<HtmlCrawlStructuredHttpHeader> headers) {
        List<HtmlCrawlStructuredHttpHeader> clonedHeaders = headers.Select(CloneStructuredHttpHeader).ToList();
        string signature = BuildStructuredHeaderSetSignature(clonedHeaders);
        if (refs.TryGetValue(signature, out string? existingRef)) {
            return existingRef;
        }

        string key = BuildStructuredComponentKey(prefix, prefix.StartsWith("request", StringComparison.OrdinalIgnoreCase)
            ? components.RequestHeaderSets.Keys
            : components.ResponseHeaderSets.Keys);
        if (prefix.StartsWith("request", StringComparison.OrdinalIgnoreCase)) {
            components.RequestHeaderSets[key] = clonedHeaders;
        } else {
            components.ResponseHeaderSets[key] = clonedHeaders;
        }
        refs[signature] = key;
        return key;
    }

    private static string GetOrAddStructuredRequestExampleSetComponent(
        HtmlCrawlStructuredOpenApiComponents components,
        IDictionary<string, string> refs,
        IEnumerable<HtmlCrawlStructuredRequestExample> examples) {
        List<HtmlCrawlStructuredRequestExample> clonedExamples = examples.Select(CloneStructuredRequestExample).ToList();
        string signature = BuildStructuredRequestExampleSetSignature(clonedExamples);
        if (refs.TryGetValue(signature, out string? existingRef)) {
            return existingRef;
        }

        string key = BuildStructuredComponentKey("requestExampleSet", components.RequestExampleSets.Keys);
        components.RequestExampleSets[key] = clonedExamples;
        refs[signature] = key;
        return key;
    }

    private static string GetOrAddStructuredResponseExampleSetComponent(
        HtmlCrawlStructuredOpenApiComponents components,
        IDictionary<string, string> refs,
        IEnumerable<HtmlCrawlStructuredResponseExample> examples) {
        List<HtmlCrawlStructuredResponseExample> clonedExamples = examples.Select(CloneStructuredResponseExample).ToList();
        string signature = BuildStructuredResponseExampleSetSignature(clonedExamples);
        if (refs.TryGetValue(signature, out string? existingRef)) {
            return existingRef;
        }

        string key = BuildStructuredComponentKey("responseExampleSet", components.ResponseExampleSets.Keys);
        components.ResponseExampleSets[key] = clonedExamples;
        refs[signature] = key;
        return key;
    }

    private static string GetOrAddStructuredErrorCatalogComponent(
        HtmlCrawlStructuredOpenApiComponents components,
        IDictionary<string, string> refs,
        IEnumerable<HtmlCrawlStructuredApiError> errors) {
        List<HtmlCrawlStructuredApiError> clonedErrors = errors.Select(CloneStructuredApiError).ToList();
        string signature = BuildStructuredErrorCatalogSignature(clonedErrors);
        if (refs.TryGetValue(signature, out string? existingRef)) {
            return existingRef;
        }

        string key = BuildStructuredComponentKey("errorCatalog", components.ErrorCatalogs.Keys);
        components.ErrorCatalogs[key] = clonedErrors;
        refs[signature] = key;
        return key;
    }

    private static string BuildStructuredComponentKey(string prefix, IEnumerable<string> existingKeys) {
        HashSet<string> keys = new(existingKeys, StringComparer.OrdinalIgnoreCase);
        int index = 1;
        string candidate;
        do {
            candidate = prefix + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            index++;
        } while (keys.Contains(candidate));

        return candidate;
    }

    private static string BuildStructuredSchemaSignature(IEnumerable<KeyValuePair<string, string?>> schema) {
        return string.Join("|", schema
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => $"{item.Key}={item.Value ?? string.Empty}"));
    }

    private static string BuildStructuredFieldSetSignature(IEnumerable<HtmlCrawlStructuredField> fields) {
        return string.Join("|", fields
            .OrderBy(field => field.Path, StringComparer.OrdinalIgnoreCase)
            .Select(field => string.Join("~", new[] {
                field.Path,
                field.ParentPath ?? string.Empty,
                field.Kind ?? string.Empty,
                field.Type ?? string.Empty,
                field.Format ?? string.Empty,
                field.Required?.ToString() ?? string.Empty,
                field.Nullable?.ToString() ?? string.Empty,
                string.Join(",", field.ChildPaths.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                string.Join(",", field.EnumValues.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
            })));
    }

    private static string BuildStructuredAuthProfileSignature(HtmlCrawlStructuredApiAuthentication auth) {
        return string.Join("|", new[] {
            auth.Required?.ToString() ?? string.Empty,
            string.Join(",", auth.Schemes.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
            string.Join(",", auth.Headers.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        });
    }

    private static string BuildStructuredRateLimitProfileSignature(HtmlCrawlStructuredApiRateLimit rateLimit) {
        return string.Join("|", new[] {
            rateLimit.Mentioned.ToString(),
            rateLimit.StatusCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            rateLimit.Limit ?? string.Empty,
            rateLimit.Window ?? string.Empty,
            string.Join(",", rateLimit.Headers.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        });
    }

    private static string BuildStructuredParameterSetSignature(IEnumerable<HtmlCrawlStructuredApiParameter> parameters) {
        return string.Join("|", parameters
            .OrderBy(parameter => parameter.Location, StringComparer.OrdinalIgnoreCase)
            .ThenBy(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase)
            .Select(parameter => string.Join("~", new[] {
                parameter.Name,
                parameter.Type ?? string.Empty,
                parameter.Format ?? string.Empty,
                parameter.Location ?? string.Empty,
                parameter.Required?.ToString() ?? string.Empty,
                parameter.Nullable?.ToString() ?? string.Empty,
                parameter.Pattern ?? string.Empty,
                parameter.DefaultValue ?? string.Empty,
                parameter.ExampleValue ?? string.Empty,
                string.Join(",", parameter.EnumValues.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
            })));
    }

    private static string BuildStructuredHeaderSetSignature(IEnumerable<HtmlCrawlStructuredHttpHeader> headers) {
        return string.Join("|", headers
            .OrderBy(header => header.Name, StringComparer.OrdinalIgnoreCase)
            .Select(header => $"{header.Name}={header.Value ?? string.Empty}"));
    }

    private static string BuildStructuredRequestExampleSetSignature(IEnumerable<HtmlCrawlStructuredRequestExample> examples) {
        return string.Join("|", examples
            .OrderBy(example => example.Method, StringComparer.OrdinalIgnoreCase)
            .ThenBy(example => example.Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(example => example.Title, StringComparer.OrdinalIgnoreCase)
            .Select(example => string.Join("~", new[] {
                example.Method ?? string.Empty,
                example.Path ?? string.Empty,
                example.ContentType ?? string.Empty,
                example.Kind,
                BuildStructuredHeaderSetSignature(example.Headers),
                example.Body
            })));
    }

    private static string BuildStructuredResponseExampleSetSignature(IEnumerable<HtmlCrawlStructuredResponseExample> examples) {
        return string.Join("|", examples
            .OrderBy(example => example.StatusCode ?? int.MaxValue)
            .ThenBy(example => example.Title, StringComparer.OrdinalIgnoreCase)
            .Select(example => string.Join("~", new[] {
                example.StatusCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                example.StatusText ?? string.Empty,
                example.ContentType ?? string.Empty,
                example.Kind,
                BuildStructuredHeaderSetSignature(example.Headers),
                example.Body
            })));
    }

    private static string BuildStructuredErrorCatalogSignature(IEnumerable<HtmlCrawlStructuredApiError> errors) {
        return string.Join("|", errors
            .OrderBy(error => error.StatusCode ?? int.MaxValue)
            .ThenBy(error => error.StatusText, StringComparer.OrdinalIgnoreCase)
            .Select(error => string.Join("~", new[] {
                error.StatusCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                error.StatusText ?? string.Empty,
                error.ContentType ?? string.Empty,
                BuildStructuredHeaderSetSignature(error.Headers),
                BuildStructuredSchemaSignature(error.Schema),
                BuildStructuredFieldSetSignature(error.Fields)
            })));
    }

    private static string NormalizeStrictOpenApiParameterLocation(string? location) {
        string normalized = NormalizeWhitespace(location)?.ToLowerInvariant() ?? string.Empty;
        return normalized is "path" or "query" or "header" or "cookie" ? normalized : "query";
    }

    private static object? BuildStrictOpenApiSchemaReference(string? fieldSetRef, string? schemaRef) {
        string? reference = !string.IsNullOrWhiteSpace(fieldSetRef) ? fieldSetRef : schemaRef;
        if (string.IsNullOrWhiteSpace(reference)) {
            return null;
        }

        return new Dictionary<string, object?> {
            ["$ref"] = $"#/components/schemas/{reference}"
        };
    }

    private static Dictionary<string, object?> BuildStrictOpenApiRequestExamples(IEnumerable<HtmlCrawlStructuredRequestExample> examples) {
        Dictionary<string, object?> values = new(StringComparer.OrdinalIgnoreCase);
        int index = 1;
        foreach (HtmlCrawlStructuredRequestExample example in examples) {
            values["example" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)] = new Dictionary<string, object?> {
                ["summary"] = example.Title ?? example.Description,
                ["value"] = ParseStrictOpenApiExampleValue(example.Body)
            };
            index++;
        }

        return values;
    }

    private static Dictionary<string, object?> BuildStrictOpenApiResponseExamples(IEnumerable<HtmlCrawlStructuredResponseExample> examples) {
        Dictionary<string, object?> values = new(StringComparer.OrdinalIgnoreCase);
        int index = 1;
        foreach (HtmlCrawlStructuredResponseExample example in examples) {
            values["example" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)] = new Dictionary<string, object?> {
                ["summary"] = example.Title ?? example.Description ?? example.StatusText,
                ["value"] = ParseStrictOpenApiExampleValue(example.Body)
            };
            index++;
        }

        return values;
    }

    private static Dictionary<string, object?> BuildStrictOpenApiHeaderDefinitions(IEnumerable<HtmlCrawlStructuredHttpHeader> headers) {
        Dictionary<string, object?> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (HtmlCrawlStructuredHttpHeader header in headers.Where(header => !string.IsNullOrWhiteSpace(header.Name)).GroupBy(header => header.Name, StringComparer.OrdinalIgnoreCase).Select(group => group.First())) {
            Dictionary<string, object?> headerDefinition = new(StringComparer.OrdinalIgnoreCase) {
                ["schema"] = new Dictionary<string, object?> {
                    ["type"] = "string"
                }
            };
            if (!string.IsNullOrWhiteSpace(header.Value)) {
                headerDefinition["example"] = header.Value;
            }
            values[header.Name] = headerDefinition;
        }

        return values;
    }

    private static object? ParseStrictOpenApiExampleValue(string? body) {
        if (string.IsNullOrWhiteSpace(body)) {
            return null;
        }

        if (TryParseStructuredJsonPayload(body!, out object? jsonBody, out _, out _)) {
            return jsonBody;
        }

        return body;
    }

    private static object BuildStrictOpenApiSchemaFromFields(IList<HtmlCrawlStructuredField> fields) {
        Dictionary<string, HtmlCrawlStructuredField> byPath = fields
            .Where(field => !string.IsNullOrWhiteSpace(field.Path))
            .GroupBy(field => field.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        bool rootArray = byPath.Keys.Any(path => path.StartsWith("$[]", StringComparison.Ordinal));
        Dictionary<string, object?> schema = rootArray
            ? BuildStrictOpenApiArraySchema(byPath, "$[]", null)
            : BuildStrictOpenApiObjectSchema(byPath, null);
        AddStrictOpenApiSchemaProvenance(schema, fields);
        AddStrictOpenApiSchemaConfidenceSummary(schema, fields);
        return schema;
    }

    private static object BuildStrictOpenApiSchemaFromFlatMap(IDictionary<string, string?> schemaMap) {
        IList<HtmlCrawlStructuredField> fields = BuildStructuredFieldsFromSchemaMap(schemaMap);
        return fields.Count > 0 ? BuildStrictOpenApiSchemaFromFields(fields) : new Dictionary<string, object?> {
            ["type"] = "object"
        };
    }

    private static IList<HtmlCrawlStructuredField> BuildStructuredFieldsFromSchemaMap(IDictionary<string, string?> schemaMap) {
        List<HtmlCrawlStructuredField> fields = new();
        foreach (KeyValuePair<string, string?> item in schemaMap.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
            if (string.IsNullOrWhiteSpace(item.Key) || string.Equals(item.Key, "$", StringComparison.Ordinal)) {
                continue;
            }

            fields.Add(new HtmlCrawlStructuredField {
                Name = ExtractStructuredFieldName(item.Key),
                Path = item.Key,
                ParentPath = GetStructuredParentPath(item.Key),
                Kind = item.Key.EndsWith("[]", StringComparison.Ordinal)
                    ? "array-item"
                    : string.Equals(item.Value, "object", StringComparison.OrdinalIgnoreCase)
                        ? "object"
                        : string.Equals(item.Value, "array", StringComparison.OrdinalIgnoreCase)
                            ? "array"
                            : "field",
                Depth = GetStructuredFieldDepth(item.Key),
                Type = item.Value,
                Source = "JsonSchemaMap"
            });
        }

        return FinalizeStructuredFieldConfidence(FinalizeStructuredFieldRelationships(fields))
            .OrderBy(field => field.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, object?> BuildStrictOpenApiObjectSchema(
        IDictionary<string, HtmlCrawlStructuredField> byPath,
        string? path) {
        Dictionary<string, object?> schema = new(StringComparer.OrdinalIgnoreCase) {
            ["type"] = "object"
        };

        IEnumerable<HtmlCrawlStructuredField> children = byPath.Values
            .Where(field => string.Equals(field.ParentPath, path, StringComparison.OrdinalIgnoreCase))
            .OrderBy(field => field.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        Dictionary<string, object?> properties = new(StringComparer.OrdinalIgnoreCase);
        List<string> required = new();

        foreach (HtmlCrawlStructuredField child in children) {
            string propertyName = child.Name;
            if (string.IsNullOrWhiteSpace(propertyName) || string.Equals(propertyName, "$[]", StringComparison.Ordinal)) {
                propertyName = ExtractStructuredFieldName(child.Path);
            }

            properties[propertyName] = BuildStrictOpenApiSchemaNode(byPath, child.Path, child);
            if (child.Required == true) {
                required.Add(propertyName);
            }
        }

        schema["properties"] = properties;
        if (required.Count > 0) {
            schema["required"] = required;
        }

        if (!string.IsNullOrWhiteSpace(path) && byPath.TryGetValue(path!, out HtmlCrawlStructuredField? field)) {
            AddStrictOpenApiFieldProvenance(schema, field);
            AddStrictOpenApiFieldConfidence(schema, field);
        }

        return schema;
    }

    private static Dictionary<string, object?> BuildStrictOpenApiArraySchema(
        IDictionary<string, HtmlCrawlStructuredField> byPath,
        string path,
        HtmlCrawlStructuredField? ownerField) {
        Dictionary<string, object?> schema = new(StringComparer.OrdinalIgnoreCase) {
            ["type"] = "array"
        };

        if (ownerField != null) {
            AddStrictOpenApiFieldProvenance(schema, ownerField);
            AddStrictOpenApiFieldConfidence(schema, ownerField);
        }

        if (byPath.TryGetValue(path, out HtmlCrawlStructuredField? itemField)) {
            schema["items"] = BuildStrictOpenApiSchemaNode(byPath, path, itemField);
        } else {
            IEnumerable<HtmlCrawlStructuredField> children = byPath.Values
                .Where(field => string.Equals(field.ParentPath, path, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (children.Any()) {
                schema["items"] = BuildStrictOpenApiObjectSchema(byPath, path);
            } else {
                schema["items"] = new Dictionary<string, object?> {
                    ["type"] = "string"
                };
            }
        }

        return schema;
    }

    private static object BuildStrictOpenApiSchemaNode(
        IDictionary<string, HtmlCrawlStructuredField> byPath,
        string path,
        HtmlCrawlStructuredField field) {
        if (string.Equals(field.Kind, "array", StringComparison.OrdinalIgnoreCase)) {
            return BuildStrictOpenApiArraySchema(byPath, path + "[]", field);
        }
        if (string.Equals(field.Kind, "object", StringComparison.OrdinalIgnoreCase)) {
            return BuildStrictOpenApiObjectSchema(byPath, path);
        }
        if (string.Equals(field.Kind, "array-item", StringComparison.OrdinalIgnoreCase)
            && byPath.Values.Any(candidate => string.Equals(candidate.ParentPath, path, StringComparison.OrdinalIgnoreCase))) {
            return BuildStrictOpenApiObjectSchema(byPath, path);
        }

        Dictionary<string, object?> schema = new(StringComparer.OrdinalIgnoreCase);
        ApplyStrictOpenApiType(schema, field.Type, field.Format);
        if (field.Nullable == true) {
            schema["nullable"] = true;
        }
        if (field.EnumValues.Count > 0) {
            schema["enum"] = field.EnumValues.Cast<object>().ToList();
        }
        if (!string.IsNullOrWhiteSpace(field.ExampleValue)) {
            schema["example"] = ParseStrictOpenApiExampleValue(field.ExampleValue);
        }
        AddStrictOpenApiFieldProvenance(schema, field);
        AddStrictOpenApiFieldConfidence(schema, field);

        return schema;
    }

    private static void AddStrictOpenApiSchemaProvenance(IDictionary<string, object?> schema, IEnumerable<HtmlCrawlStructuredField> fields) {
        List<Dictionary<string, object?>> provenance = BuildStrictOpenApiFieldProvenance(fields.SelectMany(field => field.Provenance));
        if (provenance.Count > 0) {
            schema["x-htmltinkerx-schemaProvenance"] = provenance;
        }
    }

    private static void AddStrictOpenApiFieldProvenance(IDictionary<string, object?> schema, HtmlCrawlStructuredField field) {
        List<Dictionary<string, object?>> provenance = BuildStrictOpenApiFieldProvenance(field.Provenance);
        if (provenance.Count > 0) {
            schema["x-htmltinkerx-fieldProvenance"] = provenance;
        }
    }

    private static void AddStrictOpenApiFieldConfidence(IDictionary<string, object?> schema, HtmlCrawlStructuredField field) {
        if (field.ConfidenceScore > 0) {
            schema["x-htmltinkerx-confidence"] = field.ConfidenceScore;
        }
        if (field.EvidenceCount > 0) {
            schema["x-htmltinkerx-evidenceCount"] = field.EvidenceCount;
        }
    }

    private static void AddStrictOpenApiSchemaConfidenceSummary(IDictionary<string, object?> schema, IEnumerable<HtmlCrawlStructuredField> fields) {
        List<HtmlCrawlStructuredField> scoredFields = fields
            .Where(field => field.ConfidenceScore > 0)
            .ToList();
        if (scoredFields.Count == 0) {
            return;
        }

        schema["x-htmltinkerx-confidenceSummary"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) {
            ["average"] = Math.Round(scoredFields.Average(field => field.ConfidenceScore), 2, MidpointRounding.AwayFromZero),
            ["min"] = scoredFields.Min(field => field.ConfidenceScore),
            ["max"] = scoredFields.Max(field => field.ConfidenceScore),
            ["fieldCount"] = scoredFields.Count,
            ["evidenceCount"] = scoredFields.Sum(field => field.EvidenceCount)
        };
    }

    private static List<Dictionary<string, object?>> BuildStrictOpenApiFieldProvenance(IEnumerable<HtmlCrawlStructuredFieldProvenanceEntry> entries) {
        return entries
            .GroupBy(entry => string.Join("|",
                entry.PageUrl ?? string.Empty,
                entry.Kind ?? string.Empty,
                entry.SelectorHint ?? string.Empty,
                entry.Label ?? string.Empty), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(entry => entry.PageUrl, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Kind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Label, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) {
                ["pageUrl"] = entry.PageUrl,
                ["kind"] = entry.Kind,
                ["selectorHint"] = entry.SelectorHint,
                ["label"] = entry.Label
            })
            .ToList();
    }

    private static void ApplyStrictOpenApiType(Dictionary<string, object?> schema, string? type, string? format) {
        string normalizedType = NormalizeWhitespace(type)?.ToLowerInvariant() ?? string.Empty;
        switch (normalizedType) {
            case "integer":
                schema["type"] = "integer";
                break;
            case "number":
                schema["type"] = "number";
                break;
            case "boolean":
                schema["type"] = "boolean";
                break;
            case "array":
                schema["type"] = "array";
                schema["items"] = new Dictionary<string, object?> {
                    ["type"] = "string"
                };
                break;
            case "object":
                schema["type"] = "object";
                break;
            default:
                schema["type"] = "string";
                break;
        }

        if (!string.IsNullOrWhiteSpace(format)) {
            schema["format"] = format;
        }
    }

    private static string GetStrictOpenApiResponseCode(int? statusCode) =>
        statusCode.HasValue
            ? statusCode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "default";

    private static void AddStrictOpenApiExtension(IDictionary<string, object?> value, string key, object? extensionValue) {
        if (extensionValue == null) {
            return;
        }

        if (extensionValue is string stringValue && string.IsNullOrWhiteSpace(stringValue)) {
            return;
        }

        value[key] = extensionValue;
    }

    private static void AddStrictOpenApiComponentExtension(IDictionary<string, object?> components, string key, object? extensionValue) {
        if (extensionValue == null) {
            return;
        }

        switch (extensionValue) {
            case IDictionary<string, object?> objectDictionary when objectDictionary.Count == 0:
                return;
            case System.Collections.IDictionary dictionary when dictionary.Count == 0:
                return;
            case System.Collections.ICollection collection when collection.Count == 0:
                return;
        }

        components[key] = extensionValue;
    }

    private static IList<string> BuildStructuredOpenApiServers(HtmlCrawlPage page, HtmlCrawlStructuredMetadata metadata) =>
        BuildStructuredOpenApiServers(new[] { page.Url, metadata.CanonicalUrl, metadata.ImageUrl });

    private static IList<string> BuildStructuredOpenApiServers(IEnumerable<string?> values) {
        List<string> servers = new();

        foreach (string? value in values) {
            if (string.IsNullOrWhiteSpace(value)) {
                continue;
            }

            if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)) {
                string origin = uri.GetLeftPart(UriPartial.Authority);
                if (!string.IsNullOrWhiteSpace(origin)) {
                    AppendDistinct(servers, origin);
                }
            }
        }

        return servers;
    }

    private static IDocument BuildStructuredSectionDocument(IElement heading) {
        int headingLevel = GetHeadingLevel(heading);
        StringBuilder builder = new();
        builder.Append("<div>")
            .Append(heading.OuterHtml);
        IElement? sibling = heading.NextElementSibling;
        while (sibling != null) {
            if (GetHeadingLevel(sibling) is int siblingLevel && siblingLevel <= headingLevel) {
                break;
            }

            builder.Append(sibling.OuterHtml);
            sibling = sibling.NextElementSibling;
        }

        builder.Append("</div>");
        return HtmlParser.ParseWithAngleSharp(builder.ToString());
    }

    private static int GetHeadingLevel(IElement element) {
        if (element == null || element.LocalName.Length != 2 || element.LocalName[0] != 'h' || !char.IsDigit(element.LocalName[1])) {
            return int.MaxValue;
        }

        return element.LocalName[1] - '0';
    }

    private static List<HtmlCrawlStructuredApiParameter> BuildStructuredApiParameters(IDocument sectionDocument) {
        List<HtmlCrawlStructuredApiParameter> parameters = new();
        foreach (IElement table in sectionDocument.QuerySelectorAll("table")) {
            List<HtmlTableResult> parsedTables = HtmlParser.ParseTablesWithAngleSharpDetailed(table.OuterHtml);
            HtmlTableResult? parsed = parsedTables.FirstOrDefault();
            if (parsed == null || !LooksLikeApiParameterTable(parsed, table)) {
                continue;
            }

            string? location = DetectApiParameterLocation(table, parsed);
            foreach (Dictionary<string, string?> row in parsed.Data) {
                HtmlCrawlStructuredApiParameter? parameter = BuildStructuredApiParameter(row, location, BuildElementSelectorHint(table));
                if (parameter != null) {
                    parameters.Add(parameter);
                }
            }
        }

        return parameters;
    }

    private static bool LooksLikeApiParameterTable(HtmlTableResult table, IElement tableElement) {
        string headers = string.Join(" ", table.Metadata.Headers);
        string nearbyHeading = FindNearbyHeadingText(tableElement) ?? string.Empty;
        bool hasNameColumn = ContainsAnyToken(headers, "parameter", "name", "field");
        bool hasDetailColumn = ContainsAnyToken(headers, "type", "required", "description", "default", "location");
        bool headingSignal = ContainsAnyToken(nearbyHeading, "parameter", "request body", "query parameter", "path parameter", "header");
        return hasNameColumn && (hasDetailColumn || headingSignal);
    }

    private static List<HtmlCrawlStructuredRequestExample> BuildStructuredRequestExamples(
        IReadOnlyList<HtmlCrawlStructuredCodeSample> codeSamples) {
        List<HtmlCrawlStructuredRequestExample> requestExamples = new();
        foreach (HtmlCrawlStructuredCodeSample sample in codeSamples) {
            HtmlCrawlStructuredRequestExample? requestExample = BuildStructuredRequestExample(sample);
            if (requestExample == null) {
                continue;
            }

            requestExamples.Add(requestExample);
        }

        return requestExamples;
    }

    private static HtmlCrawlStructuredApiParameter? BuildStructuredApiParameter(Dictionary<string, string?> row, string? fallbackLocation, string? selectorHint) {
        string? name = GetStructuredRowValue(row, "parameter", "name", "field");
        if (string.IsNullOrWhiteSpace(name)) {
            return null;
        }

        string? type = GetStructuredRowValue(row, "type", "data type");
        string? description = GetStructuredRowValue(row, "description", "details", "summary");
        string? format = NormalizeStructuredApiParameterFormat(
            GetStructuredRowValue(row, "format", "data format"),
            type,
            name,
            description,
            GetStructuredRowValue(row, "example", "example value", "sample", "sample value"));
        string? exampleValue = GetStructuredRowValue(row, "example", "example value", "sample", "sample value");
        string? pattern = GetStructuredRowValue(row, "pattern", "regex", "regexp");
        IList<string> enumValues = ParseStructuredApiEnumValues(
            GetStructuredRowValue(row, "enum", "allowed values", "allowed", "values"),
            description);
        string? defaultValue = GetStructuredRowValue(row, "default", "default value");
        string? location = GetStructuredRowValue(row, "location", "in") ?? fallbackLocation;
        bool? required = ParseNullableBoolean(GetStructuredRowValue(row, "required", "mandatory"));
        bool? nullable = ParseNullableBoolean(GetStructuredRowValue(row, "nullable", "allow null", "allows null", "null"));
        nullable ??= InferStructuredApiNullable(description);

        return new HtmlCrawlStructuredApiParameter {
            Name = NormalizeWhitespace(name),
            Type = NormalizeWhitespace(type),
            Format = NormalizeWhitespace(format),
            Location = NormalizeWhitespace(location),
            Required = required,
            Nullable = nullable,
            Description = NormalizeWhitespace(description),
            DefaultValue = NormalizeWhitespace(defaultValue),
            ExampleValue = NormalizeWhitespace(exampleValue),
            Pattern = NormalizeWhitespace(pattern),
            EnumValues = enumValues,
            SelectorHint = selectorHint
        };
    }

    private static HtmlCrawlStructuredApiAuthentication BuildStructuredApiAuthentication(
        IDocument sectionDocument,
        IReadOnlyList<HtmlCrawlStructuredCodeSample> codeSamples,
        IEnumerable<HtmlCrawlStructuredApiParameter> parameters) {
        HtmlCrawlStructuredApiAuthentication authentication = new();
        string sectionText = NormalizeWhitespace(sectionDocument.DocumentElement?.TextContent);

        foreach (HtmlCrawlStructuredApiParameter parameter in parameters) {
            AppendStructuredApiAuthenticationSignals(authentication, parameter.Name);
            AppendStructuredApiAuthenticationSignals(authentication, parameter.Description);
            AppendStructuredApiAuthenticationSignals(authentication, parameter.DefaultValue);

            string? headerName = NormalizeStructuredAuthenticationHeader(parameter.Name);
            if (string.Equals(parameter.Location, "header", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(headerName)) {
                AppendDistinct(authentication.Headers, headerName!);
                authentication.Required ??= parameter.Required;
            }
        }

        foreach (HtmlCrawlStructuredCodeSample sample in codeSamples) {
            AppendStructuredApiAuthenticationSignals(authentication, sample.Heading);
            AppendStructuredApiAuthenticationSignals(authentication, sample.Title);
            AppendStructuredApiAuthenticationSignals(authentication, sample.Code);
        }

        AppendStructuredApiAuthenticationSignals(authentication, sectionText);

        if (!authentication.Required.HasValue
            && (authentication.Schemes.Count > 0 || authentication.Headers.Count > 0)) {
            authentication.Required = true;
        }

        authentication.Summary = FindFirstStructuredSignalText(sectionDocument,
            "authentication",
            "authorization",
            "bearer",
            "api key",
            "x-api-key",
            "oauth",
            "jwt",
            "basic auth",
            "token");

        if (string.IsNullOrWhiteSpace(authentication.Summary)
            && (authentication.Required.HasValue || authentication.Schemes.Count > 0 || authentication.Headers.Count > 0)) {
            List<string> parts = new();
            if (authentication.Required == true) {
                parts.Add("Authentication required");
            } else if (authentication.Required == false) {
                parts.Add("No authentication required");
            }

            if (authentication.Schemes.Count > 0) {
                parts.Add("schemes: " + string.Join(", ", authentication.Schemes));
            }
            if (authentication.Headers.Count > 0) {
                parts.Add("headers: " + string.Join(", ", authentication.Headers));
            }

            authentication.Summary = string.Join("; ", parts);
        }

        return authentication;
    }

    private static void MergeStructuredApiAuthentication(
        HtmlCrawlStructuredApiAuthentication target,
        HtmlCrawlStructuredApiAuthentication source) {
        bool sourceIndicatesRequired = source.Required == true || source.Schemes.Count > 0 || source.Headers.Count > 0;
        if (sourceIndicatesRequired) {
            target.Required = true;
        } else if (!target.Required.HasValue && source.Required.HasValue) {
            target.Required = source.Required;
        }

        foreach (string scheme in source.Schemes) {
            AppendDistinct(target.Schemes, scheme);
        }
        foreach (string header in source.Headers) {
            AppendDistinct(target.Headers, header);
        }

        target.Summary ??= source.Summary;
    }

    private static void MergeStructuredApiParameter(
        HtmlCrawlStructuredApiParameter target,
        HtmlCrawlStructuredApiParameter source) {
        target.Type = MergeStructuredTypeValues(target.Type, source.Type);
        target.Format ??= source.Format;
        target.Location ??= source.Location;
        target.Required = target.Required == true || source.Required == true
            ? true
            : target.Required ?? source.Required;
        target.Nullable = target.Nullable == true || source.Nullable == true
            ? true
            : target.Nullable ?? source.Nullable;
        target.Description ??= source.Description;
        target.DefaultValue ??= source.DefaultValue;
        target.ExampleValue ??= source.ExampleValue;
        target.Pattern ??= source.Pattern;
        target.SelectorHint ??= source.SelectorHint;
        foreach (string enumValue in source.EnumValues) {
            AppendDistinct(target.EnumValues, enumValue);
        }
    }

    private static void ApplyStructuredApiParameterGrouping(HtmlCrawlStructuredApiEndpoint endpoint, string pageUrl) {
        endpoint.PathParameters = endpoint.Parameters
            .Where(parameter => string.Equals(ResolveStructuredApiParameterLocation(endpoint.Path, parameter), "path", StringComparison.OrdinalIgnoreCase))
            .ToList();
        endpoint.QueryParameters = endpoint.Parameters
            .Where(parameter => string.Equals(ResolveStructuredApiParameterLocation(endpoint.Path, parameter), "query", StringComparison.OrdinalIgnoreCase))
            .ToList();
        endpoint.HeaderParameters = endpoint.Parameters
            .Where(parameter => string.Equals(ResolveStructuredApiParameterLocation(endpoint.Path, parameter), "header", StringComparison.OrdinalIgnoreCase))
            .ToList();
        endpoint.BodyParameters = endpoint.Parameters
            .Where(parameter => string.Equals(ResolveStructuredApiParameterLocation(endpoint.Path, parameter), "body", StringComparison.OrdinalIgnoreCase))
            .ToList();

        endpoint.RequestBodySchema = endpoint.BodyParameters
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Name))
            .GroupBy(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(parameter => parameter.Type).FirstOrDefault(type => !string.IsNullOrWhiteSpace(type)),
                StringComparer.OrdinalIgnoreCase);
        endpoint.RequestBodyFields = FinalizeStructuredFieldConfidence(FinalizeStructuredFieldRelationships(endpoint.BodyParameters
            .Select(parameter => BuildStructuredRequestBodyField(parameter, pageUrl)))
            )
            .OrderBy(field => field.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (HtmlCrawlStructuredApiParameter parameter in endpoint.HeaderParameters) {
            string? headerName = NormalizeStructuredAuthenticationHeader(parameter.Name);
            if (!string.IsNullOrWhiteSpace(headerName)) {
                AppendDistinct(endpoint.Authentication.Headers, headerName!);
                endpoint.Authentication.Required ??= parameter.Required;
            }

            AppendStructuredApiAuthenticationSignals(endpoint.Authentication, parameter.Name);
            AppendStructuredApiAuthenticationSignals(endpoint.Authentication, parameter.Description);
        }

        if (!endpoint.Authentication.Required.HasValue
            && (endpoint.Authentication.Schemes.Count > 0 || endpoint.Authentication.Headers.Count > 0)) {
            endpoint.Authentication.Required = true;
        }
    }

    private static string ResolveStructuredApiParameterLocation(string endpointPath, HtmlCrawlStructuredApiParameter parameter) {
        if (!string.IsNullOrWhiteSpace(parameter.Location)) {
            string explicitLocation = parameter.Location!.Trim().ToLowerInvariant();
            if (explicitLocation is "path" or "query" or "header" or "cookie" or "body") {
                return explicitLocation;
            }
        }

        if (!string.IsNullOrWhiteSpace(parameter.Name)
            && endpointPath.IndexOf("{" + parameter.Name + "}", StringComparison.OrdinalIgnoreCase) >= 0) {
            return "path";
        }

        return "body";
    }

    private static string? DetectApiParameterLocation(IElement tableElement, HtmlTableResult table) {
        string heading = FindNearbyHeadingText(tableElement) ?? string.Empty;
        string headers = string.Join(" ", table.Metadata.Headers);
        string combined = heading + " " + headers;
        if (ContainsAnyToken(combined, "path")) {
            return "path";
        }
        if (ContainsAnyToken(combined, "query")) {
            return "query";
        }
        if (ContainsAnyToken(combined, "cookie")) {
            return "cookie";
        }
        if (ContainsAnyToken(combined, "header")) {
            return "header";
        }
        if (ContainsAnyToken(combined, "body", "request")) {
            return "body";
        }

        return null;
    }

    private static string? GetStructuredRowValue(Dictionary<string, string?> row, params string[] names) {
        foreach (string name in names) {
            foreach (KeyValuePair<string, string?> item in row) {
                if (string.Equals(item.Key, name, StringComparison.OrdinalIgnoreCase)) {
                    return item.Value;
                }
            }
        }

        return null;
    }

    private static bool? ParseNullableBoolean(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }

        string normalized = value!.Trim();
        if (normalized.Equals("true", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("required", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }
        if (normalized.Equals("false", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("no", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("optional", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        return null;
    }

    private static bool? InferStructuredApiNullable(string? description) {
        if (string.IsNullOrWhiteSpace(description)) {
            return null;
        }

        string normalized = NormalizeWhitespace(description);
        if (Regex.IsMatch(normalized, @"\b(nullable|may be null|can be null|or null)\b", RegexOptions.IgnoreCase)) {
            return true;
        }
        if (Regex.IsMatch(normalized, @"\b(not null|non-null|must not be null|cannot be null)\b", RegexOptions.IgnoreCase)) {
            return false;
        }

        return null;
    }

    private static string? NormalizeStructuredApiParameterFormat(
        string? explicitFormat,
        string? type,
        string? name,
        string? description,
        string? exampleValue) {
        foreach (string? candidate in new[] { explicitFormat, type, name, description, exampleValue }) {
            string? normalized = MapStructuredApiParameterFormat(candidate);
            if (!string.IsNullOrWhiteSpace(normalized)) {
                return normalized;
            }
        }

        return NormalizeWhitespace(explicitFormat);
    }

    private static string? MapStructuredApiParameterFormat(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }

        string normalized = NormalizeWhitespace(value).ToLowerInvariant();
        if (normalized.Contains("uuid") || normalized.Contains("guid")) {
            return "uuid";
        }
        if (normalized.Contains("date-time") || normalized.Contains("datetime") || normalized.Contains("timestamp")) {
            return "date-time";
        }
        if (Regex.IsMatch(normalized, @"\bdate\b", RegexOptions.IgnoreCase)) {
            return "date";
        }
        if (normalized.Contains("email") || Regex.IsMatch(normalized, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")) {
            return "email";
        }
        if (normalized.Contains("uri") || normalized.Contains("url") || Uri.TryCreate(value!.Trim(), UriKind.Absolute, out _)) {
            return "uri";
        }
        if (normalized.Contains("hostname")) {
            return "hostname";
        }
        if (normalized.Contains("ipv4")) {
            return "ipv4";
        }
        if (normalized.Contains("ipv6")) {
            return "ipv6";
        }
        if (normalized.Contains("slug")) {
            return "slug";
        }
        if (normalized.Contains("base64")) {
            return "base64";
        }

        return null;
    }

    private static IList<string> ParseStructuredApiEnumValues(string? rawValues, string? description) {
        List<string> values = new();

        void AppendCandidates(string? input, bool prose) {
            if (string.IsNullOrWhiteSpace(input)) {
                return;
            }

            string normalized = NormalizeWhitespace(input);
            if (string.IsNullOrWhiteSpace(normalized)) {
                return;
            }

            if (prose) {
                Match match = Regex.Match(normalized, @"\b(?:one of|allowed values?|valid values?)\s*[:\-]\s*(.+)$", RegexOptions.IgnoreCase);
                if (match.Success) {
                    normalized = match.Groups[1].Value;
                } else {
                    return;
                }
            }

            normalized = normalized.Trim('[', ']', '(', ')');
            foreach (string part in Regex.Split(normalized, @"\s*(?:,|\||/|;)\s*")) {
                string candidate = NormalizeWhitespace(part.Trim('\"', '\'', '`'));
                if (!string.IsNullOrWhiteSpace(candidate) && !candidate.Contains(' ')) {
                    AppendDistinct(values, candidate);
                }
            }
        }

        AppendCandidates(rawValues, prose: false);
        if (values.Count == 0) {
            AppendCandidates(description, prose: true);
        }

        return values;
    }

    private static HtmlCrawlStructuredApiRateLimit BuildStructuredApiRateLimit(
        IDocument sectionDocument,
        IReadOnlyList<HtmlCrawlStructuredCodeSample> codeSamples,
        IEnumerable<HtmlCrawlStructuredResponseExample> responseExamples) {
        HtmlCrawlStructuredApiRateLimit rateLimit = new();
        string sectionText = NormalizeWhitespace(sectionDocument.DocumentElement?.TextContent);

        foreach (HtmlCrawlStructuredCodeSample sample in codeSamples) {
            AppendStructuredApiRateLimitSignals(rateLimit, sample.Heading);
            AppendStructuredApiRateLimitSignals(rateLimit, sample.Title);
            AppendStructuredApiRateLimitSignals(rateLimit, sample.Code);
        }

        foreach (HtmlCrawlStructuredResponseExample responseExample in responseExamples) {
            if (responseExample.StatusCode == 429) {
                rateLimit.Mentioned = true;
                rateLimit.StatusCode ??= 429;
            }

            AppendStructuredApiRateLimitSignals(rateLimit, responseExample.Title);
            AppendStructuredApiRateLimitSignals(rateLimit, responseExample.Body);
        }

        AppendStructuredApiRateLimitSignals(rateLimit, sectionText);
        rateLimit.Summary = FindFirstStructuredSignalText(sectionDocument,
            "rate limit",
            "rate-limit",
            "quota",
            "throttle",
            "throttling",
            "retry-after",
            "too many requests",
            "x-ratelimit",
            "ratelimit");

        if (string.IsNullOrWhiteSpace(rateLimit.Summary)
            && (rateLimit.Mentioned || rateLimit.StatusCode.HasValue || rateLimit.Headers.Count > 0 || !string.IsNullOrWhiteSpace(rateLimit.Limit))) {
            List<string> parts = new();
            if (!string.IsNullOrWhiteSpace(rateLimit.Limit)) {
                parts.Add(rateLimit.Limit!);
            }
            if (rateLimit.StatusCode.HasValue) {
                parts.Add("status " + rateLimit.StatusCode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            if (rateLimit.Headers.Count > 0) {
                parts.Add("headers: " + string.Join(", ", rateLimit.Headers));
            }

            rateLimit.Summary = string.Join("; ", parts);
        }

        return rateLimit;
    }

    private static void MergeStructuredApiRateLimit(
        HtmlCrawlStructuredApiRateLimit target,
        HtmlCrawlStructuredApiRateLimit source) {
        target.Mentioned |= source.Mentioned;
        target.StatusCode ??= source.StatusCode;
        target.Limit ??= source.Limit;
        target.Window ??= source.Window;
        foreach (string header in source.Headers) {
            AppendDistinct(target.Headers, header);
        }

        target.Summary ??= source.Summary;
    }

    private static List<HtmlCrawlStructuredResponseExample> BuildStructuredResponseExamples(
        IDocument sectionDocument,
        IReadOnlyList<HtmlCrawlStructuredCodeSample> codeSamples,
        string pageUrl) {
        List<HtmlCrawlStructuredResponseExample> responseExamples = new();
        foreach (HtmlCrawlStructuredCodeSample sample in codeSamples) {
            if (!LooksLikeResponseExample(sample)) {
                continue;
            }

            List<HtmlCrawlStructuredHttpHeader> headers = new();
            string body = sample.Code;
            int? parsedStatusCode = null;
            string? parsedStatusText = null;
            string? contentType = null;
            if (TryParseStructuredHttpResponseSample(sample.Code, out int? sampleStatusCode, out string? sampleStatusText, out List<HtmlCrawlStructuredHttpHeader> sampleHeaders, out string sampleBody)) {
                parsedStatusCode = sampleStatusCode;
                parsedStatusText = sampleStatusText;
                headers = sampleHeaders;
                body = sampleBody;
                contentType = sampleHeaders
                    .FirstOrDefault(header => string.Equals(header.Name, "Content-Type", StringComparison.OrdinalIgnoreCase))
                    ?.Value;
            }

            int? statusCode = parsedStatusCode ?? ExtractStatusCode(sample.Heading) ?? ExtractStatusCode(sample.Title);
            string? statusText = parsedStatusText
                ?? ExtractStatusText(sample.Heading)
                ?? ExtractStatusText(sample.Title)
                ?? (statusCode.HasValue ? GetDefaultHttpStatusText(statusCode.Value) : null);
            object? jsonBody = null;
            Dictionary<string, string?> bodySchema = new(StringComparer.OrdinalIgnoreCase);
            List<string> topLevelKeys = new();
            List<HtmlCrawlStructuredField> bodyFields = new();
            if (TryParseStructuredJsonPayload(body, out object? parsedJsonBody, out Dictionary<string, string?> parsedBodySchema, out List<string> parsedTopLevelKeys)) {
                jsonBody = parsedJsonBody;
                bodySchema = parsedBodySchema;
                topLevelKeys = parsedTopLevelKeys;
                bodyFields = BuildStructuredFieldsFromJsonPayload(
                    parsedJsonBody,
                    "JsonResponse",
                    pageUrl,
                    sample.SelectorHint,
                    sample.Title ?? sample.Heading ?? (statusCode.HasValue ? "Response " + statusCode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : null));
            }

            responseExamples.Add(new HtmlCrawlStructuredResponseExample {
                Title = sample.Title,
                Description = sample.Heading,
                Language = sample.Language,
                Kind = sample.Kind,
                StatusCode = statusCode,
                StatusText = statusText,
                Headers = headers,
                ContentType = contentType ?? InferStructuredResponseContentType(sample.Language, sample.Kind, body),
                IsError = statusCode is >= 400,
                Body = body,
                BodySchema = bodySchema,
                TopLevelKeys = topLevelKeys,
                JsonBody = jsonBody,
                BodyFields = bodyFields,
                SelectorHint = sample.SelectorHint
            });
        }

        foreach (HtmlCrawlStructuredResponseExample response in BuildStructuredDocumentedErrorResponses(sectionDocument, responseExamples)) {
            if (!responseExamples.Any(existing =>
                    existing.StatusCode == response.StatusCode
                    && string.Equals(existing.Description, response.Description, StringComparison.OrdinalIgnoreCase))) {
                responseExamples.Add(response);
            }
        }

        return responseExamples;
    }

    private static List<HtmlCrawlStructuredHttpHeader> BuildStructuredEndpointRequestHeaders(HtmlCrawlStructuredApiEndpoint endpoint) {
        List<HtmlCrawlStructuredHttpHeader> headers = new();
        foreach (HtmlCrawlStructuredRequestExample requestExample in endpoint.RequestExamples) {
            foreach (HtmlCrawlStructuredHttpHeader header in requestExample.Headers) {
                AppendStructuredHeader(headers, header.Name, header.Value);
            }

            if (!string.IsNullOrWhiteSpace(requestExample.ContentType)) {
                AppendStructuredHeader(headers, "Content-Type", requestExample.ContentType);
            }
        }

        foreach (HtmlCrawlStructuredApiParameter parameter in endpoint.HeaderParameters) {
            AppendStructuredHeader(headers, parameter.Name, parameter.ExampleValue ?? parameter.DefaultValue);
        }

        foreach (string headerName in endpoint.Authentication.Headers) {
            HtmlCrawlStructuredApiParameter? parameter = endpoint.HeaderParameters
                .FirstOrDefault(item => string.Equals(item.Name, headerName, StringComparison.OrdinalIgnoreCase));
            AppendStructuredHeader(headers, headerName, parameter?.ExampleValue ?? parameter?.DefaultValue);
        }

        if (endpoint.RequestBodySchema.Count > 0
            && !headers.Any(header => string.Equals(header.Name, "Content-Type", StringComparison.OrdinalIgnoreCase))) {
            AppendStructuredHeader(headers, "Content-Type", "application/json");
        }

        return headers;
    }

    private static List<HtmlCrawlStructuredHttpHeader> BuildStructuredEndpointResponseHeaders(HtmlCrawlStructuredApiEndpoint endpoint) {
        List<HtmlCrawlStructuredHttpHeader> headers = new();
        foreach (HtmlCrawlStructuredResponseExample response in endpoint.ResponseExamples) {
            foreach (HtmlCrawlStructuredHttpHeader header in response.Headers) {
                AppendStructuredHeader(headers, header.Name, header.Value);
            }
        }

        foreach (string headerName in endpoint.RateLimit.Headers) {
            AppendStructuredHeader(headers, headerName, null);
        }

        return headers;
    }

    private static IDictionary<string, string?> BuildStructuredEndpointResponseSchema(IEnumerable<HtmlCrawlStructuredResponseExample> responses) {
        Dictionary<string, string?> schema = new(StringComparer.OrdinalIgnoreCase);
        foreach (HtmlCrawlStructuredResponseExample response in responses) {
            MergeStructuredSchemaMaps(schema, response.BodySchema);
        }

        return schema;
    }

    private static IList<HtmlCrawlStructuredField> BuildStructuredEndpointResponseFields(IEnumerable<HtmlCrawlStructuredResponseExample> responses) {
        List<HtmlCrawlStructuredResponseExample> responseList = responses.ToList();
        Dictionary<string, HtmlCrawlStructuredField> fields = new(StringComparer.OrdinalIgnoreCase);
        foreach (HtmlCrawlStructuredResponseExample response in responseList) {
            foreach (HtmlCrawlStructuredField field in response.BodyFields) {
                if (!fields.TryGetValue(field.Path, out HtmlCrawlStructuredField? existing)) {
                    existing = new HtmlCrawlStructuredField {
                        Name = field.Name,
                        Path = field.Path,
                        ParentPath = field.ParentPath,
                        ChildPaths = new List<string>(field.ChildPaths),
                        Kind = field.Kind,
                        Depth = field.Depth,
                        Type = field.Type,
                        Format = field.Format,
                        Required = true,
                        Nullable = field.Nullable,
                        ExampleValue = field.ExampleValue,
                        EnumValues = new List<string>(field.EnumValues),
                        Source = field.Source,
                        Provenance = field.Provenance.Select(CloneStructuredFieldProvenanceEntry).ToList(),
                        EvidenceCount = field.EvidenceCount,
                        ConfidenceScore = field.ConfidenceScore
                    };
                    fields[field.Path] = existing;
                    continue;
                }

                existing.Type = MergeStructuredTypeValues(existing.Type, field.Type);
                existing.Format ??= field.Format;
                existing.ParentPath ??= field.ParentPath;
                existing.Kind = MergeStructuredFieldKinds(existing.Kind, field.Kind);
                existing.Depth = Math.Min(existing.Depth, field.Depth);
                existing.Nullable = existing.Nullable == true || field.Nullable == true
                    ? true
                    : existing.Nullable ?? field.Nullable;
                existing.ExampleValue ??= field.ExampleValue;
                existing.Source ??= field.Source;
                MergeStructuredFieldProvenance(existing, field);
                existing.EvidenceCount = Math.Max(existing.EvidenceCount, field.EvidenceCount);
                existing.ConfidenceScore = Math.Max(existing.ConfidenceScore, field.ConfidenceScore);
                foreach (string enumValue in field.EnumValues) {
                    AppendDistinct(existing.EnumValues, enumValue);
                }
                foreach (string childPath in field.ChildPaths) {
                    AppendDistinct(existing.ChildPaths, childPath);
                }
            }
        }

        foreach (HtmlCrawlStructuredField field in fields.Values) {
            field.Required = responseList.Count > 0 && responseList.All(response =>
                response.BodyFields.Any(candidate => string.Equals(candidate.Path, field.Path, StringComparison.OrdinalIgnoreCase)));
        }

        return FinalizeStructuredFieldConfidence(FinalizeStructuredFieldRelationships(fields.Values))
            .OrderBy(field => field.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IList<HtmlCrawlStructuredApiError> BuildStructuredEndpointErrorCatalog(IEnumerable<HtmlCrawlStructuredResponseExample> errorResponses) {
        return errorResponses
            .GroupBy(response => $"{response.StatusCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown"}|{NormalizeWhitespace(response.StatusText) ?? string.Empty}", StringComparer.OrdinalIgnoreCase)
            .Select(group => {
                List<HtmlCrawlStructuredResponseExample> groupedResponses = group.ToList();
                HtmlCrawlStructuredResponseExample primary = groupedResponses[0];
                List<HtmlCrawlStructuredHttpHeader> headers = new();
                foreach (HtmlCrawlStructuredResponseExample response in groupedResponses) {
                    foreach (HtmlCrawlStructuredHttpHeader header in response.Headers) {
                        AppendStructuredHeader(headers, header.Name, header.Value);
                    }
                }

                return new HtmlCrawlStructuredApiError {
                    StatusCode = primary.StatusCode,
                    StatusText = primary.StatusText,
                    Summary = groupedResponses
                        .Select(response => NormalizeWhitespace(response.Description) ?? NormalizeWhitespace(response.Title))
                        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                        ?? primary.StatusText
                        ?? "Error response",
                    Headers = headers,
                    ContentType = groupedResponses
                        .Select(response => response.ContentType)
                        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                    Schema = new Dictionary<string, string?>(BuildStructuredEndpointResponseSchema(groupedResponses), StringComparer.OrdinalIgnoreCase),
                    Fields = BuildStructuredEndpointResponseFields(groupedResponses),
                    SampleCount = groupedResponses.Count,
                    SelectorHint = groupedResponses
                        .Select(response => response.SelectorHint)
                        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                };
            })
            .OrderBy(error => error.StatusCode ?? int.MaxValue)
            .ThenBy(error => error.StatusText, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HtmlCrawlStructuredField BuildStructuredRequestBodyField(HtmlCrawlStructuredApiParameter parameter, string pageUrl) {
        HtmlCrawlStructuredField field = new() {
            Name = parameter.Name,
            Path = parameter.Name,
            ParentPath = GetStructuredParentPath(parameter.Name),
            Kind = "field",
            Depth = GetStructuredFieldDepth(parameter.Name),
            Type = parameter.Type,
            Format = parameter.Format,
            Required = parameter.Required,
            Nullable = parameter.Nullable,
            ExampleValue = parameter.ExampleValue ?? parameter.DefaultValue,
            EnumValues = new List<string>(parameter.EnumValues),
            Source = "ParameterTable"
        };
        AppendStructuredFieldProvenance(field, pageUrl, "ParameterTable", parameter.SelectorHint, parameter.Name);
        return field;
    }

    private static List<HtmlCrawlStructuredField> FinalizeStructuredFieldRelationships(IEnumerable<HtmlCrawlStructuredField> fields) {
        Dictionary<string, HtmlCrawlStructuredField> byPath = fields
            .GroupBy(field => field.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (HtmlCrawlStructuredField field in byPath.Values) {
            field.ParentPath ??= GetStructuredParentPath(field.Path);
            field.Depth = field.Depth > 0 ? field.Depth : GetStructuredFieldDepth(field.Path);
        }

        foreach (HtmlCrawlStructuredField field in byPath.Values) {
            if (string.IsNullOrWhiteSpace(field.ParentPath)) {
                continue;
            }

            if (byPath.TryGetValue(field.ParentPath!, out HtmlCrawlStructuredField? parent)) {
                AppendDistinct(parent.ChildPaths, field.Path);
            }
        }

        return byPath.Values.ToList();
    }

    private static string? GetStructuredParentPath(string? path) {
        if (string.IsNullOrWhiteSpace(path)) {
            return null;
        }

        string normalized = path!;
        if (normalized.EndsWith("[]", StringComparison.Ordinal)) {
            return normalized.Substring(0, normalized.Length - 2);
        }

        int separatorIndex = normalized.LastIndexOf('.');
        if (separatorIndex < 0) {
            return null;
        }

        return normalized.Substring(0, separatorIndex);
    }

    private static int GetStructuredFieldDepth(string? path) {
        if (string.IsNullOrWhiteSpace(path)) {
            return 0;
        }

        int depth = 0;
        foreach (string segment in path!.Split('.')) {
            if (string.IsNullOrWhiteSpace(segment)) {
                continue;
            }

            depth++;
        }

        return depth;
    }

    private static string MergeStructuredFieldKinds(string current, string incoming) {
        if (string.IsNullOrWhiteSpace(current)) {
            return string.IsNullOrWhiteSpace(incoming) ? "field" : incoming;
        }
        if (string.IsNullOrWhiteSpace(incoming) || string.Equals(current, incoming, StringComparison.OrdinalIgnoreCase)) {
            return current;
        }

        if (string.Equals(current, "field", StringComparison.OrdinalIgnoreCase)) {
            return incoming;
        }
        if (string.Equals(incoming, "field", StringComparison.OrdinalIgnoreCase)) {
            return current;
        }

        return current;
    }

    private static List<HtmlCrawlStructuredResponseExample> BuildStructuredDocumentedErrorResponses(
        IDocument sectionDocument,
        IReadOnlyList<HtmlCrawlStructuredResponseExample> existingResponses) {
        List<HtmlCrawlStructuredResponseExample> responses = new();
        foreach (IElement element in sectionDocument.QuerySelectorAll("p, li, td, th, dd, dt, aside, [class*='callout' i], [class*='notice' i], [class*='warning' i], [class*='alert' i]")) {
            string text = NormalizeWhitespace(element.TextContent);
            if (string.IsNullOrWhiteSpace(text)) {
                continue;
            }

            int? statusCode = ExtractStatusCode(text);
            if (statusCode is not >= 400) {
                continue;
            }

            if (!LooksLikeDocumentedErrorText(text)) {
                continue;
            }

            if (existingResponses.Any(response => response.StatusCode == statusCode && response.IsError)) {
                continue;
            }

            List<HtmlCrawlStructuredHttpHeader> headers = BuildStructuredHeadersFromText(text);
            responses.Add(new HtmlCrawlStructuredResponseExample {
                Title = "Error " + statusCode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Description = text,
                Kind = "text",
                StatusCode = statusCode,
                StatusText = ExtractStatusText(text) ?? GetDefaultHttpStatusText(statusCode.Value),
                Headers = headers,
                ContentType = "text/plain",
                IsError = true,
                Body = string.Empty,
                SelectorHint = BuildElementSelectorHint(element)
            });
        }

        return responses;
    }

    private static void AppendStructuredApiAuthenticationSignals(HtmlCrawlStructuredApiAuthentication authentication, string? text) {
        if (string.IsNullOrWhiteSpace(text)) {
            return;
        }

        string normalized = NormalizeWhitespace(text);
        if (string.IsNullOrWhiteSpace(normalized)) {
            return;
        }

        if (Regex.IsMatch(normalized, @"\b(no authentication required|without authentication|public endpoint|anonymous access)\b", RegexOptions.IgnoreCase)) {
            authentication.Required ??= false;
        }

        if (ContainsAnyToken(normalized, "authorization")) {
            AppendDistinct(authentication.Headers, "Authorization");
        }
        if (ContainsAnyToken(normalized, "x-api-key", "api-key", "api key")) {
            AppendDistinct(authentication.Headers, "X-API-Key");
            AppendDistinct(authentication.Schemes, "api-key");
        }
        if (ContainsAnyToken(normalized, "x-auth-token")) {
            AppendDistinct(authentication.Headers, "X-Auth-Token");
            AppendDistinct(authentication.Schemes, "token");
        }
        if (Regex.IsMatch(normalized, @"\bbearer\b|\bjwt\b", RegexOptions.IgnoreCase)) {
            AppendDistinct(authentication.Schemes, "bearer");
        }
        if (Regex.IsMatch(normalized, @"\boauth\s*2(?:\.0)?\b|\boauth2\b", RegexOptions.IgnoreCase)) {
            AppendDistinct(authentication.Schemes, "oauth2");
        }
        if (Regex.IsMatch(normalized, @"\bbasic auth\b|\bauthorization\s*:\s*basic\b", RegexOptions.IgnoreCase)) {
            AppendDistinct(authentication.Schemes, "basic");
            AppendDistinct(authentication.Headers, "Authorization");
        }

        foreach (Match match in Regex.Matches(normalized, @"(?im)^\s*(Authorization|X-API-Key|Api-Key|X-Auth-Token|X-Access-Token)\s*:", RegexOptions.IgnoreCase)) {
            string? header = NormalizeStructuredAuthenticationHeader(match.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(header)) {
                AppendDistinct(authentication.Headers, header!);
            }
        }

        if (!authentication.Required.HasValue
            && (Regex.IsMatch(normalized, @"\b(auth(?:entication)? required|requires authentication|authenticated requests?|authorization required|include (?:your|an?) api key|provide (?:your|an?) api key|send (?:your|an?) api key|set the authorization header|bearer token required)\b", RegexOptions.IgnoreCase)
                || authentication.Schemes.Count > 0
                || authentication.Headers.Count > 0)) {
            authentication.Required = true;
        }
    }

    private static string? NormalizeStructuredAuthenticationHeader(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }

        string normalized = NormalizeWhitespace(value);
        if (normalized.Equals("authorization", StringComparison.OrdinalIgnoreCase)) {
            return "Authorization";
        }
        if (normalized.Equals("x-api-key", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("api-key", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("api key", StringComparison.OrdinalIgnoreCase)) {
            return "X-API-Key";
        }
        if (normalized.Equals("x-auth-token", StringComparison.OrdinalIgnoreCase)) {
            return "X-Auth-Token";
        }
        if (normalized.Equals("x-access-token", StringComparison.OrdinalIgnoreCase)) {
            return "X-Access-Token";
        }

        return null;
    }

    private static void AppendStructuredApiRateLimitSignals(HtmlCrawlStructuredApiRateLimit rateLimit, string? text) {
        if (string.IsNullOrWhiteSpace(text)) {
            return;
        }

        string normalized = NormalizeWhitespace(text);
        if (string.IsNullOrWhiteSpace(normalized)) {
            return;
        }

        if (ContainsAnyToken(normalized, "rate limit", "rate-limit", "quota", "throttle", "throttling", "retry-after", "too many requests", "x-ratelimit", "ratelimit")) {
            rateLimit.Mentioned = true;
        }

        if (!rateLimit.StatusCode.HasValue
            && (ContainsAnyToken(normalized, "too many requests")
                || Regex.IsMatch(normalized, @"\b429\b", RegexOptions.IgnoreCase))) {
            rateLimit.StatusCode = 429;
        }

        foreach (string header in new[] { "Retry-After", "X-RateLimit-Limit", "X-RateLimit-Remaining", "X-RateLimit-Reset", "RateLimit-Limit", "RateLimit-Remaining", "RateLimit-Reset" }) {
            if (normalized.IndexOf(header, StringComparison.OrdinalIgnoreCase) >= 0) {
                AppendDistinct(rateLimit.Headers, header);
                rateLimit.Mentioned = true;
            }
        }

        Match requestsPerWindow = Regex.Match(normalized, @"\b(\d[\d,]*)\s+requests?\s+per\s+(second|minute|hour|day|month)\b", RegexOptions.IgnoreCase);
        if (!requestsPerWindow.Success) {
            requestsPerWindow = Regex.Match(normalized, @"\b(\d[\d,]*)\s*/\s*(second|minute|hour|day|month)\b", RegexOptions.IgnoreCase);
        }

        if (requestsPerWindow.Success) {
            string amount = requestsPerWindow.Groups[1].Value;
            string window = requestsPerWindow.Groups[2].Value.ToLowerInvariant();
            rateLimit.Mentioned = true;
            rateLimit.Window ??= window;
            rateLimit.Limit ??= amount + " requests per " + window;
        }
    }

    private static string? FindFirstStructuredSignalText(IDocument sectionDocument, params string[] tokens) {
        foreach (IElement element in sectionDocument.QuerySelectorAll("p, li, td, th, dd, dt, aside, [class*='callout' i], [class*='notice' i], [class*='warning' i], [class*='alert' i], pre, code")) {
            string text = NormalizeWhitespace(element.TextContent);
            if (!string.IsNullOrWhiteSpace(text) && ContainsAnyToken(text, tokens)) {
                return text;
            }
        }

        string fallback = NormalizeWhitespace(sectionDocument.DocumentElement?.TextContent);
        return ContainsAnyToken(fallback, tokens) ? fallback : null;
    }

    private static bool LooksLikeResponseExample(HtmlCrawlStructuredCodeSample sample) {
        string heading = sample.Heading ?? sample.Title ?? string.Empty;
        if (ContainsAnyToken(heading, "response", "example response", "success response", "error response")) {
            return true;
        }
        if (ExtractStatusCode(heading).HasValue) {
            return true;
        }

        return sample.Method == null && (string.Equals(sample.Kind, "json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sample.Kind, "http", StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeRequestExample(HtmlCrawlStructuredCodeSample sample) {
        if (LooksLikeResponseExample(sample)) {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(sample.Method) && !string.IsNullOrWhiteSpace(sample.Path)) {
            return true;
        }

        return string.Equals(sample.Kind, "curl", StringComparison.OrdinalIgnoreCase)
            || (string.Equals(sample.Kind, "http", StringComparison.OrdinalIgnoreCase)
                && Regex.IsMatch(sample.Code, @"(?im)^\s*(GET|POST|PUT|PATCH|DELETE|OPTIONS|HEAD)\s+((?:https?://[^\s'""]+)?/[^\s'""]+)(?:\s+HTTP/\d(?:\.\d)?)?\s*$"));
    }

    private static HtmlCrawlStructuredRequestExample? BuildStructuredRequestExample(HtmlCrawlStructuredCodeSample sample) {
        if (!LooksLikeRequestExample(sample)) {
            return null;
        }

        List<HtmlCrawlStructuredHttpHeader> headers = new();
        string body = string.Empty;
        string? method = sample.Method;
        string? path = sample.Path;
        string? contentType = null;

        if (TryParseStructuredHttpRequestSample(sample.Code, out string? parsedMethod, out string? parsedPath, out List<HtmlCrawlStructuredHttpHeader> parsedHeaders, out string parsedBody)) {
            method = parsedMethod ?? method;
            path = parsedPath ?? path;
            headers = parsedHeaders;
            body = parsedBody;
            contentType = parsedHeaders
                .FirstOrDefault(header => string.Equals(header.Name, "Content-Type", StringComparison.OrdinalIgnoreCase))
                ?.Value;
        } else if (TryParseStructuredCurlRequestSample(sample.Code, out parsedMethod, out parsedPath, out parsedHeaders, out parsedBody)) {
            method = parsedMethod ?? method;
            path = parsedPath ?? path;
            headers = parsedHeaders;
            body = parsedBody;
            contentType = parsedHeaders
                .FirstOrDefault(header => string.Equals(header.Name, "Content-Type", StringComparison.OrdinalIgnoreCase))
                ?.Value;
        } else {
            body = sample.Code;
        }

        if (string.IsNullOrWhiteSpace(method) || string.IsNullOrWhiteSpace(path)) {
            return null;
        }

        return new HtmlCrawlStructuredRequestExample {
            Title = sample.Title,
            Description = sample.Heading,
            Language = sample.Language,
            Kind = sample.Kind,
            Method = method,
            Path = path,
            Headers = headers,
            ContentType = contentType ?? InferStructuredRequestContentType(sample.Language, sample.Kind, body),
            Body = body,
            SelectorHint = sample.SelectorHint
        };
    }

    private static bool LooksLikeDocumentedErrorText(string text) =>
        ContainsAnyToken(text,
            "error",
            "returns",
            "response",
            "too many requests",
            "unauthorized",
            "forbidden",
            "not found",
            "invalid",
            "failed");

    private static List<HtmlCrawlStructuredHttpHeader> BuildStructuredHeadersFromText(string text) {
        List<HtmlCrawlStructuredHttpHeader> headers = new();
        foreach (string headerName in BuildStructuredDocumentedResponseHeaderNames(text)) {
            AppendStructuredHeader(headers, headerName, null);
        }

        return headers;
    }

    private static bool TryParseStructuredJsonPayload(
        string body,
        out object? jsonBody,
        out Dictionary<string, string?> bodySchema,
        out List<string> topLevelKeys) {
        jsonBody = null;
        bodySchema = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        topLevelKeys = new List<string>();

        if (!LooksLikeJson(body)) {
            return false;
        }

        try {
            using JsonDocument document = JsonDocument.Parse(body);
            jsonBody = ConvertStructuredJsonElement(document.RootElement);
            if (document.RootElement.ValueKind == JsonValueKind.Object) {
                foreach (JsonProperty property in document.RootElement.EnumerateObject()) {
                    topLevelKeys.Add(property.Name);
                }
            }

            BuildStructuredJsonSchema(bodySchema, jsonBody, null);
            return true;
        } catch (JsonException) {
            return false;
        }
    }

    private static List<HtmlCrawlStructuredField> BuildStructuredFieldsFromJsonPayload(
        object? jsonBody,
        string source,
        string pageUrl,
        string? selectorHint,
        string? label) {
        List<HtmlCrawlStructuredField> fields = new();
        AppendStructuredFieldsFromJsonValue(fields, jsonBody, null, source, pageUrl, selectorHint, label);
        return FinalizeStructuredFieldConfidence(FinalizeStructuredFieldRelationships(fields))
            .OrderBy(field => field.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AppendStructuredFieldsFromJsonValue(
        IList<HtmlCrawlStructuredField> fields,
        object? value,
        string? path,
        string source,
        string pageUrl,
        string? selectorHint,
        string? label) {
        if (value is IDictionary<string, object?> dictionary) {
            if (!string.IsNullOrWhiteSpace(path)) {
                HtmlCrawlStructuredField field = new() {
                    Name = ExtractStructuredFieldName(path!),
                    Path = path!,
                    ParentPath = GetStructuredParentPath(path),
                    Kind = "object",
                    Depth = GetStructuredFieldDepth(path),
                    Type = "object",
                    Nullable = false,
                    Source = source
                };
                AppendStructuredFieldProvenance(field, pageUrl, source, selectorHint, label);
                fields.Add(field);
            }

            foreach (KeyValuePair<string, object?> item in dictionary) {
                string childPath = string.IsNullOrWhiteSpace(path) ? item.Key : path + "." + item.Key;
                AppendStructuredFieldsFromJsonValue(fields, item.Value, childPath, source, pageUrl, selectorHint, label);
            }
            return;
        }

        if (value is IList list) {
            string arrayPath = string.IsNullOrWhiteSpace(path) ? "$" : path!;
            if (!string.IsNullOrWhiteSpace(path)) {
                HtmlCrawlStructuredField field = new() {
                    Name = ExtractStructuredFieldName(arrayPath),
                    Path = arrayPath,
                    ParentPath = GetStructuredParentPath(arrayPath),
                    Kind = "array",
                    Depth = GetStructuredFieldDepth(arrayPath),
                    Type = "array",
                    Nullable = false,
                    Source = source
                };
                AppendStructuredFieldProvenance(field, pageUrl, source, selectorHint, label);
                fields.Add(field);
            }

            foreach (object? item in list) {
                AppendStructuredFieldsFromJsonValue(fields, item, arrayPath + "[]", source, pageUrl, selectorHint, label);
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(path)) {
            return;
        }

        string? exampleValue = value switch {
            null => null,
            string text => text,
            bool boolean => boolean ? "true" : "false",
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
        };

        HtmlCrawlStructuredField valueField = new() {
            Name = ExtractStructuredFieldName(path!),
            Path = path!,
            ParentPath = GetStructuredParentPath(path),
            Kind = path!.EndsWith("[]", StringComparison.Ordinal) ? "array-item" : "field",
            Depth = GetStructuredFieldDepth(path),
            Type = GetStructuredSchemaTypeName(value),
            Format = NormalizeStructuredApiParameterFormat(null, null, path, null, exampleValue),
            Required = true,
            Nullable = value == null,
            ExampleValue = exampleValue,
            Source = source
        };
        AppendStructuredFieldProvenance(valueField, pageUrl, source, selectorHint, label);
        fields.Add(valueField);
    }

    private static string ExtractStructuredFieldName(string path) {
        if (string.IsNullOrWhiteSpace(path)) {
            return string.Empty;
        }

        string normalized = path.EndsWith("[]", StringComparison.Ordinal) ? path.Substring(0, path.Length - 2) : path;
        int separatorIndex = normalized.LastIndexOf('.');
        return separatorIndex >= 0 ? normalized.Substring(separatorIndex + 1) : normalized;
    }

    private static object? ConvertStructuredJsonElement(JsonElement element) {
        switch (element.ValueKind) {
            case JsonValueKind.Object:
                Dictionary<string, object?> obj = new(StringComparer.OrdinalIgnoreCase);
                foreach (JsonProperty property in element.EnumerateObject()) {
                    obj[property.Name] = ConvertStructuredJsonElement(property.Value);
                }
                return obj;
            case JsonValueKind.Array:
                return element.EnumerateArray()
                    .Select(ConvertStructuredJsonElement)
                    .ToList();
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                if (element.TryGetInt64(out long longValue)) {
                    return longValue;
                }
                if (element.TryGetDecimal(out decimal decimalValue)) {
                    return decimalValue;
                }
                return element.GetDouble();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            default:
                return element.GetRawText();
        }
    }

    private static void BuildStructuredJsonSchema(
        IDictionary<string, string?> schema,
        object? value,
        string? path) {
        if (value is IDictionary<string, object?> dictionary) {
            if (!string.IsNullOrWhiteSpace(path)) {
                MergeStructuredSchemaValue(schema, path!, "object");
            }

            foreach (KeyValuePair<string, object?> item in dictionary) {
                string childPath = string.IsNullOrWhiteSpace(path) ? item.Key : path + "." + item.Key;
                BuildStructuredJsonSchema(schema, item.Value, childPath);
            }
            return;
        }

        if (value is IList list) {
            string arrayPath = string.IsNullOrWhiteSpace(path) ? "$" : path!;
            MergeStructuredSchemaValue(schema, arrayPath, "array");
            if (list.Count == 0) {
                return;
            }

            foreach (object? item in list) {
                BuildStructuredJsonSchema(schema, item, arrayPath + "[]");
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(path)) {
            return;
        }

        MergeStructuredSchemaValue(schema, path!, GetStructuredSchemaTypeName(value));
    }

    private static string GetStructuredSchemaTypeName(object? value) {
        return value switch {
            null => "null",
            string => "string",
            bool => "boolean",
            byte or sbyte or short or ushort or int or uint or long or ulong => "integer",
            float or double or decimal => "number",
            IDictionary<string, object?> => "object",
            IList => "array",
            _ => "string"
        };
    }

    private static void MergeStructuredSchemaMaps(
        IDictionary<string, string?> target,
        IEnumerable<KeyValuePair<string, string?>> source) {
        foreach (KeyValuePair<string, string?> item in source) {
            if (string.IsNullOrWhiteSpace(item.Key)) {
                continue;
            }

            MergeStructuredSchemaValue(target, item.Key, item.Value);
        }
    }

    private static void MergeStructuredSchemaValue(
        IDictionary<string, string?> target,
        string key,
        string? value) {
        if (!target.TryGetValue(key, out string? existing) || string.IsNullOrWhiteSpace(existing)) {
            target[key] = value;
            return;
        }

        if (string.IsNullOrWhiteSpace(value) || string.Equals(existing, value, StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        HashSet<string> types = new(existing!.Split('|'), StringComparer.OrdinalIgnoreCase);
        types.Add(value!);
        target[key] = string.Join("|", types.OrderBy(type => type, StringComparer.OrdinalIgnoreCase));
    }

    private static string? MergeStructuredTypeValues(string? first, string? second) {
        if (string.IsNullOrWhiteSpace(first)) {
            return second;
        }
        if (string.IsNullOrWhiteSpace(second) || string.Equals(first, second, StringComparison.OrdinalIgnoreCase)) {
            return first;
        }

        HashSet<string> types = new(first!.Split('|'), StringComparer.OrdinalIgnoreCase);
        foreach (string type in second!.Split('|')) {
            if (!string.IsNullOrWhiteSpace(type)) {
                types.Add(type);
            }
        }

        return string.Join("|", types.OrderBy(type => type, StringComparer.OrdinalIgnoreCase));
    }

    private static HtmlCrawlStructuredApiAuthentication CloneStructuredApiAuthentication(HtmlCrawlStructuredApiAuthentication value) {
        return new HtmlCrawlStructuredApiAuthentication {
            Required = value.Required,
            Schemes = new List<string>(value.Schemes),
            Headers = new List<string>(value.Headers),
            Summary = value.Summary
        };
    }

    private static HtmlCrawlStructuredApiRateLimit CloneStructuredApiRateLimit(HtmlCrawlStructuredApiRateLimit value) {
        return new HtmlCrawlStructuredApiRateLimit {
            Mentioned = value.Mentioned,
            Limit = value.Limit,
            Window = value.Window,
            Headers = new List<string>(value.Headers),
            StatusCode = value.StatusCode,
            Summary = value.Summary
        };
    }

    private static HtmlCrawlStructuredApiParameter CloneStructuredApiParameter(HtmlCrawlStructuredApiParameter value) {
        return new HtmlCrawlStructuredApiParameter {
            Name = value.Name,
            Type = value.Type,
            Format = value.Format,
            Location = value.Location,
            Required = value.Required,
            Nullable = value.Nullable,
            Description = value.Description,
            DefaultValue = value.DefaultValue,
            ExampleValue = value.ExampleValue,
            Pattern = value.Pattern,
            EnumValues = new List<string>(value.EnumValues),
            SelectorHint = value.SelectorHint
        };
    }

    private static HtmlCrawlStructuredHttpHeader CloneStructuredHttpHeader(HtmlCrawlStructuredHttpHeader value) {
        return new HtmlCrawlStructuredHttpHeader {
            Name = value.Name,
            Value = value.Value
        };
    }

    private static HtmlCrawlStructuredRequestExample CloneStructuredRequestExample(HtmlCrawlStructuredRequestExample value) {
        return new HtmlCrawlStructuredRequestExample {
            Title = value.Title,
            Description = value.Description,
            Language = value.Language,
            Kind = value.Kind,
            Method = value.Method,
            Path = value.Path,
            Headers = value.Headers.Select(CloneStructuredHttpHeader).ToList(),
            ContentType = value.ContentType,
            Body = value.Body,
            SelectorHint = value.SelectorHint
        };
    }

    private static HtmlCrawlStructuredResponseExample CloneStructuredResponseExample(HtmlCrawlStructuredResponseExample value) {
        return new HtmlCrawlStructuredResponseExample {
            Title = value.Title,
            Description = value.Description,
            Language = value.Language,
            Kind = value.Kind,
            StatusCode = value.StatusCode,
            StatusText = value.StatusText,
            Headers = value.Headers.Select(CloneStructuredHttpHeader).ToList(),
            ContentType = value.ContentType,
            IsError = value.IsError,
            Body = value.Body,
            BodySchema = new Dictionary<string, string?>(value.BodySchema, StringComparer.OrdinalIgnoreCase),
            TopLevelKeys = new List<string>(value.TopLevelKeys),
            JsonBody = value.JsonBody,
            BodyFields = value.BodyFields.Select(CloneStructuredField).ToList(),
            SelectorHint = value.SelectorHint
        };
    }

    private static HtmlCrawlStructuredApiError CloneStructuredApiError(HtmlCrawlStructuredApiError value) {
        return new HtmlCrawlStructuredApiError {
            StatusCode = value.StatusCode,
            StatusText = value.StatusText,
            Summary = value.Summary,
            Headers = value.Headers.Select(CloneStructuredHttpHeader).ToList(),
            ContentType = value.ContentType,
            Schema = new Dictionary<string, string?>(value.Schema, StringComparer.OrdinalIgnoreCase),
            Fields = value.Fields.Select(CloneStructuredField).ToList(),
            SampleCount = value.SampleCount,
            SelectorHint = value.SelectorHint
        };
    }

    private static HtmlCrawlStructuredOpenApiProvenance CloneStructuredOpenApiProvenance(HtmlCrawlStructuredOpenApiProvenance value) {
        return new HtmlCrawlStructuredOpenApiProvenance {
            PageUrls = new List<string>(value.PageUrls),
            SourceKinds = new List<string>(value.SourceKinds),
            Entries = value.Entries.Select(CloneStructuredOpenApiProvenanceEntry).ToList()
        };
    }

    private static HtmlCrawlStructuredOpenApiProvenanceEntry CloneStructuredOpenApiProvenanceEntry(HtmlCrawlStructuredOpenApiProvenanceEntry value) {
        return new HtmlCrawlStructuredOpenApiProvenanceEntry {
            PageUrl = value.PageUrl,
            Kind = value.Kind,
            SelectorHint = value.SelectorHint,
            Label = value.Label
        };
    }

    private static HtmlCrawlStructuredField CloneStructuredField(HtmlCrawlStructuredField value) {
        return new HtmlCrawlStructuredField {
            Name = value.Name,
            Path = value.Path,
            ParentPath = value.ParentPath,
            ChildPaths = new List<string>(value.ChildPaths),
            Kind = value.Kind,
            Depth = value.Depth,
            Type = value.Type,
            Format = value.Format,
            Required = value.Required,
            Nullable = value.Nullable,
            ExampleValue = value.ExampleValue,
            EnumValues = new List<string>(value.EnumValues),
            Source = value.Source,
            Provenance = value.Provenance.Select(CloneStructuredFieldProvenanceEntry).ToList(),
            EvidenceCount = value.EvidenceCount,
            ConfidenceScore = value.ConfidenceScore
        };
    }

    private static HtmlCrawlStructuredFieldProvenanceEntry CloneStructuredFieldProvenanceEntry(HtmlCrawlStructuredFieldProvenanceEntry value) {
        return new HtmlCrawlStructuredFieldProvenanceEntry {
            PageUrl = value.PageUrl,
            Kind = value.Kind,
            SelectorHint = value.SelectorHint,
            Label = value.Label
        };
    }

    private static IEnumerable<string> BuildStructuredDocumentedResponseHeaderNames(IDocument sectionDocument) =>
        BuildStructuredDocumentedResponseHeaderNames(NormalizeWhitespace(sectionDocument.DocumentElement?.TextContent));

    private static IEnumerable<string> BuildStructuredDocumentedResponseHeaderNames(string? text) {
        if (string.IsNullOrWhiteSpace(text)) {
            return Array.Empty<string>();
        }

        List<string> names = new();
        foreach (string headerName in new[] { "Retry-After", "WWW-Authenticate", "X-RateLimit-Limit", "X-RateLimit-Remaining", "X-RateLimit-Reset", "RateLimit-Limit", "RateLimit-Remaining", "RateLimit-Reset", "Content-Type" }) {
            if (text!.IndexOf(headerName, StringComparison.OrdinalIgnoreCase) >= 0) {
                AppendDistinct(names, headerName);
            }
        }

        return names;
    }

    private static void AppendStructuredHeader(IList<HtmlCrawlStructuredHttpHeader> headers, string? name, string? value) {
        if (string.IsNullOrWhiteSpace(name)) {
            return;
        }

        HtmlCrawlStructuredHttpHeader? existing = headers.FirstOrDefault(header => string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing == null) {
            headers.Add(new HtmlCrawlStructuredHttpHeader {
                Name = NormalizeWhitespace(name),
                Value = string.IsNullOrWhiteSpace(value) ? null : NormalizeWhitespace(value)
            });
            return;
        }

        if (string.IsNullOrWhiteSpace(existing.Value) && !string.IsNullOrWhiteSpace(value)) {
            existing.Value = NormalizeWhitespace(value);
        }
    }

    private static string? InferStructuredRequestContentType(string? language, string kind, string body) {
        if (LooksLikeJson(body) || string.Equals(language, "json", StringComparison.OrdinalIgnoreCase)) {
            return "application/json";
        }
        if (string.Equals(kind, "http", StringComparison.OrdinalIgnoreCase)
            || string.Equals(kind, "curl", StringComparison.OrdinalIgnoreCase)
            || string.Equals(kind, "command", StringComparison.OrdinalIgnoreCase)) {
            return LooksLikeJson(body) ? "application/json" : null;
        }

        return null;
    }

    private static string? InferStructuredResponseContentType(string? language, string kind, string body) {
        if (LooksLikeJson(body) || string.Equals(language, "json", StringComparison.OrdinalIgnoreCase) || string.Equals(kind, "json", StringComparison.OrdinalIgnoreCase)) {
            return "application/json";
        }
        if (string.Equals(language, "html", StringComparison.OrdinalIgnoreCase) || body.IndexOf("<html", StringComparison.OrdinalIgnoreCase) >= 0) {
            return "text/html";
        }
        if (string.Equals(kind, "http", StringComparison.OrdinalIgnoreCase)) {
            return "message/http";
        }

        return null;
    }

    private static bool TryParseStructuredHttpRequestSample(
        string code,
        out string? method,
        out string? path,
        out List<HtmlCrawlStructuredHttpHeader> headers,
        out string body) {
        method = null;
        path = null;
        headers = new List<HtmlCrawlStructuredHttpHeader>();
        body = code;

        if (string.IsNullOrWhiteSpace(code)) {
            return false;
        }

        string normalizedNewlines = code.Replace("\r\n", "\n");
        string[] lines = normalizedNewlines.Split('\n');
        if (lines.Length == 0) {
            return false;
        }

        Match requestLine = Regex.Match(lines[0].Trim(), @"^(GET|POST|PUT|PATCH|DELETE|OPTIONS|HEAD)\s+((?:https?://[^\s'""]+)?/(?:[^\s'""]*)?)(?:\s+HTTP/\d(?:\.\d)?)?$", RegexOptions.IgnoreCase);
        if (!requestLine.Success) {
            return false;
        }

        method = requestLine.Groups[1].Value.ToUpperInvariant();
        path = NormalizeStructuredApiPath(requestLine.Groups[2].Value);

        int index = 1;
        while (index < lines.Length) {
            string line = lines[index].Trim();
            index++;
            if (line.Length == 0) {
                break;
            }

            int separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0) {
                continue;
            }

            string name = NormalizeWhitespace(line.Substring(0, separatorIndex));
            string value = NormalizeWhitespace(line.Substring(separatorIndex + 1));
            AppendStructuredHeader(headers, name, value);
        }

        body = string.Join("\n", lines.Skip(index)).Trim();
        return true;
    }

    private static bool TryParseStructuredCurlRequestSample(
        string code,
        out string? method,
        out string? path,
        out List<HtmlCrawlStructuredHttpHeader> headers,
        out string body) {
        method = null;
        path = null;
        headers = new List<HtmlCrawlStructuredHttpHeader>();
        body = string.Empty;

        if (string.IsNullOrWhiteSpace(code) || !Regex.IsMatch(code, @"(?im)^\s*curl\b")) {
            return false;
        }

        TryExtractCurlMethod(code, out method);
        if (TryExtractCurlTarget(code, out string? target)) {
            path = NormalizeStructuredApiPath(target!);
        }

        foreach (Match headerMatch in Regex.Matches(code, @"(?is)(?<!\S)(?:-H|--header)\s+(?:""([^""]+)""|'([^']+)'|([^\s]+))")) {
            string rawHeader = NormalizeWhitespace(headerMatch.Groups[1].Value);
            if (string.IsNullOrWhiteSpace(rawHeader)) {
                rawHeader = NormalizeWhitespace(headerMatch.Groups[2].Value);
            }
            if (string.IsNullOrWhiteSpace(rawHeader)) {
                rawHeader = NormalizeWhitespace(headerMatch.Groups[3].Value);
            }
            if (string.IsNullOrWhiteSpace(rawHeader)) {
                continue;
            }

            int separatorIndex = rawHeader.IndexOf(':');
            if (separatorIndex <= 0) {
                continue;
            }

            AppendStructuredHeader(headers,
                rawHeader.Substring(0, separatorIndex),
                rawHeader.Substring(separatorIndex + 1));
        }

        Match bodyMatch = Regex.Match(code, @"(?is)(?<!\S)(?:--data-raw|--data-binary|--data|-d)\s+(?:""([\s\S]*?)""|'([\s\S]*?)'|([^\s]+))");
        if (bodyMatch.Success) {
            body = NormalizeWhitespace(bodyMatch.Groups[1].Value);
            if (string.IsNullOrWhiteSpace(body)) {
                body = NormalizeWhitespace(bodyMatch.Groups[2].Value);
            }
            if (string.IsNullOrWhiteSpace(body)) {
                body = NormalizeWhitespace(bodyMatch.Groups[3].Value);
            }
        }

        if (string.IsNullOrWhiteSpace(method)) {
            method = string.IsNullOrWhiteSpace(body) ? "GET" : "POST";
        }

        return !string.IsNullOrWhiteSpace(method) && !string.IsNullOrWhiteSpace(path);
    }

    private static bool TryParseStructuredHttpResponseSample(
        string code,
        out int? statusCode,
        out string? statusText,
        out List<HtmlCrawlStructuredHttpHeader> headers,
        out string body) {
        statusCode = null;
        statusText = null;
        headers = new List<HtmlCrawlStructuredHttpHeader>();
        body = code;

        if (string.IsNullOrWhiteSpace(code)) {
            return false;
        }

        string normalizedNewlines = code.Replace("\r\n", "\n");
        string[] lines = normalizedNewlines.Split('\n');
        if (lines.Length == 0) {
            return false;
        }

        Match statusLine = Regex.Match(lines[0].Trim(), @"^HTTP/\d(?:\.\d)?\s+([1-5][0-9]{2})(?:\s+(.+))?$", RegexOptions.IgnoreCase);
        if (!statusLine.Success) {
            return false;
        }

        if (int.TryParse(statusLine.Groups[1].Value, out int parsedStatusCode)) {
            statusCode = parsedStatusCode;
        }
        statusText = NormalizeWhitespace(statusLine.Groups[2].Value);

        int index = 1;
        while (index < lines.Length) {
            string line = lines[index].Trim();
            index++;
            if (line.Length == 0) {
                break;
            }

            int separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0) {
                continue;
            }

            string name = NormalizeWhitespace(line.Substring(0, separatorIndex));
            string value = NormalizeWhitespace(line.Substring(separatorIndex + 1));
            AppendStructuredHeader(headers, name, value);
        }

        body = string.Join("\n", lines.Skip(index)).Trim();
        return true;
    }

    private static int? ExtractStatusCode(string? text) {
        if (string.IsNullOrWhiteSpace(text)) {
            return null;
        }

        Match match = Regex.Match(text, @"\b([1-5][0-9]{2})\b");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int statusCode)) {
            return statusCode;
        }

        return null;
    }

    private static string? ExtractStatusText(string? text) {
        if (string.IsNullOrWhiteSpace(text)) {
            return null;
        }

        Match httpStatusMatch = Regex.Match(text, @"\b(?:HTTP/\d(?:\.\d)?\s+)?[1-5][0-9]{2}\s+([A-Za-z][A-Za-z0-9 _-]+)", RegexOptions.IgnoreCase);
        if (httpStatusMatch.Success) {
            return NormalizeWhitespace(httpStatusMatch.Groups[1].Value);
        }

        return null;
    }

    private static string? GetDefaultHttpStatusText(int statusCode) {
        return statusCode switch {
            200 => "OK",
            201 => "Created",
            202 => "Accepted",
            204 => "No Content",
            400 => "Bad Request",
            401 => "Unauthorized",
            403 => "Forbidden",
            404 => "Not Found",
            409 => "Conflict",
            422 => "Unprocessable Entity",
            429 => "Too Many Requests",
            500 => "Internal Server Error",
            502 => "Bad Gateway",
            503 => "Service Unavailable",
            504 => "Gateway Timeout",
            _ => null
        };
    }

    private static HtmlCrawlStructuredApiEndpoint GetOrCreateStructuredApiEndpoint(
        IDictionary<string, HtmlCrawlStructuredApiEndpoint> endpoints,
        string method,
        string path) {
        string key = method.ToUpperInvariant() + " " + path;
        if (endpoints.TryGetValue(key, out HtmlCrawlStructuredApiEndpoint? existing)) {
            return existing;
        }

        HtmlCrawlStructuredApiEndpoint created = new() {
            Method = method.ToUpperInvariant(),
            Path = path
        };
        endpoints[key] = created;
        return created;
    }

    private static string DetectStructuredCodeSampleKind(string code, string? language) {
        if (TryParseApiMethodAndPath(code, out _, out _)) {
            return "http";
        }
        if (string.Equals(language, "http", StringComparison.OrdinalIgnoreCase)) {
            return "http";
        }
        if (Regex.IsMatch(code, @"(?im)^\s*curl\b")) {
            return "curl";
        }
        if (string.Equals(language, "powershell", StringComparison.OrdinalIgnoreCase)
            || string.Equals(language, "ps1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(language, "bash", StringComparison.OrdinalIgnoreCase)
            || string.Equals(language, "sh", StringComparison.OrdinalIgnoreCase)
            || string.Equals(language, "shell", StringComparison.OrdinalIgnoreCase)) {
            return "command";
        }
        if (LooksLikeJson(code)) {
            return "json";
        }

        return string.IsNullOrWhiteSpace(language) ? "text" : "code";
    }

    private static string? BuildStructuredCodeSampleTitle(string? heading, string kind, string? method, string? path, string? language) {
        if (!string.IsNullOrWhiteSpace(heading)) {
            return heading;
        }
        if (!string.IsNullOrWhiteSpace(method) && !string.IsNullOrWhiteSpace(path)) {
            return method + " " + path;
        }
        if (!string.IsNullOrWhiteSpace(language)) {
            return CultureInfoInvariantTitle(language!) + " sample";
        }

        return kind switch {
            "curl" => "cURL example",
            "http" => "HTTP example",
            "json" => "JSON example",
            "command" => "Command example",
            _ => "Code sample"
        };
    }

    private static bool LooksLikeRequestPayloadHeading(string? heading) {
        if (string.IsNullOrWhiteSpace(heading)) {
            return false;
        }

        return ContainsAnyToken(heading!,
            "request body",
            "request payload",
            "payload",
            "example request",
            "request example");
    }

    private static string? FindNearbyHeadingText(IElement element) {
        IElement? sibling = element.PreviousElementSibling;
        while (sibling != null) {
            if (Regex.IsMatch(sibling.LocalName, "^h[1-6]$", RegexOptions.IgnoreCase)) {
                return NormalizeWhitespace(sibling.TextContent);
            }

            sibling = sibling.PreviousElementSibling;
        }

        return null;
    }

    private static string? FindNearbyApiHeadingText(IElement element) {
        IElement? sibling = element.PreviousElementSibling;
        while (sibling != null) {
            if (Regex.IsMatch(sibling.LocalName, "^h[1-6]$", RegexOptions.IgnoreCase)) {
                string heading = NormalizeWhitespace(sibling.TextContent);
                if (TryParseApiMethodAndPath(heading, out _, out _)) {
                    return heading;
                }
            }

            sibling = sibling.PreviousElementSibling;
        }

        return null;
    }

    private static string? FindFollowingParagraphText(IElement element) {
        IElement? sibling = element.NextElementSibling;
        while (sibling != null) {
            if (string.Equals(sibling.LocalName, "p", StringComparison.OrdinalIgnoreCase)) {
                return NormalizeWhitespace(sibling.TextContent);
            }
            if (Regex.IsMatch(sibling.LocalName, "^h[1-6]$", RegexOptions.IgnoreCase)) {
                break;
            }

            sibling = sibling.NextElementSibling;
        }

        return null;
    }

    private static string? BuildStructuredApiPrimaryResource(string? path) {
        return GetStructuredApiLiteralPathSegments(path).FirstOrDefault();
    }

    private static IList<string> BuildStructuredApiTags(string? path, string? title, string? description) {
        List<string> tags = new();
        foreach (string segment in GetStructuredApiLiteralPathSegments(path)) {
            AppendDistinct(tags, segment);
        }

        if (tags.Count == 0) {
            foreach (string token in ExtractStructuredTitleTokens(title)) {
                AppendDistinct(tags, token);
            }
        }

        if (tags.Count == 0 && !string.IsNullOrWhiteSpace(description)) {
            foreach (string token in ExtractStructuredTitleTokens(description).Take(2)) {
                AppendDistinct(tags, token);
            }
        }

        return tags;
    }

    private static string BuildStructuredApiOperationId(string method, string path, string? title) {
        List<string> tokens = new();
        tokens.Add(method.ToLowerInvariant());

        List<string> segments = GetStructuredApiPathSegments(path);
        foreach (string segment in segments) {
            if (segment.StartsWith("{", StringComparison.Ordinal) && segment.EndsWith("}", StringComparison.Ordinal)) {
                string parameterName = segment.Substring(1, segment.Length - 2);
                tokens.Add("by");
                tokens.Add(parameterName);
                continue;
            }

            if (!IsStructuredApiVersionSegment(segment)) {
                tokens.Add(segment);
            }
        }

        if (tokens.Count <= 1) {
            foreach (string token in ExtractStructuredTitleTokens(title)) {
                tokens.Add(token);
            }
        }

        if (tokens.Count <= 1) {
            tokens.Add("operation");
        }

        return BuildStructuredCamelIdentifier(tokens);
    }

    private static List<string> GetStructuredApiLiteralPathSegments(string? path) {
        return GetStructuredApiPathSegments(path)
            .Where(segment => !segment.StartsWith("{", StringComparison.Ordinal) || !segment.EndsWith("}", StringComparison.Ordinal))
            .Where(segment => !IsStructuredApiVersionSegment(segment))
            .ToList();
    }

    private static List<string> GetStructuredApiPathSegments(string? path) {
        if (string.IsNullOrWhiteSpace(path)) {
            return new List<string>();
        }

        string normalized = path!;
        int queryIndex = normalized.IndexOf('?');
        if (queryIndex >= 0) {
            normalized = normalized.Substring(0, queryIndex);
        }

        return normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => NormalizeWhitespace(segment))
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToList();
    }

    private static bool IsStructuredApiVersionSegment(string segment) =>
        Regex.IsMatch(segment, @"^v\d+(?:\.\d+)?$", RegexOptions.IgnoreCase);

    private static IEnumerable<string> ExtractStructuredTitleTokens(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return Array.Empty<string>();
        }

        return Regex.Matches(value, @"[A-Za-z][A-Za-z0-9]*")
            .Cast<Match>()
            .Select(match => match.Value)
            .Where(token => !IsStructuredStopWord(token))
            .Select(token => token.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsStructuredStopWord(string token) =>
        token.Equals("a", StringComparison.OrdinalIgnoreCase)
        || token.Equals("an", StringComparison.OrdinalIgnoreCase)
        || token.Equals("the", StringComparison.OrdinalIgnoreCase)
        || token.Equals("and", StringComparison.OrdinalIgnoreCase)
        || token.Equals("or", StringComparison.OrdinalIgnoreCase)
        || token.Equals("for", StringComparison.OrdinalIgnoreCase)
        || token.Equals("from", StringComparison.OrdinalIgnoreCase)
        || token.Equals("with", StringComparison.OrdinalIgnoreCase)
        || token.Equals("your", StringComparison.OrdinalIgnoreCase)
        || token.Equals("endpoint", StringComparison.OrdinalIgnoreCase)
        || token.Equals("api", StringComparison.OrdinalIgnoreCase)
        || token.Equals("request", StringComparison.OrdinalIgnoreCase)
        || token.Equals("response", StringComparison.OrdinalIgnoreCase);

    private static string BuildStructuredCamelIdentifier(IEnumerable<string> tokens) {
        List<string> parts = tokens
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .SelectMany(token => Regex.Matches(token, @"[A-Za-z0-9]+").Cast<Match>().Select(match => match.Value))
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToList();
        if (parts.Count == 0) {
            return "operation";
        }

        StringBuilder builder = new(parts[0].ToLowerInvariant());
        for (int index = 1; index < parts.Count; index++) {
            string part = parts[index].ToLowerInvariant();
            builder.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1) {
                builder.Append(part.Substring(1));
            }
        }

        return builder.ToString();
    }

    private static bool ContainsAnyToken(string text, params string[] tokens) {
        if (string.IsNullOrWhiteSpace(text)) {
            return false;
        }

        return tokens.Any(token => text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static void AppendDistinct(IList<string> values, string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return;
        }

        if (!values.Contains(value, StringComparer.OrdinalIgnoreCase)) {
            values.Add(value);
        }
    }

    private static string? DetectCodeBlockLanguage(IElement element) {
        foreach (IElement candidate in new[] { element, element.ParentElement }.Where(item => item != null)!) {
            string? attributeLanguage = candidate.GetAttribute("data-language")
                ?? candidate.GetAttribute("data-lang")
                ?? candidate.GetAttribute("language")
                ?? candidate.GetAttribute("lang");
            if (!string.IsNullOrWhiteSpace(attributeLanguage)) {
                return NormalizeStructuredLanguage(attributeLanguage!);
            }

            foreach (string className in candidate.ClassList) {
                if (className.StartsWith("language-", StringComparison.OrdinalIgnoreCase)) {
                    return NormalizeStructuredLanguage(className.Substring("language-".Length));
                }
                if (className.StartsWith("lang-", StringComparison.OrdinalIgnoreCase)) {
                    return NormalizeStructuredLanguage(className.Substring("lang-".Length));
                }
            }
        }

        return null;
    }

    private static string NormalizeCodeBlockText(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }

        string normalized = value!.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] lines = normalized.Split(new[] { '\n' }, StringSplitOptions.None);
        int start = 0;
        int end = lines.Length - 1;
        while (start <= end && string.IsNullOrWhiteSpace(lines[start])) {
            start++;
        }
        while (end >= start && string.IsNullOrWhiteSpace(lines[end])) {
            end--;
        }

        if (start > end) {
            return string.Empty;
        }

        return string.Join("\n", lines.Skip(start).Take(end - start + 1));
    }

    private static string NormalizeStructuredLanguage(string language) {
        return language.Trim().Trim('.', ':').ToLowerInvariant();
    }

    private static string CultureInfoInvariantTitle(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }

        string normalized = value.Trim().ToLowerInvariant();
        return char.ToUpperInvariant(normalized[0]) + normalized.Substring(1);
    }

    private static bool TryParseApiMethodAndPath(string input, out string? method, out string? path) {
        method = null;
        path = null;
        if (string.IsNullOrWhiteSpace(input)) {
            return false;
        }

        Match directMatch = Regex.Match(input, @"(?im)\b(GET|POST|PUT|PATCH|DELETE|OPTIONS|HEAD)\s+((?:https?://[^\s'""]+)?/(?:[^\s'""]*)?)");
        if (!directMatch.Success) {
            return TryParseCurlMethodAndPath(input, out method, out path);
        }

        method = directMatch.Groups[1].Value.ToUpperInvariant();
        path = NormalizeStructuredApiPath(directMatch.Groups[2].Value);
        return !string.IsNullOrWhiteSpace(path);
    }

    private static bool TryParseCurlMethodAndPath(string input, out string? method, out string? path) {
        method = null;
        path = null;
        if (string.IsNullOrWhiteSpace(input) || !Regex.IsMatch(input, @"(?im)^\s*curl\b")) {
            return false;
        }

        if (TryExtractCurlMethod(input, out string? parsedMethod)) {
            method = parsedMethod;
        }

        if (TryExtractCurlTarget(input, out string? target)) {
            path = NormalizeStructuredApiPath(target!);
        }

        if (string.IsNullOrWhiteSpace(method)) {
            method = Regex.IsMatch(input, @"(?is)(?<!\S)(?:--data-raw|--data-binary|--data|--data-urlencode|-d)(?:\s|$)")
                ? "POST"
                : "GET";
        }

        return !string.IsNullOrWhiteSpace(method) && !string.IsNullOrWhiteSpace(path);
    }

    private static bool TryExtractCurlMethod(string code, out string? method) {
        method = null;
        if (string.IsNullOrWhiteSpace(code)) {
            return false;
        }

        Match methodMatch = Regex.Match(code, @"(?is)(?<!\S)(?:-X|--request)(?:\s+|=)(GET|POST|PUT|PATCH|DELETE|OPTIONS|HEAD)\b");
        if (!methodMatch.Success) {
            return false;
        }

        method = methodMatch.Groups[1].Value.ToUpperInvariant();
        return true;
    }

    private static bool TryExtractCurlTarget(string code, out string? target) {
        target = null;
        if (string.IsNullOrWhiteSpace(code) || !Regex.IsMatch(code, @"(?im)^\s*curl\b")) {
            return false;
        }

        Match urlOptionMatch = Regex.Match(code, @"(?is)(?<!\S)--url(?:\s+|=)(?:""([^""]+)""|'([^']+)'|([^\s]+))");
        if (urlOptionMatch.Success) {
            target = FirstNonEmptyValue(
                urlOptionMatch.Groups[1].Value,
                urlOptionMatch.Groups[2].Value,
                urlOptionMatch.Groups[3].Value);
            return !string.IsNullOrWhiteSpace(target);
        }

        List<string> tokens = TokenizeShellLikeArguments(code);
        if (tokens.Count == 0 || !string.Equals(tokens[0], "curl", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        HashSet<string> optionsWithSeparateValue = new(StringComparer.OrdinalIgnoreCase) {
            "-X",
            "--request",
            "-H",
            "--header",
            "-d",
            "--data",
            "--data-raw",
            "--data-binary",
            "--data-urlencode",
            "-e",
            "--referer",
            "-A",
            "--user-agent",
            "-u",
            "--user",
            "-F",
            "--form",
            "-o",
            "--output",
            "--url",
            "--cookie",
            "-b",
            "--proxy",
            "-x",
            "--cacert",
            "--cert",
            "--key"
        };

        for (int index = 1; index < tokens.Count; index++) {
            string token = tokens[index];
            if (string.IsNullOrWhiteSpace(token)) {
                continue;
            }

            if (token == "--") {
                continue;
            }

            if (optionsWithSeparateValue.Contains(token)) {
                index++;
                continue;
            }

            if (LooksLikeCurlOptionWithInlineValue(token)) {
                continue;
            }

            if (LooksLikeCurlTargetToken(token)) {
                target = token;
            }
        }

        return !string.IsNullOrWhiteSpace(target);
    }

    private static bool LooksLikeCurlOptionWithInlineValue(string token) {
        if (string.IsNullOrWhiteSpace(token) || !token.StartsWith("-", StringComparison.Ordinal)) {
            return false;
        }

        return token.StartsWith("--request=", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("--header=", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("--data=", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("--data-raw=", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("--data-binary=", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("--data-urlencode=", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("--url=", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("--referer=", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("-X", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("-H", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("-d", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("-e", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("-A", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("-u", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("-F", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("-o", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeCurlTargetToken(string token) =>
        !string.IsNullOrWhiteSpace(token)
        && (Regex.IsMatch(token, @"^https?://", RegexOptions.IgnoreCase)
            || token.StartsWith("/", StringComparison.Ordinal));

    private static List<string> TokenizeShellLikeArguments(string command) {
        List<string> tokens = new();
        foreach (Match match in Regex.Matches(command, @"(?:""((?:\\""|[^""])*)""|'((?:\\'|[^'])*)'|(\S+))")) {
            string? value = FirstNonEmptyValue(
                match.Groups[1].Value.Replace("\\\"", "\""),
                match.Groups[2].Value.Replace("\\'", "'"),
                match.Groups[3].Value);
            if (!string.IsNullOrWhiteSpace(value)) {
                tokens.Add(value!);
            }
        }

        return tokens;
    }

    private static string? FirstNonEmptyValue(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string NormalizeStructuredApiPath(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }

        string trimmed = value.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))) {
            return string.IsNullOrWhiteSpace(uri.AbsolutePath) ? "/" : uri.AbsolutePath;
        }

        int queryIndex = trimmed.IndexOfAny(new[] { '?', '#' });
        if (queryIndex >= 0) {
            trimmed = trimmed.Substring(0, queryIndex);
        }

        return string.IsNullOrWhiteSpace(trimmed) ? "/" : trimmed;
    }

    private static bool LooksLikeJson(string code) {
        string trimmed = code.Trim();
        return (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
            || (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal));
    }

    private static string? TryResolveStructuredHref(Uri? baseUri, string? href) {
        if (string.IsNullOrWhiteSpace(href)) {
            return null;
        }

        if (baseUri == null) {
            return href;
        }

        return TryResolveAbsoluteUri(baseUri, href!, out Uri? resolved) && resolved != null ? resolved.AbsoluteUri : href;
    }

    private static string? FindMetaContent(IEnumerable<HtmlMetaTag> metaTags, params string[] names) {
        foreach (string name in names) {
            HtmlMetaTag? match = metaTags.FirstOrDefault(tag =>
                string.Equals(tag.Name, name, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(tag.Content));
            if (match != null) {
                return match.Content;
            }
        }

        return null;
    }

    private static string? FindOpenGraphValue(HtmlOpenGraph openGraph, string propertyName) {
        OpenGraphProperty? match = openGraph.Properties.FirstOrDefault(property =>
            string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase));
        return match?.Values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static IList<string> SplitMetadataKeywords(string? keywords) {
        if (string.IsNullOrWhiteSpace(keywords)) {
            return new List<string>();
        }

        return keywords!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IDictionary<string, object?> BuildStructuredSchemaExtraction(
        HtmlCrawlStructuredJson structuredJson,
        IDocument document,
        IDocument selectedDocument,
        IReadOnlyDictionary<string, HtmlCrawlJsonSchemaField> structuredSchema) {
        Dictionary<string, object?> extracted = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, HtmlCrawlJsonSchemaField> field in structuredSchema) {
            extracted[field.Key] = ExtractStructuredSchemaFieldValue(structuredJson, document, selectedDocument, field.Value);
        }

        return extracted;
    }

    private static object? ExtractStructuredSchemaFieldValue(
        HtmlCrawlStructuredJson structuredJson,
        IDocument document,
        IDocument selectedDocument,
        HtmlCrawlJsonSchemaField field) {
        if (!string.IsNullOrWhiteSpace(field.Path)) {
            return ResolveStructuredPath(structuredJson, field.Path!);
        }

        if (string.IsNullOrWhiteSpace(field.Selector)) {
            return null;
        }

        IDocument sourceDocument = ResolveStructuredSchemaSourceDocument(document, selectedDocument, field.Source);
        IHtmlCollection<IElement> elements = sourceDocument.QuerySelectorAll(field.Selector!);
        string mode = string.IsNullOrWhiteSpace(field.Mode) ? "Text" : field.Mode!.Trim();
        if (string.Equals(mode, "Exists", StringComparison.OrdinalIgnoreCase)) {
            return elements.Length > 0;
        }

        if (string.Equals(mode, "Count", StringComparison.OrdinalIgnoreCase)) {
            return elements.Length;
        }

        if (field.All) {
            return elements
                .Select(element => ExtractStructuredSchemaElementValue(element, mode, field.Attribute))
                .Where(value => value != null)
                .ToList();
        }

        IElement? first = elements.FirstOrDefault();
        return first == null ? null : ExtractStructuredSchemaElementValue(first, mode, field.Attribute);
    }

    private static IDocument ResolveStructuredSchemaSourceDocument(IDocument document, IDocument selectedDocument, string? source) {
        if (string.IsNullOrWhiteSpace(source)) {
            return selectedDocument;
        }

        return source!.Trim().ToLowerInvariant() switch {
            "page" or "document" or "full" => document,
            _ => selectedDocument
        };
    }

    private static object? ExtractStructuredSchemaElementValue(IElement element, string mode, string? attribute) {
        if (string.Equals(mode, "Html", StringComparison.OrdinalIgnoreCase)) {
            return element.OuterHtml;
        }

        if (string.Equals(mode, "Markdown", StringComparison.OrdinalIgnoreCase)) {
            return ConvertSelectedHtmlToMarkdown(element.OuterHtml, null);
        }

        if (string.Equals(mode, "Attribute", StringComparison.OrdinalIgnoreCase)) {
            return string.IsNullOrWhiteSpace(attribute) ? null : element.GetAttribute(attribute!);
        }

        return NormalizeWhitespace(element.TextContent);
    }

    private static object? ResolveStructuredPath(object? current, string path) {
        if (current == null || string.IsNullOrWhiteSpace(path)) {
            return null;
        }

        object? value = current;
        foreach (string segment in path.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries)) {
            if (value == null) {
                return null;
            }

            if (value is IDictionary<string, object?> dictionary) {
                if (!TryGetDictionaryValue(dictionary, segment, out value)) {
                    return null;
                }
                continue;
            }

            if (value is IDictionary nonGenericDictionary) {
                if (!TryGetDictionaryValue(nonGenericDictionary, segment, out value)) {
                    return null;
                }
                continue;
            }

            if (value is IList list) {
                if (string.Equals(segment, "Count", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(segment, "Length", StringComparison.OrdinalIgnoreCase)) {
                    value = list.Count;
                    continue;
                }

                if (!int.TryParse(segment, out int index) || index < 0 || index >= list.Count) {
                    return null;
                }

                value = list[index];
                continue;
            }

            PropertyInfo? property = value.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(item => string.Equals(item.Name, segment, StringComparison.OrdinalIgnoreCase));
            if (property == null) {
                return null;
            }

            value = property.GetValue(value);
        }

        return value;
    }

    private static bool TryGetDictionaryValue(IDictionary<string, object?> dictionary, string key, out object? value) {
        foreach (KeyValuePair<string, object?> item in dictionary) {
            if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)) {
                value = item.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static bool TryGetDictionaryValue(IDictionary dictionary, string key, out object? value) {
        foreach (DictionaryEntry item in dictionary) {
            string? itemKey = Convert.ToString(item.Key, System.Globalization.CultureInfo.InvariantCulture);
            if (string.Equals(itemKey, key, StringComparison.OrdinalIgnoreCase)) {
                value = item.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static string[] ExtractHeadings(string? html) {
        if (string.IsNullOrWhiteSpace(html)) {
            return Array.Empty<string>();
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html!);
        return document.QuerySelectorAll("h1, h2, h3, h4, h5, h6")
            .Select(element => NormalizeWhitespace(element.TextContent))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.Ordinal)
            .Take(12)
            .ToArray();
    }

    private static string[] ExtractKeywords(string text) {
        if (string.IsNullOrWhiteSpace(text)) {
            return Array.Empty<string>();
        }

        Dictionary<string, int> frequencies = new(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(text, @"\b[\p{L}\p{N}][\p{L}\p{N}'_-]*\b")) {
            string token = NormalizeKeyword(match.Value);
            if (token.Length < 3 || SearchStopWords.Contains(token)) {
                continue;
            }

            frequencies[token] = frequencies.TryGetValue(token, out int count) ? count + 1 : 1;
        }

        return frequencies
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Take(12)
            .Select(pair => pair.Key)
            .ToArray();
    }

    private static string NormalizeKeyword(string value) {
        string normalized = value.Trim().Trim('\'', '"', '-', '_').ToLowerInvariant();
        if (normalized.EndsWith("'s", StringComparison.Ordinal)) {
            normalized = normalized.Substring(0, normalized.Length - 2);
        }

        return normalized;
    }

    internal static int CountWords(string? text) {
        if (string.IsNullOrWhiteSpace(text)) {
            return 0;
        }

        return Regex.Matches(text, @"\b[\p{L}\p{N}][\p{L}\p{N}'_-]*\b").Count;
    }

    private static string BuildSummary(string text) {
        if (string.IsNullOrWhiteSpace(text)) {
            return string.Empty;
        }

        const int maxLength = 180;
        string normalized = NormalizeWhitespace(text);
        if (normalized.Length <= maxLength) {
            return normalized;
        }

        int cut = normalized.LastIndexOf(' ', maxLength);
        if (cut < maxLength / 2) {
            cut = maxLength;
        }

        return normalized.Substring(0, cut).TrimEnd() + "...";
    }

    private static string NormalizeWhitespace(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }

        return Regex.Replace(value!, @"\s+", " ").Trim();
    }

    private static string? BuildRelativeOptionalPath(string? fromFilePath, string? toFilePath) {
        if (string.IsNullOrWhiteSpace(fromFilePath) || string.IsNullOrWhiteSpace(toFilePath)) {
            return null;
        }

        return BuildRelativePath(fromFilePath!, toFilePath!);
    }

    private static string BuildIndexHtml(HtmlCrawlResult result, HtmlCrawlSummary summary, string indexHtmlPath) {
        List<HtmlCrawlPage> skippedContentPages = result.SkippedPages
            .Where(page => page.SkipReason != HtmlCrawlSkipReason.AssetPath)
            .OrderBy(page => page.Depth)
            .ThenBy(page => page.Url, StringComparer.OrdinalIgnoreCase)
            .ToList();
        List<HtmlCrawlPage> skippedAssetPages = result.SkippedPages
            .Where(page => page.SkipReason == HtmlCrawlSkipReason.AssetPath)
            .OrderBy(page => page.Depth)
            .ThenBy(page => page.Url, StringComparer.OrdinalIgnoreCase)
            .ToList();

        StringBuilder builder = new();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head>");
        builder.AppendLine("  <meta charset=\"utf-8\">");
        builder.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        builder.Append("  <title>Crawl Index - ")
            .Append(HtmlEncode(result.StartUrl))
            .AppendLine("</title>");
        builder.AppendLine("  <style>");
        builder.AppendLine("    :root { color-scheme: light dark; }");
        builder.AppendLine("    body { font-family: Segoe UI, Arial, sans-serif; margin: 2rem; line-height: 1.5; }");
        builder.AppendLine("    h1, h2 { margin-bottom: 0.5rem; }");
        builder.AppendLine("    .meta { color: #666; margin-bottom: 1rem; }");
        builder.AppendLine("    .stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr)); gap: 0.75rem; margin: 1rem 0 2rem; }");
        builder.AppendLine("    .stat { border: 1px solid #ccc; border-radius: 0.5rem; padding: 0.75rem; }");
        builder.AppendLine("    .stat strong { display: block; font-size: 1.35rem; }");
        builder.AppendLine("    table { border-collapse: collapse; width: 100%; margin: 1rem 0 2rem; }");
        builder.AppendLine("    th, td { border: 1px solid #ccc; padding: 0.5rem; vertical-align: top; text-align: left; }");
        builder.AppendLine("    th { background: rgba(127, 127, 127, 0.12); }");
        builder.AppendLine("    code { font-family: Consolas, monospace; }");
        builder.AppendLine("    ul { padding-left: 1.25rem; }");
        builder.AppendLine("    .muted { color: #666; }");
        builder.AppendLine("    .summary { max-width: 32rem; }");
        builder.AppendLine("  </style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.Append("  <h1>Offline Crawl Index</h1>").AppendLine();
        builder.Append("  <p class=\"meta\">Start URL: <a href=\"")
            .Append(HtmlEncode(result.StartUrl))
            .Append("\">")
            .Append(HtmlEncode(result.StartUrl))
            .AppendLine("</a></p>");
        if (result.AppliedScenario != HtmlCrawlScenario.Custom) {
            builder.Append("  <p class=\"meta\">Scenario: <code>")
                .Append(HtmlEncode(result.AppliedScenario.ToString()))
                .AppendLine("</code></p>");
        }
        if (!string.IsNullOrWhiteSpace(result.AppliedProfileName)) {
            builder.Append("  <p class=\"meta\">Profile: <code>")
                .Append(HtmlEncode(result.AppliedProfileName))
                .Append("</code>");
            if (result.AppliedProfileReasonCode != HtmlCrawlProfileSelectionReasonCode.None) {
                builder.Append(" <span class=\"muted\">(")
                    .Append(HtmlEncode(result.AppliedProfileReasonCode.ToString()))
                    .Append(")</span>");
            }
            builder.AppendLine("</p>");
            if (!string.IsNullOrWhiteSpace(result.AppliedProfileReason)) {
                builder.Append("  <p class=\"meta\">Profile reason: ")
                    .Append(HtmlEncode(result.AppliedProfileReason))
                    .AppendLine("</p>");
            }
        }

        builder.AppendLine("  <section>");
        builder.AppendLine("    <h2>Overview</h2>");
        builder.AppendLine("    <div class=\"stats\">");
        AppendStatCard(builder, "Pages", summary.PageCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "Successful", summary.SuccessfulPageCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "Failed", summary.FailedPageCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "Auto-Rendered", summary.AutoRenderedPageCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "Skipped Pages", summary.SkippedContentPageCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "Skipped Assets", summary.SkippedAssetCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "Assets", summary.AssetCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "Chunks", summary.ChunkCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "Offline Grade", summary.OfflineReadinessGrade);
        AppendStatCard(builder, "Offline-Risk Pages", summary.OfflineRiskPageCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "High-Risk Pages", summary.HighOfflineRiskPageCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "Offline Findings", summary.OfflineRiskDiagnosticCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "Interactions", summary.InteractionCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "Graph Edges", summary.GraphEdgeCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "External Nodes", summary.GraphExternalNodeCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "Links", summary.TotalDiscoveredLinks.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.AppendLine("    </div>");
        builder.AppendLine("  </section>");

        builder.AppendLine("  <section>");
        builder.AppendLine("    <h2>Extraction Settings</h2>");
        builder.AppendLine("    <ul>");
        builder.Append("      <li>Hidden-content mode: <code>")
            .Append(HtmlEncode(summary.HiddenContentMode.ToString()))
            .AppendLine("</code></li>");
        builder.Append("      <li>Markdown image mode: <code>")
            .Append(HtmlEncode(summary.MarkdownImageMode.ToString()))
            .AppendLine("</code></li>");
        builder.Append("      <li>Listing-card metadata mode: <code>")
            .Append(HtmlEncode(summary.ListingCardMetadataMode.ToString()))
            .AppendLine("</code></li>");
        builder.AppendLine("    </ul>");
        builder.AppendLine("  </section>");

        if (summary.GuidanceNotes.Count > 0) {
            builder.AppendLine("  <section>");
            builder.AppendLine("    <h2>Guidance</h2>");
            builder.AppendLine("    <ul>");
            foreach (string note in summary.GuidanceNotes.Where(note => !string.IsNullOrWhiteSpace(note))) {
                builder.Append("      <li>")
                    .Append(HtmlEncode(note))
                    .AppendLine("</li>");
            }
            builder.AppendLine("    </ul>");
            builder.AppendLine("  </section>");
        }

        builder.AppendLine("  <section>");
        builder.AppendLine("    <h2>Artifacts</h2>");
        builder.AppendLine("    <ul>");
        AppendArtifactLink(builder, indexHtmlPath, "Manifest JSON", result.ManifestPath);
        AppendArtifactLink(builder, indexHtmlPath, "Pages JSONL", result.PagesJsonlPath);
        AppendArtifactLink(builder, indexHtmlPath, "Pages CSV", result.PagesCsvPath);
        AppendArtifactLink(builder, indexHtmlPath, "Skipped Pages JSONL", result.SkippedPagesJsonlPath);
        AppendArtifactLink(builder, indexHtmlPath, "Skipped Assets JSONL", result.SkippedAssetsJsonlPath);
        AppendArtifactLink(builder, indexHtmlPath, "Links JSONL", result.LinksJsonlPath);
        AppendArtifactLink(builder, indexHtmlPath, "Assets JSONL", result.AssetsJsonlPath);
        AppendArtifactLink(builder, indexHtmlPath, "Structured Pages JSONL", result.StructuredJsonPagesJsonlPath);
        AppendArtifactLink(builder, indexHtmlPath, "OpenAPI-Like JSON", result.OpenApiLikePath);
        AppendArtifactLink(builder, indexHtmlPath, "OpenAPI JSON", result.OpenApiPath);
        AppendArtifactLink(builder, indexHtmlPath, "Chunks JSONL", result.ChunksJsonlPath);
        AppendArtifactLink(builder, indexHtmlPath, "Graph JSON", result.GraphJsonPath);
        AppendArtifactLink(builder, indexHtmlPath, "Summary JSON", result.SummaryPath);
        AppendArtifactLink(builder, indexHtmlPath, "Summary Text", result.SummaryTextPath);
        builder.AppendLine("    </ul>");
        builder.AppendLine("  </section>");

        if (result.SitemapUrls.Count > 0) {
            builder.AppendLine("  <section>");
            builder.AppendLine("    <h2>Sitemaps</h2>");
            builder.AppendLine("    <ul>");
            foreach (string sitemapUrl in result.SitemapUrls.Where(url => !string.IsNullOrWhiteSpace(url)).Distinct(StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li><a href=\"")
                    .Append(HtmlEncode(sitemapUrl))
                    .Append("\">")
                    .Append(HtmlEncode(sitemapUrl))
                    .AppendLine("</a></li>");
            }
            builder.AppendLine("    </ul>");
            builder.AppendLine("  </section>");
        }

        if (summary.RenderModeCounts.Count > 0 || summary.RenderReasonCounts.Count > 0) {
            builder.AppendLine("  <section>");
            builder.AppendLine("    <h2>Render Summary</h2>");
            builder.AppendLine("    <ul>");
            foreach (var item in summary.RenderModeCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li>Render mode <code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: ")
                    .Append(HtmlEncode(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</li>");
            }
            foreach (var item in summary.RenderReasonCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li>Render reason <code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: ")
                    .Append(HtmlEncode(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</li>");
            }
            builder.AppendLine("    </ul>");
            builder.AppendLine("  </section>");
        }

        if (summary.ContentModeCounts.Count > 0 || summary.ContentSelectionCounts.Count > 0 || summary.ContentComparisonWinnerCounts.Count > 0 || summary.ContentComparisonWinnerPreviewSamples.Count > 0) {
            builder.AppendLine("  <section>");
            builder.AppendLine("    <h2>Content Summary</h2>");
            builder.AppendLine("    <ul>");
            foreach (var item in summary.ContentModeCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li>Content mode <code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: ")
                    .Append(HtmlEncode(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</li>");
            }
            foreach (var item in summary.ContentSelectionCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li>Content selection <code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: ")
                    .Append(HtmlEncode(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</li>");
            }
            foreach (var item in summary.ContentComparisonWinnerCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li>Best comparison mode <code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: ")
                    .Append(HtmlEncode(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</li>");
            }
            if (summary.AverageBestContentComparisonWordDelta > 0) {
                builder.Append("      <li>Average best-comparison delta: ")
                    .Append(HtmlEncode(summary.AverageBestContentComparisonWordDelta.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine(" words</li>");
            }
            foreach (var item in summary.ContentComparisonWinnerPreviewSamples.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li>Best comparison sample <code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: <span class=\"muted\">")
                    .Append(HtmlEncode(item.Value))
                    .AppendLine("</span></li>");
            }
            builder.AppendLine("    </ul>");
            builder.AppendLine("  </section>");
        }

        if (summary.InteractionCounts.Count > 0) {
            builder.AppendLine("  <section>");
            builder.AppendLine("    <h2>Interaction Summary</h2>");
            builder.AppendLine("    <ul>");
            builder.Append("      <li>Pages with interactions: ")
                .Append(HtmlEncode(summary.InteractedPageCount.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                .AppendLine("</li>");
            builder.Append("      <li>Applied interactions: ")
                .Append(HtmlEncode(summary.InteractionCount.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                .AppendLine("</li>");
            foreach (var item in summary.InteractionCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li><code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: ")
                    .Append(HtmlEncode(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</li>");
            }
            builder.AppendLine("    </ul>");
            builder.AppendLine("  </section>");
        }

        if (summary.OfflineReadinessCounts.Count > 0 || summary.OfflineDependencyKinds.Count > 0) {
            builder.AppendLine("  <section>");
            builder.AppendLine("    <h2>Offline Readiness</h2>");
            builder.AppendLine("    <ul>");
            builder.Append("      <li>Pages with offline-runtime risk signals: ")
                .Append(HtmlEncode(summary.OfflineRiskPageCount.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                .AppendLine("</li>");
            builder.Append("      <li>Offline readiness grade: <code>")
                .Append(HtmlEncode(summary.OfflineReadinessGrade))
                .AppendLine("</code></li>");
            builder.Append("      <li>Total offline-runtime diagnostics: ")
                .Append(HtmlEncode(summary.OfflineRiskDiagnosticCount.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                .AppendLine("</li>");
            builder.Append("      <li>Pages with high-severity offline-runtime signals: ")
                .Append(HtmlEncode(summary.HighOfflineRiskPageCount.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                .AppendLine("</li>");
            foreach (var item in summary.OfflineReadinessCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li>Offline grade <code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: ")
                    .Append(HtmlEncode(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</li>");
            }
            foreach (var item in summary.OfflineReadinessCountsByState.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li>Offline state <code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: ")
                    .Append(HtmlEncode(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</li>");
            }
            foreach (var item in summary.OfflineDependencySeverityCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li>Offline severity <code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: ")
                    .Append(HtmlEncode(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</li>");
            }
            foreach (var item in summary.OfflineDependencyKinds.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li>Offline dependency <code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: ")
                    .Append(HtmlEncode(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</li>");
            }
            builder.AppendLine("    </ul>");
            builder.AppendLine("  </section>");
        }

        if (summary.GraphNodeCategories.Count > 0 || summary.GraphEdgeRelations.Count > 0 || summary.GraphSkippedNodeReasons.Count > 0) {
            builder.AppendLine("  <section>");
            builder.AppendLine("    <h2>Graph Summary</h2>");
            builder.AppendLine("    <ul>");
            foreach (var item in summary.GraphNodeCategories.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li>Node category <code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: ")
                    .Append(HtmlEncode(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</li>");
            }
            foreach (var item in summary.GraphEdgeRelations.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li>Edge relation <code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: ")
                    .Append(HtmlEncode(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</li>");
            }
            foreach (var item in summary.GraphSkippedNodeReasons.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                builder.Append("      <li>Skipped-node reason <code>")
                    .Append(HtmlEncode(item.Key))
                    .Append("</code>: ")
                    .Append(HtmlEncode(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</li>");
            }
            builder.AppendLine("    </ul>");
            builder.AppendLine("  </section>");
        }

        builder.AppendLine("  <section>");
        builder.AppendLine("    <h2>Pages</h2>");
        builder.AppendLine("    <table>");
        builder.AppendLine("      <thead><tr><th>Title</th><th>URL</th><th>Status</th><th>Search</th><th>Files</th></tr></thead>");
        builder.AppendLine("      <tbody>");
        foreach (HtmlCrawlPage page in result.Pages.OrderBy(page => page.Depth).ThenBy(page => page.Url, StringComparer.OrdinalIgnoreCase)) {
            PageSearchMetadata searchMetadata = BuildPageSearchMetadata(page);
            builder.AppendLine("        <tr>");
            builder.Append("          <td>")
                .Append(HtmlEncode(string.IsNullOrWhiteSpace(page.Title) ? "(untitled)" : page.Title))
                .AppendLine("</td>");
            builder.Append("          <td><a href=\"")
                .Append(HtmlEncode(page.Url))
                .Append("\">")
                .Append(HtmlEncode(page.Url))
                .AppendLine("</a></td>");
            builder.Append("          <td><code>")
                .Append(HtmlEncode(page.Status.ToString()))
                .Append("</code>");
            if (page.StatusCode.HasValue) {
                builder.Append(" <span class=\"muted\">")
                    .Append(HtmlEncode(page.StatusCode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .Append("</span>");
            }
            builder.Append("<br><span class=\"muted\">")
                .Append(HtmlEncode(page.RenderMode.ToString()));
            if (page.RenderReasonCode != HtmlCrawlRenderReasonCode.None) {
                builder.Append(" / ")
                    .Append(HtmlEncode(page.RenderReasonCode.ToString()));
            }
            if (!string.IsNullOrWhiteSpace(page.RenderReason)) {
                builder.Append(": ")
                    .Append(HtmlEncode(page.RenderReason));
            }
            if (page.AppliedScenario != HtmlCrawlScenario.Custom) {
                builder.Append("<br>scenario: <code>")
                    .Append(HtmlEncode(page.AppliedScenario.ToString()))
                    .Append("</code>");
            }
            if (!string.IsNullOrWhiteSpace(page.AppliedProfileName)) {
                builder.Append("<br>profile: <code>")
                    .Append(HtmlEncode(page.AppliedProfileName))
                    .Append("</code>");
                if (page.AppliedProfileReasonCode != HtmlCrawlProfileSelectionReasonCode.None) {
                    builder.Append(" <span class=\"muted\">(")
                        .Append(HtmlEncode(page.AppliedProfileReasonCode.ToString()))
                        .Append(")</span>");
                }
                if (!string.IsNullOrWhiteSpace(page.AppliedProfileReason)) {
                    builder.Append("<br>")
                        .Append(HtmlEncode(page.AppliedProfileReason));
                }
            }
            if (page.AppliedInteractions.Count > 0) {
                builder.Append("<br>interactions: ")
                    .Append(HtmlEncode(string.Join(" | ", page.AppliedInteractions)));
            }
            if (page.OfflineDependencyDiagnostics.Count > 0) {
                builder.Append("<br>offline risk: ")
                    .Append(HtmlEncode(page.OfflineDependencyDiagnosticCount.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .Append(" <span class=\"muted\">(")
                    .Append(HtmlEncode(page.OfflineReadinessGrade))
                    .Append("; ")
                    .Append(HtmlEncode(page.HighestOfflineRiskSeverity))
                    .Append("; ")
                    .Append(HtmlEncode(page.OfflineDependencyKindsSummary))
                    .Append(")</span>");
            }
            builder.Append("<br>content: ")
                .Append(HtmlEncode(page.ContentModeUsed.ToString()));
            if (page.ContentSelectionReasonCode != HtmlCrawlContentSelectionReasonCode.None) {
                builder.Append(" / ")
                    .Append(HtmlEncode(page.ContentSelectionReasonCode.ToString()));
            }
            if (!string.IsNullOrWhiteSpace(page.ContentElementSelectorHint)) {
                builder.Append(" <code>")
                    .Append(HtmlEncode(page.ContentElementSelectorHint))
                    .Append("</code>");
            }
            if (!string.IsNullOrWhiteSpace(page.ContentSelectionReason)) {
                builder.Append("<br>")
                    .Append(HtmlEncode(page.ContentSelectionReason));
            }
            if (page.ContentSelectionScore.HasValue) {
                builder.Append("<br>score: ")
                    .Append(HtmlEncode(page.ContentSelectionScore.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)));
                if (page.ReaderCandidateCount > 0) {
                    builder.Append(" <span class=\"muted\">(")
                        .Append(HtmlEncode(page.ReaderCandidateCount.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                        .Append(" candidates)</span>");
                }
            }
            if (!string.IsNullOrWhiteSpace(page.ReaderRootElementSelectorHint)) {
                builder.Append("<br>reader root: <code>")
                    .Append(HtmlEncode(page.ReaderRootElementSelectorHint))
                    .Append("</code>");
            }
            if (page.ContentComparisons.Count > 0) {
                builder.Append("<br>comparisons: ")
                    .Append(HtmlEncode(string.Join(" | ", page.ContentComparisons.Select(comparison => comparison.Mode.ToString()))));
                if (!string.IsNullOrWhiteSpace(page.ContentComparisonDeltaSummary)) {
                    builder.Append("<br>deltas: ")
                        .Append(HtmlEncode(page.ContentComparisonDeltaSummary));
                }
                if (!string.IsNullOrWhiteSpace(page.ContentComparisonPreviewSummary)) {
                    builder.Append("<br>preview: ")
                        .Append(HtmlEncode(page.ContentComparisonPreviewSummary));
                }
            }
            if (page.BestContentComparisonMode.HasValue) {
                builder.Append("<br>best comparison: <code>")
                    .Append(HtmlEncode(page.BestContentComparisonMode.Value.ToString()))
                    .Append("</code>");
                if (page.BestContentComparisonWordCount.HasValue) {
                    builder.Append(" ")
                        .Append(HtmlEncode(page.BestContentComparisonWordCount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                        .Append(" words");
                }
                if (page.BestContentComparisonReasonCode.HasValue) {
                    builder.Append(" <span class=\"muted\">(")
                        .Append(HtmlEncode(page.BestContentComparisonReasonCode.Value.ToString()))
                        .Append(")</span>");
                }
                if (page.RunnerUpContentComparisonMode.HasValue && page.BestContentComparisonWordDelta.HasValue) {
                    builder.Append("<br>delta vs <code>")
                        .Append(HtmlEncode(page.RunnerUpContentComparisonMode.Value.ToString()))
                        .Append("</code>: +")
                        .Append(HtmlEncode(page.BestContentComparisonWordDelta.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                        .Append(" words");
                }
            }
            builder.Append("</span>");
            builder.AppendLine("</td>");
            builder.Append("          <td class=\"summary\"><strong>")
                .Append(HtmlEncode(searchMetadata.WordCount.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                .Append("</strong> words");
            builder.Append(" <span class=\"muted\">(")
                .Append(HtmlEncode(searchMetadata.ChunkCount.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                .Append(" chunks)</span>");
            if (searchMetadata.Headings.Length > 0) {
                builder.Append("<br><span class=\"muted\">")
                    .Append(HtmlEncode(string.Join(" | ", searchMetadata.Headings.Take(3))))
                    .Append("</span>");
            }
            if (searchMetadata.Keywords.Length > 0) {
                builder.Append("<br><span class=\"muted\">keywords: ")
                    .Append(HtmlEncode(string.Join(", ", searchMetadata.Keywords.Take(6))))
                    .Append("</span>");
            }
            if (!string.IsNullOrWhiteSpace(searchMetadata.Summary)) {
                builder.Append("<br>")
                    .Append(HtmlEncode(searchMetadata.Summary));
            }
            builder.AppendLine("</td>");
            builder.Append("          <td>");
            AppendOptionalFileLink(builder, indexHtmlPath, "HTML", page.HtmlPath);
            if (!string.IsNullOrWhiteSpace(page.HtmlPath) && HasAnyFollowingFile(page.TextPath, page.MarkdownPath, page.StructuredJsonPath, page.ManifestPath)) {
                builder.Append(" | ");
            }
            AppendOptionalFileLink(builder, indexHtmlPath, "Text", page.TextPath);
            if (!string.IsNullOrWhiteSpace(page.TextPath) && HasAnyFollowingFile(page.MarkdownPath, page.StructuredJsonPath, page.ManifestPath)) {
                builder.Append(" | ");
            }
            AppendOptionalFileLink(builder, indexHtmlPath, "Markdown", page.MarkdownPath);
            if (!string.IsNullOrWhiteSpace(page.MarkdownPath) && HasAnyFollowingFile(page.StructuredJsonPath, page.ManifestPath)) {
                builder.Append(" | ");
            }
            AppendOptionalFileLink(builder, indexHtmlPath, "Structured JSON", page.StructuredJsonPath);
            if (!string.IsNullOrWhiteSpace(page.StructuredJsonPath) && HasAnyFollowingFile(page.ManifestPath)) {
                builder.Append(" | ");
            }
            AppendOptionalFileLink(builder, indexHtmlPath, "Manifest", page.ManifestPath);
            builder.AppendLine("</td>");
            builder.AppendLine("        </tr>");
        }
        builder.AppendLine("      </tbody>");
        builder.AppendLine("    </table>");
        builder.AppendLine("  </section>");

        if (result.Assets.Count > 0) {
            builder.AppendLine("  <section>");
            builder.AppendLine("    <h2>Assets</h2>");
            builder.AppendLine("    <table>");
            builder.AppendLine("      <thead><tr><th>URL</th><th>Source Page</th><th>Type</th><th>File</th></tr></thead>");
            builder.AppendLine("      <tbody>");
            foreach (HtmlCrawlAsset asset in result.Assets.OrderBy(asset => asset.PageUrl, StringComparer.OrdinalIgnoreCase).ThenBy(asset => asset.Url, StringComparer.OrdinalIgnoreCase)) {
                builder.AppendLine("        <tr>");
                builder.Append("          <td><a href=\"")
                    .Append(HtmlEncode(asset.Url))
                    .Append("\">")
                    .Append(HtmlEncode(asset.Url))
                    .AppendLine("</a></td>");
                builder.Append("          <td>");
                if (!string.IsNullOrWhiteSpace(asset.PageUrl)) {
                    builder.Append("<a href=\"")
                        .Append(HtmlEncode(asset.PageUrl))
                        .Append("\">")
                        .Append(HtmlEncode(asset.PageUrl))
                        .Append("</a>");
                } else {
                    builder.Append("&nbsp;");
                }
                builder.AppendLine("</td>");
                builder.Append("          <td>")
                    .Append(HtmlEncode(asset.ContentType ?? string.Empty))
                    .AppendLine("</td>");
                builder.Append("          <td>");
                AppendOptionalFileLink(builder, indexHtmlPath, Path.GetFileName(asset.FilePath), asset.FilePath);
                builder.AppendLine("</td>");
                builder.AppendLine("        </tr>");
            }
            builder.AppendLine("      </tbody>");
            builder.AppendLine("    </table>");
            builder.AppendLine("  </section>");
        }

        if (skippedContentPages.Count > 0) {
            builder.AppendLine("  <section>");
            builder.AppendLine("    <h2>Skipped Pages</h2>");
            builder.AppendLine("    <table>");
            builder.AppendLine("      <thead><tr><th>URL</th><th>Reason</th><th>Offline</th><th>Depth</th></tr></thead>");
            builder.AppendLine("      <tbody>");
            foreach (HtmlCrawlPage page in skippedContentPages) {
                builder.AppendLine("        <tr>");
                builder.Append("          <td><a href=\"")
                    .Append(HtmlEncode(page.Url))
                    .Append("\">")
                    .Append(HtmlEncode(page.Url))
                    .AppendLine("</a></td>");
                builder.Append("          <td><code>")
                    .Append(HtmlEncode(page.SkipReason.ToString()))
                    .AppendLine("</code></td>");
                builder.Append("          <td><code>")
                    .Append(HtmlEncode(page.OfflineReadinessGrade))
                    .Append("</code> <span class=\"muted\">(")
                    .Append(HtmlEncode(page.HighestOfflineRiskSeverity))
                    .AppendLine(")</span></td>");
                builder.Append("          <td>")
                    .Append(HtmlEncode(page.Depth.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</td>");
                builder.AppendLine("        </tr>");
            }
            builder.AppendLine("      </tbody>");
            builder.AppendLine("    </table>");
            builder.AppendLine("  </section>");
        }

        if (skippedAssetPages.Count > 0) {
            builder.AppendLine("  <section>");
            builder.AppendLine("    <h2>Skipped Assets</h2>");
            builder.AppendLine("    <p class=\"muted\">These URLs were discovered as asset candidates and intentionally not crawled as content pages.</p>");
            builder.AppendLine("    <table>");
            builder.AppendLine("      <thead><tr><th>URL</th><th>Reason</th><th>Offline</th><th>Depth</th></tr></thead>");
            builder.AppendLine("      <tbody>");
            foreach (HtmlCrawlPage page in skippedAssetPages) {
                builder.AppendLine("        <tr>");
                builder.Append("          <td><a href=\"")
                    .Append(HtmlEncode(page.Url))
                    .Append("\">")
                    .Append(HtmlEncode(page.Url))
                    .AppendLine("</a></td>");
                builder.Append("          <td><code>")
                    .Append(HtmlEncode(page.SkipReason.ToString()))
                    .AppendLine("</code></td>");
                builder.Append("          <td><code>")
                    .Append(HtmlEncode(page.OfflineReadinessGrade))
                    .Append("</code> <span class=\"muted\">(")
                    .Append(HtmlEncode(page.HighestOfflineRiskSeverity))
                    .AppendLine(")</span></td>");
                builder.Append("          <td>")
                    .Append(HtmlEncode(page.Depth.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</td>");
                builder.AppendLine("        </tr>");
            }
            builder.AppendLine("      </tbody>");
            builder.AppendLine("    </table>");
            builder.AppendLine("  </section>");
        }

        if (result.PendingPages.Count > 0) {
            builder.AppendLine("  <section>");
            builder.AppendLine("    <h2>Pending Pages</h2>");
            builder.AppendLine("    <table>");
            builder.AppendLine("      <thead><tr><th>URL</th><th>Parent</th><th>Offline</th><th>Depth</th></tr></thead>");
            builder.AppendLine("      <tbody>");
            foreach (HtmlCrawlPendingItem page in result.PendingPages.OrderBy(page => page.Depth).ThenBy(page => page.Url, StringComparer.OrdinalIgnoreCase)) {
                builder.AppendLine("        <tr>");
                builder.Append("          <td><a href=\"")
                    .Append(HtmlEncode(page.Url))
                    .Append("\">")
                    .Append(HtmlEncode(page.Url))
                    .AppendLine("</a></td>");
                builder.Append("          <td>");
                if (!string.IsNullOrWhiteSpace(page.ParentUrl)) {
                    builder.Append("<a href=\"")
                        .Append(HtmlEncode(page.ParentUrl))
                        .Append("\">")
                        .Append(HtmlEncode(page.ParentUrl))
                        .Append("</a>");
                } else {
                    builder.Append("&nbsp;");
                }
                builder.AppendLine("</td>");
                builder.Append("          <td><code>")
                    .Append(HtmlEncode(page.OfflineReadinessGrade))
                    .Append("</code> <span class=\"muted\">(")
                    .Append(HtmlEncode(page.HighestOfflineRiskSeverity))
                    .AppendLine(")</span></td>");
                builder.Append("          <td>")
                    .Append(HtmlEncode(page.Depth.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .AppendLine("</td>");
                builder.AppendLine("        </tr>");
            }
            builder.AppendLine("      </tbody>");
            builder.AppendLine("    </table>");
            builder.AppendLine("  </section>");
        }

        builder.AppendLine("</body>");
        builder.AppendLine("</html>");
        return builder.ToString();
    }

    private static void AppendStatCard(StringBuilder builder, string label, string value) {
        builder.Append("      <div class=\"stat\"><strong>")
            .Append(HtmlEncode(value))
            .Append("</strong><span>")
            .Append(HtmlEncode(label))
            .AppendLine("</span></div>");
    }

    private static void AppendArtifactLink(StringBuilder builder, string indexHtmlPath, string label, string? path) {
        if (string.IsNullOrWhiteSpace(path)) {
            return;
        }

        builder.Append("      <li>");
        AppendOptionalFileLink(builder, indexHtmlPath, label, path);
        builder.AppendLine("</li>");
    }

    private static void AppendOptionalFileLink(StringBuilder builder, string indexHtmlPath, string? label, string? path) {
        if (string.IsNullOrWhiteSpace(path)) {
            builder.Append("&nbsp;");
            return;
        }

        string relative = BuildRelativePath(indexHtmlPath, path!);
        builder.Append("<a href=\"")
            .Append(HtmlEncode(relative))
            .Append("\">")
            .Append(HtmlEncode(string.IsNullOrWhiteSpace(label) ? Path.GetFileName(path) : label))
            .Append("</a>");
    }

    private static bool HasAnyFollowingFile(params string?[] paths) => paths.Any(path => !string.IsNullOrWhiteSpace(path));

    private static string HtmlEncode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string BuildPageSlug(HtmlCrawlPage page, string prefix) {
        string source = !string.IsNullOrWhiteSpace(page.Title)
            ? page.Title!
            : page.Url;
        string slug = Regex.Replace(source, @"[^A-Za-z0-9\-]+", "-").Trim('-');
        if (string.IsNullOrEmpty(slug)) {
            slug = "page";
        }
        if (slug.Length > 48) {
            slug = slug.Substring(0, 48).Trim('-');
        }

        return $"{prefix}-{slug}";
    }

    private static CrawlArtifactPaths ResolveArtifactPaths(string path) {
        string fullPath = HtmlUtilities.ResolvePath(path);
        bool treatAsDirectory = Directory.Exists(fullPath) || !Path.HasExtension(fullPath);

        if (treatAsDirectory) {
            Directory.CreateDirectory(fullPath);
            string pagesDirectory = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "pages"), fullPath);
            string assetsDirectory = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "assets"), fullPath);
            Directory.CreateDirectory(pagesDirectory);
            Directory.CreateDirectory(assetsDirectory);
            return new CrawlArtifactPaths {
                ManifestPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "crawl-result.json"), fullPath),
                PagesDirectory = pagesDirectory,
                AssetsDirectory = assetsDirectory,
                PagesJsonlPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "pages.jsonl"), fullPath),
                PagesCsvPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "pages.csv"), fullPath),
                SkippedPagesJsonlPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "skipped-pages.jsonl"), fullPath),
                SkippedAssetsJsonlPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "skipped-assets.jsonl"), fullPath),
                LinksJsonlPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "links.jsonl"), fullPath),
                AssetsJsonlPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "assets.jsonl"), fullPath),
                StructuredJsonPagesJsonlPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "structured-pages.jsonl"), fullPath),
                OpenApiLikeJsonPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "openapi-like.json"), fullPath),
                OpenApiJsonPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "openapi.json"), fullPath),
                ChunksJsonlPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "chunks.jsonl"), fullPath),
                GraphJsonPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "graph.json"), fullPath),
                SummaryJsonPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "summary.json"), fullPath),
                SummaryTextPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "summary.txt"), fullPath),
                IndexHtmlPath = EnsurePathIsWithinDirectory(Path.Combine(fullPath, "index.html"), fullPath)
            };
        }

        string manifestPath = HtmlUtilities.EnsureDirectoryExists(fullPath);
        string baseDirectory = Path.GetDirectoryName(manifestPath) ?? Path.GetDirectoryName(fullPath) ?? fullPath;
        string pagesDirectoryForFile = EnsurePathIsWithinDirectory(Path.Combine(baseDirectory, Path.GetFileNameWithoutExtension(manifestPath) + ".pages"), baseDirectory);
        string assetsDirectoryForFile = EnsurePathIsWithinDirectory(Path.Combine(baseDirectory, Path.GetFileNameWithoutExtension(manifestPath) + ".assets"), baseDirectory);
        Directory.CreateDirectory(pagesDirectoryForFile);
        Directory.CreateDirectory(assetsDirectoryForFile);
        string stem = EnsurePathIsWithinDirectory(Path.Combine(baseDirectory, Path.GetFileNameWithoutExtension(manifestPath)), baseDirectory);
        return new CrawlArtifactPaths {
            ManifestPath = EnsurePathIsWithinDirectory(manifestPath, baseDirectory),
            PagesDirectory = pagesDirectoryForFile,
            AssetsDirectory = assetsDirectoryForFile,
            PagesJsonlPath = EnsurePathIsWithinDirectory(stem + ".pages.jsonl", baseDirectory),
            PagesCsvPath = EnsurePathIsWithinDirectory(stem + ".pages.csv", baseDirectory),
            SkippedPagesJsonlPath = EnsurePathIsWithinDirectory(stem + ".skipped.jsonl", baseDirectory),
            SkippedAssetsJsonlPath = EnsurePathIsWithinDirectory(stem + ".skipped-assets.jsonl", baseDirectory),
            LinksJsonlPath = EnsurePathIsWithinDirectory(stem + ".links.jsonl", baseDirectory),
            AssetsJsonlPath = EnsurePathIsWithinDirectory(stem + ".assets.jsonl", baseDirectory),
            StructuredJsonPagesJsonlPath = EnsurePathIsWithinDirectory(stem + ".structured-pages.jsonl", baseDirectory),
            OpenApiLikeJsonPath = EnsurePathIsWithinDirectory(stem + ".openapi-like.json", baseDirectory),
            OpenApiJsonPath = EnsurePathIsWithinDirectory(stem + ".openapi.json", baseDirectory),
            ChunksJsonlPath = EnsurePathIsWithinDirectory(stem + ".chunks.jsonl", baseDirectory),
            GraphJsonPath = EnsurePathIsWithinDirectory(stem + ".graph.json", baseDirectory),
            SummaryJsonPath = EnsurePathIsWithinDirectory(stem + ".summary.json", baseDirectory),
            SummaryTextPath = EnsurePathIsWithinDirectory(stem + ".summary.txt", baseDirectory),
            IndexHtmlPath = EnsurePathIsWithinDirectory(stem + ".index.html", baseDirectory)
        };
    }

    private static string ResolveManifestPath(string path) {
        string fullPath = HtmlUtilities.ResolvePath(path);
        if (Directory.Exists(fullPath) || !Path.HasExtension(fullPath)) {
            return Path.Combine(fullPath, "crawl-result.json");
        }

        return fullPath;
    }

    private static async Task<IReadOnlyDictionary<string, HtmlCrawlJsonSchemaField>> LoadStructuredSchemaAsync(HtmlCrawlOptions options, CancellationToken cancellationToken) {
        string? schemaJson = options.StructuredJsonSchema;
        if (string.IsNullOrWhiteSpace(schemaJson) && !string.IsNullOrWhiteSpace(options.StructuredJsonSchemaPath)) {
            schemaJson = await HtmlUtilities.ReadFileCheckedAsync(options.StructuredJsonSchemaPath!, cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(schemaJson)) {
            return new Dictionary<string, HtmlCrawlJsonSchemaField>(StringComparer.OrdinalIgnoreCase);
        }

        return ParseStructuredSchema(schemaJson!);
    }

    private static IReadOnlyDictionary<string, HtmlCrawlJsonSchemaField> ParseStructuredSchema(string schemaJson) {
        using JsonDocument document = JsonDocument.Parse(schemaJson);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) {
            throw new ArgumentException("Structured JSON schema must be an object.", nameof(schemaJson));
        }

        if (root.TryGetProperty("fields", out JsonElement fieldsElement) && fieldsElement.ValueKind == JsonValueKind.Object) {
            root = fieldsElement;
        }

        Dictionary<string, HtmlCrawlJsonSchemaField> fields = new(StringComparer.OrdinalIgnoreCase);
        JsonSerializerOptions options = new() {
            PropertyNameCaseInsensitive = true
        };
        foreach (JsonProperty property in root.EnumerateObject()) {
            fields[property.Name] = property.Value.ValueKind switch {
                JsonValueKind.String => new HtmlCrawlJsonSchemaField {
                    Path = property.Value.GetString()
                },
                JsonValueKind.Object => JsonSerializer.Deserialize<HtmlCrawlJsonSchemaField>(property.Value.GetRawText(), options) ?? new HtmlCrawlJsonSchemaField(),
                _ => throw new ArgumentException($"Structured JSON schema field '{property.Name}' must be a string path or an object rule.", nameof(schemaJson))
            };
        }

        return fields;
    }

    private static JsonSerializerOptions CreateJsonOptions() {
        JsonSerializerOptions options = new() {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static async Task WriteTextAsync(string path, string content, CancellationToken cancellationToken) {
        string fullPath = HtmlUtilities.EnsureDirectoryExists(path);
#if NETSTANDARD2_0 || NETFRAMEWORK
        await Task.Run(() => File.WriteAllText(fullPath, content), cancellationToken).ConfigureAwait(false);
#else
        await File.WriteAllTextAsync(fullPath, content, cancellationToken).ConfigureAwait(false);
#endif
    }

    private static async Task WriteBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken) {
        string fullPath = HtmlUtilities.EnsureDirectoryExists(path);
#if NETSTANDARD2_0 || NETFRAMEWORK
        await Task.Run(() => File.WriteAllBytes(fullPath, bytes), cancellationToken).ConfigureAwait(false);
#else
        await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken).ConfigureAwait(false);
#endif
    }

    private static string EscapeCsv(string? value) {
        if (string.IsNullOrEmpty(value)) {
            return string.Empty;
        }

        string normalized = value!.Replace("\r\n", "\n").Replace('\r', '\n');
        if (normalized.IndexOfAny(new[] { ',', '"', '\n' }) >= 0) {
            return "\"" + normalized.Replace("\"", "\"\"") + "\"";
        }

        return normalized;
    }

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
            using HttpResponseMessage response = await client.GetAsync(request.Uri, cancellationToken).ConfigureAwait(false);
            page.StatusCode = (int)response.StatusCode;
            page.ContentType = response.Content.Headers.ContentType?.MediaType ?? response.Content.Headers.ContentType?.ToString();
            response.EnsureSuccessStatusCode();

            byte[] bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
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
            options?.ListingCardMetadataMode ?? HtmlListingCardMetadataMode.SuppressInRepeatedCards);
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

    private static string? ExtractTitle(string html) {
        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        return document.Title;
    }

    private static string? ExtractCanonicalUrl(string html, Uri baseUri, HtmlCrawlOptions options) {
        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        Uri effectiveBaseUri = GetDocumentBaseUri(document, baseUri);
        IElement? canonical = document.QuerySelector("link[rel='canonical'][href], link[rel~='canonical'][href]");
        string? href = canonical?.GetAttribute("href");
        if (string.IsNullOrWhiteSpace(href)) {
            return null;
        }

        if (!Uri.TryCreate(effectiveBaseUri, href, out Uri? resolved)) {
            return null;
        }

        if (resolved.Scheme != Uri.UriSchemeHttp && resolved.Scheme != Uri.UriSchemeHttps) {
            return null;
        }

        return NormalizeUrl(resolved, options);
    }

    private static ContentSelectionResult SelectContent(string html, HtmlCrawlOptions options) {
        if (string.IsNullOrWhiteSpace(html)) {
            return new ContentSelectionResult {
                ModeUsed = options.ContentMode,
                ReasonCode = HtmlCrawlContentSelectionReasonCode.None,
                Reason = "No HTML was available for content selection.",
                Html = html
            };
        }

        if (options.ContentMode == HtmlCrawlContentMode.Raw && string.IsNullOrWhiteSpace(options.Selector)) {
            return new ContentSelectionResult {
                ModeUsed = HtmlCrawlContentMode.Raw,
                ReasonCode = HtmlCrawlContentSelectionReasonCode.RawDocument,
                Reason = "Raw mode kept the full fetched document because no selector was configured.",
                Html = html
            };
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        switch (options.ContentMode) {
            case HtmlCrawlContentMode.Raw:
                IElement? rawElement = document.QuerySelector(options.Selector!);
                if (rawElement != null) {
                    return BuildContentSelectionResult(
                        HtmlCrawlContentMode.Raw,
                        HtmlCrawlContentSelectionReasonCode.RawSelector,
                        $"Raw mode selected the exact configured selector '{options.Selector}'.",
                        rawElement);
                }

                return new ContentSelectionResult {
                    ModeUsed = HtmlCrawlContentMode.Raw,
                    ReasonCode = HtmlCrawlContentSelectionReasonCode.RawSelectorMiss,
                    Reason = $"Raw mode produced no stored content because selector '{options.Selector}' was not found.",
                    Html = string.Empty
                };
            case HtmlCrawlContentMode.Reader:
                return SelectReaderContent(document, html, options);
            default:
                if (!string.IsNullOrWhiteSpace(options.Selector)) {
                    IElement? selected = document.QuerySelector(options.Selector!);
                    if (selected != null) {
                        return BuildContentSelectionResult(
                            HtmlCrawlContentMode.Focused,
                            HtmlCrawlContentSelectionReasonCode.FocusedSelector,
                            $"Focused mode selected the configured selector '{options.Selector}'.",
                            selected);
                    }
                }

                if ((string.IsNullOrWhiteSpace(options.Selector) || LooksLikeSemanticContentSelector(options.Selector))
                    && TrySelectPreferredContentElement(document, out IElement? fallback)) {
                    return BuildContentSelectionResult(
                        HtmlCrawlContentMode.Focused,
                        HtmlCrawlContentSelectionReasonCode.FocusedSemanticFallback,
                        $"Focused mode fell back to semantic content element {DescribeElement(fallback!)}.",
                        fallback!);
                }

                return new ContentSelectionResult {
                    ModeUsed = HtmlCrawlContentMode.Focused,
                    ReasonCode = HtmlCrawlContentSelectionReasonCode.FocusedFullDocumentFallback,
                    Reason = string.IsNullOrWhiteSpace(options.Selector)
                        ? "Focused mode kept the full document because no preferred content element was found."
                        : $"Focused mode kept the full document because selector '{options.Selector}' did not match and no semantic fallback was found.",
                    Html = html
                };
        }
    }

    private static bool LooksLikeSemanticContentSelector(string? selector) {
        if (string.IsNullOrWhiteSpace(selector)) {
            return false;
        }

        string normalized = selector!.Trim().ToLowerInvariant();
        return normalized is "main"
            or "[role='main']"
            or "[role=\"main\"]"
            or "#main"
            or "#main-content"
            or ".main-content"
            or ".site-main"
            or "#content"
            or ".content"
            or ".entry-content"
            or ".post-content"
            or "article";
    }

    private static IList<HtmlCrawlContentComparison> BuildContentComparisons(string html, HtmlCrawlOptions options) {
        List<HtmlCrawlContentComparison> comparisons = new();
        foreach (HtmlCrawlContentMode mode in new[] { HtmlCrawlContentMode.Raw, HtmlCrawlContentMode.Focused, HtmlCrawlContentMode.Reader }) {
            HtmlCrawlOptions comparisonOptions = options.Clone();
            comparisonOptions.ContentMode = mode;
            ContentSelectionResult selection = SelectContent(html, comparisonOptions);
            string selectedHtml = ApplyContentCleanup(selection.Html, comparisonOptions);
            string text = HtmlParserToText.ConvertToText(PrepareHtmlForTextExtraction(selectedHtml, comparisonOptions));
            comparisons.Add(new HtmlCrawlContentComparison {
                Mode = mode,
                ReasonCode = selection.ReasonCode,
                Reason = selection.Reason,
                ElementSelectorHint = BuildElementSelectorHint(selection.Element),
                WordCount = CountWords(text),
                CharacterCount = text.Length,
                Summary = BuildSummary(text),
                Score = selection.Score,
                ReaderCandidateCount = selection.ReaderCandidateCount,
                ReaderRootElementSelectorHint = selection.ReaderRootElementSelectorHint
            });
        }

        return comparisons;
    }

    private static IList<HtmlCrawlContentComparison> CloneContentComparisons(IEnumerable<HtmlCrawlContentComparison> comparisons) {
        return comparisons.Select(comparison => new HtmlCrawlContentComparison {
            Mode = comparison.Mode,
            ReasonCode = comparison.ReasonCode,
            Reason = comparison.Reason,
            ElementSelectorHint = comparison.ElementSelectorHint,
            WordCount = comparison.WordCount,
            CharacterCount = comparison.CharacterCount,
            Summary = comparison.Summary,
            Score = comparison.Score,
            ReaderCandidateCount = comparison.ReaderCandidateCount,
            ReaderRootElementSelectorHint = comparison.ReaderRootElementSelectorHint
        }).ToList();
    }

    private static HtmlCrawlContentComparison? GetBestContentComparison(IEnumerable<HtmlCrawlContentComparison> comparisons) {
        List<HtmlCrawlContentComparison> comparisonList = comparisons.ToList();
        if (comparisonList.Count == 0) {
            return null;
        }

        int maxWordCount = comparisonList.Max(comparison => comparison.WordCount);
        return comparisonList
            .Where(comparison => comparison.WordCount >= maxWordCount - 10)
            .OrderBy(comparison => GetContentModePreference(comparison.Mode))
            .ThenByDescending(comparison => comparison.WordCount)
            .ThenByDescending(comparison => comparison.CharacterCount)
            .FirstOrDefault();
    }

    private static HtmlCrawlContentComparison? GetRunnerUpContentComparison(
        IEnumerable<HtmlCrawlContentComparison> comparisons,
        HtmlCrawlContentComparison? bestComparison) {
        if (bestComparison == null) {
            return null;
        }

        return comparisons
            .Where(comparison => comparison.Mode != bestComparison.Mode)
            .OrderByDescending(comparison => comparison.WordCount)
            .ThenByDescending(comparison => comparison.CharacterCount)
            .ThenBy(comparison => GetContentModePreference(comparison.Mode))
            .FirstOrDefault();
    }

    private static string? BuildContentComparisonDeltaSummary(
        IEnumerable<HtmlCrawlContentComparison> comparisons,
        HtmlCrawlContentComparison? bestComparison = null) {
        List<HtmlCrawlContentComparison> comparisonList = comparisons.ToList();
        if (comparisonList.Count == 0) {
            return null;
        }

        bestComparison ??= GetBestContentComparison(comparisonList);
        if (bestComparison == null) {
            return null;
        }

        return string.Join(" | ", comparisonList
            .OrderBy(comparison => comparison.Mode == bestComparison.Mode ? 0 : 1)
            .ThenBy(comparison => GetContentModePreference(comparison.Mode))
            .ThenByDescending(comparison => comparison.WordCount)
            .ThenByDescending(comparison => comparison.CharacterCount)
            .Select(comparison => {
                int delta = comparison.WordCount - bestComparison.WordCount;
                string formattedDelta = delta > 0
                    ? $"+{delta.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                    : delta.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return $"{comparison.Mode} {formattedDelta}";
            }));
    }

    private static string? BuildContentComparisonPreviewSummary(
        IEnumerable<HtmlCrawlContentComparison> comparisons,
        HtmlCrawlContentComparison? bestComparison = null) {
        List<HtmlCrawlContentComparison> comparisonList = comparisons.ToList();
        if (comparisonList.Count == 0) {
            return null;
        }

        bestComparison ??= GetBestContentComparison(comparisonList);
        return string.Join(" | ", comparisonList
            .OrderBy(comparison => bestComparison != null && comparison.Mode == bestComparison.Mode ? 0 : 1)
            .ThenBy(comparison => GetContentModePreference(comparison.Mode))
            .ThenByDescending(comparison => comparison.WordCount)
            .ThenByDescending(comparison => comparison.CharacterCount)
            .Select(comparison => {
                string summary = BuildCompactComparisonSummary(comparison.Summary);
                string selectorHint = string.IsNullOrWhiteSpace(comparison.ElementSelectorHint)
                    ? string.Empty
                    : $" @ {comparison.ElementSelectorHint}";
                return $"{comparison.Mode} {comparison.WordCount.ToString(System.Globalization.CultureInfo.InvariantCulture)}w{selectorHint}: {summary}";
            }));
    }

    private static string BuildCompactComparisonSummary(string? summary) {
        if (string.IsNullOrWhiteSpace(summary)) {
            return "(no summary)";
        }

        const int maxLength = 56;
        string normalized = NormalizeWhitespace(summary);
        if (normalized.Length <= maxLength) {
            return normalized;
        }

        int cut = normalized.LastIndexOf(' ', maxLength);
        if (cut < maxLength / 2) {
            cut = maxLength;
        }

        return normalized.Substring(0, cut).TrimEnd() + "...";
    }

    private static int GetContentModePreference(HtmlCrawlContentMode mode) {
        return mode switch {
            HtmlCrawlContentMode.Reader => 0,
            HtmlCrawlContentMode.Focused => 1,
            _ => 2
        };
    }

    private static bool TrySelectPreferredContentElement(IDocument document, out IElement? element) {
        foreach (string selector in ContentFallbackSelectors) {
            element = document.QuerySelector(selector);
            if (element != null) {
                return true;
            }
        }

        element = null;
        return false;
    }

    private static ContentSelectionResult SelectReaderContent(IDocument document, string html, HtmlCrawlOptions options) {
        IElement? root = null;
        string rootDescription = "document body";
        if (!string.IsNullOrWhiteSpace(options.Selector)) {
            root = document.QuerySelector(options.Selector!);
            if (root != null) {
                rootDescription = $"configured selector '{options.Selector}'";
            } else if (LooksLikeSemanticContentSelector(options.Selector) && TrySelectPreferredContentElement(document, out IElement? fallback)) {
                root = fallback;
                rootDescription = $"semantic fallback {DescribeElement(fallback!)}";
            }
        }

        root ??= document.Body;
        if (root != null && rootDescription == "document body" && !ReferenceEquals(root, document.Body)) {
            rootDescription = $"selected root {DescribeElement(root)}";
        }
        if (root == null) {
            root = document.DocumentElement;
            rootDescription = "document root";
        }

        if (root == null) {
            return new ContentSelectionResult {
                ModeUsed = HtmlCrawlContentMode.Reader,
                ReasonCode = HtmlCrawlContentSelectionReasonCode.ReaderRootFallback,
                Reason = "Reader mode kept the full document because no DOM root was available.",
                Html = html
            };
        }

        IElement selected = FindBestReaderCandidate(root, options, out bool usedRootFallback, out int candidateCount, out double? selectedScore) ?? root;
        if (usedRootFallback) {
            return BuildContentSelectionResult(
                HtmlCrawlContentMode.Reader,
                HtmlCrawlContentSelectionReasonCode.ReaderRootFallback,
                $"Reader mode started from {rootDescription} and kept {DescribeElement(selected)} because no stronger article-like candidate met the minimum score of {options.ReaderMinimumScore.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}.",
                selected,
                selectedScore,
                candidateCount,
                BuildElementSelectorHint(root));
        }

        return BuildContentSelectionResult(
            HtmlCrawlContentMode.Reader,
            HtmlCrawlContentSelectionReasonCode.ReaderBestCandidate,
            $"Reader mode started from {rootDescription} and selected the best-scoring content block {DescribeElement(selected)} with score {selectedScore?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}.",
            selected,
            selectedScore,
            candidateCount,
            BuildElementSelectorHint(root));
    }

    private static ContentSelectionResult BuildContentSelectionResult(
        HtmlCrawlContentMode mode,
        HtmlCrawlContentSelectionReasonCode reasonCode,
        string reason,
        IElement element,
        double? score = null,
        int readerCandidateCount = 0,
        string? readerRootElementSelectorHint = null) {
        return new ContentSelectionResult {
            ModeUsed = mode,
            ReasonCode = reasonCode,
            Reason = reason,
            Element = element,
            Html = element.OuterHtml,
            Score = score,
            ReaderCandidateCount = readerCandidateCount,
            ReaderRootElementSelectorHint = readerRootElementSelectorHint
        };
    }

    private static IList<string> GetElementClassNames(IElement? element) {
        if (element == null || element.ClassList.Length == 0) {
            return new List<string>();
        }

        return element.ClassList
            .Where(className => !string.IsNullOrWhiteSpace(className))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? BuildElementSelectorHint(IElement? element) {
        if (element == null) {
            return null;
        }

        StringBuilder selector = new();
        selector.Append(element.LocalName);
        if (!string.IsNullOrWhiteSpace(element.Id)) {
            selector.Append('#').Append(element.Id);
        }

        foreach (string className in element.ClassList.Where(className => !string.IsNullOrWhiteSpace(className)).Take(3)) {
            selector.Append('.').Append(className);
        }

        return selector.ToString();
    }

    private static string DescribeElement(IElement element) {
        return $"<{BuildElementSelectorHint(element) ?? element.LocalName}>";
    }

    private static IElement? FindBestReaderCandidate(
        IElement root,
        HtmlCrawlOptions options,
        out bool usedRootFallback,
        out int candidateCount,
        out double? selectedScore) {
        List<IElement> candidates = new() { root };
        candidates.AddRange(root.QuerySelectorAll("article, main, section, div"));
        List<IElement> distinctCandidates = candidates.Distinct().ToList();
        candidateCount = distinctCandidates.Count;

        IElement? best = null;
        double bestScore = double.MinValue;
        foreach (IElement candidate in distinctCandidates) {
            double score = ScoreReaderCandidate(candidate, options);
            if (score > bestScore) {
                best = candidate;
                bestScore = score;
            }
        }

        double rootScore = ScoreReaderCandidate(root, options);
        if (best == null) {
            usedRootFallback = true;
            selectedScore = NormalizeContentScore(rootScore);
            return root;
        }

        usedRootFallback = bestScore < options.ReaderMinimumScore;
        selectedScore = usedRootFallback
            ? NormalizeContentScore(rootScore)
            : NormalizeContentScore(bestScore);
        return usedRootFallback ? root : best;
    }

    private static double? NormalizeContentScore(double score) {
        return double.IsNegativeInfinity(score) || score == double.MinValue ? null : score;
    }

    private static double ScoreReaderCandidate(IElement element, HtmlCrawlOptions options) {
        if (!IsReaderCandidateElement(element)) {
            return double.MinValue;
        }

        string text = element.TextContent ?? string.Empty;
        int wordCount = CountWords(text);
        if (wordCount < options.ReaderMinimumWordCount) {
            return double.MinValue;
        }

        int paragraphCount = element.QuerySelectorAll("p").Length;
        int headingCount = element.QuerySelectorAll("h1, h2, h3").Length;
        int linkCount = element.QuerySelectorAll("a[href]").Length;
        int listItemCount = element.QuerySelectorAll("li").Length;
        int codeBlockCount = element.QuerySelectorAll("pre, code").Length;
        int tableCount = element.QuerySelectorAll("table").Length;
        int linkWordCount = CountWords(string.Join(" ", element.QuerySelectorAll("a[href]").Select(anchor => anchor.TextContent)));
        double linkDensity = (double)linkWordCount / Math.Max(1, wordCount);

        double score = wordCount;
        score += paragraphCount * 18;
        score += headingCount * 20;
        score += codeBlockCount * 14;
        score += tableCount * 10;

        string tagName = element.TagName.ToLowerInvariant();
        if (tagName == "article") {
            score += 35;
        } else if (tagName == "main") {
            score += 20;
        } else if (tagName == "section") {
            score += 10;
        }

        if (linkDensity >= 0.75) {
            score -= 80;
        } else if (linkDensity >= 0.55) {
            score -= 40;
        }

        if (listItemCount >= 5 && paragraphCount == 0) {
            score -= 45;
        }

        if (ContainsBoilerplateSignals(element)) {
            score -= 55;
        }

        if (IsLinkDenseBoilerplateBlock(element)) {
            score -= 80;
        }

        return score;
    }

    private static bool IsReaderCandidateElement(IElement element) {
        string tagName = element.TagName.ToLowerInvariant();
        return tagName is "article" or "main" or "section" or "div";
    }

    private static string PrepareHtmlForTextExtraction(string html, HtmlCrawlOptions options) {
        if (string.IsNullOrWhiteSpace(html)) {
            return html;
        }

        if (LooksLikeFullHtmlDocument(html)) {
            IDocument document = HtmlParser.ParseWithAngleSharp(html);
            if (options.HiddenContentMode == HtmlCrawlHiddenContentMode.RespectHidden) {
                RemoveHiddenElements(document);
            }
            StripBoilerplateElements(document, options);
            RemoveConfiguredElements(document, options);
            return document.DocumentElement?.OuterHtml ?? html;
        }

        IDocument fragment = HtmlParser.ParseWithAngleSharp($"<div id=\"__htmltinkerx_text\">{html}</div>");
        IElement? wrapper = fragment.QuerySelector("#__htmltinkerx_text");
        if (wrapper == null) {
            return html;
        }

        if (options.HiddenContentMode == HtmlCrawlHiddenContentMode.RespectHidden) {
            RemoveHiddenElements(wrapper);
        }
        StripBoilerplateElements(wrapper, options);
        RemoveConfiguredElements(wrapper, options);
        return wrapper.InnerHtml;
    }

    private static string ApplyContentCleanup(string html, HtmlCrawlOptions options) {
        if (string.IsNullOrWhiteSpace(html)) {
            return html;
        }

        bool hasConfiguredCleanup = options.ExcludeSelectors.Count > 0 || options.ExcludeClasses.Count > 0 || options.ExcludeIds.Count > 0;
        if (!hasConfiguredCleanup && !options.SmartContentCleanup) {
            return html;
        }

        if (LooksLikeFullHtmlDocument(html)) {
            IDocument document = HtmlParser.ParseWithAngleSharp(html);
            if (options.HiddenContentMode == HtmlCrawlHiddenContentMode.RespectHidden) {
                RemoveHiddenElements(document);
            }
            if (options.SmartContentCleanup) {
                StripBoilerplateElements(document, options);
            }
            RemoveConfiguredElements(document, options);
            return document.DocumentElement?.OuterHtml ?? html;
        }

        IDocument fragment = HtmlParser.ParseWithAngleSharp($"<div id=\"__htmltinkerx_exclude\">{html}</div>");
        IElement? wrapper = fragment.QuerySelector("#__htmltinkerx_exclude");
        if (wrapper == null) {
            return html;
        }

        if (options.HiddenContentMode == HtmlCrawlHiddenContentMode.RespectHidden) {
            RemoveHiddenElements(wrapper);
        }
        if (options.SmartContentCleanup) {
            StripBoilerplateElements(wrapper, options);
        }
        RemoveConfiguredElements(wrapper, options);
        return wrapper.InnerHtml;
    }

    private static void RemoveHiddenElements(IParentNode container) {
        foreach (IElement element in container.QuerySelectorAll("[hidden],[aria-hidden],[style],input[type='hidden'],[data-htmltinkerx-hidden='true']").ToArray()) {
            if (ShouldRemoveHiddenElement(element)) {
                element.Remove();
            }
        }
    }

    private static bool ShouldRemoveHiddenElement(IElement element) {
        if (element == null) {
            return false;
        }

        if (element.HasAttribute("hidden")) {
            return true;
        }

        if (string.Equals(element.GetAttribute("data-htmltinkerx-hidden"), "true", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        if (string.Equals(element.GetAttribute("aria-hidden"), "true", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        if (element.TagName.Equals("INPUT", StringComparison.OrdinalIgnoreCase)
            && string.Equals(element.GetAttribute("type"), "hidden", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        string? style = element.GetAttribute("style");
        if (string.IsNullOrWhiteSpace(style)) {
            return false;
        }

        string normalizedStyle = Regex.Replace(style, @"\s+", string.Empty).ToLowerInvariant();
        return normalizedStyle.Contains("display:none", StringComparison.Ordinal)
               || normalizedStyle.Contains("visibility:hidden", StringComparison.Ordinal)
               || normalizedStyle.Contains("content-visibility:hidden", StringComparison.Ordinal);
    }

    private static Task MarkRenderedHiddenElementsAsync(IPage page) {
        return page.EvaluateAsync(
            """
            () => {
                const hiddenAttributeName = 'data-htmltinkerx-hidden';
                for (const element of document.querySelectorAll(`[${hiddenAttributeName}]`)) {
                    element.removeAttribute(hiddenAttributeName);
                }

                const shouldMarkHidden = (element) => {
                    if (!(element instanceof Element)) {
                        return false;
                    }

                    const tagName = (element.tagName || '').toLowerCase();
                    if (tagName === 'html' || tagName === 'head' || tagName === 'body') {
                        return false;
                    }

                    if (element.hasAttribute('hidden')) {
                        return true;
                    }

                    if ((element.getAttribute('aria-hidden') || '').toLowerCase() === 'true') {
                        return true;
                    }

                    if (tagName === 'input' && (element.getAttribute('type') || '').toLowerCase() === 'hidden') {
                        return true;
                    }

                    const style = window.getComputedStyle(element);
                    if (!style) {
                        return false;
                    }

                    return style.display === 'none'
                        || style.visibility === 'hidden'
                        || style.visibility === 'collapse'
                        || style.contentVisibility === 'hidden';
                };

                for (const element of document.querySelectorAll('*')) {
                    if (shouldMarkHidden(element)) {
                        element.setAttribute(hiddenAttributeName, 'true');
                    }
                }
            }
            """);
    }

    private static void StripBoilerplateElements(IParentNode container, HtmlCrawlOptions options) {
        foreach (IElement element in container.QuerySelectorAll(
                     "script,style,noscript,svg,header,nav,footer,aside,[role='banner'],[role='navigation'],[role='contentinfo'],[role='search'],form[role='search'],.wpml-ls,.sharing-popup,.post-footer-sharing,.socials-sharing,.gem-pagination,.menu-toggle,.minisearch,.skip-link,.skip-link-screen-reader-text").ToArray()) {
            if (ShouldPreserveStructuredContentElement(element)
                || ShouldPreserveOfflineExecutableElement(element, options)) {
                continue;
            }

            element.Remove();
        }

        if (!options.SmartContentCleanup) {
            return;
        }

        foreach (IElement element in container.QuerySelectorAll("*").Where(ShouldRemoveBoilerplateElement).ToArray()) {
            element.Remove();
        }
    }

    private static bool ShouldPreserveStructuredContentElement(IElement element) {
        if (element == null) {
            return false;
        }

        return LooksLikeStructuredCalloutElement(element) || LooksLikeMediaNoscriptFallbackElement(element);
    }

    private static bool ShouldPreserveOfflineExecutableElement(IElement element, HtmlCrawlOptions options) {
        if (element == null || options == null) {
            return false;
        }

        if (!options.DownloadAssets || !options.RewriteAssetReferencesToLocal) {
            return false;
        }

        return element.TagName.Equals("SCRIPT", StringComparison.OrdinalIgnoreCase)
               || element.TagName.Equals("STYLE", StringComparison.OrdinalIgnoreCase)
               || element.TagName.Equals("LINK", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeMediaNoscriptFallbackElement(IElement element) {
        return TryGetNoscriptFallbackWrapper(element, out _);
    }

    private static IEnumerable<string> EnumerateNoscriptHtmlCandidates(IElement element) {
        string innerHtml = element.InnerHtml;
        if (!string.IsNullOrWhiteSpace(innerHtml)) {
            yield return innerHtml;
        }

        string textContent = element.TextContent;
        if (!string.IsNullOrWhiteSpace(textContent) && !string.Equals(textContent, innerHtml, StringComparison.Ordinal)) {
            yield return textContent;
        }
    }

    private static void RemoveConfiguredElements(IParentNode container, HtmlCrawlOptions options) {
        RemoveMatchingSelectors(container, options.ExcludeSelectors);
        RemoveMatchingClasses(container, options.ExcludeClasses);
        RemoveMatchingIds(container, options.ExcludeIds);
    }

    private static void RemoveMatchingSelectors(IParentNode container, IEnumerable<string>? selectors) {
        if (selectors == null) {
            return;
        }

        foreach (string selector in selectors.Where(selector => !string.IsNullOrWhiteSpace(selector)).Select(selector => selector.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)) {
            foreach (IElement element in container.QuerySelectorAll(selector).ToArray()) {
                element.Remove();
            }
        }
    }

    private static void RemoveMatchingClasses(IParentNode container, IEnumerable<string>? classNames) {
        if (classNames == null) {
            return;
        }

        foreach (string className in classNames.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim().TrimStart('.')).Distinct(StringComparer.OrdinalIgnoreCase)) {
            foreach (IElement element in container.QuerySelectorAll("." + CssEscapeIdentifier(className)).ToArray()) {
                element.Remove();
            }
        }
    }

    private static void RemoveMatchingIds(IParentNode container, IEnumerable<string>? ids) {
        if (ids == null) {
            return;
        }

        foreach (string id in ids.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim().TrimStart('#')).Distinct(StringComparer.OrdinalIgnoreCase)) {
            foreach (IElement element in container.QuerySelectorAll("#" + CssEscapeIdentifier(id)).ToArray()) {
                element.Remove();
            }
        }
    }

    private static bool ShouldRemoveBoilerplateElement(IElement element) {
        if (!IsLowValueContainer(element)) {
            return false;
        }

        bool hasSignalToken = ContainsBoilerplateSignals(element);
        if (hasSignalToken && IsLikelyLowValueContentBlock(element)) {
            return true;
        }

        return IsLinkDenseBoilerplateBlock(element);
    }

    private static bool IsLowValueContainer(IElement element) {
        string tagName = element.TagName.ToLowerInvariant();
        if (tagName is "body" or "html" or "main" or "article") {
            return false;
        }

        return tagName is "div" or "section" or "aside" or "nav" or "ul" or "ol" or "details" or "form";
    }

    private static string GetBoilerplateSignalText(IElement element) {
        return string.Join(" ",
            element.Id ?? string.Empty,
            element.ClassName ?? string.Empty,
            element.GetAttribute("aria-label") ?? string.Empty,
            element.GetAttribute("role") ?? string.Empty,
            element.GetAttribute("data-testid") ?? string.Empty);
    }

    private static bool ContainsBoilerplateSignals(IElement element) {
        string combined = GetBoilerplateSignalText(element);
        if (string.IsNullOrWhiteSpace(combined)) {
            return false;
        }

        string normalized = combined.ToLowerInvariant();
        return BoilerplateSignalTokens.Any(token => normalized.Contains(token, StringComparison.Ordinal));
    }

    private static bool IsLikelyLowValueContentBlock(IElement element) {
        if (element.QuerySelector("pre, code, table") != null) {
            return false;
        }

        int headingCount = element.QuerySelectorAll("h1, h2, h3").Length;
        int paragraphCount = element.QuerySelectorAll("p").Length;
        int linkCount = element.QuerySelectorAll("a[href]").Length;
        int wordCount = CountWords(element.TextContent);
        int linkWordCount = CountWords(string.Join(" ", element.QuerySelectorAll("a[href]").Select(anchor => anchor.TextContent)));

        if (headingCount > 0 && wordCount >= 60) {
            return false;
        }

        if (paragraphCount >= 2 && wordCount >= 50 && linkCount <= 6) {
            return false;
        }

        if (wordCount <= 12) {
            return true;
        }

        if (linkCount >= 3 && linkWordCount >= Math.Max(8, wordCount / 2)) {
            return true;
        }

        return false;
    }

    private static bool IsLinkDenseBoilerplateBlock(IElement element) {
        if (element.QuerySelector("pre, code, table, img") != null) {
            return false;
        }

        int wordCount = CountWords(element.TextContent);
        if (wordCount == 0) {
            return true;
        }

        int linkCount = element.QuerySelectorAll("a[href]").Length;
        if (linkCount < 3) {
            return false;
        }

        int listItemCount = element.QuerySelectorAll("li").Length;
        int linkWordCount = CountWords(string.Join(" ", element.QuerySelectorAll("a[href]").Select(anchor => anchor.TextContent)));
        double linkDensity = (double)linkWordCount / Math.Max(1, wordCount);

        if (linkDensity >= 0.75 && wordCount <= 90) {
            return true;
        }

        if (listItemCount >= 4 && linkDensity >= 0.55 && wordCount <= 140) {
            return true;
        }

        return false;
    }

    private static string CssEscapeIdentifier(string value) {
        StringBuilder builder = new(value.Length);
        foreach (char character in value) {
            if (char.IsLetterOrDigit(character) || character is '-' or '_') {
                builder.Append(character);
            } else {
                builder.Append('\\').Append(character);
            }
        }

        return builder.ToString();
    }

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

    private static async Task DownloadAssetsForPageAsync(
        HttpClient client,
        HtmlCrawlPage page,
        HtmlCrawlOptions options,
        HtmlCrawlResult result,
        ISet<string> downloadedAssets,
        string? assetsDirectory,
        CancellationToken cancellationToken) {
        Queue<string> pending = new(page.AssetUrls.Distinct(StringComparer.OrdinalIgnoreCase));
        while (pending.Count > 0) {
            cancellationToken.ThrowIfCancellationRequested();
            string assetUrl = pending.Dequeue();
            if (!downloadedAssets.Add(assetUrl)) {
                continue;
            }

            HtmlCrawlAsset asset = await DownloadAssetAsync(client, assetUrl, page.Url, options, assetsDirectory, cancellationToken).ConfigureAwait(false);
            result.Assets.Add(asset);

            if (TryReadNestedCssAssetUrls(asset, options, out List<string>? nestedAssetUrls)) {
                foreach (string nestedAssetUrl in nestedAssetUrls!) {
                    if (!downloadedAssets.Contains(nestedAssetUrl)) {
                        pending.Enqueue(nestedAssetUrl);
                    }
                }
            }
        }
    }

    private static async Task<HtmlCrawlAsset> DownloadAssetAsync(
        HttpClient client,
        string assetUrl,
        string? pageUrl,
        HtmlCrawlOptions options,
        string? assetsDirectory,
        CancellationToken cancellationToken) {
        HtmlCrawlAsset asset = new() {
            Url = assetUrl,
            PageUrl = pageUrl,
            Source = assetUrl,
            Started = DateTimeOffset.UtcNow
        };

        try {
            using HttpResponseMessage response = await client.GetAsync(assetUrl, cancellationToken).ConfigureAwait(false);
            asset.StatusCode = (int)response.StatusCode;
            asset.ContentType = response.Content.Headers.ContentType?.MediaType ?? response.Content.Headers.ContentType?.ToString();
            response.EnsureSuccessStatusCode();

            byte[] bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            asset.ContentLength = bytes.LongLength;

            if (!string.IsNullOrEmpty(assetsDirectory)) {
                string assetPath = BuildAssetPath(asset, assetsDirectory!);
                await WriteBytesAsync(assetPath, bytes, cancellationToken).ConfigureAwait(false);
                asset.FilePath = assetPath;
            }
        } catch (Exception ex) {
            asset.Error = ex.Message;
        } finally {
            asset.Finished = DateTimeOffset.UtcNow;
        }

        return asset;
    }

    private static async Task RewriteDownloadedCssAssetsAsync(
        IEnumerable<HtmlCrawlAsset> assets,
        HtmlCrawlOptions? options,
        CancellationToken cancellationToken) {
        if (options?.DownloadAssets != true || !options.RewriteAssetReferencesToLocal) {
            return;
        }

        Dictionary<string, string> assetMap = assets
            .Where(asset => !string.IsNullOrWhiteSpace(asset.Url) && !string.IsNullOrWhiteSpace(asset.FilePath) && string.IsNullOrWhiteSpace(asset.Error))
            .GroupBy(asset => asset.Url, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().FilePath!, StringComparer.OrdinalIgnoreCase);
        if (assetMap.Count == 0) {
            return;
        }

        foreach (HtmlCrawlAsset asset in assets.Where(IsCssAsset)) {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(asset.FilePath) || !File.Exists(asset.FilePath) || !Uri.TryCreate(asset.Url, UriKind.Absolute, out Uri? assetUri)) {
                continue;
            }

#if NETSTANDARD2_0 || NETFRAMEWORK
            string css = await Task.Run(() => File.ReadAllText(asset.FilePath), cancellationToken).ConfigureAwait(false);
#else
            string css = await File.ReadAllTextAsync(asset.FilePath, cancellationToken).ConfigureAwait(false);
#endif
            string rewritten = RewriteCssUrlsToLocal(css, assetUri, asset.FilePath!, assetMap, options);
            if (!string.Equals(css, rewritten, StringComparison.Ordinal)) {
                await WriteTextAsync(asset.FilePath!, rewritten, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsCssAsset(HtmlCrawlAsset asset) {
        if (!string.IsNullOrWhiteSpace(asset.ContentType)
            && asset.ContentType!.StartsWith("text/css", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(asset.FilePath)
            && string.Equals(Path.GetExtension(asset.FilePath), ".css", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(asset.Url)
            && string.Equals(Path.GetExtension(new Uri(asset.Url).AbsolutePath), ".css", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        return false;
    }

    private static bool TryReadNestedCssAssetUrls(HtmlCrawlAsset asset, HtmlCrawlOptions options, out List<string>? nestedAssetUrls) {
        nestedAssetUrls = null;
        if (!IsCssAsset(asset)
            || !string.IsNullOrWhiteSpace(asset.Error)
            || string.IsNullOrWhiteSpace(asset.FilePath)
            || !File.Exists(asset.FilePath)
            || !Uri.TryCreate(asset.Url, UriKind.Absolute, out Uri? assetUri)) {
            return false;
        }

        string css = File.ReadAllText(asset.FilePath);
        HashSet<string> discovered = new(StringComparer.OrdinalIgnoreCase);
        foreach (string cssUrl in ExtractCssUrls(css)) {
            AddAssetCandidate(cssUrl, assetUri!, options, discovered);
        }

        if (discovered.Count == 0) {
            return false;
        }

        nestedAssetUrls = discovered.ToList();
        return true;
    }

    private static string BuildAssetPath(HtmlCrawlAsset asset, string assetsDirectory) {
        Uri uri = new(asset.Url);
        string extension = Path.GetExtension(uri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(extension)) {
            extension = GuessExtensionFromContentType(asset.ContentType);
        }

        string fileName = Path.GetFileNameWithoutExtension(uri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(fileName)) {
            fileName = "asset";
        }

        string safeName = Regex.Replace(fileName, @"[^A-Za-z0-9\-]+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(safeName)) {
            safeName = "asset";
        }

        string fingerprint = ComputeContentFingerprint(asset.Url).Substring(0, 12);
        return CombinePathWithinDirectory(assetsDirectory, $"{safeName}-{fingerprint}{extension}");
    }

    private static string GuessExtensionFromContentType(string? contentType) {
        if (string.IsNullOrWhiteSpace(contentType)) {
            return ".bin";
        }

        switch (contentType!.Trim().ToLowerInvariant()) {
            case "image/jpeg":
                return ".jpg";
            case "image/png":
                return ".png";
            case "image/gif":
                return ".gif";
            case "image/webp":
                return ".webp";
            case "image/svg+xml":
                return ".svg";
            case "application/pdf":
                return ".pdf";
            default:
                return ".bin";
        }
    }

    private static string BuildRelativePath(string fromFilePath, string toFilePath) {
        string fromDirectory = Path.GetDirectoryName(fromFilePath) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fromDirectory)) {
            return toFilePath;
        }

        Uri fromUri = new(AppendDirectorySeparator(HtmlUtilities.ResolvePath(fromDirectory)));
        Uri toUri = new(HtmlUtilities.ResolvePath(toFilePath));
        string relative = Uri.UnescapeDataString(fromUri.MakeRelativeUri(toUri).ToString());
        return relative.Replace('/', Path.DirectorySeparatorChar).Replace('\\', '/');
    }

    private static string CombinePathWithinDirectory(string directory, string fileName) {
        string root = HtmlUtilities.ResolvePath(directory);
        string candidate = HtmlUtilities.ResolvePath(Path.Combine(root, fileName));
        return EnsurePathIsWithinDirectory(candidate, root);
    }

    private static string EnsurePathIsWithinDirectory(string path, string directory) {
        string fullPath = HtmlUtilities.ResolvePath(path);
        string root = AppendDirectorySeparator(HtmlUtilities.ResolvePath(directory));
        StringComparison pathComparison = Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!fullPath.StartsWith(root, pathComparison)
            && !string.Equals(fullPath, root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), pathComparison)) {
            throw new InvalidOperationException($"Generated path '{fullPath}' escapes the crawl artifact directory '{directory}'.");
        }

        return fullPath;
    }

    private static string AppendDirectorySeparator(string path) {
        if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)) {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }

    private static bool MatchesAny(string url, IEnumerable<string> patterns) {
        foreach (string pattern in patterns) {
            if (string.IsNullOrWhiteSpace(pattern)) {
                continue;
            }

            string regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            if (Regex.IsMatch(url, regexPattern, RegexOptions.IgnoreCase)) {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveAbsoluteUri(Uri baseUri, string candidate, out Uri? resolved) {
        resolved = null;
        if (string.IsNullOrWhiteSpace(candidate)) {
            return false;
        }

        if (!Uri.TryCreate(baseUri, candidate, out Uri? created)) {
            return false;
        }

        if (created.Scheme != Uri.UriSchemeHttp && created.Scheme != Uri.UriSchemeHttps) {
            return false;
        }

        resolved = created;
        return true;
    }

    private static HtmlCrawlPage CreateSkippedPage(CrawlRequest request, HtmlCrawlSkipReason reason) =>
        CreateSkippedPage(request.Uri.AbsoluteUri, request.ParentUrl, request.Depth, reason);

    private static HtmlCrawlPage CreateSkippedPage(string url, string? parentUrl, int depth, HtmlCrawlSkipReason reason) {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new HtmlCrawlPage {
            Url = url,
            RequestedUrl = url,
            ParentUrl = parentUrl,
            Depth = depth,
            Status = HtmlCrawlPageStatus.Skipped,
            SkipReason = reason,
            Started = now,
            Finished = now
        };
    }

    private static string GetHostKey(Uri uri) => $"{uri.Scheme}://{uri.Authority}";

    private static bool IsHostInScope(string candidateHost, string startHost, bool includeSubdomains) {
        if (string.Equals(candidateHost, startHost, StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        if (!includeSubdomains) {
            return false;
        }

        return candidateHost.EndsWith("." + startHost, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePathPrefix(string? pathPrefix) {
        if (string.IsNullOrWhiteSpace(pathPrefix)) {
            return string.Empty;
        }

        string normalized = pathPrefix!.Trim();
        if (!normalized.StartsWith("/", StringComparison.Ordinal)) {
            normalized = "/" + normalized;
        }

        if (normalized.Length > 1 && normalized.EndsWith("/", StringComparison.Ordinal)) {
            normalized = normalized.TrimEnd('/');
        }

        return normalized;
    }

    private static void ApplyCanonicalUrlIfAllowed(HtmlCrawlPage page, Uri startUri, HtmlCrawlOptions options, ISet<string> visited) {
        if (!options.UseCanonicalUrls || string.IsNullOrWhiteSpace(page.CanonicalUrl)) {
            return;
        }

        if (!Uri.TryCreate(page.CanonicalUrl, UriKind.Absolute, out Uri? canonicalUri)) {
            return;
        }

        if (GetSkipReasonForCandidate(canonicalUri, startUri, options) != HtmlCrawlSkipReason.None) {
            return;
        }

        string normalizedCanonical = NormalizeUrl(canonicalUri, options);
        page.CanonicalUrl = normalizedCanonical;
        page.Url = normalizedCanonical;
        visited.Add(normalizedCanonical);
    }

    private static string NormalizeUrl(Uri uri, HtmlCrawlOptions? options) {
        UriBuilder builder = new(uri) {
            Fragment = string.Empty
        };

        if (options?.IgnoreTrackingQueryParameters == true && !string.IsNullOrEmpty(builder.Query)) {
            builder.Query = FilterIgnoredQueryParameters(builder.Query, options.IgnoredQueryParameterPatterns);
        }

        return builder.Uri.AbsoluteUri;
    }

    private static string FilterIgnoredQueryParameters(string query, IEnumerable<string> patterns) {
        string rawQuery = query.StartsWith("?", StringComparison.Ordinal) ? query.Substring(1) : query;
        if (string.IsNullOrWhiteSpace(rawQuery)) {
            return string.Empty;
        }

        List<string> kept = new();
        foreach (string part in rawQuery.Split('&')) {
            if (string.IsNullOrWhiteSpace(part)) {
                continue;
            }

            int separatorIndex = part.IndexOf('=');
            string rawName = separatorIndex >= 0 ? part.Substring(0, separatorIndex) : part;
            string parameterName = Uri.UnescapeDataString(rawName.Replace("+", "%20"));
            if (MatchesParameterPattern(parameterName, patterns)) {
                continue;
            }

            kept.Add(part);
        }

        return string.Join("&", kept);
    }

    private static bool MatchesParameterPattern(string parameterName, IEnumerable<string> patterns) {
        foreach (string pattern in patterns) {
            if (string.IsNullOrWhiteSpace(pattern)) {
                continue;
            }

            string regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            if (Regex.IsMatch(parameterName, regexPattern, RegexOptions.IgnoreCase)) {
                return true;
            }
        }

        return false;
    }
}
