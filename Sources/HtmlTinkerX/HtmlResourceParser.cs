using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helpers for extracting script and stylesheet references from HTML documents.
/// </summary>
public static partial class HtmlResourceParser {
    /// <summary>Parses external and inline resources from HTML.</summary>
    /// <param name="html">HTML markup.</param>
    /// <param name="includeCss">Include CSS resources.</param>
    /// <param name="includeInline">Include inline scripts and styles.</param>
    public static List<HtmlResourceLink> Parse(string html, bool includeCss = false, bool includeInline = false) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        var result = new List<HtmlResourceLink>();
        int index = 0;

        foreach (var script in document.QuerySelectorAll("script")) {
            string src = script.GetAttribute("src") ?? string.Empty;
            string? comment = GetPrecedingComment(script);
            if (!string.IsNullOrEmpty(src)) {
                result.Add(new HtmlResourceLink {
                    Index = index++,
                    Type = HtmlResourceType.Script,
                    Source = src,
                    Comment = comment,
                    Name = GetFileNameFromUrl(src)
                });
            } else if (includeInline) {
                string content = script.TextContent ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(content)) {
                    result.Add(new HtmlResourceLink {
                        Index = index++,
                        Type = HtmlResourceType.InlineScript,
                        Content = content,
                        Comment = comment,
                        Name = !string.IsNullOrEmpty(comment) ? comment! : $"inline_script_{index}"
                    });
                }
            }
        }

        if (includeCss) {
            foreach (var link in document.QuerySelectorAll("link[rel='stylesheet'][href]")) {
                string href = link.GetAttribute("href") ?? string.Empty;
                string? comment = GetPrecedingComment(link);
                if (!string.IsNullOrEmpty(href)) {
                    result.Add(new HtmlResourceLink {
                        Index = index++,
                        Type = HtmlResourceType.Css,
                        Source = href,
                        Comment = comment,
                        Name = GetFileNameFromUrl(href)
                    });
                }
            }

            if (includeInline) {
                foreach (var style in document.QuerySelectorAll("style")) {
                    string text = style.TextContent ?? string.Empty;
                    string? comment = GetPrecedingComment(style);
                    if (!string.IsNullOrWhiteSpace(text)) {
                        result.Add(new HtmlResourceLink {
                            Index = index++,
                            Type = HtmlResourceType.InlineCss,
                            Content = text,
                            Comment = comment,
                            Name = !string.IsNullOrEmpty(comment) ? comment! : $"inline_css_{index}"
                        });
                    }
                }
            }
        }

        return result;
    }

    /// <summary>Downloads and parses resources from a URL.</summary>
    /// <param name="url">Absolute page URL.</param>
    /// <param name="includeCss">Include stylesheet references.</param>
    /// <param name="includeInline">Include inline script and style content.</param>
    /// <param name="client">Optional HTTP client.</param>
    /// <param name="fetchOptions">Optional response-size policy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<List<HtmlResourceLink>> ParseUrlAsync(string url, bool includeCss = false, bool includeInline = false, HttpClient? client = null, HtmlHttpFetchOptions? fetchOptions = null, CancellationToken cancellationToken = default) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) {
            throw new ArgumentException("The URL must be an absolute URI.", nameof(url));
        }

        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        string content = await HtmlUtilities.GetStringWithProperEncodingAsync(http, uri.ToString(), fetchOptions, cancellationToken).ConfigureAwait(false);
        return Parse(content, includeCss, includeInline);
    }

    /// <summary>Downloads resources referenced by the provided links.</summary>
    /// <param name="links">Resources to download.</param>
    /// <param name="baseUri">Base URI used to resolve relative resource links.</param>
    /// <param name="directory">Destination directory.</param>
    /// <param name="client">Optional HTTP client.</param>
    /// <param name="fetchOptions">Optional response-size policy applied to each resource.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<List<string>> DownloadResourcesAsync(
        IEnumerable<HtmlResourceLink> links,
        Uri baseUri,
        string directory,
        HttpClient? client = null,
        HtmlHttpFetchOptions? fetchOptions = null,
        CancellationToken cancellationToken = default) {
        if (links == null) {
            throw new ArgumentNullException(nameof(links));
        }
        if (directory == null) {
            throw new ArgumentNullException(nameof(directory));
        }

        string dir = HtmlUtilities.EnsureDirectoryExists(directory);
        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        List<string> paths = new();

        foreach (var link in links.Where(l => !string.IsNullOrEmpty(l.Source))) {
            Uri srcUri = new(link.Source, UriKind.RelativeOrAbsolute);
            if (!srcUri.IsAbsoluteUri) {
                srcUri = new Uri(baseUri, srcUri);
            }
            string filePath = Path.Combine(dir, Path.GetFileName(srcUri.AbsolutePath));
            await HtmlUtilities.DownloadToFileAsync(http, srcUri, filePath, fetchOptions, cancellationToken).ConfigureAwait(false);
            paths.Add(filePath);
        }

        return paths;
    }

    /// <summary>Downloads resources referenced by the page at the given URL.</summary>
    /// <param name="url">Absolute page URL.</param>
    /// <param name="directory">Destination directory.</param>
    /// <param name="includeCss">Include stylesheet references.</param>
    /// <param name="client">Optional HTTP client.</param>
    /// <param name="fetchOptions">Optional response-size policy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<List<string>> DownloadResourcesFromUrlAsync(string url, string directory, bool includeCss = false, HttpClient? client = null, HtmlHttpFetchOptions? fetchOptions = null, CancellationToken cancellationToken = default) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var baseUri)) {
            throw new ArgumentException("The URL must be an absolute URI.", nameof(url));
        }

        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        List<HtmlResourceLink> links = await ParseUrlAsync(baseUri.ToString(), includeCss, includeInline: false, client: http, fetchOptions: fetchOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await DownloadResourcesAsync(links, baseUri, directory, http, fetchOptions, cancellationToken).ConfigureAwait(false);
    }

    private static string GetFileNameFromUrl(string url) {
        if (Uri.TryCreate(url, UriKind.Absolute, out var abs) &&
            (abs.Scheme == Uri.UriSchemeHttp || abs.Scheme == Uri.UriSchemeHttps)) {
            return Path.GetFileName(abs.AbsolutePath);
        }

        string decoded = Uri.UnescapeDataString(url);
        int idx = decoded.IndexOfAny(new[] { '?', '#' });
        string path = idx >= 0 ? decoded.Substring(0, idx) : decoded;
        return Path.GetFileName(path);
    }

    private static string? GetPrecedingComment(IElement element) {
        INode? node = element.PreviousSibling;
        while (node != null && node.NodeType == NodeType.Text && string.IsNullOrWhiteSpace(node.TextContent)) {
            node = node.PreviousSibling;
        }

        return (node as IComment)?.Data.Trim();
    }
}
