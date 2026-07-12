using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for retrieving HTML content using a headless browser.
/// </summary>
public static partial class HtmlBrowser {
    private const string PlaywrightWithDepsEnvVar = "HTMLTINKERX_PLAYWRIGHT_WITH_DEPS";
    private const long MaximumDriverArchiveBytes = 512L * 1024 * 1024;

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
    /// Factory used to create <see cref="HttpClient"/> instances. Exposed for unit testing.
    /// </summary>
    internal static Func<HttpClient> HttpClientFactory { get; set; } = DefaultHttpClientFactory;
    /// <summary>
    /// Gets the version of the Playwright driver.
    /// </summary>
    private static string DriverVersion => typeof(Microsoft.Playwright.Playwright)
        .Assembly.GetName().Version?.ToString(3) ?? "1.52.0";

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
    private static string GetDriverPath() {
        string? explicitRoot = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");
        if (string.IsNullOrWhiteSpace(explicitRoot)) {
            string bundledPath = GetBundledDriverPath();
            if (HasDriverLayout(bundledPath)) {
                return bundledPath;
            }
        }

        return Path.Combine(GetDriverRoot(), ".playwright");
    }

    private static string GetBundledDriverPath() {
        string assemblyDirectory = Path.GetDirectoryName(typeof(HtmlBrowser).Assembly.Location) ?? AppContext.BaseDirectory;
        string assemblyPath = Path.Combine(assemblyDirectory, ".playwright");
        if (HasDriverLayout(assemblyPath)) {
            return assemblyPath;
        }

#if NETFRAMEWORK
#pragma warning disable SYSLIB0012
        string? codeBase = typeof(HtmlBrowser).Assembly.CodeBase;
#pragma warning restore SYSLIB0012
        if (!string.IsNullOrWhiteSpace(codeBase) && Uri.TryCreate(codeBase, UriKind.Absolute, out Uri? assemblyUri) && assemblyUri.IsFile) {
            string? originalDirectory = Path.GetDirectoryName(assemblyUri.LocalPath);
            if (!string.IsNullOrWhiteSpace(originalDirectory)) {
                string originalPath = Path.Combine(originalDirectory, ".playwright");
                if (HasDriverLayout(originalPath)) {
                    return originalPath;
                }
            }
        }
#endif

        string appBasePath = Path.Combine(AppContext.BaseDirectory, ".playwright");
        return HasDriverLayout(appBasePath) ? appBasePath : assemblyPath;
    }

    internal static bool HasDriverLayout(string driverPath) {
        string nodePath = Path.Combine(driverPath, "node", PlatformId, NodeExecutable);
        string packagePath = Path.Combine(driverPath, "package");
        if (!File.Exists(nodePath) || !Directory.Exists(packagePath)) {
            return false;
        }

        try {
            return new FileInfo(nodePath).Length > 0 && Directory.EnumerateFileSystemEntries(packagePath).Any();
        } catch (IOException) {
            return false;
        } catch (UnauthorizedAccessException) {
            return false;
        }
    }

    private static bool IsBundledDriverPath(string driverPath) {
        return string.Equals(
            Path.GetFullPath(driverPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(GetBundledDriverPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

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
        if (!HasDriverLayout(baseDir))
            return false;
        if (IsBundledDriverPath(baseDir))
            return true;
        if (!File.Exists(VersionFile))
            return false;
        string version = File.ReadAllText(VersionFile).Trim();
        return version == DriverVersion;
    }

    /// <summary>
    /// Determines whether the driver installation directory is corrupted.
    /// </summary>
    private static bool IsDriverCorrupted() {
        string baseDir = GetDriverPath();
        if (!Directory.Exists(baseDir))
            return false;

        string nodePath = Path.Combine(baseDir, "node", PlatformId, NodeExecutable);
        if (!File.Exists(nodePath))
            return true;

        try {
            if (new FileInfo(nodePath).Length == 0)
                return true;
        } catch {
            return true;
        }

        string packageDir = Path.Combine(baseDir, "package");
        if (!Directory.Exists(packageDir))
            return true;

        try {
            if (!Directory.EnumerateFileSystemEntries(packageDir).Any())
                return true;
        } catch {
            return true;
        }

        if (!File.Exists(VersionFile))
            return true;

        try {
            string version = File.ReadAllText(VersionFile).Trim();
            return !string.Equals(version, DriverVersion, StringComparison.OrdinalIgnoreCase);
        } catch {
            return true;
        }
    }


    /// <summary>
    /// Removes the Playwright driver installation directory.
    /// This is typically called when the application is being uninstalled or when
    /// the driver is no longer needed.
    /// </summary>
    internal static void CleanDriver() {
        if (IsBundledDriverPath(GetDriverPath())) {
            return;
        }

        string root = GetDriverRoot();
        if (Directory.Exists(root)) {
            try {
                Directory.Delete(root, true);
            } catch {
                // ignore
            }
        }
    }

    /// <summary>
    /// Ensures that the Playwright driver and browser runtime are installed.
    /// This method will automatically download and install the required components if they are not present.
    /// </summary>
    /// <param name="engine">The browser engine to ensure is installed.</param>
    /// <returns>A task that completes when the installation check/process is finished.</returns>
    public static async Task EnsureInstalledAsync(HtmlBrowserEngine engine) {
        ValidateExistingInstallation(engine);

        // Fast path - check without lock first
        if (IsDriverPresent() && IsBrowserRuntimeInstalled(engine)) {
            EnsureDriverSearchPath();
            return;
        }

        await InstallationSemaphore.WaitAsync().ConfigureAwait(false);
        try {
            ValidateExistingInstallation(engine);

            if (IsDriverPresent() && IsBrowserRuntimeInstalled(engine)) {
                EnsureDriverSearchPath();
                return;
            }

            bool runtimeInstalled = IsBrowserRuntimeInstalled(engine);

            if (!IsDriverPresent()) {
                await DownloadAndInstallDriverAsync().ConfigureAwait(false);
            } else {
                EnsureDriverSearchPath();
            }

            if (!runtimeInstalled) {
                InstallRuntime(engine);
            }
        } finally {
            InstallationSemaphore.Release();
        }
    }

    /// <summary>
    /// Repairs the Playwright installation by cleaning driver and runtime directories and reinstalling.
    /// </summary>
    /// <param name="engine">The browser engine to reinstall.</param>
    public static async Task RepairInstallationAsync(HtmlBrowserEngine engine) {
        await InstallationSemaphore.WaitAsync().ConfigureAwait(false);
        try {
            CleanInstallDir();
            if (!IsDriverPresent()) {
                await DownloadAndInstallDriverAsync().ConfigureAwait(false);
            } else {
                EnsureDriverSearchPath();
            }
            InstallRuntime(engine);
        } finally {
            InstallationSemaphore.Release();
        }
    }

    /// <summary>
    /// Gets the path where Playwright browsers are installed.
    /// </summary>
    /// <returns></returns>
    private static string GetBrowserInstallPath() {
        string? envDefined = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        if (envDefined == "0") {
            return Path.Combine(Path.GetDirectoryName(typeof(HtmlBrowser).Assembly.Location) ?? AppContext.BaseDirectory, ".local-browsers");
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
        var prefixes = GetRuntimePrefixes(engine);
        foreach (string prefix in prefixes) {
            bool found = false;
            foreach (string dir in Directory.GetDirectories(path)) {
                if (Path.GetFileName(dir).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
                    found = true;
                    break;
                }
            }
            if (!found)
                return false;
        }
        return true;
    }

    private static bool IsBrowserRuntimeCorrupted(HtmlBrowserEngine engine) {
        string path = GetBrowserInstallPath();
        if (!Directory.Exists(path))
            return false;
        var prefixes = GetRuntimePrefixes(engine);
        foreach (string prefix in prefixes) {
            var candidates = Directory.GetDirectories(path).Where(dir =>
                Path.GetFileName(dir).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (candidates.Length == 0)
                continue;

            bool allEmpty = true;
            foreach (string dir in candidates) {
                try {
                    if (Directory.EnumerateFileSystemEntries(dir).Any()) {
                        allEmpty = false;
                        break;
                    }
                } catch {
                    return true;
                }
            }

            if (allEmpty)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Cleans the browser installation directory and removes the Playwright driver.
    /// </summary>
    private static void CleanInstallDir() {
        string path = GetBrowserInstallPath();
        if (Directory.Exists(path)) {
            Directory.Delete(path, recursive: true);
        }
        CleanDriver();
    }

    private static void CleanBrowserRuntime(HtmlBrowserEngine engine) {
        string path = GetBrowserInstallPath();
        if (!Directory.Exists(path))
            return;

        var prefixes = GetRuntimePrefixes(engine);
        foreach (string prefix in prefixes) {
            foreach (string dir in Directory.GetDirectories(path)) {
                if (!Path.GetFileName(dir).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;
                try {
                    Directory.Delete(dir, true);
                } catch {
                    // Ignore cleanup errors - best effort only.
                }
            }
        }
    }

    /// <summary>
    /// Ensures that the Playwright driver is present without installing bundled browser runtimes.
    /// </summary>
    /// <returns>A task that completes when the driver installation check/process is finished.</returns>
    internal static async Task EnsureDriverInstalledAsync() {
        if (IsDriverCorrupted()) {
            CleanDriver();
        }

        if (IsDriverPresent()) {
            EnsureDriverSearchPath();
            return;
        }

        await InstallationSemaphore.WaitAsync().ConfigureAwait(false);
        try {
            if (IsDriverCorrupted()) {
                CleanDriver();
            }

            if (IsDriverPresent()) {
                EnsureDriverSearchPath();
                return;
            }

            await DownloadAndInstallDriverAsync().ConfigureAwait(false);
        } finally {
            InstallationSemaphore.Release();
        }
    }

    private static string[] GetRuntimePrefixes(HtmlBrowserEngine engine) {
        if (engine == HtmlBrowserEngine.Chromium) {
            return new[] { "chromium-", "chromium_headless_shell-" };
        }
        return new[] { engine.ToString().ToLowerInvariant() + "-" };
    }

    private static void ValidateExistingInstallation(HtmlBrowserEngine engine) {
        if (IsDriverCorrupted()) {
            CleanDriver();
        }

        if (IsBrowserRuntimeCorrupted(engine)) {
            CleanBrowserRuntime(engine);
        }
    }

    private static void EnsureDriverSearchPath() {
        string driverPath = GetDriverPath();
        string driverRoot = Path.GetDirectoryName(driverPath) ?? GetDriverRoot();
        Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", driverRoot);
    }

    private static Func<HttpClient> DefaultHttpClientFactory => () => new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

    private static async Task DownloadAndInstallDriverAsync() {
        string urlBase = "https://playwright.azureedge.net/builds/driver";
        if (DriverVersion.Contains("-alpha") || DriverVersion.Contains("-beta") || DriverVersion.Contains("-next"))
            urlBase += "/next";
        string url = $"{urlBase}/playwright-{DriverVersion}-{DownloadPlatformId}.zip";

        using var client = HttpClientFactory();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1L;
        if (total > MaximumDriverArchiveBytes) {
            throw new InvalidDataException($"The Playwright driver archive reported {total} bytes, exceeding the {MaximumDriverArchiveBytes}-byte safety limit.");
        }
        using var mem = new MemoryStream();
        var buffer = new byte[81920];
        using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        long read = 0;
        int lastProgress = 0;
        var sw = Stopwatch.StartNew();
        while (true) {
            int n = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            if (n == 0)
                break;
            if (mem.Length + n > MaximumDriverArchiveBytes) {
                throw new InvalidDataException($"The Playwright driver archive exceeded the {MaximumDriverArchiveBytes}-byte safety limit while downloading.");
            }
            await mem.WriteAsync(buffer, 0, n).ConfigureAwait(false);
            if (total > 0) {
                read += n;
                int progress = (int)(read * 100 / total);
                if (progress != lastProgress) {
                    double speed = read / 1024d / 1024d / sw.Elapsed.TotalSeconds;
                    Console.Write($"\rDownloading Playwright driver... {progress}% ({speed:F1} MB/s)");
                    lastProgress = progress;
                }
            }
        }
        Console.WriteLine();
        mem.Position = 0;

        string baseDir = GetDriverPath();
        string tempDir = Path.Combine(Path.GetTempPath(), "pwdriver_" + Guid.NewGuid().ToString("N"));

        try {
            if (Directory.Exists(baseDir))
                Directory.Delete(baseDir, true);

            Directory.CreateDirectory(tempDir);

            using (var archive = new ZipArchive(mem, ZipArchiveMode.Read, leaveOpen: true)) {
                archive.ExtractToDirectory(tempDir);
            }

            Directory.CreateDirectory(Path.Combine(baseDir, "node", PlatformId));

            string nodeDest = Path.Combine(baseDir, "node", PlatformId, NodeExecutable);
            string nodeSource = Path.Combine(tempDir, NodeExecutable);
            if (File.Exists(nodeDest))
                File.Delete(nodeDest);
            File.Move(nodeSource, nodeDest);
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                try {
                    var chmod = Process.Start("chmod", $"+x \"{nodeDest}\"");
                    chmod?.WaitForExit();
                } catch {
                    // ignore
                }
            }
            string licenseDest = Path.Combine(baseDir, "node", "LICENSE");
            if (File.Exists(licenseDest))
                File.Delete(licenseDest);
            File.Move(Path.Combine(tempDir, "LICENSE"), licenseDest);

            string packageSrc = Path.Combine(tempDir, "package");
            string packageDest = Path.Combine(baseDir, "package");
            if (Directory.Exists(packageDest))
                Directory.Delete(packageDest, true);
            CopyDirectory(packageSrc, packageDest);

#if NETSTANDARD2_0 || NETFRAMEWORK
            File.WriteAllText(VersionFile, DriverVersion);
#else
            await File.WriteAllTextAsync(VersionFile, DriverVersion).ConfigureAwait(false);
#endif
        } catch {
            try {
                if (Directory.Exists(tempDir)) {
                    Directory.Delete(tempDir, true);
                }
            } catch {
                // ignore cleanup failure
            }

            CleanDriver();
            throw;
        } finally {
            try {
                if (Directory.Exists(tempDir)) {
                    Directory.Delete(tempDir, true);
                }
            } catch {
                // ignore cleanup failure
            }
        }

        EnsureDriverSearchPath();
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory) {
        Directory.CreateDirectory(destinationDirectory);

        foreach (string file in Directory.GetFiles(sourceDirectory)) {
            string destinationFile = Path.Combine(destinationDirectory, Path.GetFileName(file));
            File.Copy(file, destinationFile, overwrite: true);
        }

        foreach (string directory in Directory.GetDirectories(sourceDirectory)) {
            string destinationSubdirectory = Path.Combine(destinationDirectory, Path.GetFileName(directory));
            CopyDirectory(directory, destinationSubdirectory);
        }
    }

    private static void InstallRuntime(HtmlBrowserEngine engine) {
        string runtime = engine.ToString().ToLowerInvariant();

        try {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && ShouldUsePlaywrightWithDepsOnLinux()) {
                PlaywrightInstaller(new[] { "install", "--with-deps", runtime });
            } else {
                PlaywrightInstaller(new[] { "install", runtime });
            }
        } catch {
            CleanBrowserRuntime(engine);
            throw;
        }
    }

    private static bool ShouldUsePlaywrightWithDepsOnLinux() {
        bool? overrideValue = ReadBooleanEnvironmentVariable(PlaywrightWithDepsEnvVar);
        if (overrideValue.HasValue) {
            return overrideValue.Value;
        }

        return IsRunningAsRoot();
    }

    private static bool? ReadBooleanEnvironmentVariable(string name) {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }

        value = value.Trim();
        if (value == "1") {
            return true;
        }
        if (value == "0") {
            return false;
        }

        if (bool.TryParse(value, out bool parsed)) {
            return parsed;
        }

        if (string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "y", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }
        if (string.Equals(value, "no", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "n", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "off", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        return null;
    }

    private static bool IsRunningAsRoot() {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            return false;
        }

        // Prefer /proc/self/status (fully managed) to avoid platform-specific P/Invoke.
        try {
            const string procStatus = "/proc/self/status";
            if (File.Exists(procStatus)) {
                foreach (string line in File.ReadLines(procStatus)) {
                    if (!line.StartsWith("Uid:", StringComparison.Ordinal)) {
                        continue;
                    }

                    string[] parts = line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && uint.TryParse(parts[1], out uint uid)) {
                        return uid == 0;
                    }
                    break;
                }
            }
        } catch (IOException) {
            // ignore
        } catch (UnauthorizedAccessException) {
            // ignore
        }

        return string.Equals(Environment.UserName, "root", StringComparison.OrdinalIgnoreCase);
    }
}
