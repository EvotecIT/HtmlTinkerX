using AngleSharp.Dom;
using System;
using System.Collections;
using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// A successfully fetched or rendered crawl page passed to a page handler, with methods to emit items and queue follow-up requests.
/// </summary>
/// <remarks>
/// The crawler applies emitted items and follow-up requests after the handler returns.
/// Follow-up requests go through the crawl's scope rules, MaxDepth, MaxPages, robots.txt and URL deduplication,
/// exactly like discovered links. The context is valid only during the handler call.
/// </remarks>
public sealed class HtmlCrawlPageContext {
    private static readonly IReadOnlyDictionary<string, string> EmptyData = new Dictionary<string, string>();
    private readonly string _html;
    private IDocument? _document;

    internal HtmlCrawlPageContext(HtmlCrawlPage page, string? html, int depth, IReadOnlyDictionary<string, string>? data) {
        Page = page;
        _html = html ?? string.Empty;
        Depth = depth;
        Data = data ?? EmptyData;
    }

    /// <summary>Page that was fetched, extracted and added to the crawl result.</summary>
    public HtmlCrawlPage Page { get; }

    /// <summary>Full response or rendered HTML parsed on first access, independent of content selectors.</summary>
    public IDocument Document => _document ??= HtmlParser.ParseWithAngleSharp(_html);

    /// <summary>Crawl depth of the page; the start page is zero.</summary>
    public int Depth { get; }

    /// <summary>Data attached by the follow-up request that queued this page. Empty for the start page and discovered links.</summary>
    public IReadOnlyDictionary<string, string> Data { get; }

    /// <summary>
    /// When true (the default), the crawler also queues the links it discovered on the page.
    /// Set to false to follow only the requests added with <see cref="Follow"/>.
    /// </summary>
    public bool FollowLinks { get; set; } = true;

    internal List<object> Items { get; } = new();

    internal List<FollowRequest> Requests { get; } = new();

    /// <summary>Emits an item to the crawl stream.</summary>
    /// <param name="item">Any object the handler produces.</param>
    public void Emit(object item) => Items.Add(item ?? throw new ArgumentNullException(nameof(item)));

    /// <summary>Queues a follow-up request at the next depth.</summary>
    /// <param name="url">Absolute URL, or a URL relative to the page.</param>
    /// <param name="data">Optional key/value pairs passed to the handler of the followed page as <see cref="Data"/>; values are stored as strings in checkpoints.</param>
    /// <param name="priority">Requests with a higher priority are fetched first. Equal priorities keep queue order. The default is zero.</param>
    /// <param name="render">Fetches the request with the browser. Requires MaxConcurrentPageRequests to be one.</param>
    public void Follow(string url, IDictionary? data = null, int priority = 0, bool render = false) {
        if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("A follow-up URL is required.", nameof(url));
        Dictionary<string, string>? values = null;
        if (data != null && data.Count > 0) {
            values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in data) {
                values[Convert.ToString(entry.Key, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty] =
                    Convert.ToString(entry.Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }
        Requests.Add(new FollowRequest(url, values, priority, render));
    }

    internal sealed class FollowRequest {
        public FollowRequest(string url, Dictionary<string, string>? data, int priority, bool render) {
            Url = url;
            Data = data;
            Priority = priority;
            Render = render;
        }

        public string Url { get; }
        public Dictionary<string, string>? Data { get; }
        public int Priority { get; }
        public bool Render { get; }
    }
}
