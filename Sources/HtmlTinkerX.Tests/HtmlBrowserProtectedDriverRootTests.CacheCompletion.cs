#if !NETFRAMEWORK
using HtmlTinkerX;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlBrowserProtectedDriverRootTests
{
    [Theory]
    [InlineData("root")]
    [InlineData("child")]
    [InlineData("trailing-child")]
    public void CacheCompletion_CurrentMutableDriverIsDiscoveredAndOnlyItsChildIsCleaned(string alias)
    {
        using var fixture = new DriverRootFixture();
        fixture.SetSearchRoot(fixture.Root, alias);
        string driver = Path.Combine(fixture.Root, ".playwright");
        string sibling = Path.Combine(fixture.Root, "preserve");
        File.WriteAllText(sibling, "application-owned bytes");

        var location = Assert.Single(HtmlBrowserCacheCleaner.GetCacheLocations(includeTemp: false),
            item => item.Path == driver);
        var result = HtmlBrowserCacheCleaner.CleanCache(new[] { location });

        Assert.True(result.Success);
        Assert.Same(location, Assert.Single(result.SuccessfullyCleared));
        Assert.False(Directory.Exists(driver));
        Assert.True(Directory.Exists(fixture.Root));
        Assert.Equal("application-owned bytes", File.ReadAllText(sibling));
        Assert.Equal("browser-owned bytes", File.ReadAllText(fixture.BrowserSentinel));
        Assert.Equal(0, fixture.HttpCalls);
        Assert.Equal(0, fixture.InstallerCalls);
    }

    [Fact]
    public void CacheCompletion_DiscoveredDriverRetainsCheckoutSearchParentProtection()
    {
        using var fixture = new DriverRootFixture();
        fixture.SetSearchRoot(fixture.Root, "root");
        File.WriteAllText(Path.Combine(fixture.Root, ".git"), "gitdir: ../fixture-metadata");
        var before = fixture.Snapshot();
        string driver = Path.Combine(fixture.Root, ".playwright");

        var location = Assert.Single(HtmlBrowserCacheCleaner.GetCacheLocations(includeTemp: false),
            item => item.Path == driver);
        var result = HtmlBrowserCacheCleaner.CleanCache(new[] { location });

        Assert.False(result.Success);
        Assert.Single(result.Failed);
        Assert.Empty(result.SuccessfullyCleared);
        fixture.AssertUnchanged(before);
    }

    [Fact]
    public async Task CacheCompletion_DiscoveryNeverIncludesSelectedReadOnlyBundledDriver()
    {
        using var fixture = new DriverRootFixture();
        string bundled = await GetCanonicalTestHostBundleAsync(fixture);
        string cli = Path.Combine(bundled, "package", "cli.js");
        string before = HashFile(cli);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", Path.GetDirectoryName(bundled));

        var locations = HtmlBrowserCacheCleaner.GetCacheLocations(includeTemp: false);

        Assert.DoesNotContain(locations, item => item.Path == bundled);
        Assert.Equal(before, HashFile(cli));
        Assert.Equal(0, fixture.HttpCalls);
        Assert.Equal(0, fixture.InstallerCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CacheCompletion_NestedBrowserAndTempCleanupCountsOutermostEstimateOnce(bool parentFirst)
    {
        using var fixture = new DriverRootFixture();
        string parent = Path.Combine(Path.GetTempPath(), "playwright-nested-" + Guid.NewGuid().ToString("N"));
        string browser = Path.Combine(parent, "browsers");
        Directory.CreateDirectory(browser);
        File.WriteAllText(Path.Combine(parent, "preserve"), "outer");
        File.WriteAllText(Path.Combine(browser, "preserve"), "ink");
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", browser);
        try
        {
            var locations = HtmlBrowserCacheCleaner.GetCacheLocations();
            var child = Assert.Single(locations, item => item.Path == browser);
            var outer = Assert.Single(locations, item => item.Path == parent);
            Assert.Equal(3, child.Size);
            Assert.Equal(8, outer.Size);

            var result = HtmlBrowserCacheCleaner.CleanCache(parentFirst ? new[] { outer, child } : new[] { child, outer });

            Assert.False(Directory.Exists(parent));
            Assert.Equal(8, result.TotalSizeCleared);
            Assert.Contains(outer, result.SuccessfullyCleared);
            if (parentFirst)
            {
                Assert.Same(child, Assert.Single(result.Failed).Location);
                Assert.Single(result.SuccessfullyCleared);
            }
            else
            {
                Assert.True(result.Success);
                Assert.Equal(2, result.SuccessfullyCleared.Count);
                Assert.Contains(child, result.SuccessfullyCleared);
            }
            // Results retain supplied location objects and their mutable estimates.
            outer.Size = 80;
            Assert.Equal(80, result.TotalSizeCleared);
        }
        finally
        {
            if (Directory.Exists(parent)) Directory.Delete(parent, true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CacheCompletion_ProtectedOverlappingParentFailureRemainsVisible(bool parentFirst)
    {
        using var fixture = new DriverRootFixture();
        string parent = Path.Combine(Path.GetTempPath(), "playwright-protected-" + Guid.NewGuid().ToString("N"));
        string browser = Path.Combine(parent, "browsers");
        Directory.CreateDirectory(browser);
        File.WriteAllText(Path.Combine(parent, ".git"), "gitdir: ../fixture-metadata");
        File.WriteAllText(Path.Combine(parent, "preserve"), "source-owned bytes");
        File.WriteAllText(Path.Combine(browser, "preserve"), "ink");
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", browser);
        try
        {
            var locations = HtmlBrowserCacheCleaner.GetCacheLocations();
            var child = Assert.Single(locations, item => item.Path == browser);
            var outer = Assert.Single(locations, item => item.Path == parent);

            var result = HtmlBrowserCacheCleaner.CleanCache(parentFirst ? new[] { outer, child } : new[] { child, outer });

            Assert.False(result.Success);
            Assert.Same(outer, Assert.Single(result.Failed).Location);
            Assert.Same(child, Assert.Single(result.SuccessfullyCleared));
            Assert.Equal(3, result.TotalSizeCleared);
            Assert.Equal("source-owned bytes", File.ReadAllText(Path.Combine(parent, "preserve")));
            Assert.True(File.Exists(Path.Combine(parent, ".git")));
            Assert.False(Directory.Exists(browser));
        }
        finally
        {
            if (Directory.Exists(parent)) Directory.Delete(parent, true);
        }
    }
}
#endif
