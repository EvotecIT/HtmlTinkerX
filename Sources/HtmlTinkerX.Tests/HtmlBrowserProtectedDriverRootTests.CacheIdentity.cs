#if !NETFRAMEWORK
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Xunit.Abstractions;

namespace HtmlTinkerX.Tests;

public partial class HtmlBrowserProtectedDriverRootTests
{
    private readonly ITestOutputHelper _identityOutput;

    public HtmlBrowserProtectedDriverRootTests(ITestOutputHelper output) => _identityOutput = output;

    [Fact]
    public void CacheIdentity_DarwinCaseAliasCanBeCleanedWithoutWeakeningFileSourceTrust()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || !Directory.Exists("/TMP"))
        {
            _identityOutput.WriteLine("NotExecuted: requires a case-insensitive Darwin compatibility root.");
            return;
        }
        using var fixture = new DriverRootFixture();
        string root = Path.Combine("/tmp", "htmltinkerx-case-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(root, "cache");
        Directory.CreateDirectory(path);
        string sentinel = Path.Combine(path, "preserve");
        File.WriteAllText(sentinel, "owned cache bytes");
        string alias = "/TMP" + path.Substring(4);
        try
        {
            Assert.False(HtmlBrowserFileSystemPath.TryResolveExistingPath(Path.Combine(alias, "preserve"), out _));
            var location = new HtmlBrowserCacheCleaner.CacheLocation { Path = alias, Size = 17 };

            var result = HtmlBrowserCacheCleaner.CleanCache(new[] { location });

            _identityOutput.WriteLine(JsonSerializer.Serialize(new { path, alias, result.Success, Failures = result.Failed.Select(item => item.Error) }));
            Assert.True(result.Success);
            Assert.Same(location, Assert.Single(result.SuccessfullyCleared));
            Assert.False(Directory.Exists(path));
            Assert.Equal(0, fixture.HttpCalls);
            Assert.Equal(0, fixture.InstallerCalls);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("root")]
    [InlineData("package")]
    public async Task CacheIdentity_PublicCleanerCannotDeleteBundledDriverPayload(string target)
    {
        using var fixture = new DriverRootFixture();
        using var bundle = await MinimalBundledLayout.CreateAsync(fixture, _identityOutput);
        string path = target == "root" ? bundle.Path : Path.Combine(bundle.Path, "package");
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", path);
        var before = bundle.Snapshot();
        var location = Assert.Single(HtmlBrowserCacheCleaner.GetCacheLocations(includeTemp: false), item => item.Path == path);

        var result = HtmlBrowserCacheCleaner.CleanCache(new[] { location });

        Assert.False(result.Success);
        Assert.Single(result.Failed);
        Assert.Empty(result.SuccessfullyCleared);
        bundle.AssertUnchanged(before);
        Assert.Equal(0, fixture.HttpCalls);
        Assert.Equal(0, fixture.InstallerCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CacheIdentity_EnsureAndRepairRejectBundledBrowserDestinationBeforeMutation(bool repair)
    {
        using var fixture = new DriverRootFixture();
        using var bundle = await MinimalBundledLayout.CreateAsync(fixture, _identityOutput);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", null);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", bundle.Path);
        var before = bundle.Snapshot();

        Exception? failure = await Record.ExceptionAsync(() => repair
            ? HtmlBrowser.RepairInstallationAsync(HtmlBrowserEngine.Firefox)
            : HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox));

        bundle.AssertUnchanged(before);
        Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal(0, fixture.HttpCalls);
        Assert.Equal(0, fixture.InstallerCalls);
    }

    [Fact]
    public async Task CacheIdentity_BundledAncestorIsRejectedWithoutInvokingDeletion()
    {
        using var fixture = new DriverRootFixture();
        using var bundle = await MinimalBundledLayout.CreateAsync(fixture, _identityOutput);
        var before = bundle.Snapshot();

        Assert.Throws<InvalidOperationException>(() => HtmlBrowser.ValidateCacheMutationPath(Path.GetDirectoryName(bundle.Path)!));

        bundle.AssertUnchanged(before);
    }

    [Fact]
    public async Task CacheIdentity_HermeticBrowserChildCanBeCleanedWhileBundledDriverRemainsReadOnly()
    {
        using var fixture = new DriverRootFixture();
        using var bundle = await MinimalBundledLayout.CreateAsync(fixture, _identityOutput);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", null);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", "0");
        string browsers = Path.Combine(bundle.Path, "package", ".local-browsers");
        Directory.CreateDirectory(browsers);
        File.WriteAllText(Path.Combine(browsers, "preserve"), "owned browser bytes");
        var location = Assert.Single(HtmlBrowserCacheCleaner.GetCacheLocations(includeTemp: false), item => item.Path == browsers);

        var result = HtmlBrowserCacheCleaner.CleanCache(new[] { location });

        Assert.True(result.Success);
        Assert.False(Directory.Exists(browsers));
        Assert.True(HtmlBrowser.HasDriverLayout(bundle.Path));
        Assert.Equal("minimal owned cli", File.ReadAllText(Path.Combine(bundle.Path, "package", "cli.js")));
        Assert.Equal(0, fixture.HttpCalls);
        Assert.Equal(0, fixture.InstallerCalls);
    }

    [Theory]
    [InlineData("discovery")]
    [InlineData("accounting")]
    [InlineData("runtime-preservation")]
    public async Task CacheIdentity_WindowsCaseSensitiveDirectoriesPreserveDistinctPhysicalFootprints(string mode)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _identityOutput.WriteLine("NotExecuted: requires native Windows per-directory case sensitivity.");
            return;
        }
        using var fixture = new DriverRootFixture();
        string root = Path.Combine(fixture.Root, "case-sensitive");
        Directory.CreateDirectory(root);
        using var enable = Process.Start(new ProcessStartInfo {
            FileName = "fsutil.exe", Arguments = "file setCaseSensitiveInfo \"" + root + "\" enable",
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        })!;
        Task<string> stdout = enable.StandardOutput.ReadToEndAsync();
        Task<string> stderr = enable.StandardError.ReadToEndAsync();
        if (!enable.WaitForExit(10000)) { enable.Kill(); throw new TimeoutException("Owned case-sensitivity setup timed out."); }
        _identityOutput.WriteLine(JsonSerializer.Serialize(new { mode, enable.ExitCode, Stdout = await stdout, Stderr = await stderr }));
        if (enable.ExitCode != 0)
        {
            _identityOutput.WriteLine("NotExecuted: this host cannot enable case sensitivity on the owned fixture.");
            return;
        }
        // fsutil can return zero even when the filesystem rejects the attribute.
        // Verify the fixture's capability independently of the path-identity code.
        string lowerProbe = Path.Combine(root, "case-probe");
        string upperProbe = Path.Combine(root, "CASE-PROBE");
        File.WriteAllText(lowerProbe, "lower");
        File.WriteAllText(upperProbe, "upper");
        bool caseSensitivityEnabled = File.ReadAllText(lowerProbe) == "lower"
            && File.ReadAllText(upperProbe) == "upper";
        File.Delete(lowerProbe);
        File.Delete(upperProbe);
        if (!caseSensitivityEnabled)
        {
            _identityOutput.WriteLine("NotExecuted: the owned fixture remains case insensitive after fsutil.");
            return;
        }
        if (mode == "runtime-preservation")
        {
            fixture.SetSearchRoot(fixture.Root, "root");
            File.WriteAllText(Path.Combine(fixture.Root, ".playwright", ".version"),
                typeof(Microsoft.Playwright.Playwright).Assembly.GetName().Version!.ToString(3));
            File.WriteAllText(Path.Combine(fixture.Root, ".playwright", "package", "browsers.json"), FirefoxManifest);
            string first = Path.Combine(root, "firefox-123");
            string second = Path.Combine(root, "FIREFOX-123");
            Directory.CreateDirectory(first);
            Directory.CreateDirectory(second);
            Assert.NotEqual(HtmlBrowserFileSystemPath.GetPathIdentity(first), HtmlBrowserFileSystemPath.GetPathIdentity(second));
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", root);

            await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);

            Assert.True(Directory.Exists(first));
            Assert.True(Directory.Exists(second));
            Assert.True(HtmlBrowser.VerifyCacheSlot(HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox), true, out _));
            Assert.Equal(1, fixture.InstallerCalls);
            Assert.Equal(0, fixture.HttpCalls);
            return;
        }
        fixture.SetSearchRoot(root, "root");
        string driver = Path.Combine(root, ".playwright");
        string browser = Path.Combine(root, ".PLAYWRIGHT");
        Directory.CreateDirectory(browser);
        File.WriteAllText(Path.Combine(browser, "preserve"), "ink");
        Assert.NotEqual(HtmlBrowserFileSystemPath.GetPathIdentity(driver), HtmlBrowserFileSystemPath.GetPathIdentity(browser));
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", browser);
        if (mode == "discovery")
        {
            var locations = HtmlBrowserCacheCleaner.GetCacheLocations(includeTemp: false);
            Assert.Single(locations, item => item.Path == driver);
            Assert.Single(locations, item => item.Path == browser);
            return;
        }
        string nested = Path.Combine(driver, "nested-cache");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "preserve"), "child");
        var outer = new HtmlBrowserCacheCleaner.CacheLocation { Path = browser, Size = 3 };
        var child = new HtmlBrowserCacheCleaner.CacheLocation { Path = nested, Size = 5 };
        var sibling = new HtmlBrowserCacheCleaner.CacheLocation { Path = driver, Size = 7 };
        Assert.Equal(10, HtmlBrowserCacheCleaner.GetEstimatedTotalSize(new[] { outer, sibling }));
        Assert.Equal(8, HtmlBrowserCacheCleaner.GetEstimatedTotalSize(new[] { outer, child }));

        var cleared = HtmlBrowserCacheCleaner.CleanCache(new[] { child, outer });

        Assert.True(cleared.Success);
        Assert.Equal(8, cleared.TotalSizeCleared);
    }

    /// <summary>Temporarily replaces only this test host's sealed SDK copy with a tiny owned layout.</summary>
    private sealed class MinimalBundledLayout : IDisposable
    {
        private readonly string _backup;
        private readonly Dictionary<string, string> _canonicalHashes;
        private readonly ITestOutputHelper _output;
        public string Path { get; }

        private MinimalBundledLayout(string path, ITestOutputHelper output)
        {
            Path = path;
            _output = output;
            _canonicalHashes = Snapshot();
            _output.WriteLine(JsonSerializer.Serialize(new { CanonicalTestHostCopy = path, Files = _canonicalHashes.Count, Hashes = _canonicalHashes }));
            _backup = path + "-owned-proof-" + Guid.NewGuid().ToString("N");
            Directory.Move(path, _backup);
            string node = System.IO.Path.Combine(path, "node", PlatformExtensions.GetCurrentPlatform().ToPlatformId());
            Directory.CreateDirectory(node);
            Directory.CreateDirectory(System.IO.Path.Combine(path, "package"));
            File.WriteAllText(System.IO.Path.Combine(node, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node"), "minimal owned node");
            File.WriteAllText(System.IO.Path.Combine(path, "package", "cli.js"), "minimal owned cli");
            File.WriteAllText(System.IO.Path.Combine(path, "package", "browsers.json"), "{\"browsers\":[]}");
            Assert.True(HtmlBrowser.HasDriverLayout(path));
        }

        public static async Task<MinimalBundledLayout> CreateAsync(DriverRootFixture fixture, ITestOutputHelper output) =>
            new(await GetCanonicalTestHostBundleAsync(fixture), output);

        public Dictionary<string, string> Snapshot() => Directory.GetFiles(Path, "*", SearchOption.AllDirectories)
            .ToDictionary(file => System.IO.Path.GetRelativePath(Path, file), HashFile);

        public void AssertUnchanged(Dictionary<string, string> before) =>
            Assert.Equal(before.OrderBy(item => item.Key), Snapshot().OrderBy(item => item.Key));

        public void Dispose()
        {
            try
            {
                // The canonical SDK is held in _backup; this path contains only the three minimal owned files.
                if (Directory.Exists(Path)) Directory.Delete(Path, true);
            }
            finally
            {
                MoveTestSdkDirectory(_backup, Path);
                AssertUnchanged(_canonicalHashes);
                _output.WriteLine("Canonical test-host SDK copy restored; all pre-move file hashes verified.");
            }
        }
    }
}
#endif
