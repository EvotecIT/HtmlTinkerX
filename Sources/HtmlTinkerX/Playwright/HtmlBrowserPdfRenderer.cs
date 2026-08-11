namespace HtmlTinkerX;

using Microsoft.Playwright;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Production browser-backed HTML-to-PDF renderer with bounded leasing, warm Chromium reuse,
/// isolated per-render contexts, recycling, cancellation, and lifecycle metrics.
/// </summary>
public sealed partial class HtmlBrowserPdfRenderer : IAsyncDisposable {
    private readonly HtmlBrowserPdfRendererOptions _options;
    private readonly HtmlBrowserNetworkPolicyEvaluator _networkPolicy;
    private readonly SemaphoreSlim _admissionGate;
    private readonly SemaphoreSlim _leaseGate;
    private readonly SemaphoreSlim _poolMutation = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly object _lifecycleSync = new();
    private readonly ConcurrentQueue<BrowserSlot> _available = new();
    private readonly ConcurrentDictionary<long, BrowserSlot> _slots = new();
    private long _nextBrowserId;
    private long _accepted;
    private long _succeeded;
    private long _failed;
    private long _cancelled;
    private long _rejected;
    private long _retries;
    private long _created;
    private long _recycled;
    private int _active;
    private int _queued;
    private int _operations;
    private TaskCompletionSource<bool>? _drained;
    private int _disposed;

    /// <summary>Initializes a pooled Chromium PDF renderer.</summary>
    public HtmlBrowserPdfRenderer(HtmlBrowserPdfRendererOptions? options = null) {
        _options = options ?? new HtmlBrowserPdfRendererOptions();
        _networkPolicy = new HtmlBrowserNetworkPolicyEvaluator(_options.NetworkPolicy);
        if (!_options.NetworkPolicy.AllowPrivateNetworks && !string.IsNullOrWhiteSpace(_options.Proxy)) {
            throw new ArgumentException("A caller-supplied proxy cannot be combined with public-network-only enforcement because HtmlTinkerX cannot bind the proxy's DNS decision to the remote socket. Enable private-network access explicitly when the trusted proxy owns that boundary.", nameof(options));
        }
        if (!string.IsNullOrWhiteSpace(_options.Proxy)
            && (_options.NetworkPolicy.AllowedHosts.Count > 0 || _options.NetworkPolicy.DeniedHosts.Count > 0)) {
            throw new ArgumentException("A caller-supplied proxy cannot be combined with allowed or denied host rules because HtmlTinkerX cannot enforce those rules for WebSocket tunnels through that proxy.", nameof(options));
        }
        _admissionGate = new SemaphoreSlim(
            _options.MaximumBrowserInstances + _options.MaximumQueuedCaptures,
            _options.MaximumBrowserInstances + _options.MaximumQueuedCaptures);
        _leaseGate = new SemaphoreSlim(_options.MaximumBrowserInstances, _options.MaximumBrowserInstances);
    }

    /// <summary>Gets the immutable renderer configuration.</summary>
    public HtmlBrowserPdfRendererOptions Options => _options;

    /// <summary>Starts the configured minimum number of Chromium processes.</summary>
    public async Task PreWarmAsync(CancellationToken cancellationToken = default) {
        BeginOperation();
        using CancellationTokenSource operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeCancellation.Token);
        try {
            CancellationToken operationToken = operationCancellation.Token;
            await _poolMutation.WaitAsync(operationToken).ConfigureAwait(false);
            try {
                await RecycleUnavailableIdleSlotsAsync().ConfigureAwait(false);
                while (_slots.Count < _options.MinimumBrowserInstances) {
                    operationToken.ThrowIfCancellationRequested();
                    using CancellationTokenSource setupDeadline = CancellationTokenSource.CreateLinkedTokenSource(operationToken);
                    setupDeadline.CancelAfter(_options.SetupTimeout);
                    BrowserSlot slot;
                    try {
                        slot = await CreateSlotAsync(setupDeadline.Token).ConfigureAwait(false);
                    } catch (OperationCanceledException) when (!operationToken.IsCancellationRequested && setupDeadline.IsCancellationRequested) {
                        throw new TimeoutException($"Browser prewarm setup did not complete within {_options.SetupTimeout.TotalMilliseconds:0} ms.");
                    }
                    _available.Enqueue(slot);
                }
            } finally {
                _poolMutation.Release();
            }
        } finally {
            EndOperation();
        }
    }

    private async Task RecycleUnavailableIdleSlotsAsync() {
        int candidates = _available.Count;
        for (int index = 0; index < candidates && _available.TryDequeue(out BrowserSlot? slot); index++) {
            if (ShouldRecycle(slot)) {
                await RecycleSlotAsync(slot).ConfigureAwait(false);
            } else {
                _available.Enqueue(slot);
            }
        }
    }

    /// <summary>Returns a point-in-time lifecycle metrics snapshot.</summary>
    public HtmlBrowserPdfRendererMetrics GetMetricsSnapshot() => new(
        Interlocked.Read(ref _accepted),
        Interlocked.Read(ref _succeeded),
        Interlocked.Read(ref _failed),
        Interlocked.Read(ref _cancelled),
        Interlocked.Read(ref _rejected),
        Interlocked.Read(ref _retries),
        Interlocked.Read(ref _created),
        Interlocked.Read(ref _recycled),
        Volatile.Read(ref _active),
        Volatile.Read(ref _queued),
        CountUsableIdleSlots());

    private int CountUsableIdleSlots() {
        int count = 0;
        foreach (BrowserSlot slot in _available) {
            if (!ShouldRecycle(slot)) count++;
        }
        return count;
    }

    private async Task<BrowserSlot> RentSlotAsync(CancellationToken cancellationToken) {
        while (_available.TryDequeue(out BrowserSlot? available)) {
            if (!ShouldRecycle(available)) {
                return available;
            }
            await RecycleSlotAsync(available).ConfigureAwait(false);
        }

        await _poolMutation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            while (_available.TryDequeue(out BrowserSlot? available)) {
                if (!ShouldRecycle(available)) {
                    return available;
                }
                await RecycleSlotAsync(available).ConfigureAwait(false);
            }

            return await CreateSlotAsync(cancellationToken).ConfigureAwait(false);
        } finally {
            _poolMutation.Release();
        }
    }

    private async Task<BrowserSlot> CreateSlotAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        HtmlBrowserPolicyProxy? policyProxy = null;
        HtmlBrowserLaunchOptions launchOptions = _options.CreateLaunchOptions();
        if (_options.RequiresManagedPolicyProxy) {
            policyProxy = new HtmlBrowserPolicyProxy(_options.NetworkPolicy);
            launchOptions.Proxy = policyProxy.Server;
        }
        IPlaywright playwright;
        IBrowser browser;
        try {
            (playwright, browser) = await LaunchBrowserWithCancellationAsync(launchOptions, cancellationToken).ConfigureAwait(false);
        } catch {
            if (policyProxy != null) await policyProxy.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        long id = Interlocked.Increment(ref _nextBrowserId);
        BrowserSlot slot = new(id, playwright, browser, policyProxy);
        browser.Disconnected += (_, _) => slot.MarkBroken();
        _slots[id] = slot;
        Interlocked.Increment(ref _created);
        return slot;
    }

    private static async Task<(IPlaywright Playwright, IBrowser Browser)> LaunchBrowserWithCancellationAsync(
        HtmlBrowserLaunchOptions launchOptions,
        CancellationToken cancellationToken) {
        Task<(IPlaywright Playwright, IBrowser Browser)> launch = HtmlBrowser.LaunchBrowserAsync(launchOptions, cancellationToken);
        if (!cancellationToken.CanBeCanceled || launch.IsCompleted) return await launch.ConfigureAwait(false);

        TaskCompletionSource<bool> cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = cancellationToken.Register(
            static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true),
            cancelled);
        if (await Task.WhenAny(launch, cancelled.Task).ConfigureAwait(false) != launch) {
            _ = launch.ContinueWith(
                static async completed => {
                    if (completed.Status == TaskStatus.RanToCompletion) {
                        (IPlaywright playwright, IBrowser browser) = completed.Result;
                        try { if (browser.IsConnected) await browser.CloseAsync().ConfigureAwait(false); } catch (PlaywrightException) { }
                        playwright.Dispose();
                    } else if (completed.IsFaulted) {
                        _ = completed.Exception;
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default).Unwrap();
            cancellationToken.ThrowIfCancellationRequested();
        }
        return await launch.ConfigureAwait(false);
    }

    private async Task ReturnSlotAsync(BrowserSlot slot) {
        slot.RecordRender();
        if (Volatile.Read(ref _disposed) != 0 || ShouldRecycle(slot)) {
            await RecycleSlotAsync(slot).ConfigureAwait(false);
        } else {
            _available.Enqueue(slot);
        }
    }

    private bool ShouldRecycle(BrowserSlot slot) =>
        slot.IsBroken
        || !slot.Browser.IsConnected
        || slot.RenderCount >= _options.MaximumRendersPerBrowser
        || DateTimeOffset.UtcNow - slot.CreatedAt >= _options.MaximumBrowserAge;

    private async Task RecycleSlotAsync(BrowserSlot slot) {
        if (!_slots.TryRemove(slot.Id, out _)) {
            return;
        }

        Interlocked.Increment(ref _recycled);
        await slot.DisposeAsync().ConfigureAwait(false);
    }

    private void ThrowIfDisposed() {
        if (Volatile.Read(ref _disposed) != 0) {
            throw new ObjectDisposedException(nameof(HtmlBrowserPdfRenderer));
        }
    }

    private void BeginOperation() {
        lock (_lifecycleSync) {
            ThrowIfDisposed();
            if (_operations++ == 0) {
                _drained = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }

    private void EndOperation() {
        TaskCompletionSource<bool>? drained = null;
        lock (_lifecycleSync) {
            if (--_operations == 0) {
                drained = _drained;
                _drained = null;
            }
        }
        drained?.TrySetResult(true);
    }

    /// <summary>Stops accepting captures, drains active leases, and closes all warm browsers.</summary>
    public async ValueTask DisposeAsync() {
        Task drain;
        lock (_lifecycleSync) {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            drain = _operations == 0 ? Task.CompletedTask : _drained!.Task;
        }

        _lifetimeCancellation.Cancel();
        await drain.ConfigureAwait(false);

        foreach (BrowserSlot slot in _slots.Values) {
            await RecycleSlotAsync(slot).ConfigureAwait(false);
        }

        _lifetimeCancellation.Dispose();
        _poolMutation.Dispose();
        _leaseGate.Dispose();
        _admissionGate.Dispose();
    }

    private sealed class BrowserSlot : IAsyncDisposable {
        private int _broken;
        private int _renderCount;
        private int _disposed;
        private int _playwrightDisposed;

        internal BrowserSlot(long id, IPlaywright playwright, IBrowser browser, HtmlBrowserPolicyProxy? policyProxy) {
            Id = id;
            Playwright = playwright;
            Browser = browser;
            PolicyProxy = policyProxy;
            CreatedAt = DateTimeOffset.UtcNow;
        }

        internal long Id { get; }
        internal IPlaywright Playwright { get; }
        internal IBrowser Browser { get; }
        internal HtmlBrowserPolicyProxy? PolicyProxy { get; }
        internal DateTimeOffset CreatedAt { get; }
        internal int RenderCount => Volatile.Read(ref _renderCount);
        internal bool IsBroken => Volatile.Read(ref _broken) != 0;
        internal void MarkBroken() => Interlocked.Exchange(ref _broken, 1);
        internal void RecordRender() => Interlocked.Increment(ref _renderCount);
        internal void DisposePlaywright() {
            if (Interlocked.Exchange(ref _playwrightDisposed, 1) == 0) Playwright.Dispose();
        }

        public async ValueTask DisposeAsync() {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            try {
                if (Browser.IsConnected) {
                    Task close = Browser.CloseAsync();
                    if (await Task.WhenAny(close, Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(false) == close) {
                        await close.ConfigureAwait(false);
                    } else {
                        _ = close.ContinueWith(static completed => _ = completed.Exception, TaskContinuationOptions.OnlyOnFaulted);
                    }
                }
            } catch (Exception) {
                // A crashed browser is already closed from the renderer's perspective.
            } finally {
                DisposePlaywright();
                if (PolicyProxy != null) await PolicyProxy.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static TimeSpan StopwatchElapsed(long started) =>
        TimeSpan.FromSeconds((Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency);
}
