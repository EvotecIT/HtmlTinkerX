#if !NETFRAMEWORK
using HtmlTinkerX;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlBrowserProtectedDriverRootTests
{
    [Theory]
    [InlineData("links-directory", "ensure")]
    [InlineData("reference-file", "ensure")]
    [InlineData("lock-directory", "repair")]
    [InlineData("runtime-directory", "ensure")]
    [InlineData("validation-leaf", "ensure")]
    public async Task CliPreflight_PublicMutationRejectsWriteTargetAliasesBeforeInstallerOrCleanup(
        string kind, string operation)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return; // Creating Windows links requires host privileges.
        using var fixture = new DriverRootFixture();
        PrepareCliPathFixtureDriver(fixture);
        string browserRoot = Path.GetDirectoryName(fixture.BrowserSentinel)!;
        string external = Path.Combine(fixture.Root, "external-sentinel");
        Directory.CreateDirectory(external);
        File.WriteAllText(Path.Combine(external, ".nupkg.metadata"), "{}");
        string sentinel = Path.Combine(external, "preserve");
        File.WriteAllText(sentinel, "external temporary bytes");
        switch (kind)
        {
            case "links-directory":
                Directory.CreateSymbolicLink(Path.Combine(browserRoot, ".links"), external);
                break;
            case "reference-file":
                string references = Path.Combine(browserRoot, ".links");
                Directory.CreateDirectory(references);
                File.CreateSymbolicLink(Path.Combine(references, new string('a', 40)), sentinel);
                break;
            case "lock-directory":
                Directory.CreateSymbolicLink(Path.Combine(browserRoot, "__dirlock"), external);
                break;
            case "runtime-directory":
                Directory.CreateSymbolicLink(Path.Combine(browserRoot, "ffmpeg-456"), external);
                break;
            case "validation-leaf":
                string runtime = Path.Combine(browserRoot, "ffmpeg-456");
                Directory.CreateDirectory(runtime);
                File.WriteAllText(Path.Combine(runtime, "INSTALLATION_COMPLETE"), "fixture runtime");
                File.CreateSymbolicLink(Path.Combine(runtime, "DEPENDENCIES_VALIDATED"), sentinel);
                break;
            default:
                throw new ArgumentException("Unknown CLI mutation fixture.", nameof(kind));
        }
        var before = fixture.Snapshot();

        Exception? failure = await Record.ExceptionAsync(() => operation == "repair"
            ? HtmlBrowser.RepairInstallationAsync(HtmlBrowserEngine.Firefox)
            : HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox));

        Assert.Equal("external temporary bytes", File.ReadAllText(sentinel));
        Assert.Equal("{}", File.ReadAllText(Path.Combine(external, ".nupkg.metadata")));
        fixture.AssertUnchanged(before);
        Assert.IsType<ArgumentException>(failure);
    }

    [Fact]
    public async Task CliPreflight_PublicEnsurePreservesNestedNativeFrameworkAndCyclicLinks()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        using var fixture = new DriverRootFixture();
        PrepareCliPathFixtureDriver(fixture);
        string browserRoot = Path.GetDirectoryName(fixture.BrowserSentinel)!;
        string framework = Path.Combine(browserRoot, "firefox-456", "browser.app", "Contents", "Frameworks", "Example.framework");
        string versions = Path.Combine(framework, "Versions");
        string version = Path.Combine(versions, "A");
        Directory.CreateDirectory(version);
        File.WriteAllText(Path.Combine(version, "Example"), "owned native framework bytes");
        Directory.CreateSymbolicLink(Path.Combine(versions, "Current"), "A");
        File.CreateSymbolicLink(Path.Combine(framework, "Example"), Path.Combine("Versions", "Current", "Example"));
        Directory.CreateSymbolicLink(Path.Combine(version, "Cycle"), framework);
        string external = Path.Combine(fixture.Root, "external-sentinel");
        Directory.CreateDirectory(external);
        File.WriteAllText(Path.Combine(external, ".nupkg.metadata"), "{}");
        File.WriteAllText(Path.Combine(external, "preserve"), "external temporary bytes");
        Directory.CreateSymbolicLink(Path.Combine(framework, "External"), external);

        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);

        Assert.Equal(1, fixture.InstallerCalls);
        Assert.Equal(0, fixture.HttpCalls);
        Assert.Equal("external temporary bytes", File.ReadAllText(Path.Combine(external, "preserve")));
        Assert.Equal("{}", File.ReadAllText(Path.Combine(external, ".nupkg.metadata")));
        Assert.Equal("owned native framework bytes", File.ReadAllText(Path.Combine(framework, "Example")));
        Assert.True((File.GetAttributes(Path.Combine(version, "Cycle")) & FileAttributes.ReparsePoint) != 0);
    }

    private static void PrepareCliPathFixtureDriver(DriverRootFixture fixture)
    {
        fixture.SetSearchRoot(fixture.Root, "root");
        string driver = Path.Combine(fixture.Root, ".playwright");
        File.WriteAllText(Path.Combine(driver, ".version"),
            typeof(Microsoft.Playwright.Playwright).Assembly.GetName().Version!.ToString(3));
        File.WriteAllText(Path.Combine(driver, "package", "browsers.json"),
            "{\"browsers\":[{\"name\":\"firefox\",\"revision\":\"123\",\"installByDefault\":true},{\"name\":\"ffmpeg\",\"revision\":\"456\"}]}");
    }
}
#endif
