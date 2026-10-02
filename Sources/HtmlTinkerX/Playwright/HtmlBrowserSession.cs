using Microsoft.Playwright;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Represents a headless browser session consisting of Playwright objects.
/// </summary>
public sealed class HtmlBrowserSession : IAsyncDisposable {
    private const int DefaultBufferedResponseBodyReadLimitBytes = 1024 * 1024;
    /// <summary>
    /// Gets the <see cref="IPlaywright"/> instance used by the session.
    /// </summary>
    public IPlaywright Playwright { get; }

    /// <summary>
    /// Gets the browser instance opened for this session.
    /// </summary>
    public IBrowser? Browser { get; }

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

    /// <summary>
    /// Gets the persistent user-data directory used by this session, when launched as a persistent profile.
    /// </summary>
    public string? UserDataDirectory { get; }

    /// <summary>
    /// Gets whether this session was launched with a persistent browser context.
    /// </summary>
    public bool IsPersistent => !string.IsNullOrWhiteSpace(UserDataDirectory);

    /// <summary>
    /// Gets the Chrome DevTools Protocol endpoint used by this session, when attached to an existing browser.
    /// </summary>
    public string? CdpEndpointUrl { get; }

    /// <summary>
    /// Gets whether this session is attached to an already-running browser through CDP.
    /// </summary>
    public bool IsCdpAttached => !string.IsNullOrWhiteSpace(CdpEndpointUrl);

    private readonly bool _closeContextOnDispose;
    private readonly bool _closeBrowserOnDispose;
    private readonly bool _closePageOnDispose;
    private readonly ConcurrentDictionary<IRequest, HtmlNetworkEntry> _network;
    private readonly ConcurrentDictionary<IRequest, IResponse> _responses = new();
    private readonly ConditionalWeakTable<IRequest, object> _observedRequests = new();
    private ConcurrentQueue<IRequest>? _order;
    private object? _networkSync;
    private Task? _disposeTask;
    private bool _pageClosed;
    private bool _contextClosed;
    private bool _videoSaved;
    private bool _browserClosed;
    private bool _playwrightDisposed;
    private object? _disposeSync;
    private ConcurrentQueue<IRequest> RequestOrder => LazyInitializer.EnsureInitialized(ref _order, () => new ConcurrentQueue<IRequest>())!;
    private object NetworkSync => LazyInitializer.EnsureInitialized(ref _networkSync, () => new object())!;
    private object DisposeSync => LazyInitializer.EnsureInitialized(ref _disposeSync, () => new object())!;
    private readonly ConcurrentQueue<HtmlConsoleEntry> _console = new();
    private readonly Dictionary<long, HtmlNetworkEntry> _networkBySequence = new();
    private long _firstNetworkSequence = 1;
    private long _networkSequence;
    internal long NetworkLogPosition => Interlocked.Read(ref _networkSequence);
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
    public IEnumerable<HtmlNetworkEntry> NetworkLog => GetNetworkLogSince(-1);

    internal IReadOnlyList<HtmlNetworkEntry> GetNetworkLogSince(long position) {
        if (_network == null) return Array.Empty<HtmlNetworkEntry>();
        List<HtmlNetworkEntry> entries = new();
        lock (NetworkSync) {
            if (position >= 0) {
                for (long sequence = Math.Max(position + 1, _firstNetworkSequence); sequence <= _networkSequence; sequence++) {
                    if (_networkBySequence.TryGetValue(sequence, out HtmlNetworkEntry? entry)) entries.Add(entry);
                }
                return entries;
            }
            foreach (IRequest request in RequestOrder.ToArray()) {
                if (_network.TryGetValue(request, out HtmlNetworkEntry? entry) && entry.CaptureSequence > position) {
                    entries.Add(entry);
                }
            }
            if (RequestOrder.IsEmpty) {
                entries.AddRange(_network.Values.Where(entry => entry.CaptureSequence > position));
            }
        }
        return entries;
    }
    /// <summary>Captured console log entries.</summary>
    public IEnumerable<HtmlConsoleEntry> ConsoleLog => _console;

    /// <summary>Current recipe recorder attached to this session, when recording is active or recently stopped.</summary>
    public HtmlBrowserRecipeRecorder? RecipeRecorder { get; internal set; }

    internal bool SuppressRecipeRecording { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HtmlBrowserSession"/> class.
    /// </summary>
    public HtmlBrowserSession(
        IPlaywright playwright,
        IBrowser? browser,
        IBrowserContext context,
        IPage page,
        IVideo? video = null,
        string? videoPath = null,
        ConcurrentDictionary<IRequest, HtmlNetworkEntry>? network = null,
        string? userDataDirectory = null,
        string? cdpEndpointUrl = null,
        bool closeContextOnDispose = true,
        bool closeBrowserOnDispose = true,
        bool closePageOnDispose = false) {
        Playwright = playwright;
        Browser = browser;
        Context = context;
        Page = page;
        Video = video;
        VideoPath = videoPath;
        UserDataDirectory = userDataDirectory;
        CdpEndpointUrl = cdpEndpointUrl;
        _closeContextOnDispose = closeContextOnDispose;
        _closeBrowserOnDispose = closeBrowserOnDispose;
        _closePageOnDispose = closePageOnDispose;
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
            lock (NetworkSync) {
                CaptureRequest(req);
            }
        };

        Page.Response += (_, res) => {
            lock (NetworkSync) {
                if (!_network.TryGetValue(res.Request, out HtmlNetworkEntry? entry)) {
                    // Existing pages can have requests in flight before event subscription.
                    // Weak identities distinguish those from evicted requests without retaining them.
                    if (_observedRequests.TryGetValue(res.Request, out _)) return;
                    CaptureRequest(res.Request);
                    if (!_network.TryGetValue(res.Request, out entry)) return;
                }
                entry.Status = (System.Net.HttpStatusCode)res.Status;
                entry.ResponseHeaders = new Dictionary<string, string>(res.Headers);
                entry.ResponseReceived = System.DateTimeOffset.UtcNow;
                _responses[res.Request] = res;
            }
        };

        Page.RequestFinished += (_, req) => {
            if (_network.TryGetValue(req, out HtmlNetworkEntry? entry)) {
                entry.Finished = System.DateTimeOffset.UtcNow;
            }
        };

        Page.RequestFailed += (_, req) => {
            if (_network.TryGetValue(req, out HtmlNetworkEntry? entry)) {
                entry.Finished = System.DateTimeOffset.UtcNow;
                entry.FailureText = req.Failure;
            }
        };
    }

    // Called under NetworkSync by both request-start and pre-subscription response events.
    private void CaptureRequest(IRequest request) {
        _observedRequests.GetValue(request, static _ => new object());
        HtmlNetworkEntry entry = new() {
            Url = request.Url,
            Method = HtmlEnumParser.ParseHttpMethod(request.Method),
            RequestHeaders = new Dictionary<string, string>(request.Headers),
            ResourceType = HtmlEnumParser.ParseNetworkResourceType(request.ResourceType),
            Started = DateTimeOffset.UtcNow,
            CaptureSequence = ++_networkSequence
        };
        _networkBySequence[entry.CaptureSequence] = entry;
        _network[request] = entry;
        RequestOrder.Enqueue(request);
        if (NetworkLogLimit.HasValue) TrimNetworkLog(NetworkLogLimit.Value);
    }

    internal async Task CaptureResponseBodiesAsync(int maxBytes, ISet<HtmlNetworkResourceType> resourceTypes, CancellationToken cancellationToken, bool redactSensitiveValues = false) {
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
                if (TryGetDeclaredContentLength(item.Entry, out long contentLength)
                    && contentLength > Math.Max(maxBytes, DefaultBufferedResponseBodyReadLimitBytes)) {
                    item.Entry.ResponseBody = null;
                    item.Entry.ResponseBodyTruncated = true;
                    item.Entry.ResponseBodyError = $"Response body length {contentLength} exceeds buffered capture limit {Math.Max(maxBytes, DefaultBufferedResponseBodyReadLimitBytes)}.";
                    continue;
                }

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
                if (redactSensitiveValues) {
                    body = HtmlSensitiveValueRedactor.RedactSensitiveStructuredText(body);
                    item.Entry.ResponseBodyRedacted = true;
                } else {
                    item.Entry.ResponseBodyRedacted = false;
                }

                string storedBody = TruncateUtf8(body, maxBytes, out bool truncated);
                item.Entry.ResponseBody = storedBody;
                item.Entry.ResponseBodyTruncated = truncated;
                item.Entry.ResponseBodyError = null;
            } catch (Exception ex) when (ex is PlaywrightException || ex is InvalidOperationException) {
                item.Entry.ResponseBodyError = ex.Message;
            }
        }
    }

    private static bool TryGetDeclaredContentLength(HtmlNetworkEntry entry, out long contentLength) {
        contentLength = 0;
        if (entry.ResponseHeaders == null) {
            return false;
        }

        if (!entry.ResponseHeaders.TryGetValue("content-length", out string? value)
            || string.IsNullOrWhiteSpace(value)) {
            return false;
        }

        return long.TryParse(value, out contentLength) && contentLength >= 0;
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
            if (_network != null && _network.TryRemove(oldReq, out HtmlNetworkEntry? oldEntry)) {
                _networkBySequence.Remove(oldEntry.CaptureSequence);
                _firstNetworkSequence = Math.Max(_firstNetworkSequence, oldEntry.CaptureSequence + 1);
            }
            _responses.TryRemove(oldReq, out _);
        }
    }

    /// <summary>
    /// Asynchronously disposes of the browser session, closing the page, context, and browser.
    /// </summary>
    public ValueTask DisposeAsync() {
        lock (DisposeSync) {
            if (_disposeTask == null || _disposeTask.IsFaulted || _disposeTask.IsCanceled) {
                _disposeTask = DisposeCoreAsync();
            }
            return new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeCoreAsync() {
        List<Exception> errors = new();
        bool browserAttempted = false;
        if (!_pageClosed && _closePageOnDispose && Page != null && !Page.IsClosed) {
            try {
                await Page.CloseAsync().ConfigureAwait(false);
                _pageClosed = true;
            } catch (PlaywrightException) {
                // An attached browser may already have been closed by its owner.
                _pageClosed = true;
            } catch (Exception ex) {
                errors.Add(ex);
            }
        }

        if (!_contextClosed && _closeContextOnDispose && Context != null) {
            try {
                await Context.CloseAsync().ConfigureAwait(false);
                _contextClosed = true;
            } catch (Exception ex) {
                errors.Add(ex);
            }
        }

        // Closing the browser also releases a context whose close operation failed.
        // Do that before saving video in the failure path, so SaveAs cannot wait on it.
        if (errors.Count > 0) {
            await CloseBrowserAsync().ConfigureAwait(false);
        }
        if (!_videoSaved && Video != null && !string.IsNullOrEmpty(VideoPath)
            && (!_closeContextOnDispose || _contextClosed || _browserClosed)) {
            try {
                string fullPath = VideoPath!.ToFullPath();
                await Video.SaveAsAsync(fullPath).ConfigureAwait(false);
                _videoSaved = true;
                try {
                    string tempPath = await Video.PathAsync().ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(tempPath) &&
                        !string.Equals(tempPath, fullPath, StringComparison.OrdinalIgnoreCase) &&
                        System.IO.File.Exists(tempPath)) {
                        System.IO.File.Delete(tempPath);
                    }
                } catch {
                    // Saving succeeded; deleting the recording's temporary copy is best effort.
                }
            } catch (Exception ex) {
                errors.Add(ex);
            }
        }
        await CloseBrowserAsync().ConfigureAwait(false);
        if (!_playwrightDisposed && Playwright != null) {
            try {
                Playwright.Dispose();
                _playwrightDisposed = true;
            } catch (Exception ex) {
                errors.Add(ex);
            }
        }
        if (errors.Count == 1) {
            ExceptionDispatchInfo.Capture(errors[0]).Throw();
        }
        if (errors.Count > 1) {
            throw new AggregateException("Browser session cleanup failed.", errors);
        }

        async Task CloseBrowserAsync() {
            if (!browserAttempted && !_browserClosed && _closeBrowserOnDispose && Browser != null) {
                browserAttempted = true;
                try {
                    await Browser.CloseAsync().ConfigureAwait(false);
                    _browserClosed = true;
                } catch (Exception ex) {
                    errors.Add(ex);
                }
            }
        }
    }
}
