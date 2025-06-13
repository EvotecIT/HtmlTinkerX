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

        using var response = await client.GetAsync(url).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        string baseDir = GetDriverPath();
        if (Directory.Exists(baseDir))
            Directory.Delete(baseDir, true);
        Directory.CreateDirectory(baseDir);

        using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
        using (var archive = new ZipArchive(stream))
        {
            archive.ExtractToDirectory(baseDir);
        }

        File.WriteAllText(VersionFile, DriverVersion);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", baseDir);
    }
}
