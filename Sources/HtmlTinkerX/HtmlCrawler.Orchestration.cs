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
        if (!resolvedOptions.IsScenarioOptionExplicit(nameof(HtmlCrawlOptions.IncludeStructuredJson))
            && (structuredSchema.Count > 0 || resolvedOptions.StructuredJsonPreset != HtmlCrawlStructuredJsonPreset.None)) {
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
        resolvedOptions.CrawlOrigin = startUri;

        CrawlRenderSession? renderSession = null;
        CrawlCheckpointWriter? checkpointWriter = null;
        async Task<HtmlBrowserSession> GetRenderSessionAsync() {
            renderSession ??= await CreateRenderSessionAsync(resolvedOptions, startUri, cancellationToken).ConfigureAwait(false);
            return renderSession.Session;
        }
        try {
            string persistencePath = resolvedOptions.OutputPath ?? resolvedOptions.ResumePath ?? string.Empty;
            bool persistSnapshots = !string.IsNullOrEmpty(persistencePath);
            if (persistSnapshots) checkpointWriter = new CrawlCheckpointWriter(persistencePath);
            HtmlCrawlResult result;
            if (!string.IsNullOrEmpty(resolvedOptions.ResumePath)) {
                result = await LoadResultAsync(resolvedOptions.ResumePath!, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(result.StartUrl, startUri.AbsoluteUri, StringComparison.Ordinal)) {
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
            HashSet<string> queued = new(StringComparer.Ordinal);
            HashSet<string> visited = new(StringComparer.Ordinal);
            Dictionary<string, RobotsDocument?> robotsCache = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> contentFingerprints = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> downloadedAssets = new(StringComparer.Ordinal);

            foreach (HtmlCrawlPage page in result.Pages) {
                if (!string.IsNullOrEmpty(page.Url)) {
                    visited.Add(page.Url);
                }
                if (!string.IsNullOrWhiteSpace(page.RequestedUrl) && TryResolveAbsoluteUri(startUri, page.RequestedUrl!, out Uri? requestedUri)) {
                    visited.Add(NormalizeUrl(requestedUri!, resolvedOptions));
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
                if (!string.IsNullOrWhiteSpace(page.RequestedUrl) && TryResolveAbsoluteUri(startUri, page.RequestedUrl!, out Uri? requestedUri)) {
                    visited.Add(NormalizeUrl(requestedUri!, resolvedOptions));
                }
            }

            foreach (HtmlCrawlPendingItem item in result.PendingPages) {
                if (TryResolveAbsoluteUri(startUri, item.Url, out Uri? pendingUri)) {
                    EnqueuePage(pendingUri!, item.ParentUrl, item.Depth, pending, queued, resolvedOptions);
                }
            }

            using HttpClient client = CreateClient(resolvedOptions, startUri);


            if (result.PageCount == 0 && result.PendingPages.Count == 0) {
                EnqueuePage(startUri, null, 0, pending, queued, resolvedOptions);
            }

            await DiscoverSitemapCandidatesAsync(startUri, client, resolvedOptions, robotsCache, result, pending, queued, visited, cancellationToken).ConfigureAwait(false);
            if (persistSnapshots) {
                await checkpointWriter!.SaveAsync(result, pending, cancellationToken).ConfigureAwait(false);
            }

            int fetchedCount = result.Pages.Count + result.SkippedPages.Count(page =>
                page.SkipReason == HtmlCrawlSkipReason.DuplicateContent || page.SkipReason == HtmlCrawlSkipReason.UnsupportedContentType);
            int previousDelay = 0;
            while (pending.Count > 0 && fetchedCount < resolvedOptions.MaxPages) {
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
                            await checkpointWriter!.SaveAsync(result, pending, cancellationToken).ConfigureAwait(false);
                        }
                        continue;
                    }
                }

                if (previousDelay > 0) await Task.Delay(previousDelay, cancellationToken).ConfigureAwait(false);
                previousDelay = await GetEffectiveDelayAsync(next.Uri, client, resolvedOptions, robotsCache, cancellationToken).ConfigureAwait(false);
                fetchedCount++;
                HtmlCrawlPage page;
                FetchedPageData fetchedPage;
                if (resolvedOptions.Render) {
                    fetchedPage = await FetchRenderedPageAsync(await GetRenderSessionAsync().ConfigureAwait(false), next, resolvedOptions, structuredSchema, cancellationToken).ConfigureAwait(false);
                    page = fetchedPage.Page;
                    page.RenderMode = HtmlCrawlRenderMode.Rendered;
                    page.RenderReasonCode = HtmlCrawlRenderReasonCode.ExplicitRender;
                    page.RenderReason = "Rendered because browser mode was explicitly requested.";
                } else {
                    fetchedPage = await FetchHttpPageAsync(client, next, resolvedOptions, structuredSchema, cancellationToken).ConfigureAwait(false);
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
                                PopulatePageFromHtml(page, fetchedPage.RawHtml!, new Uri(page.Url), resolvedOptions, structuredSchema);
                            }
                        }
                    }

                    if (resolvedOptions.AutoRender) {
                        AutoRenderDecision decision = EvaluateAutoRender(page, resolvedOptions);
                        page.RenderReasonCode = decision.ReasonCode;
                        page.RenderReason = decision.Reason;
                        if (decision.ShouldRender) {
                            fetchedPage = await FetchRenderedPageAsync(await GetRenderSessionAsync().ConfigureAwait(false), next, resolvedOptions, structuredSchema, cancellationToken).ConfigureAwait(false);
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

                renderSession?.ThrowIfFaulted();
                visited.Add(page.Url);
                ApplyRunMetadata(page, result);

                if (page.Status == HtmlCrawlPageStatus.Skipped) {
                    result.SkippedPages.Add(page);
                    if (persistSnapshots) {
                        await checkpointWriter!.SaveAsync(result, pending, cancellationToken).ConfigureAwait(false);
                    }
                    continue;
                }

                ApplyCanonicalUrlIfAllowed(page, startUri, resolvedOptions, visited);

                if (TrySkipDuplicateContent(page, resolvedOptions, contentFingerprints, out HtmlCrawlPage? duplicatePage)) {
                    result.SkippedPages.Add(duplicatePage!);
                    if (persistSnapshots) {
                        await checkpointWriter!.SaveAsync(result, pending, cancellationToken).ConfigureAwait(false);
                    }
                    continue;
                }

                if (page.Status == HtmlCrawlPageStatus.Success && page.Rendered && resolvedOptions.RenderedPageObserver != null) {
                    HtmlCrawlRenderedPageContext observerContext = new(renderSession!.Session, page, fetchedPage.RenderedNetworkLog);
                    await resolvedOptions.RenderedPageObserver.ObserveAsync(observerContext, cancellationToken).ConfigureAwait(false);
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


                if (persistSnapshots) {
                    await checkpointWriter!.SaveAsync(result, pending, cancellationToken).ConfigureAwait(false);
                }
            }

            result.PendingPages = SnapshotPendingPages(pending);
            result.Finished = DateTimeOffset.UtcNow;
            UpdateDerivedResultData(result);
            if (persistSnapshots) {
                await PersistSnapshotAsync(result, persistencePath, pending, cancellationToken, resolvedOptions).ConfigureAwait(false);
                checkpointWriter!.RemoveAfterFinalExport();
            }
            return result;
        } finally {
            checkpointWriter?.RemoveUnpublished();
            try {
                if (renderSession != null) await renderSession.DisposeAsync().ConfigureAwait(false);
            } finally {
                resolvedOptions.ClearSensitiveData();
            }
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

        HashSet<string> initialSitemaps = new(StringComparer.Ordinal);
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
        HashSet<string> processedSitemaps = new(StringComparer.Ordinal);
        foreach (string sitemap in initialSitemaps) {
            if (Uri.TryCreate(sitemap, UriKind.Absolute, out Uri? sitemapUri) && processedSitemaps.Add(sitemap)) {
                sitemapQueue.Enqueue(sitemapUri);
            }
        }

        HtmlHttpFetchOptions sitemapFetchOptions = new() {
            MaximumResponseBytes = options.MaximumPageResponseBytes
        };

        while (sitemapQueue.Count > 0) {
            cancellationToken.ThrowIfCancellationRequested();
            Uri sitemapUri = sitemapQueue.Dequeue();
            if (!IsCrawlHostAllowed(sitemapUri, startUri, options)) continue;
            result.SitemapUrls.Add(sitemapUri.AbsoluteUri);

            string xml;
            try {
                HtmlHttpTextResult sitemapResponse = await HtmlUtilities.GetTextWithProperEncodingAsync(client, sitemapUri.AbsoluteUri, sitemapFetchOptions, cancellationToken).ConfigureAwait(false);
                xml = sitemapResponse.Content;
                sitemapUri = sitemapResponse.FinalUri ?? sitemapUri;
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
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
                string candidate = Uri.TryCreate(sitemapUri, location, out Uri? resolvedLocation) ? resolvedLocation.AbsoluteUri : location;
                QueueCandidate(candidate, sitemapUri.AbsoluteUri, 0, startUri, options, pending, queued, visited, result);
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
            !candidate.AbsolutePath.StartsWith(pathPrefix, StringComparison.Ordinal)) {
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

}
