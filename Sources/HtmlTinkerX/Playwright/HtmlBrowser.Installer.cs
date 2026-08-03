using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for retrieving HTML content using a headless browser.
/// </summary>
public static partial class HtmlBrowser {
    private const string PlaywrightWithDepsEnvVar = "HTMLTINKERX_PLAYWRIGHT_WITH_DEPS";
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

    private static string NodeExecutable => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node";
    private static StringComparison FileSystemPathComparison => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

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
        string browsersManifestPath = Path.Combine(packagePath, "browsers.json");
        string cliPath = Path.Combine(packagePath, "cli.js");
        if (!File.Exists(nodePath) || !Directory.Exists(packagePath) ||
            !File.Exists(browsersManifestPath) || !File.Exists(cliPath)) {
            return false;
        }

        try {
            return new FileInfo(nodePath).Length > 0 &&
                   new FileInfo(cliPath).Length > 0 &&
                   IsValidBrowserManifest(browsersManifestPath);
        } catch (IOException) {
            return false;
        } catch (UnauthorizedAccessException) {
            return false;
        }
    }

    private static bool IsValidBrowserManifest(string manifestPath) {
        try {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            return document.RootElement.TryGetProperty("browsers", out JsonElement browsers) &&
                   browsers.ValueKind == JsonValueKind.Array;
        } catch (IOException) {
            return false;
        } catch (UnauthorizedAccessException) {
            return false;
        } catch (JsonException) {
            return false;
        }
    }

    private static bool IsBundledDriverPath(string driverPath) {
        return string.Equals(
            Path.GetFullPath(driverPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(GetBundledDriverPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            FileSystemPathComparison);
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

        if (!HasDriverLayout(baseDir))
            return true;

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
        // Fast path - check without lock first
        if (IsDriverPresent() && IsBrowserRuntimeInstalled(engine)) {
            EnsureDriverSearchPath();
            return;
        }

        await InstallationSemaphore.WaitAsync().ConfigureAwait(false);
        try {
            using FileStream installationLock = await AcquireInstallationFileLockAsync().ConfigureAwait(false);
            ValidateExistingInstallation(engine);

            if (IsDriverPresent() && IsBrowserRuntimeInstalled(engine)) {
                EnsureDriverSearchPath();
                return;
            }

            if (!IsDriverPresent()) {
                await DownloadAndInstallDriverAsync().ConfigureAwait(false);
            } else {
                EnsureDriverSearchPath();
            }

            // A repaired driver provides the manifest needed to identify an already-installed exact runtime.
            if (!IsBrowserRuntimeInstalled(engine)) {
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
            using FileStream installationLock = await AcquireInstallationFileLockAsync().ConfigureAwait(false);
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

        IReadOnlyDictionary<string, IReadOnlyList<string>> expectedDirectories = GetExpectedRuntimeDirectories(path, engine);
        if (expectedDirectories.Count == 0)
            return false;

        foreach (IReadOnlyList<string> candidates in expectedDirectories.Values) {
            if (!candidates.Any(IsCompleteBrowserRuntime))
                return false;
        }

        return true;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> GetExpectedRuntimeDirectories(string browserRoot, HtmlBrowserEngine engine) {
        string manifestPath = Path.Combine(GetDriverPath(), "package", "browsers.json");
        if (!File.Exists(manifestPath))
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        string[] requiredNames = engine == HtmlBrowserEngine.Chromium
            ? new[] { "chromium", "chromium-headless-shell" }
            : new[] { engine.ToString().ToLowerInvariant() };
        var required = new HashSet<string>(requiredNames, StringComparer.OrdinalIgnoreCase);
        var expected = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        try {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (!document.RootElement.TryGetProperty("browsers", out JsonElement browsers) || browsers.ValueKind != JsonValueKind.Array)
                return expected;

            foreach (JsonElement browser in browsers.EnumerateArray()) {
                if (!browser.TryGetProperty("name", out JsonElement nameElement) || nameElement.ValueKind != JsonValueKind.String ||
                    !browser.TryGetProperty("revision", out JsonElement revisionElement) || revisionElement.ValueKind != JsonValueKind.String) {
                    continue;
                }

                string? name = nameElement.GetString();
                string? revision = revisionElement.GetString();
                if (name is null || name.Trim().Length == 0 || revision is null || revision.Trim().Length == 0 || !required.Contains(name))
                    continue;

                string selectedName = name;
                string selectedRevision = revision;
                string hostPlatform = GetCurrentPlaywrightHostPlatform();
                if (browser.TryGetProperty("revisionOverrides", out JsonElement overrides) &&
                    overrides.ValueKind == JsonValueKind.Object &&
                    overrides.TryGetProperty(hostPlatform, out JsonElement revisionOverride) &&
                    revisionOverride.ValueKind == JsonValueKind.String) {
                    string? overrideRevision = revisionOverride.GetString();
                    if (overrideRevision is not null && overrideRevision.Trim().Length > 0) {
                        selectedName = $"{name}_{hostPlatform}_special";
                        selectedRevision = overrideRevision;
                    }
                }

                expected[name] = new[] { BuildRuntimeDirectory(browserRoot, selectedName, selectedRevision) };
            }
        } catch (IOException) {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        } catch (UnauthorizedAccessException) {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        } catch (JsonException) {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        }

        return expected.Count == required.Count
            ? expected
            : new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildRuntimeDirectory(string browserRoot, string name, string revision) {
        string directoryName = name.Replace('-', '_') + "-" + revision;
        return Path.Combine(browserRoot, directoryName);
    }

    private static bool IsCompleteBrowserRuntime(string directory) {
        return Directory.Exists(directory) && File.Exists(Path.Combine(directory, "INSTALLATION_COMPLETE"));
    }

    private static string GetCurrentPlaywrightHostPlatform() {
        string? overridePlatform = Environment.GetEnvironmentVariable("PLAYWRIGHT_HOST_PLATFORM_OVERRIDE");
        if (!string.IsNullOrWhiteSpace(overridePlatform))
            return overridePlatform.Trim();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "win64";

        string? architecture = RuntimeInformation.OSArchitecture switch {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => null
        };
        if (architecture is null)
            return "<unknown>";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            return GetMacPlaywrightHostPlatform(RuntimeInformation.OSArchitecture, GetDarwinMajorVersion());
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "<unknown>";

        string suffix = "-" + architecture;
        IReadOnlyDictionary<string, string> release = ReadLinuxOsRelease();
        release.TryGetValue("ID", out string? id);
        release.TryGetValue("VERSION_ID", out string? version);
        id = id?.ToLowerInvariant() ?? string.Empty;
        version ??= string.Empty;
        int.TryParse(version.Split('.')[0], out int major);

        if (id is "ubuntu" or "pop" or "neon" or "tuxedo") {
            if (major < 20) return "ubuntu18.04" + suffix;
            if (major < 22) return "ubuntu20.04" + suffix;
            if (major < 24) return "ubuntu22.04" + suffix;
            if (major < 26) return "ubuntu24.04" + suffix;
            return "ubuntu" + version + suffix;
        }
        if (id == "linuxmint") {
            if (major <= 20) return "ubuntu20.04" + suffix;
            if (major == 21) return "ubuntu22.04" + suffix;
            return "ubuntu24.04" + suffix;
        }
        if (id is "debian" or "raspbian") {
            if (version is "11" or "12" or "13") return "debian" + version + suffix;
            if (version.Length == 0) return "debian13" + suffix;
        }
        return "ubuntu24.04" + suffix;
    }

    internal static string GetMacPlaywrightHostPlatform(Architecture architecture, int darwinMajor) {
        string macVersion = darwinMajor switch {
            < 18 => "mac10.13",
            18 => "mac10.14",
            19 => "mac10.15",
            _ => "mac" + Math.Min(darwinMajor - 9, 15)
        };
        return architecture == Architecture.Arm64 && darwinMajor >= 20
            ? macVersion + "-arm64"
            : macVersion;
    }

    private static int GetDarwinMajorVersion() {
        try {
            var startInfo = new ProcessStartInfo {
                FileName = "/usr/bin/uname",
                Arguments = "-r",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using Process? process = Process.Start(startInfo);
            if (process is not null) {
                string release = process.StandardOutput.ReadToEnd().Trim();
                if (process.WaitForExit(5000) && process.ExitCode == 0 &&
                    int.TryParse(release.Split('.')[0], out int darwinMajor)) {
                    return darwinMajor;
                }
            }
        } catch (InvalidOperationException) {
            // Fall back to Environment.OSVersion when uname cannot be started.
        } catch (System.ComponentModel.Win32Exception) {
            // Fall back to Environment.OSVersion when uname is unavailable.
        }

        Version version = Environment.OSVersion.Version;
        if (version.Major >= 20)
            return version.Major;
        if (version.Major >= 11)
            return version.Major + 9;
        if (version.Major == 10 && version.Minor >= 13)
            return version.Minor + 4;
        return version.Major;
    }

    private static IReadOnlyDictionary<string, string> ReadLinuxOsRelease() {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try {
            foreach (string line in File.ReadLines("/etc/os-release")) {
                int separator = line.IndexOf('=');
                if (separator <= 0)
                    continue;
                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim().Trim('"', '\'');
                values[key] = value;
            }
        } catch (IOException) {
            // Playwright falls back to the current Ubuntu platform for unknown Linux distributions.
        } catch (UnauthorizedAccessException) {
            // Playwright falls back to the current Ubuntu platform for unreadable release metadata.
        }
        return values;
    }

    private static bool IsBrowserRuntimeCorrupted(HtmlBrowserEngine engine) {
        string path = GetBrowserInstallPath();
        if (!Directory.Exists(path))
            return false;
        IReadOnlyDictionary<string, IReadOnlyList<string>> expectedDirectories = GetExpectedRuntimeDirectories(path, engine);
        if (expectedDirectories.Values.SelectMany(static candidates => candidates)
            .Any(directory => Directory.Exists(directory) && !IsCompleteBrowserRuntime(directory))) {
            return true;
        }
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

    internal static async Task CleanInstallationAsync() {
        await InstallationSemaphore.WaitAsync().ConfigureAwait(false);
        try {
            using FileStream installationLock = await AcquireInstallationFileLockAsync().ConfigureAwait(false);
            CleanInstallDir();
        } finally {
            InstallationSemaphore.Release();
        }
    }

    private static void CleanBrowserRuntime(HtmlBrowserEngine engine) {
        string path = GetBrowserInstallPath();
        if (!Directory.Exists(path))
            return;

        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string expectedDirectory in GetExpectedRuntimeDirectories(path, engine).Values.SelectMany(static candidates => candidates)) {
            directories.Add(expectedDirectory);
        }
        var prefixes = GetRuntimePrefixes(engine);
        foreach (string prefix in prefixes) {
            foreach (string dir in Directory.GetDirectories(path)) {
                if (Path.GetFileName(dir).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    directories.Add(dir);
            }
        }
        foreach (string directory in directories) {
            if (!Directory.Exists(directory))
                continue;
            try {
                Directory.Delete(directory, true);
            } catch {
                // Ignore cleanup errors - best effort only.
            }
        }
    }

    /// <summary>
    /// Ensures that the Playwright driver is present without installing bundled browser runtimes.
    /// </summary>
    /// <returns>A task that completes when the driver installation check/process is finished.</returns>
    internal static async Task EnsureDriverInstalledAsync() {
        if (IsDriverPresent()) {
            EnsureDriverSearchPath();
            return;
        }

        await InstallationSemaphore.WaitAsync().ConfigureAwait(false);
        try {
            using FileStream installationLock = await AcquireInstallationFileLockAsync().ConfigureAwait(false);
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
            return new[] { "chromium-", "chromium_" };
        }
        string name = engine.ToString().ToLowerInvariant();
        return new[] { name + "-", name + "_" };
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

    private static Func<HttpClient> DefaultHttpClientFactory => () => new HttpClient { Timeout = TimeSpan.FromMinutes(15) };

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
