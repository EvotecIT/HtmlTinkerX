using AngleSharp.Dom;
using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
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
        public string Fingerprint { get; set; } = string.Empty;
    }

    private sealed class GraphNodeRecord {
        public string Url { get; set; } = string.Empty;
        public string? Title { get; set; }
        public int Depth { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? SkipReason { get; set; }
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
                    Started = DateTimeOffset.UtcNow
                };
            }

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
                    page = await FetchRenderedPageAsync(session!, next, resolvedOptions, cancellationToken).ConfigureAwait(false);
                    page.RenderMode = HtmlCrawlRenderMode.Rendered;
                    page.RenderReasonCode = HtmlCrawlRenderReasonCode.ExplicitRender;
                    page.RenderReason = "Rendered because browser mode was explicitly requested.";
                } else {
                    page = await FetchHttpPageAsync(client, next, resolvedOptions, cancellationToken).ConfigureAwait(false);
                    if (resolvedOptions.AutoRender) {
                        AutoRenderDecision decision = EvaluateAutoRender(page, resolvedOptions);
                        page.RenderReasonCode = decision.ReasonCode;
                        page.RenderReason = decision.Reason;
                        if (decision.ShouldRender) {
                            page = await FetchRenderedPageAsync(session!, next, resolvedOptions, cancellationToken).ConfigureAwait(false);
                            page.RenderMode = HtmlCrawlRenderMode.AutoRendered;
                            page.RenderReasonCode = decision.ReasonCode;
                            page.RenderReason = decision.Reason;
                        }
                    } else {
                        page.RenderReasonCode = HtmlCrawlRenderReasonCode.StaticRenderDisabled;
                        page.RenderReason = "Kept static because browser rendering was not enabled.";
                    }
                }

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
        result.ChunksJsonlPath = artifactPaths.ChunksJsonlPath;
        result.GraphJsonPath = artifactPaths.GraphJsonPath;
        result.SummaryPath = artifactPaths.SummaryJsonPath;
        result.SummaryTextPath = artifactPaths.SummaryTextPath;
        result.IndexHtmlPath = artifactPaths.IndexHtmlPath;

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

            await WriteTextAsync(page.ManifestPath!, BuildPageManifestJson(page, result.Assets, localPageMap, assetMap), cancellationToken).ConfigureAwait(false);
        }

        await RewriteDownloadedCssAssetsAsync(result.Assets, options, cancellationToken).ConfigureAwait(false);

        StringBuilder pagesJsonl = new();
        StringBuilder pagesCsv = new();
        pagesCsv.AppendLine("Url,RequestedUrl,CanonicalUrl,ParentUrl,Depth,Status,StatusCode,ContentType,Title,HtmlPath,TextPath,ManifestPath,ContentFingerprint,DuplicateOfUrl,Rendered,RenderMode,RenderReasonCode,RenderReason,Started,Finished,DurationMs,LinkCount,AssetCount,InteractionCount,Error");
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
                page.ManifestPath,
                page.ContentFingerprint,
                page.DuplicateOfUrl,
                page.Rendered,
                page.RenderMode,
                page.RenderReasonCode,
                page.RenderReason,
                page.AppliedInteractions,
                page.Started,
                page.Finished,
                DurationMs = (long)page.Duration.TotalMilliseconds,
                LinkCount = page.Links.Count,
                AssetCount = page.AssetUrls.Count,
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
                EscapeCsv(page.ManifestPath),
                EscapeCsv(page.ContentFingerprint),
                EscapeCsv(page.DuplicateOfUrl),
                EscapeCsv(page.Rendered.ToString()),
                EscapeCsv(page.RenderMode.ToString()),
                EscapeCsv(page.RenderReasonCode.ToString()),
                EscapeCsv(page.RenderReason),
                EscapeCsv(page.Started.ToString("O", System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.Finished.ToString("O", System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(((long)page.Duration.TotalMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.Links.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.AssetUrls.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                EscapeCsv(page.AppliedInteractions.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
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
            page.AppliedInteractions,
            page.Started,
            page.Finished,
            DurationMs = (long)page.Duration.TotalMilliseconds,
            PageFiles = new {
                HtmlPath = BuildRelativeOptionalPath(manifestPath, page.HtmlPath),
                TextPath = BuildRelativeOptionalPath(manifestPath, page.TextPath)
            },
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
            page.Error
        };

        return JsonSerializer.Serialize(manifest, CreateJsonOptions());
    }

    private static PageSearchMetadata BuildPageSearchMetadata(HtmlCrawlPage page) {
        string sourceText = GetSearchText(page);
        string[] headings = ExtractHeadings(page.Html);
        List<string> chunkTexts = BuildPageChunkTexts(page);
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

        object graphDocument = new {
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
            List<string> pageChunks = BuildPageChunkTexts(page);
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
                    Fingerprint = fingerprint
                });
                nextChunkId++;
            }
        }

        return chunks;
    }

    private static List<string> BuildPageChunkTexts(HtmlCrawlPage page) {
        string sourceText = GetSearchText(page);
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

    private static string GetSearchText(HtmlCrawlPage page) {
        if (!string.IsNullOrWhiteSpace(page.Text)) {
            return NormalizeWhitespace(page.Text);
        }

        if (!string.IsNullOrWhiteSpace(page.Html)) {
            return NormalizeWhitespace(HtmlParserToText.ConvertToText(page.Html));
        }

        return string.Empty;
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

        return BuildRelativePath(fromFilePath, toFilePath);
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
        AppendStatCard(builder, "Graph Edges", summary.GraphEdgeCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "External Nodes", summary.GraphExternalNodeCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendStatCard(builder, "Links", summary.TotalDiscoveredLinks.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.AppendLine("    </div>");
        builder.AppendLine("  </section>");

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
            if (page.AppliedInteractions.Count > 0) {
                builder.Append("<br>interactions: ")
                    .Append(HtmlEncode(string.Join(" | ", page.AppliedInteractions)));
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
            if (!string.IsNullOrWhiteSpace(page.HtmlPath) && HasAnyFollowingFile(page.TextPath, page.ManifestPath)) {
                builder.Append(" | ");
            }
            AppendOptionalFileLink(builder, indexHtmlPath, "Text", page.TextPath);
            if (!string.IsNullOrWhiteSpace(page.TextPath) && HasAnyFollowingFile(page.ManifestPath)) {
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
            builder.AppendLine("      <thead><tr><th>URL</th><th>Reason</th><th>Depth</th></tr></thead>");
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
            builder.AppendLine("      <thead><tr><th>URL</th><th>Reason</th><th>Depth</th></tr></thead>");
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

        string relative = BuildRelativePath(indexHtmlPath, path);
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

        string normalized = value.Replace("\r\n", "\n").Replace('\r', '\n');
        if (normalized.IndexOfAny(new[] { ',', '"', '\n' }) >= 0) {
            return "\"" + normalized.Replace("\"", "\"\"") + "\"";
        }

        return normalized;
    }

    private static async Task<HtmlCrawlPage> FetchHttpPageAsync(HttpClient client, CrawlRequest request, HtmlCrawlOptions options, CancellationToken cancellationToken) {
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
                return page;
            }

            page.Title = ExtractTitle(html);
            page.CanonicalUrl = ExtractCanonicalUrl(html, request.Uri, options);
            page.Links = ExtractLinks(html, request.Uri, options);
            page.AssetUrls = ExtractAssetUrls(html, request.Uri, options);
            page.Html = options.IncludeHtml ? ApplyExcludedSelectors(SelectHtml(html, options.Selector), options.ExcludeSelectors) : string.Empty;
            string textSourceHtml = SelectTextSourceHtml(html, options.Selector, page.Html);
            page.Text = options.IncludeText ? HtmlParserToText.ConvertToText(PrepareHtmlForTextExtraction(textSourceHtml, options.ExcludeSelectors)) : string.Empty;
        } catch (Exception ex) {
            page.Status = HtmlCrawlPageStatus.Failed;
            page.Error = ex.Message;
        } finally {
            page.Finished = DateTimeOffset.UtcNow;
        }

        return page;
    }

    private static async Task<HtmlCrawlPage> FetchRenderedPageAsync(HtmlBrowserSession session, CrawlRequest request, HtmlCrawlOptions options, CancellationToken cancellationToken) {
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

            string fullHtml = await session.Page.ContentAsync().ConfigureAwait(false);
            page.StatusCode = response?.Status;
            page.ContentType = TryGetResponseContentType(response);

            if (!IsAllowedPageContent(page.ContentType, fullHtml, options)) {
                page.Status = HtmlCrawlPageStatus.Skipped;
                page.SkipReason = HtmlCrawlSkipReason.UnsupportedContentType;
                page.Error = $"Skipped content type '{page.ContentType ?? "unknown"}'.";
                return page;
            }

            page.Title = await session.Page.TitleAsync().ConfigureAwait(false);
            page.CanonicalUrl = ExtractCanonicalUrl(fullHtml, request.Uri, options);
            page.Links = ExtractLinks(fullHtml, request.Uri, options);
            page.AssetUrls = ExtractAssetUrls(fullHtml, request.Uri, options);
            page.Html = options.IncludeHtml
                ? ApplyExcludedSelectors(SelectHtml(fullHtml, options.Selector), options.ExcludeSelectors)
                : string.Empty;
            string textSourceHtml = SelectTextSourceHtml(fullHtml, options.Selector, page.Html);
            page.Text = options.IncludeText
                ? HtmlParserToText.ConvertToText(PrepareHtmlForTextExtraction(textSourceHtml, options.ExcludeSelectors))
                : string.Empty;
        } catch (Exception ex) {
            page.Status = HtmlCrawlPageStatus.Failed;
            page.Error = ex.Message;
        } finally {
            page.Finished = DateTimeOffset.UtcNow;
        }

        return page;
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

        if (!string.IsNullOrWhiteSpace(contentType) && MatchesParameterPattern(contentType.Trim(), options.AllowedContentTypePatterns)) {
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

    private static string SelectHtml(string html, string? selector) {
        if (string.IsNullOrEmpty(selector)) {
            return html;
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        IElement? selected = document.QuerySelector(selector!);
        if (selected != null) {
            return selected.OuterHtml;
        }

        if (LooksLikeSemanticContentSelector(selector) && TrySelectPreferredContentElement(document, out IElement? fallback)) {
            return fallback!.OuterHtml;
        }

        return string.Empty;
    }

    private static string SelectTextSourceHtml(string html, string? selector, string? selectedHtml) {
        if (!string.IsNullOrWhiteSpace(selectedHtml)) {
            return selectedHtml!;
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        if ((string.IsNullOrWhiteSpace(selector) || LooksLikeSemanticContentSelector(selector))
            && TrySelectPreferredContentElement(document, out IElement? fallback)) {
            return fallback!.OuterHtml;
        }

        return html;
    }

    private static bool LooksLikeSemanticContentSelector(string? selector) {
        if (string.IsNullOrWhiteSpace(selector)) {
            return false;
        }

        string normalized = selector.Trim().ToLowerInvariant();
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

    private static string PrepareHtmlForTextExtraction(string html, IEnumerable<string>? excludeSelectors = null) {
        if (string.IsNullOrWhiteSpace(html)) {
            return html;
        }

        if (LooksLikeFullHtmlDocument(html)) {
            IDocument document = HtmlParser.ParseWithAngleSharp(html);
            StripBoilerplateElements(document);
            RemoveMatchingSelectors(document, excludeSelectors);
            return document.DocumentElement?.OuterHtml ?? html;
        }

        IDocument fragment = HtmlParser.ParseWithAngleSharp($"<div id=\"__htmltinkerx_text\">{html}</div>");
        IElement? wrapper = fragment.QuerySelector("#__htmltinkerx_text");
        if (wrapper == null) {
            return html;
        }

        StripBoilerplateElements(wrapper);
        RemoveMatchingSelectors(wrapper, excludeSelectors);
        return wrapper.InnerHtml;
    }

    private static string ApplyExcludedSelectors(string html, IEnumerable<string>? excludeSelectors) {
        if (string.IsNullOrWhiteSpace(html)) {
            return html;
        }

        string[] selectors = excludeSelectors?
            .Where(selector => !string.IsNullOrWhiteSpace(selector))
            .Select(selector => selector.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? Array.Empty<string>();
        if (selectors.Length == 0) {
            return html;
        }

        if (LooksLikeFullHtmlDocument(html)) {
            IDocument document = HtmlParser.ParseWithAngleSharp(html);
            RemoveMatchingSelectors(document, selectors);
            return document.DocumentElement?.OuterHtml ?? html;
        }

        IDocument fragment = HtmlParser.ParseWithAngleSharp($"<div id=\"__htmltinkerx_exclude\">{html}</div>");
        IElement? wrapper = fragment.QuerySelector("#__htmltinkerx_exclude");
        if (wrapper == null) {
            return html;
        }

        RemoveMatchingSelectors(wrapper, selectors);
        return wrapper.InnerHtml;
    }

    private static void StripBoilerplateElements(IParentNode container) {
        foreach (IElement element in container.QuerySelectorAll(
                     "script,style,noscript,svg,header,nav,footer,aside,[role='banner'],[role='navigation'],[role='contentinfo'],[role='search'],form[role='search'],.wpml-ls,.sharing-popup,.post-footer-sharing,.socials-sharing,.gem-pagination,.menu-toggle,.minisearch,.skip-link,.skip-link-screen-reader-text").ToArray()) {
            element.Remove();
        }

        foreach (IElement element in container.QuerySelectorAll("*").Where(ShouldRemoveBoilerplateElement).ToArray()) {
            element.Remove();
        }
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

    private static bool ShouldRemoveBoilerplateElement(IElement element) {
        string combined = string.Join(" ",
            element.Id ?? string.Empty,
            element.ClassName ?? string.Empty,
            element.GetAttribute("aria-label") ?? string.Empty);
        if (string.IsNullOrWhiteSpace(combined)) {
            return false;
        }

        string normalized = combined.ToLowerInvariant();
        return normalized.Contains("site-header", StringComparison.Ordinal)
               || normalized.Contains("primary-navigation", StringComparison.Ordinal)
               || normalized.Contains("nav-menu", StringComparison.Ordinal)
               || normalized.Contains("menu-item-search", StringComparison.Ordinal)
               || normalized.Contains("footer-nav", StringComparison.Ordinal)
               || normalized.Contains("footer-site-info", StringComparison.Ordinal)
               || normalized.Contains("wpml-ls", StringComparison.Ordinal)
               || normalized.Contains("sharing-popup", StringComparison.Ordinal)
               || normalized.Contains("post-footer-sharing", StringComparison.Ordinal)
               || normalized.Contains("socials-sharing", StringComparison.Ordinal)
               || normalized.Contains("gem-pagination", StringComparison.Ordinal)
               || normalized.Contains("skip-link", StringComparison.Ordinal)
               || normalized.Contains("search", StringComparison.Ordinal) && normalized.Contains("form", StringComparison.Ordinal);
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

        foreach (IElement element in document.QuerySelectorAll("img[src], source[src], video[src], audio[src], link[href], a[href], img[srcset], source[srcset], style, [style]")) {
            switch (element.TagName.ToUpperInvariant()) {
                case "IMG":
                case "SOURCE":
                case "VIDEO":
                case "AUDIO":
                    AddAssetCandidate(element.GetAttribute("src"), effectiveBaseUri, options, assets);
                    foreach (string srcSetCandidate in ExtractSrcSetUrls(element.GetAttribute("srcset"))) {
                        AddAssetCandidate(srcSetCandidate, effectiveBaseUri, options, assets);
                    }
                    break;
                case "LINK":
                    string rel = element.GetAttribute("rel") ?? string.Empty;
                    if (rel.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0
                        || rel.IndexOf("image", StringComparison.OrdinalIgnoreCase) >= 0
                        || rel.IndexOf("stylesheet", StringComparison.OrdinalIgnoreCase) >= 0) {
                        AddAssetCandidate(element.GetAttribute("href"), effectiveBaseUri, options, assets);
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

        return assets.ToList();
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
        foreach (IElement element in container.QuerySelectorAll("img[src], source[src], video[src], audio[src], link[href], a[href]")) {
            switch (element.TagName.ToUpperInvariant()) {
                case "IMG":
                case "SOURCE":
                case "VIDEO":
                case "AUDIO":
                    RewriteAssetAttribute(element, "src", resolutionBaseUri, pageHtmlPath, assetMap, options);
                    break;
                case "LINK":
                    string rel = element.GetAttribute("rel") ?? string.Empty;
                    if (rel.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0
                        || rel.IndexOf("image", StringComparison.OrdinalIgnoreCase) >= 0
                        || rel.IndexOf("stylesheet", StringComparison.OrdinalIgnoreCase) >= 0) {
                        RewriteAssetAttribute(element, "href", resolutionBaseUri, pageHtmlPath, assetMap, options);
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

        foreach (IElement element in container.QuerySelectorAll("img[srcset], source[srcset]")) {
            string? srcSet = element.GetAttribute("srcset");
            if (string.IsNullOrWhiteSpace(srcSet)) {
                continue;
            }

            string rewritten = RewriteSrcSetToLocal(srcSet!, resolutionBaseUri, pageHtmlPath, assetMap, options);
            if (!string.Equals(rewritten, srcSet, StringComparison.Ordinal)) {
                element.SetAttribute("srcset", rewritten);
            }
        }
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
        foreach (string entry in srcSet.Split(',')) {
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

    private static IEnumerable<string> ExtractSrcSetUrls(string? srcSet) {
        if (string.IsNullOrWhiteSpace(srcSet)) {
            yield break;
        }

        foreach (string entry in srcSet.Split(',')) {
            string trimmed = entry.Trim();
            if (trimmed.Length == 0) {
                continue;
            }

            int separatorIndex = trimmed.IndexOf(' ');
            yield return separatorIndex > 0 ? trimmed.Substring(0, separatorIndex) : trimmed;
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
                string assetPath = BuildAssetPath(asset, assetsDirectory);
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
            string rewritten = RewriteCssUrlsToLocal(css, assetUri, asset.FilePath, assetMap, options);
            if (!string.Equals(css, rewritten, StringComparison.Ordinal)) {
                await WriteTextAsync(asset.FilePath, rewritten, cancellationToken).ConfigureAwait(false);
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
