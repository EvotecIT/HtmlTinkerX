#if !NETFRAMEWORK
using HtmlTinkerX;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlBrowserProtectedDriverRootTests
{
    [Theory]
    [InlineData("absolute")]
    [InlineData("relative-init")]
    [InlineData("relative-current")]
    [InlineData("hermetic")]
    public void CacheDiscovery_SelectedBrowserLocationCanBeCleanedWithoutTouchingDriverOrOtherCaches(string mode)
    {
        string? initialDirectory = Environment.GetEnvironmentVariable("INIT_CWD");
        using var fixture = new DriverRootFixture();
        fixture.SetSearchRoot(fixture.Root, "root");
        string driver = Path.Combine(fixture.Root, ".playwright");
        string cache = mode == "hermetic" ? Path.Combine(driver, "package", ".local-browsers")
            : Path.Combine(fixture.Root, "selected-browsers");
        Directory.CreateDirectory(cache);
        File.WriteAllText(Path.Combine(cache, "preserve"), "selected cache bytes");
        Environment.SetEnvironmentVariable("INIT_CWD", mode == "relative-init" ? fixture.Root : null);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", mode == "hermetic" ? "0"
            : mode == "relative-init" ? "selected-browsers"
            : mode == "relative-current" ? Path.GetRelativePath(Environment.CurrentDirectory, cache)
            : cache + Path.DirectorySeparatorChar);
        try
        {
            var locations = HtmlBrowserCacheCleaner.GetCacheLocations(includeTemp: false);
            var location = Assert.Single(locations,
                item => string.Equals(Path.GetFullPath(item.Path), cache, StringComparison.Ordinal));
            Assert.Equal("selected cache bytes".Length, location.Size);
            string legacy = GetExpectedLegacyBrowserCache();
            Assert.Equal(Directory.Exists(legacy) ? 1 : 0, locations.Count(item => item.Path == legacy));
            Assert.Empty(HtmlBrowserCacheCleaner.GetCacheLocations(includeBrowsers: false, includeTemp: false));

            var result = HtmlBrowserCacheCleaner.CleanCache(new[] { location });

            Assert.True(result.Success);
            Assert.Single(result.SuccessfullyCleared);
            Assert.False(Directory.Exists(cache));
            Assert.Equal("cli", File.ReadAllText(Path.Combine(driver, "package", "cli.js")));
            Assert.Equal("browser-owned bytes", File.ReadAllText(fixture.BrowserSentinel));
            Assert.Equal(0, fixture.HttpCalls);
            Assert.Equal(0, fixture.InstallerCalls);
        }
        finally
        {
            Environment.SetEnvironmentVariable("INIT_CWD", initialDirectory);
        }
    }

    [Theory]
    [InlineData(".git")]
    [InlineData(".nupkg.metadata")]
    public void CacheDiscovery_ProtectedSelectedCacheIsReportedAndCannotBeCleared(string marker)
    {
        using var fixture = new DriverRootFixture();
        fixture.SetSearchRoot(fixture.Root, "root");
        string cache = Path.Combine(fixture.Root, "selected-browsers");
        Directory.CreateDirectory(cache);
        File.WriteAllText(Path.Combine(cache, marker), "protected metadata");
        File.WriteAllText(Path.Combine(cache, "preserve"), "protected cache bytes");
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", cache);
        var before = fixture.Snapshot();

        var location = Assert.Single(HtmlBrowserCacheCleaner.GetCacheLocations(includeTemp: false),
            item => item.Path == cache);
        var result = HtmlBrowserCacheCleaner.CleanCache(new[] { location });

        Assert.False(result.Success);
        Assert.Single(result.Failed);
        Assert.Empty(result.SuccessfullyCleared);
        fixture.AssertUnchanged(before);
    }

    [Fact]
    public void CacheDiscovery_ReportsOwnedSizeWithoutFollowingNativeExternalOrCyclicLinks()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        using var fixture = new DriverRootFixture();
        string cache = Path.Combine(fixture.Root, "selected-browsers");
        string framework = Path.Combine(cache, "firefox-123", "browser.app", "Example.framework");
        string version = Path.Combine(framework, "Versions", "A");
        Directory.CreateDirectory(version);
        File.WriteAllText(Path.Combine(version, "Example"), "owned bytes");
        Directory.CreateSymbolicLink(Path.Combine(framework, "Versions", "Current"), "A");
        File.CreateSymbolicLink(Path.Combine(framework, "Example"), Path.Combine("Versions", "Current", "Example"));
        Directory.CreateSymbolicLink(Path.Combine(version, "Cycle"), framework);
        string external = Path.Combine(fixture.Root, "external");
        Directory.CreateDirectory(external);
        File.WriteAllText(Path.Combine(external, "preserve"), "external bytes");
        Directory.CreateSymbolicLink(Path.Combine(framework, "External"), external);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", cache);

        var location = Assert.Single(HtmlBrowserCacheCleaner.GetCacheLocations(includeTemp: false),
            item => item.Path == cache);

        Assert.Equal("owned bytes".Length, location.Size);
        Assert.Equal("external bytes", File.ReadAllText(Path.Combine(external, "preserve")));
        Assert.Equal("owned bytes", File.ReadAllText(Path.Combine(framework, "Example")));
        Assert.Equal(0, fixture.HttpCalls);
        Assert.Equal(0, fixture.InstallerCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CacheDiscovery_LinuxXdgLocationMatchesInstallerResolution(bool relative)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;
        string? xdg = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        string? initialDirectory = Environment.GetEnvironmentVariable("INIT_CWD");
        using var fixture = new DriverRootFixture();
        string cache = Path.Combine(fixture.Root, "xdg", "ms-playwright");
        Directory.CreateDirectory(cache);
        File.WriteAllText(Path.Combine(cache, "preserve"), "xdg browser bytes");
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", null);
        Environment.SetEnvironmentVariable("XDG_CACHE_HOME", relative ? "xdg" : Path.GetDirectoryName(cache));
        Environment.SetEnvironmentVariable("INIT_CWD", fixture.Root);
        try
        {
            var location = Assert.Single(HtmlBrowserCacheCleaner.GetCacheLocations(includeTemp: false),
                item => item.Path == cache);
            Assert.Equal("xdg browser bytes".Length, location.Size);
            Assert.Equal("xdg browser bytes", File.ReadAllText(Path.Combine(cache, "preserve")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CACHE_HOME", xdg);
            Environment.SetEnvironmentVariable("INIT_CWD", initialDirectory);
        }
    }

    [Fact]
    public void CacheDiscovery_KeepsLegacyDefaultBrowserLocationAndAvoidsDuplicateSelectedPaths()
    {
        using var fixture = new DriverRootFixture();
        string cache = GetExpectedLegacyBrowserCache();
        // Read-only observation of the real default; this test never creates or clears a user cache.
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", Path.Combine(cache, "."));

        var locations = HtmlBrowserCacheCleaner.GetCacheLocations(includeTemp: false);

        Assert.Equal(Directory.Exists(cache) ? 1 : 0,
            locations.Count(item => Path.GetFullPath(item.Path) == cache));
    }

    [Fact]
    public void CacheDiscovery_SelectedRootAliasRemainsUnauthorizedForCleanup()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        using var fixture = new DriverRootFixture();
        string target = Path.Combine(fixture.Root, "real-cache");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "preserve"), "external target bytes");
        string alias = Path.Combine(fixture.Root, "selected-cache");
        Directory.CreateSymbolicLink(alias, target);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", alias);

        var location = Assert.Single(HtmlBrowserCacheCleaner.GetCacheLocations(includeTemp: false),
            item => item.Path == alias);
        Assert.Equal(0, location.Size);
        var result = HtmlBrowserCacheCleaner.CleanCache(new[] { location });

        Assert.False(result.Success);
        Assert.Single(result.Failed);
        Assert.Empty(result.SuccessfullyCleared);
        Assert.Equal("external target bytes", File.ReadAllText(Path.Combine(target, "preserve")));
        Assert.True((File.GetAttributes(alias) & FileAttributes.ReparsePoint) != 0);
    }

    [Fact]
    public void CacheDiscovery_PreservesTempAndLegacyDriverDiscovery()
    {
        using var fixture = new DriverRootFixture();
        string temporary = Path.Combine(Path.GetTempPath(), "playwright-discovery-" + Guid.NewGuid().ToString("N"));
        string trace = Path.Combine(Path.GetTempPath(), "trace-discovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        Directory.CreateDirectory(trace);
        File.WriteAllText(Path.Combine(temporary, "preserve"), "temporary bytes");
        File.WriteAllText(Path.Combine(trace, "capture.trace"), "trace bytes");
        try
        {
            var tempLocations = HtmlBrowserCacheCleaner.GetCacheLocations(includeBrowsers: false);
            Assert.Equal("temporary bytes".Length, Assert.Single(tempLocations, item => item.Path == temporary).Size);
            Assert.Equal("trace bytes".Length, Assert.Single(tempLocations, item => item.Path == trace).Size);
            Assert.DoesNotContain(tempLocations, item => item.Description == "Playwright browsers");
            string driver = Path.Combine(Path.GetDirectoryName(GetExpectedLegacyBrowserCache())!, "ms-playwright-driver", ".playwright");
            var browserLocations = HtmlBrowserCacheCleaner.GetCacheLocations(includeTemp: false);
            Assert.Equal(Directory.Exists(driver) ? 1 : 0,
                browserLocations.Count(item => item.Path == driver && item.Description == "Playwright driver and Node.js"));
            Assert.DoesNotContain(browserLocations, item => item.Path == temporary || item.Path == trace);
            Assert.Equal("temporary bytes", File.ReadAllText(Path.Combine(temporary, "preserve")));
            Assert.Equal("trace bytes", File.ReadAllText(Path.Combine(trace, "capture.trace")));
        }
        finally
        {
            Directory.Delete(temporary, true);
            Directory.Delete(trace, true);
        }
    }

    [Fact]
    public void CacheDiscovery_SelectedBrowserPathMatchingLegacyDriverIsReportedOnce()
    {
        using var fixture = new DriverRootFixture();
        string driver = Path.Combine(Path.GetDirectoryName(GetExpectedLegacyBrowserCache())!, "ms-playwright-driver");
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", driver);

        // Observe an existing legacy location without creating or cleaning it.
        var locations = HtmlBrowserCacheCleaner.GetCacheLocations(includeTemp: false);

        Assert.Equal(Directory.Exists(driver) ? 1 : 0, locations.Count(item => item.Path == driver));
    }

    private static string GetExpectedLegacyBrowserCache()
    {
        string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ms-playwright")
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? Path.Combine(user, "Library", "Caches", "ms-playwright")
            : Path.Combine(user, ".cache", "ms-playwright");
    }
}
#endif
