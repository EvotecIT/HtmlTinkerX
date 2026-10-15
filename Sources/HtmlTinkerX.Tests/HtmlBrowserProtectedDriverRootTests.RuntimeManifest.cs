#if !NETFRAMEWORK
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace HtmlTinkerX.Tests;

public partial class HtmlBrowserProtectedDriverRootTests
{
    [Theory]
    [InlineData("revision")]
    [InlineData("revision-override")]
    [InlineData("host-override")]
    public async Task RuntimeManifest_UnsafeComponentsCannotDeleteOutsideSelectedBrowserRoot(string mode)
    {
        using var fixture = new DriverRootFixture();
        fixture.SetSearchRoot(fixture.Root, "root");
        string driver = Path.Combine(fixture.Root, ".playwright");
        File.WriteAllText(Path.Combine(driver, ".version"), typeof(Microsoft.Playwright.Playwright).Assembly.GetName().Version!.ToString(3));
        string browsers = Path.Combine(fixture.Root, "browsers");
        Directory.CreateDirectory(Path.Combine(browsers, "firefox-123"));
        string? originalHost = Environment.GetEnvironmentVariable("PLAYWRIGHT_HOST_PLATFORM_OVERRIDE");
        string host = mode == "host-override" ? "host/../../victim" : "test-host";
        string revision = mode == "revision" ? "123/../../victim" : "123";
        string selectedRevision = mode == "revision-override" ? "123/../../victim" : revision;
        string selectedName = mode == "revision" ? "firefox" : "firefox_" + host + "_special";
        string victim = Path.GetFullPath(Path.Combine(browsers, selectedName + "-" + selectedRevision));
        Assert.StartsWith(fixture.Root + Path.DirectorySeparatorChar, victim);
        Assert.False(victim.StartsWith(browsers + Path.DirectorySeparatorChar, StringComparison.Ordinal));
        Directory.CreateDirectory(victim);
        string sentinel = Path.Combine(victim, "preserve");
        File.WriteAllText(sentinel, "owned external victim bytes");
        var entry = new Dictionary<string, object> { ["name"] = "firefox", ["revision"] = revision };
        if (mode != "revision") entry["revisionOverrides"] = new Dictionary<string, string> { [host] = selectedRevision };
        File.WriteAllText(Path.Combine(driver, "package", "browsers.json"), JsonSerializer.Serialize(new { browsers = new[] { entry } }));
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", browsers);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_HOST_PLATFORM_OVERRIDE", host);
        var before = fixture.Snapshot();
        try
        {
            Exception? failure = await Record.ExceptionAsync(() => HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox));

            _identityOutput.WriteLine(JsonSerializer.Serialize(new { mode, browsers, victim, SentinelExists = File.Exists(sentinel), Failure = failure?.ToString(), fixture.HttpCalls, fixture.InstallerCalls }));
            fixture.AssertUnchanged(before);
            Assert.IsType<InvalidOperationException>(failure);
            Assert.Contains("manifest", failure!.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_HOST_PLATFORM_OVERRIDE", originalHost);
        }
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("\"corrupt root\"")]
    [InlineData("{\"browsers\":{}}")]
    [InlineData("{\"browsers\":[null]}")]
    [InlineData("{\"browsers\":[{\"name\":7,\"revision\":\"123\"}]}")]
    [InlineData("{\"browsers\":[{\"name\":\"firefox\",\"revision\":7}]}")]
    [InlineData("{\"browsers\":[{\"name\":\"firefox\",\"revision\":\"123\",\"revisionOverrides\":[]}]}")]
    [InlineData("{\"browsers\":[{\"name\":\"firefox\",\"revision\":\"123\",\"revisionOverrides\":{\"test-host\":7}}]}")]
    public async Task RuntimeManifest_MalformedJsonShapesRecoverThroughDriverReinstall(string json)
    {
        using var fixture = new DriverRootFixture();
        fixture.SetSearchRoot(fixture.Root, "root");
        string driver = Path.Combine(fixture.Root, ".playwright");
        string manifest = Path.Combine(driver, "package", "browsers.json");
        File.WriteAllText(Path.Combine(driver, ".version"), typeof(Microsoft.Playwright.Playwright).Assembly.GetName().Version!.ToString(3));
        File.WriteAllText(manifest, json);
        string? originalHost = Environment.GetEnvironmentVariable("PLAYWRIGHT_HOST_PLATFORM_OVERRIDE");
        Environment.SetEnvironmentVariable("PLAYWRIGHT_HOST_PLATFORM_OVERRIDE", "test-host");
        int requests = 0;
        using var handler = new HtmlBrowserDriverPackageTests.FakeHandler(HtmlBrowserDriverPackageTests.CreateDriverPackage());
        HtmlBrowser.HttpClientFactory = () => { requests++; return new HttpClient(handler, disposeHandler: false); };
        try
        {
            await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);

            _identityOutput.WriteLine(JsonSerializer.Serialize(new { json, requests, fixture.InstallerCalls, ManifestAfter = File.ReadAllText(manifest) }));
            Assert.Equal(1, requests);
            Assert.Equal(1, fixture.InstallerCalls);
            Assert.True(HtmlBrowser.HasDriverLayout(driver));
            Assert.NotEqual(json, File.ReadAllText(manifest));
            Assert.Equal("browser-owned bytes", File.ReadAllText(fixture.BrowserSentinel));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_HOST_PLATFORM_OVERRIDE", originalHost);
        }
    }

    [Fact]
    public async Task RuntimeManifest_CompleteRuntimeLinkRetainsReadOnlyFastReuse()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _identityOutput.WriteLine("NotExecuted: native Windows runtime-link creation is not qualified by this control.");
            return;
        }
        using var fixture = new DriverRootFixture();
        fixture.SetSearchRoot(fixture.Root, "root");
        string driver = Path.Combine(fixture.Root, ".playwright");
        File.WriteAllText(Path.Combine(driver, ".version"), typeof(Microsoft.Playwright.Playwright).Assembly.GetName().Version!.ToString(3));
        File.WriteAllText(Path.Combine(driver, "package", "browsers.json"), "{\"browsers\":[{\"name\":\"firefox\",\"revision\":\"123\"}]}");
        string browsers = Path.Combine(fixture.Root, "browsers");
        string external = Path.Combine(fixture.Root, "complete-runtime");
        Directory.CreateDirectory(browsers);
        Directory.CreateDirectory(external);
        File.WriteAllText(Path.Combine(external, "INSTALLATION_COMPLETE"), "complete");
        File.WriteAllText(Path.Combine(external, "preserve"), "owned runtime bytes");
        string runtime = Path.Combine(browsers, "firefox-123");
        Directory.CreateSymbolicLink(runtime, external);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", browsers);
        try
        {
            await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);

            Assert.Equal("owned runtime bytes", File.ReadAllText(Path.Combine(external, "preserve")));
            Assert.NotNull(new DirectoryInfo(runtime).LinkTarget);
            Assert.Equal(0, fixture.HttpCalls);
            Assert.Equal(0, fixture.InstallerCalls);
        }
        finally
        {
            Directory.Delete(runtime);
        }
    }
}
#endif
