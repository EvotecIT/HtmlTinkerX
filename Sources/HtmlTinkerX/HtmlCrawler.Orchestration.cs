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
                    MarkdownProfile = resolvedOptions.MarkdownProfile,
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
            result.MarkdownProfile = resolvedOptions.MarkdownProfile;
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
        if (options.MaximumPageResponseBytes <= 0) {
            throw new ArgumentOutOfRangeException(nameof(options.MaximumPageResponseBytes), "MaximumPageResponseBytes must be greater than zero.");
        }
        if (options.MaximumAssetResponseBytes <= 0) {
            throw new ArgumentOutOfRangeException(nameof(options.MaximumAssetResponseBytes), "MaximumAssetResponseBytes must be greater than zero.");
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
            using HttpResponseMessage response = await client.GetAsync(robotsUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) {
                cache[hostKey] = new RobotsDocument();
                return cache[hostKey];
            }

            byte[] bytes = await HtmlUtilities.ReadResponseBytesAsync(response, options.MaximumPageResponseBytes, cancellationToken).ConfigureAwait(false);
            string text = Encoding.UTF8.GetString(bytes);
            RobotsDocument robots = ParseRobots(text, options.RobotsUserAgent);
            cache[hostKey] = robots;
            return robots;
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
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

}
