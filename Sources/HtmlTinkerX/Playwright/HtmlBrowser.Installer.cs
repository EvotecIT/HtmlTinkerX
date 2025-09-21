using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Linq;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for retrieving HTML content using a headless browser.
/// </summary>
public static partial class HtmlBrowser {
    private const int DownloadBufferSize = 81920; // 80 KiB default HttpClient buffer size
    private static readonly TimeSpan InstallLockTimeout = TimeSpan.FromMinutes(10);

    internal static Action<string>? Logger { get; set; }
    private static void LogInfo(string message) { try { Logger?.Invoke(message); } catch { } }
    private static void LogError(string message, Exception? ex = null) { try { Logger?.Invoke(ex is null ? message : message + ": " + ex.Message); } catch { } }
    /// <summary>
    /// Semaphore to ensure thread-safe Playwright installation.
    /// Only one installation process can run at a time across all threads.
    /// </summary>
    private static readonly SemaphoreSlim InstallationSemaphore = new SemaphoreSlim(1, 1);

    /// <summary>
    /// Delegate used to execute Playwright CLI commands. Exposed for unit testing.
    /// </summary>
    internal static Action<string[]> PlaywrightInstaller { get; set; } = static args => Microsoft.Playwright.Program.Main(args);
    /// <summary>
    /// Gets the version of the Playwright driver. Prefer InformationalVersion so pre-release channels are honored.
    /// </summary>
    private static string DriverVersion {
        get {
            var asm = typeof(Microsoft.Playwright.Playwright).Assembly;
            string? info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrEmpty(info)) {
                var trimmed = info!.Split('+')[0];
                return trimmed;
            }
            return asm.GetName().Version?.ToString(3) ?? "1.52.0";
        }
    }

    /// <summary>
    /// Gets the platform identifier for the Playwright driver.
    /// </summary>
    private static string PlatformId => CurrentPlatform.ToPlatformId();

    /// <summary>
    /// Gets the current platform value.
    /// </summary>
    private static HtmlPlatform CurrentPlatform => PlatformExtensions.GetCurrentPlatform();

    /// <summary>
    /// Gets the platform identifier for downloading the Playwright driver.
    /// </summary>
    private static string DownloadPlatformId => CurrentPlatform.ToDownloadPlatformId();

    private static string NodeExecutable => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node";

    /// <summary>
    /// Gets the root directory for the Playwright driver installation.
    /// This directory is determined based on environment variables and platform-specific paths.
    /// It is used to store the Playwright driver executable and other related files.
    /// </summary>
    private static string GetDriverRoot() {
        string? envRoot = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");
        if (!string.IsNullOrEmpty(envRoot)) {
            if (envRoot.EndsWith(".playwright")) {
                envRoot = Path.GetDirectoryName(envRoot) ?? envRoot;
            }
            return Path.GetFullPath(envRoot);
        }

        string? browsersPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        if (browsersPath == "0") {
            string baseDir = Path.GetDirectoryName(typeof(HtmlBrowser).Assembly.Location) ?? AppContext.BaseDirectory;
            return Path.Combine(baseDir, "ms-playwright-driver");
        }

        if (!string.IsNullOrEmpty(browsersPath)) {
            string parent = Path.GetDirectoryName(Path.GetFullPath(browsersPath)) ?? Path.GetFullPath(browsersPath);
            return Path.Combine(parent, "ms-playwright-driver");
        }

        string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "ms-playwright-driver");
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            return Path.Combine(user, "Library", "Caches", "ms-playwright-driver");
        }
        return Path.Combine(user, ".cache", "ms-playwright-driver");
    }

    /// <summary>
    /// Gets the path to the Playwright driver installation directory.
    /// This directory contains the Playwright driver executable and other related files.
    /// </summary>
    private static string GetDriverPath() => Path.Combine(GetDriverRoot(), ".playwright");

    /// <summary>
    /// Gets the path to the Playwright driver version file.
    /// This file contains the version of the Playwright driver that is currently installed.
    /// </summary>
    private static string VersionFile => Path.Combine(GetDriverPath(), ".version");

    /// <summary>
    /// Checks if the Playwright driver is already installed.
    /// </summary>
    /// <returns></returns>
    private static bool IsDriverPresent() {
        string baseDir = GetDriverPath();
        string nodePath = Path.Combine(baseDir, "node", PlatformId, NodeExecutable);
        string packageDir = Path.Combine(baseDir, "package");
        if (!File.Exists(nodePath) || !Directory.Exists(packageDir))
            return false;
        if (!File.Exists(VersionFile))
            return false;
        string version = File.ReadAllText(VersionFile).Trim();
        return version == DriverVersion;
    }

    /// <summary>
    /// Heuristic for broken/partial driver installs (e.g. missing CLI entrypoint).
    /// </summary>
    private static bool IsDriverComplete() {
        try {
            var baseDir = GetDriverPath();
            if (!Directory.Exists(baseDir)) return false;
            var nodeOk = File.Exists(Path.Combine(baseDir, "node", PlatformId, NodeExecutable));
            var packageDir = Path.Combine(baseDir, "package");
            if (!Directory.Exists(packageDir)) return false;
            // Look for any cli.js under the package folder.
            var anyCli = Directory.EnumerateFiles(packageDir, "cli.js", SearchOption.AllDirectories).Any();
            var pkgJson = File.Exists(Path.Combine(packageDir, "package.json"));
            return nodeOk && (anyCli || pkgJson);
        } catch { return false; }
    }


    /// <summary>
    /// Removes the Playwright driver installation directory.
    /// This is typically called when the application is being uninstalled or when
    /// the driver is no longer needed.
    /// </summary>
    internal static void CleanDriver() {
        string root = GetDriverRoot();
        if (Directory.Exists(root)) TryDeleteDirectory(root);
    }

    /// <summary>
    /// Ensures that the Playwright driver and browser runtime are installed.
    /// This method will automatically download and install the required components if they are not present.
    /// </summary>
    /// <param name="engine">The browser engine to ensure is installed.</param>
    /// <returns>A task that completes when the installation check/process is finished.</returns>
    public static async Task EnsureInstalledAsync(HtmlBrowserEngine engine) {
        // Unit-test shortcut: when a custom PlaywrightFactory is injected, skip installer + smoke.
        if (PlaywrightFactory != null) return;
        // Quick check – if browsers are present and a smoke launch works, we’re done
        if (IsBrowserRuntimeInstalled(engine) && await TrySmokeLaunchAsync(engine, CancellationToken.None).ConfigureAwait(false))
            return;

        // Cross-process file lock + in-process semaphore to avoid races among parallel runs
        using var fileLock = AcquireInstallFileLock();
        try {
            await InstallationSemaphore.WaitAsync().ConfigureAwait(false);
            try {
                // Ensure the browser runtime is installed (this also bootstraps the driver via Program.Main)
                bool withDeps = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && ShouldTryInstallDeps();
                await EnsureBrowsersAsync(engine, preferWithDeps: withDeps).ConfigureAwait(false);

                // Smoke launch to self-heal partial/broken installs (unless explicitly skipped)
                if (!SkipSmokeLaunch() && !await TrySmokeLaunchAsync(engine, CancellationToken.None).ConfigureAwait(false)) {
                    // Attempt repair: force driver + runtime reinstall, then re-smoke
                    CleanInstallDir();
                    await EnsureBrowsersAsync(engine, preferWithDeps: ShouldTryInstallDeps()).ConfigureAwait(false);

                    if (!SkipSmokeLaunch() && !await TrySmokeLaunchAsync(engine, CancellationToken.None).ConfigureAwait(false)) {
                        throw new InvalidOperationException("Playwright failed to launch after repair attempt. Please review environment and logs.");
                    }
                }
            } finally { InstallationSemaphore.Release(); }
        } finally { fileLock?.Dispose(); }
    }

    private sealed class FileLock : IDisposable {
        private readonly FileStream _stream;
        public FileLock(FileStream stream) { _stream = stream; }
        public void Dispose() { try { _stream.Dispose(); } catch { } }
    }

    private static IDisposable AcquireInstallFileLock() {
        string root = GetDriverRoot();
        Directory.CreateDirectory(root);
        string lockPath = Path.Combine(root, ".install.lock");
        var start = DateTime.UtcNow;
        while (true) {
            try {
                var fs = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                return new FileLock(fs);
            } catch (IOException) {
                if (DateTime.UtcNow - start > InstallLockTimeout) throw;
                Thread.Sleep(200);
            }
        }
    }

    private static bool ShouldTryInstallDeps() {
        var ci = (Environment.GetEnvironmentVariable("CI") ?? string.Empty).Equals("true", StringComparison.OrdinalIgnoreCase);
        var optIn = (Environment.GetEnvironmentVariable("HTMLINKERX_INSTALL_DEPS") ?? Environment.GetEnvironmentVariable("PLAYWRIGHT_INSTALL_DEPS") ?? string.Empty)
            .Equals("1", StringComparison.OrdinalIgnoreCase);
        return ci || optIn;
    }

    private static bool SkipSmokeLaunch() {
        var skip = (Environment.GetEnvironmentVariable("HTMLINKERX_SKIP_SMOKE") ?? string.Empty).Equals("1", StringComparison.OrdinalIgnoreCase);
        return skip;
    }

    private static async Task DownloadAndExtractDriverAsync() {
        string baseUrl = Environment.GetEnvironmentVariable("PLAYWRIGHT_DOWNLOAD_HOST")
            ?? Environment.GetEnvironmentVariable("HTMLINKERX_PLAYWRIGHT_HOST")
            ?? "https://playwright.azureedge.net";
        string urlBase = baseUrl.TrimEnd('/') + "/builds/driver";
        if (DriverVersion.Contains("-alpha") || DriverVersion.Contains("-beta") || DriverVersion.Contains("-next"))
            urlBase += "/next";
        string url = $"{urlBase}/playwright-{DriverVersion}-{DownloadPlatformId}.zip";

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1L;
        var buffer = new byte[DownloadBufferSize];
        string tempZip = Path.Combine(Path.GetTempPath(), "pwdriver_" + Guid.NewGuid().ToString("N") + ".zip");
        long read = 0; int lastProgress = 0; var sw = Stopwatch.StartNew();
        using (var inStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
        using (var outStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None, DownloadBufferSize, useAsync: true)) {
            while (true) {
                int n = await inStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                if (n == 0) break;
                await outStream.WriteAsync(buffer, 0, n).ConfigureAwait(false);
                if (total > 0) {
                    read += n;
                    int progress = (int)(read * 100 / total);
                    if (progress != lastProgress) {
                        double speed = read / 1024d / 1024d / Math.Max(sw.Elapsed.TotalSeconds, 0.1);
                        LogInfo($"Downloading Playwright driver... {progress}% ({speed:F1} MB/s)");
                        lastProgress = progress;
                    }
                }
            }
        }

        string baseDir = GetDriverPath();
        if (Directory.Exists(baseDir))
            Directory.Delete(baseDir, true);

        string tempDir = Path.Combine(Path.GetTempPath(), "pwdriver_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        using (var zipFs = new FileStream(tempZip, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var archive = new ZipArchive(zipFs, ZipArchiveMode.Read)) {
            ExtractZipSafely(archive, tempDir);
        }
        try { File.Delete(tempZip); } catch { }

        Directory.CreateDirectory(Path.Combine(baseDir, "node", PlatformId));

        string nodeDest = Path.Combine(baseDir, "node", PlatformId, NodeExecutable);
        string nodeSource = Path.Combine(tempDir, NodeExecutable);
        if (File.Exists(nodeDest))
            File.Delete(nodeDest);
        File.Move(nodeSource, nodeDest);
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            try {
                using var proc = Process.Start(new ProcessStartInfo {
                    FileName = "chmod",
                    Arguments = $"+x \"{nodeDest}\"",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                });
                proc?.WaitForExit(5000);
                if (proc is { ExitCode: not 0 }) LogError("chmod failed", null);
            } catch (Exception ex) { LogError("chmod threw", ex); }
        }
        string licenseDest = Path.Combine(baseDir, "node", "LICENSE");
        if (File.Exists(licenseDest))
            File.Delete(licenseDest);
        File.Move(Path.Combine(tempDir, "LICENSE"), licenseDest);

        string packageSrc = Path.Combine(tempDir, "package");
        string packageDest = Path.Combine(baseDir, "package");
        if (Directory.Exists(packageDest))
            Directory.Delete(packageDest, true);
        MoveDirectoryRobust(packageSrc, packageDest);
        TryDeleteDirectory(tempDir);

#if NETSTANDARD2_0 || NETFRAMEWORK
        File.WriteAllText(VersionFile, DriverVersion);
#else
        await File.WriteAllTextAsync(VersionFile, DriverVersion).ConfigureAwait(false);
#endif
        Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", GetDriverRoot());
    }

    private static void ExtractZipSafely(ZipArchive archive, string destinationDir) {
        string destFull = Path.GetFullPath(destinationDir) + Path.DirectorySeparatorChar;
        foreach (var entry in archive.Entries) {
            string targetPath = Path.GetFullPath(Path.Combine(destinationDir, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
            if (!targetPath.StartsWith(destFull, StringComparison.Ordinal)) {
                throw new InvalidOperationException("Zip entry outside target directory detected.");
            }
            if (string.IsNullOrEmpty(entry.Name)) {
                Directory.CreateDirectory(targetPath);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            entry.ExtractToFile(targetPath, overwrite: true);
        }
    }

    private static void MoveDirectoryRobust(string src, string dest) {
        try {
            Directory.Move(src, dest);
        } catch (Exception) {
            // Cross-volume or other move issues – fallback to copy
            CopyDirectory(src, dest);
        }
    }

    private static void CopyDirectory(string sourceDir, string destDir) {
        foreach (var dir in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories)) {
            var rel = dir.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Directory.CreateDirectory(Path.Combine(destDir, rel));
        }
        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories)) {
            var rel = file.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var target = Path.Combine(destDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    private static Task EnsureBrowsersAsync(HtmlBrowserEngine engine, bool preferWithDeps = false) {
        bool runtimeInstalled = IsBrowserRuntimeInstalled(engine);
        if (runtimeInstalled) return Task.CompletedTask;
        string runtime = engine.ToString().ToLowerInvariant();
        try {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && preferWithDeps) {
                PlaywrightInstaller(new[] { "install", "--with-deps", runtime });
            } else {
                PlaywrightInstaller(new[] { "install", runtime });
            }
        } catch (Exception ex) {
            LogError("Playwright install failed", ex);
            // Retry without deps or with deps as a fallback depending on first attempt
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                try {
                    if (preferWithDeps) {
                        PlaywrightInstaller(new[] { "install", runtime });
                    } else if (ShouldTryInstallDeps()) {
                        PlaywrightInstaller(new[] { "install", "--with-deps", runtime });
                    } else {
                        throw;
                    }
                } catch (Exception ex2) {
                    LogError("Playwright install retry failed", ex2);
                    throw;
                }
            } else throw;
        }
        return Task.CompletedTask;
    }

    private static async Task<bool> TrySmokeLaunchAsync(HtmlBrowserEngine engine, CancellationToken cancellationToken) {
        try {
            var playwright = PlaywrightFactory != null
                ? await PlaywrightFactory().ConfigureAwait(false)
                : await Microsoft.Playwright.Playwright.CreateAsync();

            Microsoft.Playwright.IBrowserType type = engine switch {
                HtmlBrowserEngine.Firefox => playwright.Firefox,
                HtmlBrowserEngine.WebKit => playwright.Webkit,
                _ => playwright.Chromium,
            };
            await using var browser = await type.LaunchAsync(new Microsoft.Playwright.BrowserTypeLaunchOptions { Headless = true });
            await browser.CloseAsync();
            return true;
        } catch (Exception) { return false; }
    }

    /// <summary>
    /// Gets the path where Playwright browsers are installed.
    /// </summary>
    /// <returns></returns>
    private static string GetBrowserInstallPath() {
        string? envDefined = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        if (envDefined == "0") {
            // Hermetic install: browsers live under the driver package's playwright-core directory
            string pkg = Path.Combine(GetDriverPath(), "package");
            string core = Path.Combine(pkg, "node_modules", "playwright-core", ".local-browsers");
            string fallback = Path.Combine(pkg, ".local-browsers");
            return Directory.Exists(core) || !Directory.Exists(fallback) ? core : fallback;
        }
        if (!string.IsNullOrEmpty(envDefined)) {
            return Path.GetFullPath(envDefined);
        }
        string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "ms-playwright");
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            return Path.Combine(user, "Library", "Caches", "ms-playwright");
        }
        return Path.Combine(user, ".cache", "ms-playwright");
    }

    private static bool IsBrowserRuntimeInstalled(HtmlBrowserEngine engine) {
        string path = GetBrowserInstallPath();
        if (!Directory.Exists(path))
            return false;
        string prefix = engine.ToString().ToLowerInvariant() + "-";
        foreach (string dir in Directory.GetDirectories(path)) {
            if (Path.GetFileName(dir).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Cleans the browser installation directory and removes the Playwright driver.
    /// </summary>
    private static void CleanInstallDir() {
        string path = GetBrowserInstallPath();
        if (Directory.Exists(path)) TryDeleteDirectory(path);
        CleanDriver();
    }

    private static void TryDeleteDirectory(string dir) {
        for (int i = 0; i < 5; i++) {
            try { Directory.Delete(dir, true); return; }
            catch { Thread.Sleep(200); }
        }
        try { Directory.Delete(dir, true); } catch (Exception ex) { LogError($"Failed to delete directory {dir}", ex); }
    }
}
