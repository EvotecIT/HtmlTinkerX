using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace PSParseHTML;

internal static class PlaywrightInstaller
{
    private static string DriverVersion => typeof(Microsoft.Playwright.Playwright)
        .Assembly.GetName().Version?.ToString(3) ?? "1.52.0";

    private static string PlatformId
    {
        get
        {
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

    private static string NodeExecutable => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node";

    private static string GetDriverPath()
    {
        string dir = Path.Combine(Path.GetDirectoryName(typeof(PlaywrightInstaller).Assembly.Location) ?? AppContext.BaseDirectory, ".playwright");
        return Path.GetFullPath(dir);
    }

    private static string VersionFile => Path.Combine(GetDriverPath(), ".version");

    private static bool IsDriverPresent()
    {
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

    internal static async Task EnsureInstalledAsync()
    {
        if (IsDriverPresent())
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", GetDriverPath());
            return;
        }

        string urlBase = "https://playwright.azureedge.net/builds/driver";
        if (DriverVersion.Contains("-alpha") || DriverVersion.Contains("-beta") || DriverVersion.Contains("-next"))
            urlBase += "/next";
        string url = $"{urlBase}/playwright-{DriverVersion}-{PlatformId}.zip";

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
        while (true)
        {
            int n = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            if (n == 0)
                break;
            await mem.WriteAsync(buffer, 0, n).ConfigureAwait(false);
            if (total > 0)
            {
                read += n;
                int progress = (int)(read * 100 / total);
                if (progress != lastProgress)
                {
                    Console.Write($"\rDownloading Playwright driver... {progress}%   ");
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

        using (var archive = new ZipArchive(mem))
        {
            archive.ExtractToDirectory(tempDir);
        }

        Directory.CreateDirectory(Path.Combine(baseDir, "node", PlatformId));
        Directory.CreateDirectory(Path.Combine(baseDir, "package"));

        File.Move(Path.Combine(tempDir, NodeExecutable), Path.Combine(baseDir, "node", PlatformId, NodeExecutable));
        File.Move(Path.Combine(tempDir, "LICENSE"), Path.Combine(baseDir, "node", "LICENSE"));
        Directory.Move(Path.Combine(tempDir, "package"), Path.Combine(baseDir, "package"));
        Directory.Delete(tempDir, true);

        File.WriteAllText(VersionFile, DriverVersion);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", baseDir);
    }
}
