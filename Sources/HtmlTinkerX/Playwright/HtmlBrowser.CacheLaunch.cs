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

    private static string SessionCacheDirectory(HtmlBrowserEngine engine, bool recording) => recording
        ? FindCacheSlot(engine, true) ?? FindCacheSlot(engine, false, compatibleAuxiliary: true)
            ?? throw new InvalidOperationException($"{engine} browser fallback has no FFmpeg revision compatible with the loaded driver. Repair the browser cache before recording video.")
        : GetBrowserRuntimeDirectory(engine);

    private static string GetManagedExecutable(HtmlBrowserEngine engine, bool headless, bool recording = false) {
        string slot = SessionCacheDirectory(engine, recording);
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
        message.IndexOf("Invalid file descriptor to ICU data", StringComparison.OrdinalIgnoreCase) >= 0 ||
        message.IndexOf("exitCode=255", StringComparison.OrdinalIgnoreCase) >= 0 ||
        message.IndexOf("exit code 255", StringComparison.OrdinalIgnoreCase) >= 0 ||
        message.IndexOf("Executable doesn't exist", StringComparison.OrdinalIgnoreCase) >= 0 ||
        message.IndexOf("ENOENT", StringComparison.OrdinalIgnoreCase) >= 0;

    private static async Task<T> LaunchWithCacheRepairAsync<T>(HtmlBrowserLaunchOptions options, Func<Task<T>> launch) {
        try { return await launch().ConfigureAwait(false); }
        catch (PlaywrightException ex) when (PlaywrightFactory == null && ShouldInstallBundledRuntime(options) && IsCacheLaunchFailure(ex.Message)) {
            string slot = GetBrowserRuntimeDirectory(options.Browser);
            Console.Error.WriteLine($"{options.Browser} cache launch failed ({ex.Message}). Repairing the browser cache before retrying.");
            File.WriteAllText(Path.Combine(slot, "launch-failed.txt"), ex.Message);
            await RepairInstallationAsync(options.Browser).ConfigureAwait(false);
            return await launch().ConfigureAwait(false);
        }
    }
}
