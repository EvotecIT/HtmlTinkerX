namespace HtmlTinkerX;

using Microsoft.Playwright;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Attaches streaming header interception before newly opened popups navigate.</summary>
internal sealed class HtmlBrowserPopupHeaderCoordinator : IAsyncDisposable {
    private static readonly TimeSpan DefaultCleanupTimeout = TimeSpan.FromSeconds(2);
    private const string NavigationShimResource = "HtmlTinkerX.Playwright.Scripts.HtmlBrowserPopupNavigation.js";
    private const string ReleasePropertyPlaceholder = "__HTMLTINKERX_POPUP_RELEASE_PROPERTY__";
    private static readonly Lazy<string> NavigationShim = new(LoadNavigationShim, LazyThreadSafetyMode.ExecutionAndPublication);
    private readonly IBrowserContext _context;
    private readonly IPage _primaryPage;
    private readonly Uri? _origin;
    private readonly IReadOnlyDictionary<string, string> _captureHeaders;
    private readonly CancellationToken _cancellationToken;
    private readonly ConcurrentDictionary<IPage, HtmlBrowserScopedHeaderInterceptor> _interceptors = new();
    private readonly ConcurrentDictionary<Task, byte> _pending = new();
    private readonly EventHandler<IPage> _pageHandler;
    private readonly Action _cleanupTimedOut;
    private readonly TimeSpan _cleanupTimeout;
    private readonly Func<string, Task<bool>>? _requestAllowed;
    private readonly Action<string, bool>? _requestBlocked;
    private readonly string _releasePropertyName;
    private readonly string _navigationShim;
    private Exception? _failure;
    private int _disposed;

    internal HtmlBrowserPopupHeaderCoordinator(
        IBrowserContext context,
        IPage primaryPage,
        Uri? origin,
        IReadOnlyDictionary<string, string> captureHeaders,
        CancellationToken cancellationToken,
        Action cleanupTimedOut,
        TimeSpan? cleanupTimeout = null,
        Func<string, Task<bool>>? requestAllowed = null,
        Action<string, bool>? requestBlocked = null) {
        _context = context;
        _primaryPage = primaryPage;
        _origin = origin;
        _captureHeaders = captureHeaders;
        _cancellationToken = cancellationToken;
        _cleanupTimedOut = cleanupTimedOut;
        _cleanupTimeout = cleanupTimeout ?? DefaultCleanupTimeout;
        _requestAllowed = requestAllowed;
        _requestBlocked = requestBlocked;
        _releasePropertyName = Guid.NewGuid().ToString("N");
        _navigationShim = NavigationShim.Value.Replace(ReleasePropertyPlaceholder, _releasePropertyName);
        _pageHandler = OnPage;
        context.Page += _pageHandler;
    }

    internal Task AddNavigationShimAsync(IPage page) => page.AddInitScriptAsync(_navigationShim);

    private static string LoadNavigationShim() {
        Assembly assembly = typeof(HtmlBrowserPopupHeaderCoordinator).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(NavigationShimResource)
            ?? throw new InvalidOperationException($"Embedded browser script '{NavigationShimResource}' was not found.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    private void OnPage(object? sender, IPage page) {
        if (page == _primaryPage || Volatile.Read(ref _disposed) != 0) return;
        Task pending = AttachAsync(page);
        _pending[pending] = 0;
        _ = pending.ContinueWith(
            completed => {
                _pending.TryRemove(completed, out _);
                if (!completed.IsFaulted || Volatile.Read(ref _disposed) != 0) return;
                Interlocked.CompareExchange(ref _failure, completed.Exception!.GetBaseException(), null);
                _cleanupTimedOut();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task AttachAsync(IPage page) {
        HtmlBrowserScopedHeaderInterceptor interceptor = await HtmlBrowserScopedHeaderInterceptor.CreateAsync(
            _context,
            page,
            _origin,
            _captureHeaders,
            _cancellationToken,
            _cleanupTimedOut,
            _cleanupTimeout,
            _requestAllowed,
            _requestBlocked).ConfigureAwait(false);
        if (!_interceptors.TryAdd(page, interceptor)) {
            await interceptor.DisposeAsync().ConfigureAwait(false);
            return;
        }
        try {
            // Every attached popup can open another popup. Install the same staging shim
            // before releasing this page so nested navigation cannot outrun interception.
            await AddNavigationShimAsync(page).ConfigureAwait(false);
            await page.EvaluateAsync(@"propertyName => {
                const release = globalThis[propertyName];
                if (typeof release !== 'function') return;
                delete globalThis[propertyName];
                release();
            }", _releasePropertyName).ConfigureAwait(false);
        } catch (PlaywrightException) when (page.IsClosed || !string.Equals(page.Url, "about:blank", StringComparison.OrdinalIgnoreCase)) {
            // A caller can close the popup or navigate it before the release handshake finishes.
        }
    }

    internal void ThrowIfFaulted() {
        Exception? failure = Volatile.Read(ref _failure);
        if (failure != null) {
            throw new InvalidOperationException("Popup header interception failed before capture completed.", failure);
        }
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _context.Page -= _pageHandler;
        Task cleanup = DisposeCoreAsync();
        if (await Task.WhenAny(cleanup, Task.Delay(_cleanupTimeout)).ConfigureAwait(false) == cleanup) {
            await cleanup.ConfigureAwait(false);
            return;
        }
        _cleanupTimedOut();
        _ = cleanup.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task DisposeCoreAsync() {
        Task[] pending = _pending.Keys.ToArray();
        if (pending.Length > 0) {
            try { await Task.WhenAll(pending).ConfigureAwait(false); } catch (Exception) { }
        }
        foreach (HtmlBrowserScopedHeaderInterceptor interceptor in _interceptors.Values) {
            await interceptor.DisposeAsync().ConfigureAwait(false);
        }
        _interceptors.Clear();
    }
}
