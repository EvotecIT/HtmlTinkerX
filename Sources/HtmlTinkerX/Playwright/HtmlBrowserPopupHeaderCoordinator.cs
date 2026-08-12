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
    private const string AttributeGuardsResource = "HtmlTinkerX.Playwright.Scripts.HtmlBrowserPopupAttributeGuards.js";
    private const string AnimatedAttributeGuardsResource = "HtmlTinkerX.Playwright.Scripts.HtmlBrowserPopupAnimatedAttributeGuards.js";
    private const string ContextRegistryResource = "HtmlTinkerX.Playwright.Scripts.HtmlBrowserPopupContextRegistry.js";
    private const string CacheGuardsResource = "HtmlTinkerX.Playwright.Scripts.HtmlBrowserPopupCacheGuards.js";
    private const string FrameGuardsResource = "HtmlTinkerX.Playwright.Scripts.HtmlBrowserPopupFrameGuards.js";
    private const string CodeGuardsResource = "HtmlTinkerX.Playwright.Scripts.HtmlBrowserPopupCodeGuards.js";
    private const string DomGuardsResource = "HtmlTinkerX.Playwright.Scripts.HtmlBrowserPopupDomGuards.js";
    private const string AsyncConstructorsResource = "HtmlTinkerX.Playwright.Scripts.HtmlBrowserPopupAsyncConstructors.js";
    private const string MarkupGuardsResource = "HtmlTinkerX.Playwright.Scripts.HtmlBrowserPopupMarkupGuards.js";
    private const string RealmGuardsResource = "HtmlTinkerX.Playwright.Scripts.HtmlBrowserPopupRealmGuards.js";
    private const string TransportGuardsResource = "HtmlTinkerX.Playwright.Scripts.HtmlBrowserPopupTransportGuards.js";
    private const string XhrStagingResource = "HtmlTinkerX.Playwright.Scripts.HtmlBrowserPopupXhrStaging.js";
    private const string ResourceQueueResource = "HtmlTinkerX.Playwright.Scripts.HtmlBrowserPopupResourceQueue.js";
    private const string NavigationShimResource = "HtmlTinkerX.Playwright.Scripts.HtmlBrowserPopupNavigation.js";
    private const string ReleasePropertyPlaceholder = "__HTMLTINKERX_POPUP_RELEASE_PROPERTY__";
    private const string ReleaseTokenPlaceholder = "__HTMLTINKERX_POPUP_RELEASE_TOKEN__";
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
    private readonly string _releaseToken;
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
        _releaseToken = Guid.NewGuid().ToString("N");
        _navigationShim = NavigationShim.Value
            .Replace(ReleasePropertyPlaceholder, _releasePropertyName)
            .Replace(ReleaseTokenPlaceholder, _releaseToken);
        _pageHandler = OnPage;
        context.Page += _pageHandler;
    }

    internal Task AddNavigationShimAsync(IPage page) => page.AddInitScriptAsync(_navigationShim);

    private static string LoadNavigationShim() {
        Assembly assembly = typeof(HtmlBrowserPopupHeaderCoordinator).Assembly;
        return $"{LoadEmbeddedScript(assembly, ContextRegistryResource)}\n{LoadEmbeddedScript(assembly, CacheGuardsResource)}\n{LoadEmbeddedScript(assembly, AttributeGuardsResource)}\n{LoadEmbeddedScript(assembly, AnimatedAttributeGuardsResource)}\n{LoadEmbeddedScript(assembly, FrameGuardsResource)}\n{LoadEmbeddedScript(assembly, CodeGuardsResource)}\n{LoadEmbeddedScript(assembly, DomGuardsResource)}\n{LoadEmbeddedScript(assembly, AsyncConstructorsResource)}\n{LoadEmbeddedScript(assembly, MarkupGuardsResource)}\n{LoadEmbeddedScript(assembly, RealmGuardsResource)}\n{LoadEmbeddedScript(assembly, TransportGuardsResource)}\n{LoadEmbeddedScript(assembly, XhrStagingResource)}\n{LoadEmbeddedScript(assembly, ResourceQueueResource)}\n{LoadEmbeddedScript(assembly, NavigationShimResource)}";
    }

    private static string LoadEmbeddedScript(Assembly assembly, string resourceName) {
        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded browser script '{resourceName}' was not found.");
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
                if (!completed.IsFaulted) return;
                Interlocked.CompareExchange(ref _failure, completed.Exception!.GetBaseException(), null);
                _cleanupTimedOut();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task AttachAsync(IPage page) {
        HtmlBrowserScopedHeaderInterceptor interceptor;
        try {
            interceptor = await HtmlBrowserScopedHeaderInterceptor.CreateAsync(
                _context,
                page,
                _origin,
                _captureHeaders,
                _cancellationToken,
                _cleanupTimedOut,
                _cleanupTimeout,
                _requestAllowed,
                _requestBlocked).ConfigureAwait(false);
        } catch (Exception) when (page.IsClosed) {
            return;
        }
        if (!_interceptors.TryAdd(page, interceptor)) {
            await interceptor.DisposeAsync().ConfigureAwait(false);
            return;
        }
        try {
            // Future popup documents inherit the complete shim. The opener-side blank-popup
            // facade already installs the nested window.open route in the current realm.
            await AddNavigationShimAsync(page).ConfigureAwait(false);
            await ReleasePopupAsync(page).ConfigureAwait(false);
        } catch (PlaywrightException) when (page.IsClosed || !string.Equals(page.Url, "about:blank", StringComparison.OrdinalIgnoreCase)) {
            // A caller can close the popup or navigate it before the release handshake finishes.
        }
    }

    private async Task ReleasePopupAsync(IPage page) {
        await page.EvaluateAsync(@"release => {
            globalThis[release.propertyName] = release.token;
        }", new { propertyName = _releasePropertyName, token = _releaseToken }).ConfigureAwait(false);
        while (!page.IsClosed && string.Equals(page.Url, "about:blank", StringComparison.OrdinalIgnoreCase)) {
            bool completed;
            try {
                completed = await page.EvaluateAsync<bool>(
                    "release => globalThis[release.propertyName] === release.token",
                    new { propertyName = _releasePropertyName, token = _releaseToken }).ConfigureAwait(false);
            } catch (PlaywrightException) {
                // The release callback can navigate and destroy this one-time blank realm
                // between the setter and acknowledgement probe.
                return;
            }
            if (completed) return;
            await Task.Delay(10, _cancellationToken).ConfigureAwait(false);
        }
    }

    internal void ThrowIfFaulted() {
        Exception? failure = Volatile.Read(ref _failure);
        if (failure != null) {
            throw new InvalidOperationException("Popup header interception failed before capture completed.", failure);
        }
    }

    /// <summary>Waits until popup interception and the opener-side release handshake are quiescent.</summary>
    internal async Task WaitForPendingAsync(CancellationToken cancellationToken) {
        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            Task[] pending = _pending.Keys.ToArray();
            if (pending.Length == 0) {
                await Task.Yield();
                if (_pending.IsEmpty) {
                    ThrowIfFaulted();
                    return;
                }
                continue;
            }
            Task completion = Task.WhenAll(pending);
            if (cancellationToken.CanBeCanceled && !completion.IsCompleted) {
                TaskCompletionSource<bool> cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
                using CancellationTokenRegistration registration = cancellationToken.Register(
                    static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true),
                    cancelled);
                if (await Task.WhenAny(completion, cancelled.Task).ConfigureAwait(false) != completion) {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            try { await completion.ConfigureAwait(false); } catch { }
            ThrowIfFaulted();
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
            foreach (Task completed in pending) {
                if (completed.IsFaulted) {
                    Interlocked.CompareExchange(ref _failure, completed.Exception!.GetBaseException(), null);
                }
            }
        }
        foreach (HtmlBrowserScopedHeaderInterceptor interceptor in _interceptors.Values) {
            await interceptor.DisposeAsync().ConfigureAwait(false);
        }
        _interceptors.Clear();
        ThrowIfFaulted();
    }
}
