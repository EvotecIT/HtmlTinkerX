using HtmlTinkerX;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Process diagnostics")]
public class HtmlBrowserInstallerTests
{


    [Fact]
    public void PlaywrightInstaller_HonorsConfiguredNodeExecutable()
    {
        string? originalNode = Environment.GetEnvironmentVariable("PLAYWRIGHT_NODEJS_PATH");
        string? originalBrowsers = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        try
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", Path.Combine(Path.GetTempPath(), "htmltinkerx-cli-" + Guid.NewGuid().ToString("N")));
            Environment.SetEnvironmentVariable("PLAYWRIGHT_NODEJS_PATH", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "unavailable node"));
            Assert.Throws<System.ComponentModel.Win32Exception>(() =>
                HtmlBrowser.PlaywrightInstaller(new[] { "install", "--dry-run", "chromium" }));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_NODEJS_PATH", originalNode);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", originalBrowsers);
        }
    }

    [Fact]
    public void PlaywrightInstaller_RunsOfficialBrowserPlanWithoutDownloading()
    {
        string? originalDriverPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");
        string? originalBrowsers = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        try
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", null);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", Path.Combine(Path.GetTempPath(), "htmltinkerx-cli-" + Guid.NewGuid().ToString("N")));
            HtmlBrowser.PlaywrightInstaller(new[] { "install", "--dry-run", "chromium" });
        }
        finally
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", originalDriverPath);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", originalBrowsers);
        }
    }

    [Fact]
    public void PlaywrightInstaller_ReportsCliFailureInsteadOfClaimingSuccess()
    {
        string? originalDriverPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");
        string? originalBrowsers = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        try
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", null);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", Path.Combine(Path.GetTempPath(), "htmltinkerx-cli-" + Guid.NewGuid().ToString("N")));
            var failure = Assert.Throws<InvalidOperationException>(() =>
                HtmlBrowser.PlaywrightInstaller(new[] { "install", "--htmltinkerx-invalid-option" }));
            Assert.Contains("exit code 1", failure.Message);
            Assert.Contains("--htmltinkerx-invalid-option", failure.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", originalDriverPath);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", originalBrowsers);
        }
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void HasDriverLayout_RejectsIncompleteBundledAssets(bool hasHealthyNode, bool hasPackageContent)
    {
        string driverPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string nodeDir = Path.Combine(driverPath, "node", PlatformExtensions.GetCurrentPlatform().ToPlatformId());
        string packageDir = Path.Combine(driverPath, "package");

        try
        {
            Directory.CreateDirectory(nodeDir);
            Directory.CreateDirectory(packageDir);
            File.WriteAllText(
                Path.Combine(nodeDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node"),
                hasHealthyNode ? "node" : string.Empty);
            if (hasPackageContent)
            {
                File.WriteAllText(Path.Combine(packageDir, "package.json"), "{}");
            }

            Assert.False(HtmlBrowser.HasDriverLayout(driverPath));
        }
        finally
        {
            if (Directory.Exists(driverPath)) Directory.Delete(driverPath, true);
        }
    }

    [Fact]
    public void HasDriverLayout_RejectsDriverWithoutPlaywrightCli()
    {
        string driverPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string nodeDir = Path.Combine(driverPath, "node", PlatformExtensions.GetCurrentPlatform().ToPlatformId());
        string packageDir = Path.Combine(driverPath, "package");

        try
        {
            Directory.CreateDirectory(nodeDir);
            Directory.CreateDirectory(packageDir);
            File.WriteAllText(
                Path.Combine(nodeDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node"),
                "node");
            File.WriteAllText(Path.Combine(packageDir, "browsers.json"), "{\"browsers\":[]}");

            Assert.False(HtmlBrowser.HasDriverLayout(driverPath));
        }
        finally
        {
            if (Directory.Exists(driverPath)) Directory.Delete(driverPath, true);
        }
    }

    [Fact]
    public async Task EnsureDriverInstalledAsync_UsesBundledDriverWithoutNetworkDownload()
    {
        string? originalDriverPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");
        var originalFactory = HtmlBrowser.HttpClientFactory;

        try
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", null);
            HtmlBrowser.HttpClientFactory = () => throw new InvalidOperationException("Bundled driver should avoid network download.");

            await HtmlBrowser.EnsureDriverInstalledAsync();

            string configuredRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH")!);
            string nodeExecutable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node";
            string nodePath = Path.Combine(configuredRoot, ".playwright", "node", PlatformExtensions.GetCurrentPlatform().ToPlatformId(), nodeExecutable);
            Assert.True(File.Exists(nodePath), $"Expected the bundled Playwright driver at '{nodePath}'.");
        }
        finally
        {
            HtmlBrowser.HttpClientFactory = originalFactory;
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", originalDriverPath);
        }
    }

    [Fact]
    public void ShouldInstallBundledRuntime_SkipsRuntimeForExternalBrowsers()
    {
        Assert.True(HtmlBrowser.ShouldInstallBundledRuntime(new HtmlBrowserLaunchOptions()));
        Assert.False(HtmlBrowser.ShouldInstallBundledRuntime(new HtmlBrowserLaunchOptions
        {
            BrowserChannel = "chrome"
        }));
        Assert.False(HtmlBrowser.ShouldInstallBundledRuntime(new HtmlBrowserLaunchOptions
        {
            BrowserExecutablePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? @"C:\Program Files\Google\Chrome\Application\chrome.exe"
                : "/usr/bin/google-chrome"
        }));

        HtmlBrowserLaunchOptions blank = new() {
            BrowserChannel = " ",
            BrowserExecutablePath = "\t",
            Proxy = "  ",
            UserAgent = " ",
            Locale = "\t",
            Timezone = "\r\n"
        };
        Assert.True(HtmlBrowser.ShouldInstallBundledRuntime(blank));
        Assert.Null(HtmlBrowser.CreateLaunchOptions(blank).Channel);
        Assert.Null(HtmlBrowser.CreateLaunchOptions(blank).ExecutablePath);
        Assert.Null(HtmlBrowser.CreateLaunchOptions(blank).Proxy);
        Assert.Null(HtmlBrowser.CreatePersistentContextOptions(blank).Channel);
        Assert.Null(HtmlBrowser.CreatePersistentContextOptions(blank).ExecutablePath);
        Assert.Null(HtmlBrowser.CreatePersistentContextOptions(blank).Proxy);
        Assert.Null(HtmlBrowser.CreatePersistentContextOptions(blank).UserAgent);
        Assert.Null(HtmlBrowser.CreatePersistentContextOptions(blank).Locale);
        Assert.Null(HtmlBrowser.CreatePersistentContextOptions(blank).TimezoneId);
    }













    [Fact]
    public async Task EnsureInstalledAsync_ReinstallsDriverWithMissingBrowserManifest()
    {
        string tempBrowsers = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string tempDriver = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string? originalBrowsersPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        string? originalDriverPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");
        var originalInstaller = HtmlBrowser.PlaywrightInstaller;
        var originalFactory = HtmlBrowser.HttpClientFactory;

        try
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", tempBrowsers);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", tempDriver);
            CreateHealthyDriver(tempDriver);
            string manifestPath = Path.Combine(tempDriver, ".playwright", "package", "browsers.json");
            File.Delete(manifestPath);
            HtmlBrowser.HttpClientFactory = () => new HttpClient(new FakeHandler(CreateDriverArchive()));

            string[]? captured = null;
            HtmlBrowser.PlaywrightInstaller = args => captured = args;

            await HtmlBrowser.EnsureDriverInstalledAsync();

            Assert.True(File.Exists(manifestPath));
            Assert.Null(captured);
        }
        finally
        {
            HtmlBrowser.PlaywrightInstaller = originalInstaller;
            HtmlBrowser.HttpClientFactory = originalFactory;
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", originalBrowsersPath);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", originalDriverPath);
            if (Directory.Exists(tempBrowsers)) Directory.Delete(tempBrowsers, true);
            if (Directory.Exists(tempDriver)) Directory.Delete(tempDriver, true);
        }
    }

    [Fact]
    public async Task EnsureInstalledAsync_ReinstallsDriverWithMalformedBrowserManifest()
    {
        string tempBrowsers = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string tempDriver = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string? originalBrowsersPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        string? originalDriverPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");
        var originalInstaller = HtmlBrowser.PlaywrightInstaller;
        var originalFactory = HtmlBrowser.HttpClientFactory;

        try
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", tempBrowsers);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", tempDriver);
            CreateHealthyDriver(tempDriver);
            string manifestPath = Path.Combine(tempDriver, ".playwright", "package", "browsers.json");
            File.WriteAllText(manifestPath, "{not-json");
            HtmlBrowser.HttpClientFactory = () => new HttpClient(new FakeHandler(CreateDriverArchive()));
            HtmlBrowser.PlaywrightInstaller = _ => CreateCompleteChromiumRuntime(tempBrowsers, "1217");

            await HtmlBrowser.EnsureDriverInstalledAsync();

            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            Assert.Equal(JsonValueKind.Array, manifest.RootElement.GetProperty("browsers").ValueKind);
        }
        finally
        {
            HtmlBrowser.PlaywrightInstaller = originalInstaller;
            HtmlBrowser.HttpClientFactory = originalFactory;
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", originalBrowsersPath);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", originalDriverPath);
            if (Directory.Exists(tempBrowsers)) Directory.Delete(tempBrowsers, true);
            if (Directory.Exists(tempDriver)) Directory.Delete(tempDriver, true);
        }
    }

    [Fact]
    public async Task EnsureInstalledAsync_RechecksExistingRuntimeAfterDriverDownload()
    {
        string tempBrowsers = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string tempDriver = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string? originalBrowsersPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        string? originalDriverPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");
        var originalInstaller = HtmlBrowser.PlaywrightInstaller;
        var originalFactory = HtmlBrowser.HttpClientFactory;

        try
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", tempBrowsers);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", tempDriver);
            CreateCompleteChromiumRuntime(tempBrowsers, "1217");
            HtmlBrowser.HttpClientFactory = () => new HttpClient(new FakeHandler(CreateDriverArchive()));
            HtmlBrowser.PlaywrightInstaller = _ => throw new InvalidOperationException("An existing exact runtime should not be reinstalled.");

            await HtmlBrowser.EnsureDriverInstalledAsync();

            Assert.True(HtmlBrowser.HasDriverLayout(Path.Combine(tempDriver, ".playwright")));
        }
        finally
        {
            HtmlBrowser.PlaywrightInstaller = originalInstaller;
            HtmlBrowser.HttpClientFactory = originalFactory;
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", originalBrowsersPath);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", originalDriverPath);
            if (Directory.Exists(tempBrowsers)) Directory.Delete(tempBrowsers, true);
            if (Directory.Exists(tempDriver)) Directory.Delete(tempDriver, true);
        }
    }

    [Theory]
    [InlineData(23, Architecture.X64, "mac14")]
    [InlineData(24, Architecture.Arm64, "mac15-arm64")]
    [InlineData(19, Architecture.X64, "mac10.15")]
    public void GetMacPlaywrightHostPlatform_UsesDarwinMajor(int darwinMajor, Architecture architecture, string expected)
    {
        Assert.Equal(expected, HtmlBrowser.GetMacPlaywrightHostPlatform(architecture, darwinMajor));
    }







    private static void CreateHealthyDriver(string driverRoot)
    {
        string baseDir = Path.Combine(driverRoot, ".playwright");
        string nodeDir = Path.Combine(baseDir, "node", PlatformExtensions.GetCurrentPlatform().ToPlatformId());
        string packageDir = Path.Combine(baseDir, "package");
        Directory.CreateDirectory(nodeDir);
        Directory.CreateDirectory(packageDir);
        File.WriteAllText(Path.Combine(nodeDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node"), "node");
        File.WriteAllText(Path.Combine(packageDir, "package.json"), "{}");
        File.WriteAllText(Path.Combine(packageDir, "cli.js"), "console.log('playwright');");
        File.WriteAllText(Path.Combine(packageDir, "browsers.json"), """
            {
              "browsers": [
                { "name": "chromium", "revision": "1217" },
                { "name": "chromium-headless-shell", "revision": "1217" },
                { "name": "firefox", "revision": "1511" },
                { "name": "webkit", "revision": "2272" }
              ]
            }
            """);
        File.WriteAllText(Path.Combine(baseDir, ".version"), typeof(Microsoft.Playwright.Playwright).Assembly.GetName().Version?.ToString(3) ?? "1.52.0");
    }

    private static void CreateCompleteChromiumRuntime(string browserRoot, string revision)
    {
        foreach (string name in new[] { "chromium", "chromium_headless_shell" })
        {
            CreateCompleteRuntime(browserRoot, $"{name}-{revision}");
        }
    }

    private static void CreateCompleteRuntime(string browserRoot, string directoryName)
    {
        string directory = Path.Combine(browserRoot, directoryName);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "INSTALLATION_COMPLETE"), string.Empty);
    }

    private static byte[] CreateDriverArchive()
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            string nodeFile = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node";
            string platformId = PlatformExtensions.GetCurrentPlatform().ToPlatformId();
            using (var nodeStream = new StreamWriter(archive.CreateEntry($".playwright/node/{platformId}/{nodeFile}").Open()))
            {
                nodeStream.Write("node");
            }

            using (var licenseStream = new StreamWriter(archive.CreateEntry(".playwright/node/LICENSE").Open()))
            {
                licenseStream.Write("license");
            }

            using (var packageStream = new StreamWriter(archive.CreateEntry(".playwright/package/package.json").Open()))
            {
                packageStream.Write("{}");
            }

            using (var cliStream = new StreamWriter(archive.CreateEntry(".playwright/package/cli.js").Open()))
            {
                cliStream.Write("console.log('playwright');");
            }

            using (var browsersStream = new StreamWriter(archive.CreateEntry(".playwright/package/browsers.json").Open()))
            {
                browsersStream.Write("""
                    {
                      "browsers": [
                        { "name": "chromium", "revision": "1217" },
                        { "name": "chromium-headless-shell", "revision": "1217" }
                      ]
                    }
                    """);
            }
        }

        return memory.ToArray();
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly byte[] _content;

        public FakeHandler(byte[] content)
        {
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_content)
            };
            response.Content.Headers.ContentLength = _content.Length;
            return Task.FromResult(response);
        }
    }
}
