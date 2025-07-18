using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helpers for extracting script and stylesheet references from HTML documents.
/// </summary>
public static class HtmlResourceParser
{
    /// <summary>Parses external and inline resources from HTML.</summary>
    /// <param name="html">HTML markup.</param>
    /// <param name="includeCss">Include CSS resources.</param>
    /// <param name="includeInline">Include inline scripts and styles.</param>
    public static List<HtmlResourceLink> Parse(string html, bool includeCss = false, bool includeInline = false)
    {
        if (html == null)
        {
            throw new ArgumentNullException(nameof(html));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        var result = new List<HtmlResourceLink>();
        int index = 0;

        foreach (var script in document.QuerySelectorAll("script"))
        {
            string src = script.GetAttribute("src") ?? string.Empty;
            string? comment = GetPrecedingComment(script);
            if (!string.IsNullOrEmpty(src))
            {
                result.Add(new HtmlResourceLink
                {
                    Index = index++,
                    Type = HtmlResourceType.Script,
                    Source = src,
                    Comment = comment,
                    Name = Path.GetFileName(src)
                });
            }
            else if (includeInline)
            {
                string content = script.TextContent ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(content))
                {
                    result.Add(new HtmlResourceLink
                    {
                        Index = index++,
                        Type = HtmlResourceType.InlineScript,
                        Content = content,
                        Comment = comment,
                        Name = !string.IsNullOrEmpty(comment) ? comment : $"inline_script_{index}"
                    });
                }
            }
        }

        if (includeCss)
        {
            foreach (var link in document.QuerySelectorAll("link[rel='stylesheet'][href]"))
            {
                string href = link.GetAttribute("href") ?? string.Empty;
                string? comment = GetPrecedingComment(link);
                if (!string.IsNullOrEmpty(href))
                {
                    result.Add(new HtmlResourceLink
                    {
                        Index = index++,
                        Type = HtmlResourceType.Css,
                        Source = href,
                        Comment = comment,
                        Name = Path.GetFileName(href)
                    });
                }
            }

            if (includeInline)
            {
                foreach (var style in document.QuerySelectorAll("style"))
                {
                    string text = style.TextContent ?? string.Empty;
                    string? comment = GetPrecedingComment(style);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        result.Add(new HtmlResourceLink
                        {
                            Index = index++,
                            Type = HtmlResourceType.InlineCss,
                            Content = text,
                            Comment = comment,
                            Name = !string.IsNullOrEmpty(comment) ? comment : $"inline_css_{index}"
                        });
                    }
                }
            }
        }

        return result;
    }

    /// <summary>Downloads and parses resources from a URL.</summary>
    public static async Task<List<HtmlResourceLink>> ParseUrlAsync(string url, bool includeCss = false, bool includeInline = false, HttpClient? client = null)
    {
        if (url == null)
        {
            throw new ArgumentNullException(nameof(url));
        }

        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        string content = await HtmlUtilities.GetStringWithProperEncodingAsync(http, url).ConfigureAwait(false);
        return Parse(content, includeCss, includeInline);
    }

    /// <summary>Downloads resources referenced by the provided links.</summary>
    public static async Task<List<string>> DownloadResourcesAsync(IEnumerable<HtmlResourceLink> links, Uri baseUri, string directory, HttpClient? client = null)
    {
        if (links == null)
        {
            throw new ArgumentNullException(nameof(links));
        }
        if (directory == null)
        {
            throw new ArgumentNullException(nameof(directory));
        }

        string dir = HtmlUtilities.ResolvePath(directory);
        Directory.CreateDirectory(dir);
        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        List<string> paths = new();

        foreach (var link in links.Where(l => !string.IsNullOrEmpty(l.Source)))
        {
            Uri srcUri = Uri.TryCreate(link.Source, UriKind.Absolute, out var abs) ? abs : new Uri(baseUri, link.Source);
#if NETSTANDARD2_0 || NETFRAMEWORK
            using (HttpResponseMessage response = await http.GetAsync(srcUri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                using Stream contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                string filePath = Path.Combine(dir, Path.GetFileName(srcUri.LocalPath));
                using FileStream fileStream = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await contentStream.CopyToAsync(fileStream).ConfigureAwait(false);
                paths.Add(filePath);
            }
#else
            using HttpResponseMessage response = await http.GetAsync(srcUri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using Stream contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            string filePath = Path.Combine(dir, Path.GetFileName(srcUri.LocalPath));
            await using FileStream fileStream = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await contentStream.CopyToAsync(fileStream).ConfigureAwait(false);
            paths.Add(filePath);
#endif
        }

        return paths;
    }

    /// <summary>Downloads resources referenced by the page at the given URL.</summary>
    public static async Task<List<string>> DownloadResourcesFromUrlAsync(string url, string directory, bool includeCss = false, HttpClient? client = null)
    {
        if (url == null)
        {
            throw new ArgumentNullException(nameof(url));
        }

        Uri baseUri = new(url);
        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        List<HtmlResourceLink> links = await ParseUrlAsync(url, includeCss, includeInline: false, client: http).ConfigureAwait(false);
        return await DownloadResourcesAsync(links, baseUri, directory, http).ConfigureAwait(false);
    }

    private static string? GetPrecedingComment(IElement element)
    {
        INode? node = element.PreviousSibling;
        while (node != null && node.NodeType == NodeType.Text && string.IsNullOrWhiteSpace(node.TextContent))
        {
            node = node.PreviousSibling;
        }

        return (node as IComment)?.Data.Trim();
    }
}
