using HtmlTinkerX;
using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlBrowserProtectedDriverRootTests
{
    private const string FirefoxManifest = "{\"browsers\":[{\"name\":\"firefox\",\"revision\":\"123\",\"installByDefault\":true}]}";

    [Theory]
    [InlineData(".nupkg.metadata")]
    [InlineData(".git")]
    public async Task Remediation_IncompleteAssemblyAdjacentDriverCannotBypassProtectedRoot(string markerName)
    {
        using var fixture = new DriverRootFixture();
        string output = Path.GetDirectoryName(typeof(HtmlBrowser).Assembly.Location)!;
        string bundle = Path.Combine(output, ".playwright");
        string backup = Path.Combine(output, ".playwright-proof-" + Guid.NewGuid().ToString("N"));
        string marker = Path.Combine(output, markerName);
        Assert.False(File.Exists(marker));
        Assert.False(Directory.Exists(marker));
        Assert.True(HtmlBrowser.HasDriverLayout(bundle));
        Directory.Move(bundle, backup); // Move only this disposable test host's SDK copy; never a real cache or checkout.
        int requests = 0;
        using var handler = new HtmlBrowserDriverPackageTests.FakeHandler(HtmlBrowserDriverPackageTests.CreateDriverPackage());
        try
        {
            Directory.CreateDirectory(bundle);
            string sentinel = Path.Combine(bundle, "incomplete-driver-sentinel");
            File.WriteAllText(sentinel, "package-or-source-owned bytes");
            File.WriteAllText(marker, markerName == ".git" ? "gitdir: ./fake-source-metadata" : "{}");
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", output);
            HtmlBrowser.HttpClientFactory = () =>
            {
                requests++;
                return new HttpClient(handler, disposeHandler: false);
            };

            Exception? failure = await Record.ExceptionAsync(() => HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox));

            Assert.True(File.Exists(sentinel), "The protected incomplete driver payload was replaced.");
            Assert.Equal("package-or-source-owned bytes", File.ReadAllText(sentinel));
            Assert.Equal("browser-owned bytes", File.ReadAllText(fixture.BrowserSentinel));
            Assert.Equal(0, requests);
            Assert.Equal(0, fixture.InstallerCalls);
            Assert.IsType<InvalidOperationException>(failure);
        }
        finally
        {
            File.Delete(marker);
            if (Directory.Exists(bundle)) Directory.Delete(bundle, true);
            Directory.Move(backup, bundle);
        }
    }

    [Fact]
    public async Task Remediation_BrowserCleanupCannotDeleteAnAncestorOfTheConfiguredPackageCache()
    {
        using var fixture = new DriverRootFixture();
        fixture.SetSearchRoot(fixture.Root, "root");
        File.WriteAllText(Path.Combine(fixture.Root, ".playwright", ".version"),
            typeof(Microsoft.Playwright.Playwright).Assembly.GetName().Version!.ToString(3));
        string browserRoot = Path.GetDirectoryName(fixture.BrowserSentinel)!;
        string cache = Path.Combine(browserRoot, "chromium-123", "dependency-state", "packages");
        Directory.CreateDirectory(cache);
        File.WriteAllText(Path.Combine(cache, "package-owned.dll"), "package-owned bytes");
        Environment.SetEnvironmentVariable("NUGET_PACKAGES", cache);
        var before = fixture.Snapshot();

        Exception? failure = await Record.ExceptionAsync(() => HtmlBrowser.RepairInstallationAsync(HtmlBrowserEngine.Chromium));

        fixture.AssertUnchanged(before);
        Assert.IsType<InvalidOperationException>(failure);
    }

    [Fact]
    public void Remediation_DefaultPackageCacheAncestorIsRejectedWithoutInvokingDeletion()
    {
        string cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        var failure = Assert.Throws<InvalidOperationException>(() =>
            HtmlBrowser.ValidateDriverMutationPath(Path.GetDirectoryName(cache)!));
        Assert.Contains("NuGet", failure.Message);
    }

    [Theory]
    [InlineData("metadata")]
    [InlineData("git-file")]
    [InlineData("git-directory")]
    [InlineData("cache-ancestor")]
    public void Remediation_PublicCacheCleanerPreservesProtectedLocationsAndReportsIndependentSuccess(string kind)
    {
        using var fixture = new DriverRootFixture();
        string root = Path.Combine(fixture.Root, "selected-cache");
        Directory.CreateDirectory(root);
        if (kind == "metadata") File.WriteAllText(Path.Combine(root, ".nupkg.metadata"), "{}");
        if (kind == "git-file") File.WriteAllText(Path.Combine(root, ".git"), "gitdir: ../fake-source-metadata");
        if (kind == "git-directory") Directory.CreateDirectory(Path.Combine(root, ".git"));
        string sentinel = Path.Combine(root, "preserve");
        File.WriteAllText(sentinel, "protected bytes");
        if (kind == "cache-ancestor")
        {
            string cache = Path.Combine(root, "nested", "packages");
            Directory.CreateDirectory(cache);
            Environment.SetEnvironmentVariable("NUGET_PACKAGES", cache);
        }
        string dedicated = Path.Combine(fixture.Root, "dedicated-cache");
        Directory.CreateDirectory(dedicated);
        File.WriteAllText(Path.Combine(dedicated, "disposable"), "owned cache");
        var protectedLocation = new HtmlBrowserCacheCleaner.CacheLocation { Path = root, Size = 1000 };
        var dedicatedLocation = new HtmlBrowserCacheCleaner.CacheLocation { Path = dedicated, Size = 123 };

        var result = HtmlBrowserCacheCleaner.CleanCache(new[] { protectedLocation, dedicatedLocation });

        Assert.True(File.Exists(sentinel), "The public cleaner deleted the protected location.");
        Assert.Equal("protected bytes", File.ReadAllText(sentinel));
        Assert.False(result.Success);
        Assert.Same(protectedLocation, Assert.Single(result.Failed).Location);
        Assert.Same(dedicatedLocation, Assert.Single(result.SuccessfullyCleared));
        Assert.Equal(123, result.TotalSizeCleared);
        Assert.False(Directory.Exists(dedicated));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Remediation_HermeticRuntimeDetectionUsesTheSelectedCustomOrDownloadedDriverPackage(bool download)
    {
        using var fixture = new DriverRootFixture();
        fixture.SetSearchRoot(fixture.Root, "root");
        string driver = Path.Combine(fixture.Root, ".playwright");
        if (download) Directory.Delete(driver, true);
        else
        {
            File.WriteAllText(Path.Combine(driver, ".version"), typeof(Microsoft.Playwright.Playwright).Assembly.GetName().Version!.ToString(3));
            File.WriteAllText(Path.Combine(driver, "package", "browsers.json"), FirefoxManifest);
        }
        string runtime = Path.Combine(driver, "package", ".local-browsers", "firefox-123");
        int requests = 0;
        int installs = 0;
        using var handler = new HtmlBrowserDriverPackageTests.FakeHandler(HtmlBrowserDriverPackageTests.CreateDriverPackage(FirefoxManifest));
        HtmlBrowser.HttpClientFactory = () => { requests++; return new HttpClient(handler, disposeHandler: false); };
        HtmlBrowser.PlaywrightInstaller = _ =>
        {
            installs++;
            Directory.CreateDirectory(runtime);
            File.WriteAllText(Path.Combine(runtime, "INSTALLATION_COMPLETE"), "fixture runtime");
        };
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", "0");

        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);

        Assert.Equal(download ? 1 : 0, requests);
        Assert.Equal(1, installs);
        Assert.True(File.Exists(Path.Combine(runtime, "INSTALLATION_COMPLETE")));
    }

    [Fact]
    public async Task Remediation_HermeticCleanupValidatesTheSelectedPackageLocalBrowserRoot()
    {
        using var fixture = new DriverRootFixture();
        fixture.SetSearchRoot(fixture.Root, "root");
        string driver = Path.Combine(fixture.Root, ".playwright");
        File.WriteAllText(Path.Combine(driver, ".version"), typeof(Microsoft.Playwright.Playwright).Assembly.GetName().Version!.ToString(3));
        string runtimeRoot = Path.Combine(driver, "package", ".local-browsers");
        Directory.CreateDirectory(runtimeRoot);
        File.WriteAllText(Path.Combine(runtimeRoot, ".nupkg.metadata"), "{}");
        File.WriteAllText(Path.Combine(runtimeRoot, "preserve"), "package-owned bytes");
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", "0");
        var before = fixture.Snapshot();

        Exception? failure = await Record.ExceptionAsync(() => HtmlBrowser.RepairInstallationAsync(HtmlBrowserEngine.Firefox));

        fixture.AssertUnchanged(before);
        Assert.IsType<InvalidOperationException>(failure);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("relative-browsers")]
    public async Task Remediation_InstallerForwardsTheResolvedPathToTheSelectedDriverCli(string browserSetting)
    {
        var installer = HtmlBrowser.PlaywrightInstaller;
        string? nodeOverride = Environment.GetEnvironmentVariable("PLAYWRIGHT_NODEJS_PATH");
        string? initialDirectory = Environment.GetEnvironmentVariable("INIT_CWD");
        using var fixture = new DriverRootFixture();
        fixture.SetSearchRoot(fixture.Root, "root");
        string driver = Path.Combine(fixture.Root, ".playwright");
        File.WriteAllText(Path.Combine(driver, ".version"), typeof(Microsoft.Playwright.Playwright).Assembly.GetName().Version!.ToString(3));
        string witness = Path.Combine(fixture.Root, "cli-browser-path.txt");
        string escaped = System.Text.Json.JsonSerializer.Serialize(witness);
        File.WriteAllText(Path.Combine(driver, "package", "cli.js"),
            $"require('fs').writeFileSync({escaped}, process.env.PLAYWRIGHT_BROWSERS_PATH);");
        string output = Path.GetDirectoryName(typeof(HtmlBrowser).Assembly.Location)!;
        string node = Path.Combine(output, ".playwright", "node", PlatformExtensions.GetCurrentPlatform().ToPlatformId(),
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node");
        HtmlBrowser.PlaywrightInstaller = installer;
        Environment.SetEnvironmentVariable("PLAYWRIGHT_NODEJS_PATH", node);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", browserSetting);
        Environment.SetEnvironmentVariable("INIT_CWD", fixture.Root);
        try
        {
            await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
            string expected = browserSetting == "0" ? Path.Combine(driver, "package", ".local-browsers")
                : Path.Combine(fixture.Root, browserSetting);
            Assert.Equal(expected, File.ReadAllText(witness));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_NODEJS_PATH", nodeOverride);
            Environment.SetEnvironmentVariable("INIT_CWD", initialDirectory);
        }
    }

#if !NETFRAMEWORK
    [Fact]
    public async Task Remediation_PhysicalPackageCacheAncestorIsProtectedThroughAConfiguredAlias()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        using var fixture = new DriverRootFixture();
        fixture.SetSearchRoot(fixture.Root, "root");
        File.WriteAllText(Path.Combine(fixture.Root, ".playwright", ".version"), typeof(Microsoft.Playwright.Playwright).Assembly.GetName().Version!.ToString(3));
        string browserRoot = Path.GetDirectoryName(fixture.BrowserSentinel)!;
        string cache = Path.Combine(browserRoot, "chromium-123", "nested", "packages");
        Directory.CreateDirectory(cache);
        File.WriteAllText(Path.Combine(cache, "preserve"), "package-owned bytes");
        string alias = Path.Combine(fixture.Root, "cache-alias");
        Directory.CreateSymbolicLink(alias, cache);
        Environment.SetEnvironmentVariable("NUGET_PACKAGES", alias);
        try
        {
            Exception? failure = await Record.ExceptionAsync(() => HtmlBrowser.RepairInstallationAsync(HtmlBrowserEngine.Chromium));
            Assert.True(File.Exists(Path.Combine(cache, "preserve")), "The physical package cache was deleted through its ancestor.");
            Assert.Equal(0, fixture.HttpCalls);
            Assert.Equal(0, fixture.InstallerCalls);
            Assert.IsType<InvalidOperationException>(failure);
        }
        finally
        {
            if (Directory.Exists(alias)) Directory.Delete(alias);
        }
    }
#endif
}
