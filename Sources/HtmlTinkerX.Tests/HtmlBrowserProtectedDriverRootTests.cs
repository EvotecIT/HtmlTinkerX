using HtmlTinkerX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Text.Json;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Process diagnostics")]
public partial class HtmlBrowserProtectedDriverRootTests
{
    private static void MoveTestSdkDirectory(string source, string destination) {
        for (int attempt = 0; ; attempt++) {
            try { Directory.Move(source, destination); return; }
            catch (IOException) when (attempt < 20) { System.Threading.Thread.Sleep(250); }
            catch (UnauthorizedAccessException) when (attempt < 20) { System.Threading.Thread.Sleep(250); }
        }
    }
    [Theory]
    [InlineData("metadata", "root")]
    [InlineData("metadata", "child")]
    [InlineData("metadata", "trailing-child")]
    [InlineData("legacy-metadata", "root")]
    [InlineData("configured-cache", "root")]
    [InlineData("configured-cache-child", "root")]
    [InlineData("configured-cache-absent", "root")]
    [InlineData("metadata-descendant", "root")]
    public async Task EnsureInstalledAsync_RejectsPackageDestinationsBeforeChangingMissingMarkerPayload(
        string kind, string alias)
    {
        using var fixture = new DriverRootFixture();
        fixture.SelectProtectedRoot(kind, alias);
        var before = fixture.Snapshot();

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium));

        Assert.Contains("PLAYWRIGHT_DRIVER_SEARCH_PATH", failure.Message);
        fixture.AssertUnchanged(before);
    }

    [Theory]
    [InlineData("git-directory", "repair")]
    [InlineData("git-file", "repair")]
    [InlineData("metadata", "repair")]
    [InlineData("metadata", "clean")]
    [InlineData("git-file", "clean")]
    public async Task RepairAndClean_RejectProtectedRootsBeforeDeletingBrowserState(string kind, string operation)
    {
        using var fixture = new DriverRootFixture();
        fixture.SelectProtectedRoot(kind, "trailing-child");
        var before = fixture.Snapshot();

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => operation == "repair"
            ? HtmlBrowser.RepairInstallationAsync(HtmlBrowserEngine.Chromium)
            : HtmlBrowser.CleanInstallationAsync());

        Assert.Contains("PLAYWRIGHT_DRIVER_SEARCH_PATH", failure.Message);
        fixture.AssertUnchanged(before);
    }

    [Theory]
    [InlineData("git-directory")]
    [InlineData("git-file")]
    [InlineData("cache-prefix-sibling")]
    public void CleanDriver_PreservesDedicatedApplicationRootsAndTheirSiblings(string kind)
    {
        using var fixture = new DriverRootFixture();
        string root = fixture.Root;
        if (kind == "cache-prefix-sibling")
        {
            root = fixture.NuGetRoot + "-driver";
        }
        else
        {
            if (kind == "git-directory") Directory.CreateDirectory(Path.Combine(root, ".git"));
            else File.WriteAllText(Path.Combine(root, ".git"), "gitdir: ../metadata");
            root = Path.Combine(root, "bin", "app", "ms-playwright-driver");
        }
        fixture.SetSearchRoot(root, "child");
        string sibling = Path.Combine(root, "application.json");
        File.WriteAllText(sibling, "caller-owned settings");

        HtmlBrowser.CleanDriver();

        Assert.False(Directory.Exists(Path.Combine(root, ".playwright")));
        Assert.Equal("caller-owned settings", File.ReadAllText(sibling));
        Assert.Equal(root, Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH"));
    }

    [Fact]
    public void DefaultNuGetCache_IsRejectedByValidationWithoutInvokingCleanup()
    {
        string cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        Assert.Throws<InvalidOperationException>(() =>
            HtmlBrowser.ValidateDriverMutationPath(Path.Combine(cache, "package", "1.0.0", ".playwright")));
    }

    [Fact]
    public void CleanDriver_DoesNotSwallowAProtectedRootRejection()
    {
        using var fixture = new DriverRootFixture();
        fixture.SelectProtectedRoot("metadata", "root");
        var before = fixture.Snapshot();

        Assert.Throws<InvalidOperationException>(() => HtmlBrowser.CleanDriver());

        fixture.AssertUnchanged(before);
    }

    [Theory]
    [InlineData("metadata", "ensure")]
    [InlineData("configured-cache", "repair")]
    [InlineData("configured-cache-child", "ensure")]
    [InlineData("git-file", "clean")]
    [InlineData("git-directory", "ensure")]
    public async Task BrowserMutations_RejectProtectedRootsBeforeChangingEitherInstallation(string kind, string operation)
    {
        using var fixture = new DriverRootFixture();
        fixture.SetSearchRoot(fixture.Root, "root");
        string version = typeof(Microsoft.Playwright.Playwright).Assembly.GetName().Version!.ToString(3);
        File.WriteAllText(Path.Combine(fixture.Root, ".playwright", ".version"), version);
        fixture.SelectProtectedBrowserRoot(kind);
        var before = fixture.Snapshot();

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => operation == "repair"
            ? HtmlBrowser.RepairInstallationAsync(HtmlBrowserEngine.Chromium)
            : operation == "clean" ? HtmlBrowser.CleanInstallationAsync()
            : HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium));

        Assert.Contains("PLAYWRIGHT_BROWSERS_PATH", failure.Message);
        fixture.AssertUnchanged(before);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BundledDriver_RemainsReadOnlyWhenReusedFromAProtectedLocation(bool hermetic)
    {
        using var fixture = new DriverRootFixture();
        string? driver = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");
        string? browsers = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        string? nuget = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        var factory = HtmlBrowser.HttpClientFactory;
        string bundled = await GetCanonicalTestHostBundleAsync(fixture);
        string assemblyRoot = Path.GetDirectoryName(bundled)!;
        string node = Path.Combine(bundled, "node", PlatformExtensions.GetCurrentPlatform().ToPlatformId(),
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node");
        string cli = Path.Combine(bundled, "package", "cli.js");
        string nodeHash = HashFile(node);
        string cliHash = HashFile(cli);
        try
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", null);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", hermetic ? "0" : null);
            // The canonical test-host bundle is disposable output, never the real package cache.
            Environment.SetEnvironmentVariable("NUGET_PACKAGES", assemblyRoot);
            HtmlBrowser.HttpClientFactory = () => throw new InvalidOperationException("Bundled reuse must not download.");

            await HtmlBrowser.EnsureDriverInstalledAsync();
            HtmlBrowser.CleanDriver();

            Assert.Equal(nodeHash, HashFile(node));
            Assert.Equal(cliHash, HashFile(cli));
            Assert.Equal(assemblyRoot, Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH"));
        }
        finally
        {
            HtmlBrowser.HttpClientFactory = factory;
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", driver);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", browsers);
            Environment.SetEnvironmentVariable("NUGET_PACKAGES", nuget);
        }
    }

    private static async Task<string> GetCanonicalTestHostBundleAsync(DriverRootFixture fixture)
    {
        string? search = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");
        string? browsers = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        var factory = HtmlBrowser.HttpClientFactory;
        try
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", null);
            // If the SDK bundle is absent, any fallback stays inside this disposable fixture and cannot download.
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", Path.GetDirectoryName(fixture.BrowserSentinel));
            HtmlBrowser.HttpClientFactory = () => throw new InvalidOperationException("The canonical test-host SDK bundle must be present; no download is allowed.");
            await HtmlBrowser.EnsureDriverInstalledAsync();
            return Path.Combine(Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH")!, ".playwright");
        }
        finally
        {
            HtmlBrowser.HttpClientFactory = factory;
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", search);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", browsers);
        }
    }

    private static string HashFile(string path)
    {
        using var hash = SHA256.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToBase64String(hash.ComputeHash(stream));
    }

    [Fact]
    public async Task EnsureInstalledAsync_RejectsACheckoutInTheSelectedRuntimeDirectoryBeforeCleanup()
    {
        using var fixture = new DriverRootFixture();
        fixture.SetSearchRoot(fixture.Root, "root");
        File.WriteAllText(Path.Combine(fixture.Root, ".playwright", ".version"),
            typeof(Microsoft.Playwright.Playwright).Assembly.GetName().Version!.ToString(3));
        fixture.SelectProtectedRuntimeChild();
        var before = fixture.Snapshot();

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium));

        Assert.Contains("PLAYWRIGHT_BROWSERS_PATH", failure.Message);
        fixture.AssertUnchanged(before);
    }

#if !NETFRAMEWORK
    [Fact]
    public async Task ConfiguredNuGetCacheAlias_ProtectsItsPhysicalDestination()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        using var fixture = new DriverRootFixture();
        fixture.SetSearchRoot(fixture.NuGetRoot, "root");
        string link = Path.Combine(fixture.Root, "cache-alias");
        Directory.CreateSymbolicLink(link, fixture.NuGetRoot);
        Environment.SetEnvironmentVariable("NUGET_PACKAGES", link);
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium));
            Assert.True(HtmlBrowser.HasDriverLayout(Path.Combine(fixture.NuGetRoot, ".playwright")));
            Assert.Equal(0, fixture.HttpCalls);
            Assert.Equal(0, fixture.InstallerCalls);
            Assert.Equal("browser-owned bytes", File.ReadAllText(fixture.BrowserSentinel));
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    [Fact]
    public async Task BrowserMutation_RejectsASymbolicLinkBeforeChangingTheValidDriver()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        using var fixture = new DriverRootFixture();
        fixture.SetSearchRoot(fixture.Root, "root");
        string versionFile = Path.Combine(fixture.Root, ".playwright", ".version");
        File.WriteAllText(versionFile, typeof(Microsoft.Playwright.Playwright).Assembly.GetName().Version!.ToString(3));
        fixture.SelectProtectedBrowserRoot("metadata");
        string target = Path.GetDirectoryName(fixture.BrowserSentinel)!;
        string link = Path.Combine(fixture.Root, "browser-alias");
        Directory.CreateSymbolicLink(link, target);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", link);
        try
        {
            await Assert.ThrowsAsync<ArgumentException>(() => HtmlBrowser.RepairInstallationAsync(HtmlBrowserEngine.Chromium));
            Assert.True(HtmlBrowser.HasDriverLayout(Path.Combine(fixture.Root, ".playwright")));
            Assert.True(File.Exists(versionFile));
            Assert.Equal("browser-owned bytes", File.ReadAllText(fixture.BrowserSentinel));
            Assert.Equal(0, fixture.HttpCalls);
            Assert.Equal(0, fixture.InstallerCalls);
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EnsureInstalledAsync_RejectsSymbolicLinkDriverDestinations(bool linkIsDriverChild)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return; // Creating Windows links requires host privileges.
        using var fixture = new DriverRootFixture();
        string target = Path.Combine(fixture.Root, "package");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, ".nupkg.metadata"), "{}");
        File.WriteAllText(Path.Combine(target, "preserve"), "package-owned bytes");
        string link = Path.Combine(fixture.Root, linkIsDriverChild ? ".playwright" : "alias");
        Directory.CreateSymbolicLink(link, target);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", linkIsDriverChild ? fixture.Root : link);

        try
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium));
            Assert.Equal("package-owned bytes", File.ReadAllText(Path.Combine(target, "preserve")));
            Assert.Equal(0, fixture.HttpCalls);
            Assert.Equal(0, fixture.InstallerCalls);
            Assert.Equal("browser-owned bytes", File.ReadAllText(fixture.BrowserSentinel));
        }
        finally
        {
            if (Directory.Exists(link)) Directory.Delete(link);
        }
    }
#endif

    private sealed class DriverRootFixture : IDisposable
    {
        private readonly string? _driver = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");
        private readonly string? _browsers = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        private readonly string? _nuget = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        private readonly Func<HttpClient> _factory = HtmlBrowser.HttpClientFactory;
        private readonly Action<string[]> _installer = HtmlBrowser.PlaywrightInstaller;
        private readonly Func<string, HtmlBrowserEngine, Task>? _cacheStage = HtmlBrowser.CacheStageInstaller;
        private readonly Func<string, HtmlBrowserEngine, Task>? _cacheSmoke = HtmlBrowser.CacheSmokeTest;
        private readonly string _container = Path.Combine(Path.GetTempPath(), "protected-driver-" + Guid.NewGuid().ToString("N"));

        public string Root { get; }
        public string NuGetRoot { get; }
        public string BrowserSentinel { get; private set; }
        public int HttpCalls { get; private set; }
        public int InstallerCalls { get; private set; }

        public DriverRootFixture()
        {
            Root = Path.Combine(_container, "application");
            NuGetRoot = Path.Combine(_container, "packages");
            BrowserSentinel = Path.Combine(_container, "browsers", "preserve");
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(Path.GetDirectoryName(BrowserSentinel)!);
            File.WriteAllText(BrowserSentinel, "browser-owned bytes");
            Environment.SetEnvironmentVariable("NUGET_PACKAGES", NuGetRoot);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", Path.GetDirectoryName(BrowserSentinel));
            HtmlBrowser.HttpClientFactory = () =>
            {
                HttpCalls++;
                throw new InvalidOperationException("A rejected driver root must not download a package.");
            };
            HtmlBrowser.PlaywrightInstaller = _ => InstallerCalls++;
            HtmlBrowser.CacheStageInstaller = (path, engine) => {
                InstallerCalls++;
                var entries = JsonDocument.Parse(File.ReadAllText(Path.Combine(Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH")!, ".playwright", "package", "browsers.json"))).RootElement.GetProperty("browsers");
                string revision = entries.EnumerateArray().First(entry => entry.GetProperty("name").GetString() == engine.ToString().ToLowerInvariant()).GetProperty("revision").GetString()!;
                return HtmlBrowserCacheResilienceTests.CacheFixture.CreateStageAsync(path, engine, revision);
            };
            HtmlBrowser.CacheSmokeTest = (_, _) => Task.CompletedTask;
        }

        public void SelectProtectedRoot(string kind, string alias)
        {
            string root = kind == "configured-cache" ? NuGetRoot
                : kind.StartsWith("configured-cache-", StringComparison.Ordinal) ? Path.Combine(NuGetRoot, "microsoft.playwright", "1.63.0")
                : kind == "metadata-descendant" ? Path.Combine(Root, "payload", "application")
                : Root;
            SetSearchRoot(root, alias);
            if (kind == "metadata") File.WriteAllText(Path.Combine(root, ".nupkg.metadata"), "{}");
            if (kind == "metadata-descendant") File.WriteAllText(Path.Combine(Root, ".nupkg.metadata"), "{}");
            if (kind == "configured-cache-absent") Directory.Delete(Path.Combine(root, ".playwright"), true);
            if (kind == "legacy-metadata")
            {
                File.WriteAllText(Path.Combine(root, "package.nuspec"), "<package />");
                File.WriteAllText(Path.Combine(root, "package.nupkg.sha512"), "package-hash");
            }
            if (kind == "git-directory") Directory.CreateDirectory(Path.Combine(root, ".git"));
            if (kind == "git-file") File.WriteAllText(Path.Combine(root, ".git"), "gitdir: ../metadata");
            File.WriteAllText(Path.Combine(root, "package-or-source.dll"), "caller-owned bytes");
        }

        public void SelectProtectedBrowserRoot(string kind)
        {
            string root = kind == "configured-cache" ? NuGetRoot
                : kind == "configured-cache-child" ? Path.Combine(NuGetRoot, "package", "1.0.0")
                : Path.Combine(_container, "protected-browsers");
            Directory.CreateDirectory(root);
            if (kind == "metadata") File.WriteAllText(Path.Combine(root, ".nupkg.metadata"), "{}");
            if (kind == "git-directory") Directory.CreateDirectory(Path.Combine(root, ".git"));
            if (kind == "git-file") File.WriteAllText(Path.Combine(root, ".git"), "gitdir: ../metadata");
            BrowserSentinel = Path.Combine(root, "preserve");
            File.WriteAllText(BrowserSentinel, "browser-owned bytes");
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", root + Path.DirectorySeparatorChar);
        }

        public void SelectProtectedRuntimeChild()
        {
            File.WriteAllText(Path.Combine(Root, ".playwright", "package", "browsers.json"),
                "{\"browsers\":[{\"name\":\"chromium\",\"revision\":\"123\",\"installByDefault\":true}]}");
            string runtime = Path.Combine(Path.GetDirectoryName(BrowserSentinel)!, "chromium-123");
            Directory.CreateDirectory(runtime);
            File.WriteAllText(Path.Combine(runtime, ".git"), "gitdir: ../source-metadata");
            BrowserSentinel = Path.Combine(runtime, "preserve");
            File.WriteAllText(BrowserSentinel, "source-owned bytes");
        }

        public void SetSearchRoot(string root, string alias)
        {
            string driver = Path.Combine(root, ".playwright");
            string node = Path.Combine(driver, "node", PlatformExtensions.GetCurrentPlatform().ToPlatformId());
            Directory.CreateDirectory(node);
            Directory.CreateDirectory(Path.Combine(driver, "package"));
            File.WriteAllText(Path.Combine(node, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node"), "node");
            File.WriteAllText(Path.Combine(driver, "package", "cli.js"), "cli");
            File.WriteAllText(Path.Combine(driver, "package", "browsers.json"), "{\"browsers\":[]}");
            // Official package payloads do not contain HtmlTinkerX's downloaded-driver .version marker.
            string search = alias == "root" ? root : driver;
            if (alias == "trailing-child") search += Path.DirectorySeparatorChar;
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", search);
        }

        public Dictionary<string, string> Snapshot() => Directory.GetFiles(_container, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, HashFile);

        public void AssertUnchanged(Dictionary<string, string> before)
        {
            Assert.Equal(before.OrderBy(x => x.Key), Snapshot().OrderBy(x => x.Key));
            Assert.Equal(0, HttpCalls);
            Assert.Equal(0, InstallerCalls);
            Assert.Empty(Directory.GetDirectories(_container, ".playwright-install-*", SearchOption.AllDirectories));
        }

        public void Dispose()
        {
            HtmlBrowser.HttpClientFactory = _factory;
            HtmlBrowser.PlaywrightInstaller = _installer;
            HtmlBrowser.CacheStageInstaller = _cacheStage;
            HtmlBrowser.CacheSmokeTest = _cacheSmoke;
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", _driver);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", _browsers);
            Environment.SetEnvironmentVariable("NUGET_PACKAGES", _nuget);
            if (Directory.Exists(_container)) Directory.Delete(_container, true);
        }
    }
}
