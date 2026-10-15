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
    [InlineData("browser", "git-directory", "repair")]
    [InlineData("browser", "git-file", "repair")]
    [InlineData("browser", "metadata", "repair")]
    [InlineData("browser", "legacy-metadata", "repair")]
    [InlineData("driver", "git-directory", "ensure")]
    [InlineData("driver", "metadata", "repair")]
    [InlineData("cache", "git-file", "clean")]
    [InlineData("cache", "legacy-metadata", "clean")]
    [InlineData("cache", "bare-git", "clean")]
    public async Task DeletionFootprint_ProtectedNestedContentStopsPublicMutationBeforeAnyChanges(
        string role, string kind, string operation)
    {
        using var fixture = new DriverRootFixture();
        fixture.SetSearchRoot(fixture.Root, "root");
        string driver = Path.Combine(fixture.Root, ".playwright");
        string root = role == "driver" ? driver : role == "browser"
            ? Path.GetDirectoryName(fixture.BrowserSentinel)! : Path.Combine(fixture.Root, "selected-cache");
        string nested = Path.Combine(root, "work", "repository");
        CreateProtectedNestedContent(nested, kind);
        int requests = 0;
        using var handler = new HtmlBrowserDriverPackageTests.FakeHandler(HtmlBrowserDriverPackageTests.CreateDriverPackage());
        HtmlBrowser.HttpClientFactory = () => { requests++; return new HttpClient(handler, disposeHandler: false); };
        var before = fixture.Snapshot();

        if (operation == "clean")
        {
            var location = new HtmlBrowserCacheCleaner.CacheLocation { Path = root };
            var result = HtmlBrowserCacheCleaner.CleanCache(new[] { location });
            fixture.AssertUnchanged(before);
            Assert.Same(location, Assert.Single(result.Failed).Location);
            Assert.Empty(result.SuccessfullyCleared);
        }
        else
        {
            Exception? failure = await Record.ExceptionAsync(() => operation == "repair"
                ? HtmlBrowser.RepairInstallationAsync(HtmlBrowserEngine.Firefox)
                : HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox));
            fixture.AssertUnchanged(before);
            Assert.IsType<InvalidOperationException>(failure);
        }
        Assert.Equal(0, requests);
    }

    private static void CreateProtectedNestedContent(string root, string kind)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "preserve"), "protected repository or package bytes");
        switch (kind)
        {
            case "git-directory":
                Directory.CreateDirectory(Path.Combine(root, ".git"));
                break;
            case "git-file":
                File.WriteAllText(Path.Combine(root, ".git"), "gitdir: ../immutable-source-metadata");
                break;
            case "metadata":
                File.WriteAllText(Path.Combine(root, ".nupkg.metadata"), "{}");
                break;
            case "legacy-metadata":
                File.WriteAllText(Path.Combine(root, "dependency.nuspec"), "<package />");
                File.WriteAllText(Path.Combine(root, "dependency.nupkg.sha512"), "package hash");
                break;
            case "bare-git":
                File.WriteAllText(Path.Combine(root, "HEAD"), "ref: refs/heads/main");
                File.WriteAllText(Path.Combine(root, "config"), "[core]\n\tbare = true\n");
                Directory.CreateDirectory(Path.Combine(root, "objects"));
                Directory.CreateDirectory(Path.Combine(root, "refs"));
                break;
            default:
                throw new ArgumentException("Unknown protected fixture.", nameof(kind));
        }
    }

#if !NETFRAMEWORK
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeletionFootprint_CacheCleanerDeletesNativeBundleLinksWithoutFollowingExternalOrCyclicTargets(bool cyclic)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        using var fixture = new DriverRootFixture();
        string root = Path.Combine(fixture.Root, "selected-cache");
        string framework = Path.Combine(root, "browser.app", "Contents", "Frameworks", "Example.framework");
        string versions = Path.Combine(framework, "Versions");
        string version = Path.Combine(versions, "A");
        Directory.CreateDirectory(version);
        File.WriteAllText(Path.Combine(version, "Example"), "owned native framework bytes");
        Directory.CreateSymbolicLink(Path.Combine(versions, "Current"), "A");
        File.CreateSymbolicLink(Path.Combine(framework, "Example"), Path.Combine("Versions", "Current", "Example"));
        string external = Path.Combine(fixture.Root, "external-source");
        CreateProtectedNestedContent(external, "git-directory");
        Directory.CreateSymbolicLink(Path.Combine(framework, "External"), external);
        if (cyclic) Directory.CreateSymbolicLink(Path.Combine(version, "Cycle"), framework);
        var location = new HtmlBrowserCacheCleaner.CacheLocation { Path = root };

        var result = HtmlBrowserCacheCleaner.CleanCache(new[] { location });

        Assert.True(result.Success, string.Join("; ", result.Failed.ConvertAll(x => x.Error)));
        Assert.Same(location, Assert.Single(result.SuccessfullyCleared));
        Assert.False(Directory.Exists(root));
        Assert.Equal("protected repository or package bytes", File.ReadAllText(Path.Combine(external, "preserve")));
        Assert.True(Directory.Exists(Path.Combine(external, ".git")));
    }
#endif
}
