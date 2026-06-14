using Microsoft.Playwright;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
    private readonly ConcurrentDictionary<IRequest, IResponse> _responses = new();
    private ConcurrentQueue<IRequest>? _order;
    private object? _networkSync;
    private ConcurrentQueue<IRequest> RequestOrder => LazyInitializer.EnsureInitialized(ref _order, () => new ConcurrentQueue<IRequest>())!;
    private object NetworkSync => LazyInitializer.EnsureInitialized(ref _networkSync, () => new object())!;
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
                lock (NetworkSync) {
                    TrimNetworkLog(value.Value);
                }
            }
        }
    }
    /// <summary>Captured network log entries.</summary>
    public IEnumerable<HtmlNetworkEntry> NetworkLog {
        get {
            ConcurrentDictionary<IRequest, HtmlNetworkEntry>? network = _network;
            if (network is null) {
                return Array.Empty<HtmlNetworkEntry>();
            }

            List<IRequest> orderedRequests = new List<IRequest>();
            List<HtmlNetworkEntry> orderedEntries = new List<HtmlNetworkEntry>();

            lock (NetworkSync) {
                ConcurrentQueue<IRequest> requestOrder = RequestOrder;

                while (requestOrder.TryDequeue(out IRequest? request)) {
                    orderedRequests.Add(request);
                }

                if (orderedRequests.Count == 0) {
                    orderedEntries.AddRange(network.Values);
                } else {
                    foreach (IRequest request in orderedRequests) {
                        requestOrder.Enqueue(request);
                        if (network.TryGetValue(request, out HtmlNetworkEntry? entry)) {
                            orderedEntries.Add(entry);
                        }
                    }
                }
            }

            return orderedEntries;
        }
    }
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
                ResourceType = HtmlEnumParser.ParseNetworkResourceType(req.ResourceType),
                Started = System.DateTimeOffset.UtcNow
            };

            lock (NetworkSync) {
                _network[req] = entry;
                RequestOrder.Enqueue(req);
                if (NetworkLogLimit.HasValue) {
                    TrimNetworkLog(NetworkLogLimit.Value);
                }
            }
        };

        Page.Response += (_, res) => {
            HtmlNetworkEntry entry = _network.GetOrAdd(res.Request, r => new HtmlNetworkEntry {
                Url = r.Url,
                Method = HtmlEnumParser.ParseHttpMethod(r.Method),
                RequestHeaders = new Dictionary<string, string>(r.Headers),
                ResourceType = HtmlEnumParser.ParseNetworkResourceType(r.ResourceType),
                Started = System.DateTimeOffset.UtcNow
            });
            entry.Status = (System.Net.HttpStatusCode)res.Status;
            entry.ResponseHeaders = new Dictionary<string, string>(res.Headers);
            entry.ResponseReceived = System.DateTimeOffset.UtcNow;
            _responses[res.Request] = res;
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

    internal async Task CaptureResponseBodiesAsync(int maxBytes, ISet<HtmlNetworkResourceType> resourceTypes, CancellationToken cancellationToken) {
        if (maxBytes <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maxBytes), "Response body capture size must be greater than zero.");
        }

        IReadOnlyList<(IRequest Request, HtmlNetworkEntry Entry)> entries;
        lock (NetworkSync) {
            entries = _network
                .Where(item => resourceTypes.Contains(item.Value.ResourceType))
                .Select(item => (item.Key, item.Value))
                .ToArray();
        }

        foreach ((IRequest Request, HtmlNetworkEntry Entry) item in entries) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_responses.TryGetValue(item.Request, out IResponse? response)) {
                item.Entry.ResponseBodyError = "Response body is not available for this request.";
                continue;
            }

            try {
                Task<string> readTask = response.TextAsync();
                Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(3));
                Task cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                Task completed = await Task.WhenAny(readTask, timeoutTask, cancellationTask).ConfigureAwait(false);
                if (ReferenceEquals(completed, cancellationTask)) {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (ReferenceEquals(completed, timeoutTask)) {
                    item.Entry.ResponseBodyError = "Response body capture timed out.";
                    continue;
                }

                string body = await readTask.ConfigureAwait(false);
                item.Entry.ResponseBody = TruncateUtf8(body, maxBytes, out bool truncated);
                item.Entry.ResponseBodyTruncated = truncated;
                item.Entry.ResponseBodyError = null;
            } catch (Exception ex) when (ex is PlaywrightException || ex is InvalidOperationException) {
                item.Entry.ResponseBodyError = ex.Message;
            }
        }
    }

    private static string TruncateUtf8(string value, int maxBytes, out bool truncated) {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length <= maxBytes) {
            truncated = false;
            return value;
        }

        truncated = true;
        int length = Math.Min(maxBytes, bytes.Length);
        Encoding strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        while (length > 0) {
            try {
                return strictUtf8.GetString(bytes, 0, length);
            } catch (DecoderFallbackException) {
                length--;
            }
        }

        return string.Empty;
    }

    private void TrimNetworkLog(int limit) {
        ConcurrentQueue<IRequest> requestOrder = RequestOrder;
        while (requestOrder.Count > limit && requestOrder.TryDequeue(out IRequest? oldReq)) {
            _network?.TryRemove(oldReq, out _);
            _responses.TryRemove(oldReq, out _);
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
