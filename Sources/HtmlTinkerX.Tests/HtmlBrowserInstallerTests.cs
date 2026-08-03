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

[Collection("Playwright collection")]
public class HtmlBrowserInstallerTests
{
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
            CreateHealthyDriver(tempDriver);

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
            CreateHealthyDriver(tempDriver);

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
    public async Task EnsureInstalledAsync_ReplacesStaleBrowserRevision()
    {
        string tempBrowsers = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string tempDriver = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string? originalBrowsersPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        string? originalDriverPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");
        var originalInstaller = HtmlBrowser.PlaywrightInstaller;

        try
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", tempBrowsers);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", tempDriver);
            CreateHealthyDriver(tempDriver);
            CreateCompleteChromiumRuntime(tempBrowsers, "1194");

            string[]? captured = null;
            HtmlBrowser.PlaywrightInstaller = args => captured = args;

            await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);

            Assert.NotNull(captured);
            Assert.Contains("install", captured!);
            Assert.Contains("chromium", captured!);
        }
        finally
        {
            HtmlBrowser.PlaywrightInstaller = originalInstaller;
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", originalBrowsersPath);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", originalDriverPath);
            if (Directory.Exists(tempBrowsers)) Directory.Delete(tempBrowsers, true);
            if (Directory.Exists(tempDriver)) Directory.Delete(tempDriver, true);
        }
    }

    [Fact]
    public async Task EnsureInstalledAsync_SkipsCurrentCompleteBrowserRevision()
    {
        string tempBrowsers = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string tempDriver = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string? originalBrowsersPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        string? originalDriverPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");
        var originalInstaller = HtmlBrowser.PlaywrightInstaller;

        try
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", tempBrowsers);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", tempDriver);
            CreateHealthyDriver(tempDriver);
            CreateCompleteChromiumRuntime(tempBrowsers, "1217");

            HtmlBrowser.PlaywrightInstaller = _ => throw new InvalidOperationException("Current browser runtime should not be reinstalled.");

            await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);
        }
        finally
        {
            HtmlBrowser.PlaywrightInstaller = originalInstaller;
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", originalBrowsersPath);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", originalDriverPath);
            if (Directory.Exists(tempBrowsers)) Directory.Delete(tempBrowsers, true);
            if (Directory.Exists(tempDriver)) Directory.Delete(tempDriver, true);
        }
    }

    [Fact]
    public async Task EnsureInstalledAsync_UsesOnlyCurrentPlatformRevisionOverride()
    {
        string tempBrowsers = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string tempDriver = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string? originalBrowsersPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        string? originalDriverPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");
        string? originalHostOverride = Environment.GetEnvironmentVariable("PLAYWRIGHT_HOST_PLATFORM_OVERRIDE");
        var originalInstaller = HtmlBrowser.PlaywrightInstaller;

        try
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", tempBrowsers);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", tempDriver);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_HOST_PLATFORM_OVERRIDE", "ubuntu20.04-x64");
            CreateHealthyDriver(tempDriver);
            string packageDirectory = Path.Combine(tempDriver, ".playwright", "package");
            File.WriteAllText(Path.Combine(packageDirectory, "browsers.json"), """
                {
                  "browsers": [
                    {
                      "name": "webkit",
                      "revision": "2272",
                      "revisionOverrides": {
                        "debian11-x64": "2105",
                        "ubuntu20.04-x64": "2092"
                      }
                    }
                  ]
                }
                """);
            CreateCompleteRuntime(tempBrowsers, "webkit-2272");
            CreateCompleteRuntime(tempBrowsers, "webkit_debian11_x64_special-2105");

            string[]? captured = null;
            HtmlBrowser.PlaywrightInstaller = args => captured = args;

            await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.WebKit);

            Assert.NotNull(captured);
            Assert.Contains("webkit", captured!);

            CreateCompleteRuntime(tempBrowsers, "webkit_ubuntu20.04_x64_special-2092");
            HtmlBrowser.PlaywrightInstaller = _ => throw new InvalidOperationException("The current host override should be accepted.");

            await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.WebKit);
        }
        finally
        {
            HtmlBrowser.PlaywrightInstaller = originalInstaller;
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", originalBrowsersPath);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", originalDriverPath);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_HOST_PLATFORM_OVERRIDE", originalHostOverride);
            if (Directory.Exists(tempBrowsers)) Directory.Delete(tempBrowsers, true);
            if (Directory.Exists(tempDriver)) Directory.Delete(tempDriver, true);
        }
    }

    [Fact]
    public async Task EnsureInstalledAsync_CleansIncompleteCurrentPlatformRevisionOverride()
    {
        string tempBrowsers = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string tempDriver = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string? originalBrowsersPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        string? originalDriverPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");
        string? originalHostOverride = Environment.GetEnvironmentVariable("PLAYWRIGHT_HOST_PLATFORM_OVERRIDE");
        var originalInstaller = HtmlBrowser.PlaywrightInstaller;
        string overrideDirectory = Path.Combine(tempBrowsers, "webkit_ubuntu20.04_x64_special-2092");

        try
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", tempBrowsers);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", tempDriver);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_HOST_PLATFORM_OVERRIDE", "ubuntu20.04-x64");
            CreateHealthyDriver(tempDriver);
            File.WriteAllText(Path.Combine(tempDriver, ".playwright", "package", "browsers.json"), """
                {
                  "browsers": [
                    {
                      "name": "webkit",
                      "revision": "2272",
                      "revisionOverrides": { "ubuntu20.04-x64": "2092" }
                    }
                  ]
                }
                """);
            Directory.CreateDirectory(overrideDirectory);
            File.WriteAllText(Path.Combine(overrideDirectory, "partial-download"), "incomplete");

            HtmlBrowser.PlaywrightInstaller = _ =>
            {
                Assert.False(Directory.Exists(overrideDirectory));
                CreateCompleteRuntime(tempBrowsers, "webkit_ubuntu20.04_x64_special-2092");
            };

            await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.WebKit);

            Assert.True(File.Exists(Path.Combine(overrideDirectory, "INSTALLATION_COMPLETE")));
        }
        finally
        {
            HtmlBrowser.PlaywrightInstaller = originalInstaller;
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", originalBrowsersPath);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", originalDriverPath);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_HOST_PLATFORM_OVERRIDE", originalHostOverride);
            if (Directory.Exists(tempBrowsers)) Directory.Delete(tempBrowsers, true);
            if (Directory.Exists(tempDriver)) Directory.Delete(tempDriver, true);
        }
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

            await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);

            Assert.True(File.Exists(manifestPath));
            Assert.NotNull(captured);
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

            await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);

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

            await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);

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
        HtmlBrowser.HttpClientFactory = () => new HttpClient(new FakeHandler(CreateDriverArchive()));

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
