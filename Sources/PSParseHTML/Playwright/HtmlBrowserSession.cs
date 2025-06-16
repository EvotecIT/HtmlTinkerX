using Microsoft.Playwright;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PSParseHTML;

/// <summary>
/// Represents a headless browser session consisting of Playwright objects.
/// </summary>
public sealed class HtmlBrowserSession : IAsyncDisposable {
    public IPlaywright Playwright { get; }
    public IBrowser Browser { get; }
    public IBrowserContext Context { get; }
    public IPage Page { get; }
    public IVideo? Video { get; }
    public string? VideoPath { get; }
    private readonly ConcurrentDictionary<IRequest, HtmlNetworkEntry> _network;
    /// <summary>Captured network log entries.</summary>
    public IEnumerable<HtmlNetworkEntry> NetworkLog => _network.Values;

    /// <summary>
    /// Initializes a new instance of the <see cref="HtmlBrowserSession"/> class.
    /// /// </summary>
    public HtmlBrowserSession(IPlaywright playwright, IBrowser browser, IBrowserContext context, IPage page, IVideo? video = null, string? videoPath = null, ConcurrentDictionary<IRequest, HtmlNetworkEntry>? network = null) {
        Playwright = playwright;
        Browser = browser;
        Context = context;
        Page = page;
        Video = video;
        VideoPath = videoPath;
        _network = network ?? new ConcurrentDictionary<IRequest, HtmlNetworkEntry>();

        Page.Request += (_, req) => {
            HtmlNetworkEntry entry = new() {
                Url = req.Url,
                Method = req.Method,
                RequestHeaders = new Dictionary<string, string>(req.Headers)
            };
            _network[req] = entry;
        };

        Page.Response += (_, res) => {
            HtmlNetworkEntry entry = _network.GetOrAdd(res.Request, r => new HtmlNetworkEntry {
                Url = r.Url,
                Method = r.Method,
                RequestHeaders = new Dictionary<string, string>(r.Headers)
            });
            entry.Status = res.Status;
            entry.ResponseHeaders = new Dictionary<string, string>(res.Headers);
        };
    }

    /// <summary>
    /// Asynchronously disposes of the browser session, closing the page, context, and browser.
    /// /// </summary>
    public async ValueTask DisposeAsync() {
        await Context.CloseAsync();
        await Browser.CloseAsync();
        Playwright.Dispose();
    }
}
