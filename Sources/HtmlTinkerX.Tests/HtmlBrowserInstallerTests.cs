using HtmlTinkerX;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Playwright collection")]
public class HtmlBrowserInstallerTests
{
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
    }

    [Fact]
    public async Task EnsureInstalledAsync_InstallsDepsOnLinux_WhenEnabled()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return;
        }

        string tempBrowsers = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string tempDriver = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", tempBrowsers);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", tempDriver);
        Environment.SetEnvironmentVariable("HTMLTINKERX_PLAYWRIGHT_WITH_DEPS", "1");

        var originalInstaller = HtmlBrowser.PlaywrightInstaller;

        try
        {
            // prepare fake driver so IsDriverPresent returns true
            string baseDir = Path.Combine(tempDriver, ".playwright");
            string platformId = PlatformExtensions.GetCurrentPlatform().ToPlatformId();
            string nodeDir = Path.Combine(baseDir, "node", platformId);
            Directory.CreateDirectory(nodeDir);
            Directory.CreateDirectory(Path.Combine(baseDir, "package"));
            File.WriteAllText(Path.Combine(nodeDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node"), "");
            File.WriteAllText(Path.Combine(baseDir, ".version"), typeof(Microsoft.Playwright.Playwright).Assembly.GetName().Version?.ToString(3) ?? "1.52.0");

            string[]? captured = null;
            HtmlBrowser.PlaywrightInstaller = args => captured = args;

            await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);

            Assert.NotNull(captured);
            Assert.Contains("--with-deps", captured);
        }
        finally
        {
            HtmlBrowser.PlaywrightInstaller = originalInstaller;
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", null);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", null);
            Environment.SetEnvironmentVariable("HTMLTINKERX_PLAYWRIGHT_WITH_DEPS", null);
            if (Directory.Exists(tempBrowsers)) Directory.Delete(tempBrowsers, true);
            if (Directory.Exists(tempDriver)) Directory.Delete(tempDriver, true);
        }
    }

    [Fact]
    public async Task EnsureInstalledAsync_DoesNotInstallDepsOnLinux_WhenDisabled()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return;
        }

        string tempBrowsers = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string tempDriver = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", tempBrowsers);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", tempDriver);
        Environment.SetEnvironmentVariable("HTMLTINKERX_PLAYWRIGHT_WITH_DEPS", "0");

        var originalInstaller = HtmlBrowser.PlaywrightInstaller;

        try
        {
            // prepare fake driver so IsDriverPresent returns true
            string baseDir = Path.Combine(tempDriver, ".playwright");
            string platformId = PlatformExtensions.GetCurrentPlatform().ToPlatformId();
            string nodeDir = Path.Combine(baseDir, "node", platformId);
            Directory.CreateDirectory(nodeDir);
            Directory.CreateDirectory(Path.Combine(baseDir, "package"));
            File.WriteAllText(Path.Combine(nodeDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node"), "");
            File.WriteAllText(Path.Combine(baseDir, ".version"), typeof(Microsoft.Playwright.Playwright).Assembly.GetName().Version?.ToString(3) ?? "1.52.0");

            string[]? captured = null;
            HtmlBrowser.PlaywrightInstaller = args => captured = args;

            await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);

            Assert.NotNull(captured);
            Assert.DoesNotContain("--with-deps", captured);
        }
        finally
        {
            HtmlBrowser.PlaywrightInstaller = originalInstaller;
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", null);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", null);
            Environment.SetEnvironmentVariable("HTMLTINKERX_PLAYWRIGHT_WITH_DEPS", null);
            if (Directory.Exists(tempBrowsers)) Directory.Delete(tempBrowsers, true);
            if (Directory.Exists(tempDriver)) Directory.Delete(tempDriver, true);
        }
    }

    [Fact]
    public async Task EnsureInstalledAsync_ReinstallsCorruptedDriver()
    {
        string tempBrowsers = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string tempDriver = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", tempBrowsers);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", tempDriver);

        string baseDir = Path.Combine(tempDriver, ".playwright");
        string platformId = PlatformExtensions.GetCurrentPlatform().ToPlatformId();
        string nodeDir = Path.Combine(baseDir, "node", platformId);
        Directory.CreateDirectory(nodeDir);
        Directory.CreateDirectory(Path.Combine(baseDir, "package"));
        File.WriteAllText(Path.Combine(nodeDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node"), string.Empty);

        string[]? captured = null;
        string? runtimeDirectory = null;
        var originalInstaller = HtmlBrowser.PlaywrightInstaller;
        var originalFactory = HtmlBrowser.HttpClientFactory;

        HtmlBrowser.PlaywrightInstaller = args =>
        {
            captured = args;
            runtimeDirectory = Path.Combine(tempBrowsers, "chromium-reinstalled");
            Directory.CreateDirectory(runtimeDirectory);
            File.WriteAllText(Path.Combine(runtimeDirectory, "marker.txt"), "installed");
        };
        HtmlBrowser.HttpClientFactory = () => new HttpClient(new FakeHandler(CreateDriverArchive()))
        {
            BaseAddress = new Uri("https://playwright.azureedge.net/")
        };

        try
        {
            await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);

            Assert.NotNull(captured);
            Assert.Contains("chromium", captured!);
            string nodePath = Path.Combine(baseDir, "node", platformId, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node");
            Assert.True(new FileInfo(nodePath).Length > 0, "Expected node binary to be replaced during reinstall.");
            string version = File.ReadAllText(Path.Combine(baseDir, ".version"));
            Assert.Equal(typeof(Microsoft.Playwright.Playwright).Assembly.GetName().Version?.ToString(3) ?? "1.52.0", version);
            Assert.NotNull(runtimeDirectory);
            Assert.True(Directory.Exists(runtimeDirectory!));
            Assert.True(File.Exists(Path.Combine(runtimeDirectory!, "marker.txt")));
        }
        finally
        {
            HtmlBrowser.PlaywrightInstaller = originalInstaller;
            HtmlBrowser.HttpClientFactory = originalFactory;
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", null);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", null);
            if (Directory.Exists(tempBrowsers)) Directory.Delete(tempBrowsers, true);
            if (Directory.Exists(tempDriver)) Directory.Delete(tempDriver, true);
        }
    }

    [Fact]
    public async Task EnsureInstalledAsync_RepairsEmptyRuntimeDirectories()
    {
        string tempBrowsers = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string tempDriver = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", tempBrowsers);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", tempDriver);

        var originalInstaller = HtmlBrowser.PlaywrightInstaller;
        var originalFactory = HtmlBrowser.HttpClientFactory;

        string initialRuntime = Path.Combine(tempBrowsers, "chromium-initial");
        HtmlBrowser.PlaywrightInstaller = _ =>
        {
            Directory.CreateDirectory(initialRuntime);
            File.WriteAllText(Path.Combine(initialRuntime, "marker.txt"), "installed");
        };
        HtmlBrowser.HttpClientFactory = () => new HttpClient(new FakeHandler(CreateDriverArchive()));

        try
        {
            await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);

            string corruptedRuntime = Directory.GetDirectories(tempBrowsers).First(dir => Path.GetFileName(dir).StartsWith("chromium-", StringComparison.OrdinalIgnoreCase));
            foreach (string entry in Directory.GetFileSystemEntries(corruptedRuntime))
            {
                if (Directory.Exists(entry))
                {
                    Directory.Delete(entry, true);
                }
                else
                {
                    File.Delete(entry);
                }
            }

            string freshRuntime = Path.Combine(tempBrowsers, "chromium-fresh");
            HtmlBrowser.PlaywrightInstaller = _ =>
            {
                if (Directory.Exists(corruptedRuntime))
                {
                    Directory.Delete(corruptedRuntime, true);
                }

                Directory.CreateDirectory(freshRuntime);
                File.WriteAllText(Path.Combine(freshRuntime, "marker.txt"), "installed");
            };

            await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);

            Assert.False(Directory.Exists(corruptedRuntime));
            Assert.True(Directory.Exists(freshRuntime));
            Assert.True(File.Exists(Path.Combine(freshRuntime, "marker.txt")));
        }
        finally
        {
            HtmlBrowser.PlaywrightInstaller = originalInstaller;
            HtmlBrowser.HttpClientFactory = originalFactory;
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", null);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", null);
            if (Directory.Exists(tempBrowsers)) Directory.Delete(tempBrowsers, true);
            if (Directory.Exists(tempDriver)) Directory.Delete(tempDriver, true);
        }
    }

    [Fact]
    public async Task RepairInstallationAsync_ReinstallsDriverAndRuntime()
    {
        string tempBrowsers = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string tempDriver = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", tempBrowsers);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", tempDriver);

        var originalInstaller = HtmlBrowser.PlaywrightInstaller;
        var originalFactory = HtmlBrowser.HttpClientFactory;

        Directory.CreateDirectory(Path.Combine(tempDriver, ".playwright", "node"));
        Directory.CreateDirectory(Path.Combine(tempBrowsers, "chromium-old"));

        string? runtimeDirectory = null;
        HtmlBrowser.PlaywrightInstaller = args =>
        {
            runtimeDirectory = Path.Combine(tempBrowsers, "chromium-repaired");
            Directory.CreateDirectory(runtimeDirectory);
            File.WriteAllText(Path.Combine(runtimeDirectory, "marker.txt"), string.Join(" ", args));
        };
        HtmlBrowser.HttpClientFactory = () => new HttpClient(new FakeHandler(CreateDriverArchive()));

        try
        {
            await HtmlBrowser.RepairInstallationAsync(HtmlBrowserEngine.Chromium);

            Assert.NotNull(runtimeDirectory);
            Assert.True(Directory.Exists(runtimeDirectory!));
            Assert.True(File.Exists(Path.Combine(runtimeDirectory!, "marker.txt")));

            string baseDir = Path.Combine(tempDriver, ".playwright");
            string nodePath = Path.Combine(baseDir, "node", PlatformExtensions.GetCurrentPlatform().ToPlatformId(), RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node");
            Assert.True(File.Exists(nodePath));
        }
        finally
        {
            HtmlBrowser.PlaywrightInstaller = originalInstaller;
            HtmlBrowser.HttpClientFactory = originalFactory;
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", null);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", null);
            if (Directory.Exists(tempBrowsers)) Directory.Delete(tempBrowsers, true);
            if (Directory.Exists(tempDriver)) Directory.Delete(tempDriver, true);
        }
    }

    private static byte[] CreateDriverArchive()
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            string nodeFile = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node";
            using (var nodeStream = new StreamWriter(archive.CreateEntry(nodeFile).Open()))
            {
                nodeStream.Write("node");
            }

            using (var licenseStream = new StreamWriter(archive.CreateEntry("LICENSE").Open()))
            {
                licenseStream.Write("license");
            }

            using (var packageStream = new StreamWriter(archive.CreateEntry("package/package.json").Open()))
            {
                packageStream.Write("{}");
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
