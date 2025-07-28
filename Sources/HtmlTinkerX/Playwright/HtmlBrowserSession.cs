using Microsoft.Playwright;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Represents a headless browser session consisting of Playwright objects.
/// </summary>
public sealed class HtmlBrowserSession : IAsyncDisposable {
    /// <summary>
    /// Gets the <see cref="IPlaywright"/> instance used by the session.
    /// </summary>
    public IPlaywright Playwright { get; }

    /// <summary>
    /// Gets the browser instance opened for this session.
    /// </summary>
    public IBrowser Browser { get; }

    /// <summary>
    /// Gets the browser context used to create pages.
    /// </summary>
    public IBrowserContext Context { get; }

    /// <summary>
    /// Gets the page associated with the session.
    /// </summary>
    public IPage Page { get; }

    /// <summary>
    /// Gets the video recording object when video capture is enabled.
    /// </summary>
    public IVideo? Video { get; }

    /// <summary>
    /// Gets the path where the recorded video is stored.
    /// </summary>
    public string? VideoPath { get; internal set; }
    private readonly ConcurrentDictionary<IRequest, HtmlNetworkEntry> _network;
    private readonly ConcurrentQueue<IRequest> _order = new();
    private readonly ConcurrentQueue<HtmlConsoleEntry> _console = new();
    private int? _networkLogLimit;
    /// <summary>
    /// Gets or sets the maximum number of network log entries to keep.
    /// </summary>
    public int? NetworkLogLimit {
        get => _networkLogLimit;
        set {
            _networkLogLimit = value;
            if (value.HasValue) {
                TrimNetworkLog(value.Value);
            }
        }
    }
    /// <summary>Captured network log entries.</summary>
    public IEnumerable<HtmlNetworkEntry> NetworkLog => _network.Values;
    /// <summary>Captured console log entries.</summary>
    public IEnumerable<HtmlConsoleEntry> ConsoleLog => _console;

    /// <summary>
    /// Initializes a new instance of the <see cref="HtmlBrowserSession"/> class.
    /// </summary>
    public HtmlBrowserSession(IPlaywright playwright, IBrowser browser, IBrowserContext context, IPage page, IVideo? video = null, string? videoPath = null, ConcurrentDictionary<IRequest, HtmlNetworkEntry>? network = null) {
        Playwright = playwright;
        Browser = browser;
        Context = context;
        Page = page;
        Video = video;
        VideoPath = videoPath;
        _network = network ?? new ConcurrentDictionary<IRequest, HtmlNetworkEntry>();

        Page.Console += (_, msg) => {
            HtmlConsoleEntry entry = new() {
                Text = msg.Text,
                Type = HtmlEnumParser.ParseConsoleMessageType(msg.Type),
                Location = msg.Location?.ToString()
            };
            _console.Enqueue(entry);
        };

        Page.Request += (_, req) => {
            HtmlNetworkEntry entry = new() {
                Url = req.Url,
                Method = HtmlEnumParser.ParseHttpMethod(req.Method),
                RequestHeaders = new Dictionary<string, string>(req.Headers),
                Started = System.DateTimeOffset.UtcNow
            };
            _network[req] = entry;
            _order.Enqueue(req);
            if (NetworkLogLimit.HasValue) {
                TrimNetworkLog(NetworkLogLimit.Value);
            }
        };

        Page.Response += (_, res) => {
            HtmlNetworkEntry entry = _network.GetOrAdd(res.Request, r => new HtmlNetworkEntry {
                Url = r.Url,
                Method = HtmlEnumParser.ParseHttpMethod(r.Method),
                RequestHeaders = new Dictionary<string, string>(r.Headers),
                Started = System.DateTimeOffset.UtcNow
            });
            entry.Status = (System.Net.HttpStatusCode)res.Status;
            entry.ResponseHeaders = new Dictionary<string, string>(res.Headers);
            entry.ResponseReceived = System.DateTimeOffset.UtcNow;
        };

        Page.RequestFinished += (_, req) => {
            if (_network.TryGetValue(req, out HtmlNetworkEntry? entry)) {
                entry.Finished = System.DateTimeOffset.UtcNow;
            }
        };

        Page.RequestFailed += (_, req) => {
            if (_network.TryGetValue(req, out HtmlNetworkEntry? entry)) {
                entry.Finished = System.DateTimeOffset.UtcNow;
            }
        };
    }

    private void TrimNetworkLog(int limit) {
        while (_order.Count > limit && _order.TryDequeue(out IRequest? oldReq)) {
            _network.TryRemove(oldReq, out _);
        }
    }

    /// <summary>
    /// Asynchronously disposes of the browser session, closing the page, context, and browser.
    /// </summary>
    public async ValueTask DisposeAsync() {
        if (Context != null) {
            await Context.CloseAsync().ConfigureAwait(false);
        }

        if (Video != null && !string.IsNullOrEmpty(VideoPath)) {
            string fullPath = VideoPath!.ToFullPath();
            await Video.SaveAsAsync(fullPath).ConfigureAwait(false);
            try {
                string tempPath = await Video.PathAsync().ConfigureAwait(false);
                if (!string.IsNullOrEmpty(tempPath) &&
                    !string.Equals(tempPath, fullPath, System.StringComparison.OrdinalIgnoreCase) &&
                    System.IO.File.Exists(tempPath)) {
                    System.IO.File.Delete(tempPath);
                }
            } catch {
                // Ignore cleanup errors
            }
        }

        if (Browser != null) {
            await Browser.CloseAsync().ConfigureAwait(false);
        }
        if (Playwright != null) {
            Playwright.Dispose();
        }
    }
}