using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PSParseHTML;

/// <summary>
/// Helper methods for retrieving HTML content using a headless browser.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Gets the version of the Playwright driver.
    /// </summary>
    private static string DriverVersion => typeof(Microsoft.Playwright.Playwright)
        .Assembly.GetName().Version?.ToString(3) ?? "1.52.0";

    /// <summary>
    /// Gets the platform identifier for the Playwright driver.
    /// </summary>
    private static string PlatformId {
        get {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "win32_x64";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return RuntimeInformation.OSArchitecture == Architecture.Arm64
                    ? "darwin-arm64"
                    : "darwin-x64";
            if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
                return "linux-arm64";
            return "linux-x64";
        }
    }

    /// <summary>
    /// Gets the platform identifier for downloading the Playwright driver.
    /// </summary>
    private static string DownloadPlatformId {
        get {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "win32_x64";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return RuntimeInformation.OSArchitecture == Architecture.Arm64
                    ? "mac-arm64"
                    : "mac";
            if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
                return "linux-arm64";
            return "linux";
        }
    }

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
        return Path.Combine(GetDriverRoot(), ".playwright");
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
    /// Removes the Playwright driver installation directory.
    /// This is typically called when the application is being uninstalled or when
    /// the driver is no longer needed.
    /// </summary>
    internal static void CleanDriver() {
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
    /// Ensures that the Playwright driver is installed.
    /// </summary>
    /// <returns></returns>
    internal static async Task EnsureInstalledAsync(HtmlBrowserEngine engine) {
        bool runtimeInstalled = IsBrowserRuntimeInstalled(engine);

        if (IsDriverPresent()) {
            // PLAYWRIGHT_DRIVER_SEARCH_PATH must point to the directory containing
            // the '.playwright' folder, not to the folder itself.
            Environment.SetEnvironmentVariable(
                "PLAYWRIGHT_DRIVER_SEARCH_PATH",
                GetDriverRoot());
        } else {
            string urlBase = "https://playwright.azureedge.net/builds/driver";
            if (DriverVersion.Contains("-alpha") || DriverVersion.Contains("-beta") || DriverVersion.Contains("-next"))
                urlBase += "/next";
            string url = $"{urlBase}/playwright-{DriverVersion}-{DownloadPlatformId}.zip";

            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? -1L;
            var mem = new MemoryStream();
            var buffer = new byte[81920];
            var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            long read = 0;
            int lastProgress = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (true) {
                int n = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                if (n == 0)
                    break;
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
            if (Directory.Exists(baseDir))
                Directory.Delete(baseDir, true);

            string tempDir = Path.Combine(Path.GetTempPath(), "pwdriver_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            using (var archive = new ZipArchive(mem)) {
                archive.ExtractToDirectory(tempDir);
            }

            Directory.CreateDirectory(Path.Combine(baseDir, "node", PlatformId));

            string nodeDest = Path.Combine(baseDir, "node", PlatformId, NodeExecutable);
            File.Move(Path.Combine(tempDir, NodeExecutable), nodeDest);
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                try {
                    var chmod = Process.Start("chmod", $"+x \"{nodeDest}\"");
                    chmod?.WaitForExit();
                } catch {
                    // ignore
                }
            }
            File.Move(Path.Combine(tempDir, "LICENSE"), Path.Combine(baseDir, "node", "LICENSE"));

            string packageSrc = Path.Combine(tempDir, "package");
            string packageDest = Path.Combine(baseDir, "package");
            if (Directory.Exists(packageDest))
                Directory.Delete(packageDest, true);
            Directory.Move(packageSrc, packageDest);
            Directory.Delete(tempDir, true);

            File.WriteAllText(VersionFile, DriverVersion);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", GetDriverRoot());
        }

        if (!runtimeInstalled) {
            string runtime = engine.ToString().ToLowerInvariant();
            Microsoft.Playwright.Program.Main(new[] { "install", runtime });
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
        if (Directory.Exists(path)) {
            Directory.Delete(path, recursive: true);
        }
        CleanDriver();
    }
}
