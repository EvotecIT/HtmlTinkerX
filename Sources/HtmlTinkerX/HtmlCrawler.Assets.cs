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
            using CancellationTokenSource requestTimeout = HtmlUtilities.CreateRequestTimeoutTokenSource(client, cancellationToken);
            CancellationToken requestToken = requestTimeout.Token;
            using HttpResponseMessage response = await client.GetAsync(assetUrl, HttpCompletionOption.ResponseHeadersRead, requestToken).ConfigureAwait(false);
            asset.StatusCode = (int)response.StatusCode;
            asset.ContentType = response.Content.Headers.ContentType?.MediaType ?? response.Content.Headers.ContentType?.ToString();
            response.EnsureSuccessStatusCode();

            byte[] bytes = await HtmlUtilities.ReadResponseBytesAsync(response, options.MaximumAssetResponseBytes, requestToken).ConfigureAwait(false);
            asset.ContentLength = bytes.LongLength;

            if (!string.IsNullOrEmpty(assetsDirectory)) {
                string assetPath = BuildAssetPath(asset, assetsDirectory!);
                await WriteBytesAsync(assetPath, bytes, cancellationToken).ConfigureAwait(false);
                asset.FilePath = assetPath;
            }
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
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
