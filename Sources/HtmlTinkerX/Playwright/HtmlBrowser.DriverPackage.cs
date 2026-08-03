using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for retrieving HTML content using a headless browser.
/// </summary>
public static partial class HtmlBrowser {
    private const long MaximumDriverPackageBytes = 512L * 1024 * 1024;
    private const string PlaywrightPackageId = "microsoft.playwright";
    private const string PlaywrightPackageBaseUrl = "https://api.nuget.org/v3-flatcontainer";
    private static readonly TimeSpan InstallationLockTimeout = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Downloads the official Microsoft.Playwright package that matches the loaded assembly and installs only
    /// the driver assets for the current platform. This is the fallback used when a host, such as a packaged
    /// PowerShell module, does not preserve the SDK-copied <c>.playwright</c> output directory.
    /// </summary>
    private static async Task DownloadAndInstallDriverAsync() {
        string packageUrl = GetDriverPackageUrl();
        string packagePath = Path.Combine(Path.GetTempPath(), "playwright-driver-" + Guid.NewGuid().ToString("N") + ".nupkg");
        string baseDir = GetDriverPath();
        string driverRoot = Path.GetDirectoryName(baseDir) ?? GetDriverRoot();
        string stagingDir = Path.Combine(driverRoot, ".playwright-install-" + Guid.NewGuid().ToString("N"));

        try {
            Directory.CreateDirectory(driverRoot);
            await DownloadDriverPackageAsync(packageUrl, packagePath).ConfigureAwait(false);
            ExtractCurrentPlatformDriver(packagePath, stagingDir);

            if (!HasDriverLayout(stagingDir)) {
                throw new InvalidDataException(
                    $"Microsoft.Playwright {DriverVersion} did not contain a complete driver for platform '{PlatformId}'.");
            }

#if NETSTANDARD2_0 || NETFRAMEWORK
            File.WriteAllText(Path.Combine(stagingDir, ".version"), DriverVersion);
#else
            await File.WriteAllTextAsync(Path.Combine(stagingDir, ".version"), DriverVersion).ConfigureAwait(false);
#endif

            if (Directory.Exists(baseDir)) {
                Directory.Delete(baseDir, true);
            }
            Directory.Move(stagingDir, baseDir);
            EnsureNodeExecutable(baseDir);
        } catch {
            CleanDriver();
            throw;
        } finally {
            TryDeleteFile(packagePath);
            TryDeleteDirectory(stagingDir);
        }

        EnsureDriverSearchPath();
    }

    private static string GetDriverPackageUrl() {
        string version = DriverVersion.ToLowerInvariant();
        return $"{PlaywrightPackageBaseUrl}/{PlaywrightPackageId}/{version}/{PlaywrightPackageId}.{version}.nupkg";
    }

    private static async Task DownloadDriverPackageAsync(string packageUrl, string destinationPath) {
        using var client = HttpClientFactory();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HtmlTinkerX/" + DriverVersion);

        using var response = await client.GetAsync(packageUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        long total = response.Content.Headers.ContentLength ?? -1L;
        if (total > MaximumDriverPackageBytes) {
            throw new InvalidDataException(
                $"The Microsoft.Playwright package reported {total} bytes, exceeding the {MaximumDriverPackageBytes}-byte safety limit.");
        }

        using var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        var buffer = new byte[81920];
        long read = 0;
        int lastProgress = -1;
        var stopwatch = Stopwatch.StartNew();

        while (true) {
            int count = await source.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            if (count == 0) {
                break;
            }
            if (read + count > MaximumDriverPackageBytes) {
                throw new InvalidDataException(
                    $"The Microsoft.Playwright package exceeded the {MaximumDriverPackageBytes}-byte safety limit while downloading.");
            }

            await destination.WriteAsync(buffer, 0, count).ConfigureAwait(false);
            read += count;

            if (total > 0) {
                int progress = (int)(read * 100 / total);
                if (progress != lastProgress) {
                    double elapsedSeconds = Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001d);
                    double speed = read / 1024d / 1024d / elapsedSeconds;
                    Console.Write($"\rDownloading Playwright driver... {progress}% ({speed:F1} MB/s)");
                    lastProgress = progress;
                }
            }
        }

        Console.WriteLine();
    }

    private static void ExtractCurrentPlatformDriver(string packagePath, string destinationPath) {
        string nodePrefix = ".playwright/node/" + PlatformId + "/";
        const string nodeLicense = ".playwright/node/LICENSE";
        const string packagePrefix = ".playwright/package/";
        int extractedFiles = 0;

        Directory.CreateDirectory(destinationPath);
        using var archive = ZipFile.OpenRead(packagePath);
        foreach (ZipArchiveEntry entry in archive.Entries) {
            string entryPath = entry.FullName.Replace('\\', '/');
            string? relativePath = null;

            if (entryPath.StartsWith(nodePrefix, StringComparison.OrdinalIgnoreCase)) {
                relativePath = "node/" + PlatformId + "/" + entryPath.Substring(nodePrefix.Length);
            } else if (string.Equals(entryPath, nodeLicense, StringComparison.OrdinalIgnoreCase)) {
                relativePath = "node/LICENSE";
            } else if (entryPath.StartsWith(packagePrefix, StringComparison.OrdinalIgnoreCase)) {
                relativePath = "package/" + entryPath.Substring(packagePrefix.Length);
            }

            if (relativePath is null || relativePath.Length == 0 || relativePath.EndsWith("/", StringComparison.Ordinal)) {
                continue;
            }

            string destinationFile = GetSafeExtractionPath(destinationPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            using var source = entry.Open();
            using var destination = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None);
            source.CopyTo(destination);
            extractedFiles++;
        }

        if (extractedFiles == 0) {
            throw new InvalidDataException(
                $"Microsoft.Playwright {DriverVersion} did not contain driver assets for platform '{PlatformId}'.");
        }
    }

    internal static string GetSafeExtractionPath(string rootPath, string relativePath) {
        string fullRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                          + Path.DirectorySeparatorChar;
        string destinationPath = Path.GetFullPath(Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        // Archive containment must remain strict even on Windows because NTFS directories can opt into case sensitivity.
        if (!destinationPath.StartsWith(fullRoot, StringComparison.Ordinal)) {
            throw new InvalidDataException($"The Microsoft.Playwright package contains an unsafe entry path: '{relativePath}'.");
        }
        return destinationPath;
    }

    /// <summary>
    /// Serializes driver and browser publication across independent host processes. The lock file is deliberately
    /// outside both installation roots because repair can delete either root while holding the lock.
    /// </summary>
    internal static async Task<FileStream> AcquireInstallationFileLockAsync() {
        string lockPath = GetInstallationLockPath();
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        var stopwatch = Stopwatch.StartNew();

        while (true) {
            try {
                return new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    useAsync: true);
            } catch (IOException exception) {
                if (stopwatch.Elapsed >= InstallationLockTimeout) {
                    throw new TimeoutException(
                        $"Timed out waiting for another HtmlTinkerX Playwright installation to finish after {InstallationLockTimeout.TotalMinutes:F0} minutes.",
                        exception);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
            }
        }
    }

    private static string GetInstallationLockPath() {
        string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string cacheRoot;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            cacheRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            cacheRoot = Path.Combine(user, "Library", "Caches");
        } else {
            cacheRoot = Path.Combine(user, ".cache");
        }

        if (string.IsNullOrWhiteSpace(cacheRoot)) {
            cacheRoot = Path.GetTempPath();
        }

        return Path.Combine(cacheRoot, "HtmlTinkerX", "playwright-install.lock");
    }

    private static void EnsureNodeExecutable(string driverPath) {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            return;
        }

        string nodePath = Path.Combine(driverPath, "node", PlatformId, NodeExecutable);
        try {
            using Process? chmod = Process.Start("chmod", $"+x \"{nodePath}\"");
            chmod?.WaitForExit();
        } catch (InvalidOperationException) {
            // Playwright will report an actionable launch error if the host cannot set executable permissions.
        } catch (System.ComponentModel.Win32Exception) {
            // Playwright will report an actionable launch error if chmod is unavailable.
        }
    }

    private static void TryDeleteFile(string path) {
        try {
            if (File.Exists(path)) {
                File.Delete(path);
            }
        } catch (IOException) {
            // Best-effort cleanup only.
        } catch (UnauthorizedAccessException) {
            // Best-effort cleanup only.
        }
    }

    private static void TryDeleteDirectory(string path) {
        try {
            if (Directory.Exists(path)) {
                Directory.Delete(path, true);
            }
        } catch (IOException) {
            // Best-effort cleanup only.
        } catch (UnauthorizedAccessException) {
            // Best-effort cleanup only.
        }
    }
}
