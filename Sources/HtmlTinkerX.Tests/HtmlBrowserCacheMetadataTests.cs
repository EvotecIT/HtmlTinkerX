#if !NETFRAMEWORK
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Process diagnostics")]
public sealed class HtmlBrowserCacheMetadataTests {
    [Theory]
    [InlineData("history")]
    [InlineData("retry")]
    public async Task FailedRepairCannotWriteThroughLinkedMetadata(string kind) {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return; // Native links require host privileges.
        using var fixture = new HtmlBrowserCacheResilienceTests.CacheFixture();
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        string old = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox);
        string managed = Path.Combine(fixture.Cache, ".htmltinkerx");
        string name = kind == "history" ? "install.jsonl" : "repair-failed-" + typeof(Microsoft.Playwright.Playwright).Assembly.GetName().Version!.ToString(3) + "-firefox.txt";
        string metadata = Path.Combine(managed, name);
        if (File.Exists(metadata)) File.Delete(metadata);
        string outside = Path.Combine(fixture.Root, "outside");
        File.WriteAllText(outside, "keep outside bytes");
        File.CreateSymbolicLink(metadata, outside);
        HtmlBrowser.CacheStageInstaller = (_, _) => throw new IOException("network unavailable");

        await HtmlBrowser.RepairInstallationAsync(HtmlBrowserEngine.Firefox);

        Assert.Equal("keep outside bytes", File.ReadAllText(outside));
        Assert.Equal(old, HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox));
        Assert.NotNull(new FileInfo(metadata).LinkTarget);
    }

    [Fact]
    public async Task FailedDoctorSmokeCannotCreateAFileThroughADanglingRejectionLink() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        using var fixture = new HtmlBrowserCacheResilienceTests.CacheFixture();
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        string slot = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox);
        string outside = Path.Combine(fixture.Root, "missing-outside");
        File.CreateSymbolicLink(Path.Combine(slot, "launch-failed.txt"), outside);
        HtmlBrowser.CacheSmokeTest = (_, _) => throw new IOException("failed smoke");

        var report = await HtmlBrowser.TestBrowserCacheAsync(HtmlBrowserEngine.Firefox, repair: true);

        Assert.Equal(2, report.ExitCode);
        Assert.Contains(report.Revisions, item => item.Message.Contains("repair stopped"));
        Assert.False(File.Exists(outside));
        Assert.Equal(1, fixture.Installs);
    }

    [Fact]
    public async Task WriterRecordLinkIsNotMistakenForConfirmedAbsence() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        using var fixture = new HtmlBrowserCacheResilienceTests.CacheFixture();
        string outside = Path.Combine(fixture.Root, "missing-outside");
        File.CreateSymbolicLink(fixture.Lock + ".writer.json", outside);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => HtmlBrowser.AcquireInstallationFileLockAsync());

        Assert.Contains("writer", failure.Message);
        Assert.NotNull(new FileInfo(fixture.Lock + ".writer.json").LinkTarget);
        Assert.False(File.Exists(outside));
    }
}
#endif
