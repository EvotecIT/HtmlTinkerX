using Microsoft.Playwright;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlBrowser {
    private static readonly SemaphoreSlim DriverStartSemaphore = new(1, 1);
    private static volatile string? BrowserRootDuringDriverStart;
    private static volatile string? ManagedRootDuringDriverStart;
    private static readonly AsyncLocal<Action<string>?> CacheLaunchStarting = new();

    // A scoped process-boundary seam reproduces a cache deletion after SDK startup without timing races.
    internal static IDisposable OverrideCacheLaunchStarting(Action<string> callback) {
        Action<string>? previous = CacheLaunchStarting.Value;
        CacheLaunchStarting.Value = callback;
        return new CacheLaunchScope(() => CacheLaunchStarting.Value = previous);
    }

    private sealed class CacheLaunchScope : IDisposable {
        private Action? _restore;
        internal CacheLaunchScope(Action restore) => _restore = restore;
        public void Dispose() => Interlocked.Exchange(ref _restore, null)?.Invoke();
    }

    private static string SessionCacheDirectory(HtmlBrowserEngine engine, bool recording) => recording
        ? FindCacheSlot(engine, true) ?? FindCacheSlot(engine, false, compatibleAuxiliary: true)
            ?? throw new InvalidOperationException($"{engine} browser fallback has no FFmpeg revision compatible with the loaded driver. Repair the browser cache before recording video.")
        : GetBrowserRuntimeDirectory(engine);

    private static string GetManagedExecutable(HtmlBrowserEngine engine, bool headless, string slot) {
        string name = engine == HtmlBrowserEngine.Chromium && headless ? "chromium-headless-shell" : engine.ToString().ToLowerInvariant();
        return CacheFilePath(slot, ReadCacheManifest(slot).Runtimes.Single(runtime => runtime.Name == name).Executable);
    }

    /// <summary>The SDK reads browser paths from process environment when starting its driver. Serialize only
    /// driver startup, restore immediately, and never hold this gate for a download or browser session.</summary>
    private static async Task<IPlaywright> CreateCachePlaywrightAsync(HtmlBrowserLaunchOptions? options) {
        await DriverStartSemaphore.WaitAsync().ConfigureAwait(false);
        string? original = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        try {
            if (options != null && ShouldInstallBundledRuntime(options)) {
                ManagedRootDuringDriverStart = ManagedCacheRoot;
                BrowserRootDuringDriverStart = GetBrowserInstallPath();
                Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", SessionCacheDirectory(options.Browser, !string.IsNullOrWhiteSpace(options.VideoPath)));
            }
            return await Playwright.CreateAsync().ConfigureAwait(false);
        } finally {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", original);
            BrowserRootDuringDriverStart = null;
            ManagedRootDuringDriverStart = null;
            DriverStartSemaphore.Release();
        }
    }

    internal static bool IsCacheLaunchFailure(string message) =>
        string.Equals(message, "Process exited", StringComparison.Ordinal) ||
        message.IndexOf("Invalid file descriptor to ICU data", StringComparison.OrdinalIgnoreCase) >= 0 ||
        message.IndexOf("exitCode=255", StringComparison.OrdinalIgnoreCase) >= 0 ||
        message.IndexOf("exit code 255", StringComparison.OrdinalIgnoreCase) >= 0 ||
        message.IndexOf("Executable doesn't exist", StringComparison.OrdinalIgnoreCase) >= 0 ||
        message.IndexOf("ENOENT", StringComparison.OrdinalIgnoreCase) >= 0;

    internal sealed class BrowserCacheRepairedException : Exception {
        internal BrowserCacheRepairedException(Exception cause) : base("Browser cache repaired; restart the driver before retrying launch.", cause) { }
    }

    internal static async Task<T> LaunchWithCacheRepairAsync<T>(HtmlBrowserLaunchOptions options, Func<string?, Task<T>> launch, bool allowRepair = true) {
        bool managed = PlaywrightFactory == null && ShouldInstallBundledRuntime(options);
        string? slot = managed ? SessionCacheDirectory(options.Browser, !string.IsNullOrWhiteSpace(options.VideoPath)) : null;
        if (slot != null) CacheLaunchStarting.Value?.Invoke(slot);
        try { return await launch(slot).ConfigureAwait(false); }
        catch (PlaywrightException ex) when (allowRepair && managed && IsCacheLaunchFailure(ex.Message)) {
            if (!await RepairFailedCacheLaunchAsync(options, slot, ex, allowRepair).ConfigureAwait(false)) throw;
            // The SDK captures FFmpeg's registry root at driver startup. Restart the complete launch owner.
            throw new BrowserCacheRepairedException(ex);
        }
    }

    private static async Task<bool> RepairFailedCacheLaunchAsync(HtmlBrowserLaunchOptions options, string? slot, PlaywrightException failure, bool allowRepair) {
        // Exit 255, ENOENT and a closed driver can also describe a rejected profile, argument or host setup.
        if (!allowRepair || PlaywrightFactory != null || !ShouldInstallBundledRuntime(options) || !IsCacheLaunchFailure(failure.Message)
            || slot == null || VerifyCacheSlot(slot, true, out _)) return false;
        Console.Error.WriteLine($"{options.Browser} cache launch failed ({failure.Message}). Repairing the browser cache before retrying.");
        await MarkCacheLaunchFailureAsync(slot, failure.Message).ConfigureAwait(false);
        await RepairInstallationAsync(options.Browser).ConfigureAwait(false);
        return true;
    }
}
