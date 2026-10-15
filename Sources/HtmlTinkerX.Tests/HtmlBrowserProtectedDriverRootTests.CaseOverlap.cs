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
    [Theory]
    [InlineData("cache-upper")]
    [InlineData("cache-lower")]
    [InlineData("browser-ancestor")]
    [InlineData("distinct-sibling")]
    public async Task CaseOverlap_MissingPackagePathsRespectInstallationPlatformPolicy(string mode)
    {
        using var fixture = new DriverRootFixture();
        string cache = Path.Combine(fixture.Root, mode == "cache-lower" ? "packages" : "Packages");
        string driverRoot = Path.Combine(fixture.Root, mode == "cache-lower" ? "Packages"
            : mode == "distinct-sibling" ? "packages-driver" : "packages");
        if (mode == "browser-ancestor")
        {
            cache = Path.Combine(cache, "nested-cache");
            fixture.SetSearchRoot(fixture.Root, "root");
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", driverRoot);
            driverRoot = fixture.Root;
        }
        else
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", driverRoot);
        }
        Environment.SetEnvironmentVariable("NUGET_PACKAGES", cache);
        Assert.False(Directory.Exists(cache));
        int requests = 0;
        using var handler = new HtmlBrowserDriverPackageTests.FakeHandler(HtmlBrowserDriverPackageTests.CreateDriverPackage());
        HtmlBrowser.HttpClientFactory = () => { requests++; return new HttpClient(handler, disposeHandler: false); };
        var before = fixture.Snapshot();
        bool reject = mode != "distinct-sibling" && !RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        if (reject)
        {
            Exception? failure = await Record.ExceptionAsync(() => mode == "browser-ancestor"
                ? HtmlBrowser.RepairInstallationAsync(HtmlBrowserEngine.Firefox)
                : HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox));
            fixture.AssertUnchanged(before);
            Assert.IsType<InvalidOperationException>(failure);
            Assert.Equal(0, requests);
        }
        else
        {
            if (mode == "browser-ancestor") await HtmlBrowser.RepairInstallationAsync(HtmlBrowserEngine.Firefox);
            else await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
            Assert.True(HtmlBrowser.HasDriverLayout(Path.Combine(driverRoot, ".playwright")));
            Assert.Equal(1, requests);
            Assert.Equal(1, fixture.InstallerCalls);
            Assert.False(Directory.Exists(cache));
        }
    }
}
