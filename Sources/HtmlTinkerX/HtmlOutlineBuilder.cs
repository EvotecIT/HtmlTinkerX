using AngleSharp.Dom;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Builds a hierarchical outline of headings found in HTML content.
/// </summary>
public static class HtmlOutlineBuilder {
    /// <summary>
    /// Builds an outline from the provided HTML markup using the specified engine.
    /// </summary>
    /// <param name="html">HTML markup.</param>
    /// <param name="engine">Parsing engine.</param>
    /// <returns>Collection of outline items.</returns>
    public static List<HtmlOutlineItem> Build(string html, HtmlParserEngine engine = HtmlParserEngine.AgilityPack) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }
        return engine == HtmlParserEngine.AngleSharp
            ? Build(HtmlParser.ParseWithAngleSharp(html))
            : Build(HtmlParser.ParseWithHtmlAgilityPack(html));
    }

    /// <summary>
    /// Downloads HTML from a URL and builds an outline using the specified engine.
    /// </summary>
    /// <param name="url">URL of the page.</param>
    /// <param name="engine">Parsing engine.</param>
    /// <param name="client">Optional HTTP client.</param>
    /// <returns>Collection of outline items.</returns>
    public static async Task<List<HtmlOutlineItem>> BuildFromUrlAsync(string url, HtmlParserEngine engine = HtmlParserEngine.AgilityPack, HttpClient? client = null) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }
        if (engine == HtmlParserEngine.AngleSharp) {
            IDocument doc = await HtmlParser.ParseUrlWithAngleSharpAsync(url, client).ConfigureAwait(false);
            return Build(doc);
        }
        HtmlDocument doc2 = await HtmlParser.ParseUrlWithHtmlAgilityPackAsync(url, client).ConfigureAwait(false);
        return Build(doc2);
    }

    /// <summary>
    /// Builds an outline from an AngleSharp document.
    /// </summary>
    public static List<HtmlOutlineItem> Build(IDocument document) {
        if (document == null) {
            throw new ArgumentNullException(nameof(document));
        }
        List<(int level, string title, string? id)> headings = new();
        foreach (var node in document.QuerySelectorAll("h1,h2,h3,h4,h5,h6")) {
            if (node is not IElement element) {
                continue;
            }
            if (!int.TryParse(element.TagName.Substring(1), out int level)) {
                continue;
            }
            string title = element.TextContent.Trim();
            headings.Add((level, title, element.Id));
        }
        return BuildFromHeadings(headings);
    }

    /// <summary>
    /// Builds an outline from a HtmlAgilityPack document.
    /// </summary>
    public static List<HtmlOutlineItem> Build(HtmlDocument document) {
        if (document == null) {
            throw new ArgumentNullException(nameof(document));
        }
        List<(int level, string title, string? id)> headings = new();
        var nodes = document.DocumentNode.SelectNodes("//h1|//h2|//h3|//h4|//h5|//h6");
        if (nodes != null) {
            foreach (var node in nodes) {
                if (node == null) {
                    continue;
                }
                if (!int.TryParse(node.Name!.Substring(1), out int level)) {
                    continue;
                }
                string title = HtmlEntity.DeEntitize(node.InnerText ?? string.Empty)!.Trim();
                string? id = node.Id;
                headings.Add((level, title, id));
            }
        }
        return BuildFromHeadings(headings);
    }

    private static List<HtmlOutlineItem> BuildFromHeadings(List<(int level, string title, string? id)> headings) {
        List<HtmlOutlineItem> roots = new();
        Stack<HtmlOutlineItem> stack = new();
        foreach (var (level, title, id) in headings) {
            HtmlOutlineItem item = new() { Title = title, Level = level, Id = id };
            while (stack.Count > 0 && stack.Peek().Level >= level) {
                stack.Pop();
            }
            if (stack.Count == 0) {
                roots.Add(item);
            } else {
                stack.Peek().Children.Add(item);
            }
            stack.Push(item);
        }
        return roots;
    }
}