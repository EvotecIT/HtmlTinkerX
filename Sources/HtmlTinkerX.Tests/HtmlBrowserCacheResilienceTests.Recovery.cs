using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserCacheResilienceTests {
    [Fact]
    public async Task SuccessfulDriverReplacementPreservesLegacyHermeticBrowserBytes() {
        using var fixture = new CacheFixture();
        string driver = Path.Combine(fixture.Root, "driver", ".playwright");
        string legacy = Path.Combine(driver, "package", ".local-browsers", "firefox-1");
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "preserve"), "legacy browser bytes");
        File.WriteAllText(Path.Combine(driver, ".version"), "0.0.1");
        var factory = HtmlBrowser.HttpClientFactory;
        using var handler = new HtmlBrowserDriverPackageTests.FakeHandler(HtmlBrowserDriverPackageTests.CreateDriverPackage());
        try {
            HtmlBrowser.HttpClientFactory = () => new HttpClient(handler, false);
            await HtmlBrowser.EnsureDriverInstalledAsync();
            string backup = Assert.Single(Directory.GetDirectories(Path.Combine(fixture.Root, "driver"), ".playwright-previous-*"));
            Assert.Equal("legacy browser bytes", File.ReadAllText(Path.Combine(backup, "package", ".local-browsers", "firefox-1", "preserve")));
            Assert.True(HtmlBrowser.HasDriverLayout(driver));
        } finally { HtmlBrowser.HttpClientFactory = factory; }
    }

    [Theory]
    [InlineData("directory")]
    [InlineData("executable")]
    [InlineData("file")]
    public async Task DoctorReportsEscapingManifestPathsAndRepairsWithoutTouchingOutsideFiles(string field) {
        using var fixture = new CacheFixture();
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        string slot = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox);
        string outside = Path.Combine(fixture.Root, "outside");
        File.WriteAllText(outside, "keep");
        var manifest = HtmlBrowser.ReadCacheManifest(slot);
        if (field == "directory") manifest.Runtimes[0].Directory = "../../outside";
        else if (field == "executable") manifest.Runtimes[0].Executable = "../../outside";
        else manifest.Files[0].Path = "../../outside";
        File.WriteAllText(Path.Combine(slot, HtmlBrowser.CacheManifestName), JsonSerializer.Serialize(manifest));

        var diagnosis = await HtmlBrowser.TestBrowserCacheAsync(HtmlBrowserEngine.Firefox);
        Assert.Equal(2, diagnosis.ExitCode);
        Assert.Contains(diagnosis.Revisions, item => item.State == "incomplete" && item.Directory == slot);
        var repaired = await HtmlBrowser.TestBrowserCacheAsync(HtmlBrowserEngine.Firefox, repair: true);
        Assert.Equal(0, repaired.ExitCode);
        Assert.NotEqual(slot, HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox));
        Assert.Equal("keep", File.ReadAllText(outside));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DoctorFailedSmokeCannotReturnTheRejectedSlotAsFallback(bool olderFallback) {
        using var fixture = new CacheFixture();
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        string older = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox);
        if (olderFallback) {
            fixture.Revision = "101";
            fixture.WriteDriver();
            await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        }
        string rejected = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox);
        HtmlBrowser.CacheSmokeTest = (slot, _) => slot == rejected ? Task.FromException(new IOException("browser cannot start")) : Task.CompletedTask;
        HtmlBrowser.CacheStageInstaller = (_, _) => throw new IOException("network unavailable");

        var report = await HtmlBrowser.TestBrowserCacheAsync(HtmlBrowserEngine.Firefox, repair: true);

        Assert.Equal(olderFallback ? 4 : 2, report.ExitCode);
        Assert.True(File.Exists(Path.Combine(rejected, "launch-failed.txt")));
        Assert.DoesNotContain(report.Revisions, item => item.State == "verified" && item.Directory.StartsWith(rejected, StringComparison.Ordinal));
        if (olderFallback) Assert.Equal(older, HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox));
        else Assert.Throws<InvalidOperationException>(() => HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox));
    }

    [Fact]
    public async Task UnwritableHistoryCannotInterruptRepairOrVerifiedFallback() {
        using var fixture = new CacheFixture();
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        string previous = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox);
        string log = Path.Combine(fixture.Cache, ".htmltinkerx", "install.jsonl");
        File.Delete(log);
        Directory.CreateDirectory(log); // Real filesystem append failure on every platform.
        fixture.Revision = "101";
        fixture.WriteDriver();
        HtmlBrowser.CacheStageInstaller = (_, _) => throw new IOException("network unavailable");

        await HtmlBrowser.RepairInstallationAsync(HtmlBrowserEngine.Firefox);
        Assert.Equal(previous, HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox));
        HtmlBrowser.CacheStageInstaller = fixture.StageAsync;
        await HtmlBrowser.RepairInstallationAsync(HtmlBrowserEngine.Firefox);
        Assert.NotEqual(previous, HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox));
        Assert.True(HtmlBrowser.VerifyCacheSlot(previous, true, out _));
    }

    [Fact]
    public async Task RecentRepairFailureUsesFallbackUntilExpiryButForcedRepairRetries() {
        using var fixture = new CacheFixture();
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        string previous = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox);
        fixture.Revision = "101";
        fixture.WriteDriver();
        int attempts = 0;
        HtmlBrowser.CacheStageInstaller = (_, _) => { attempts++; throw new IOException("network unavailable"); };
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        Assert.Equal(1, attempts);
        Assert.Equal(previous, HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox));
        await HtmlBrowser.RepairInstallationAsync(HtmlBrowserEngine.Firefox);
        Assert.Equal(2, attempts);
        string marker = Assert.Single(Directory.GetFiles(Path.Combine(fixture.Cache, ".htmltinkerx"), "repair-failed-*.txt"));
        File.SetLastWriteTimeUtc(marker, DateTime.UtcNow.AddMinutes(-6));
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        Assert.Equal(3, attempts);
        File.Delete(HtmlBrowser.CacheFilePath(previous, HtmlBrowser.ReadCacheManifest(previous).Runtimes[0].Executable));
        await Assert.ThrowsAsync<InvalidOperationException>(() => HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox));
        Assert.Equal(4, attempts); // A retry marker cannot suppress repair when no usable fallback remains.
        HtmlBrowser.CacheStageInstaller = fixture.StageAsync;
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        Assert.False(File.Exists(marker));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenericLaunchFailurePreservesTheActualHealthyGeneration(bool recording) {
        using var fixture = new CacheFixture();
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        string slot = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox);
        var options = new HtmlBrowserLaunchOptions { Browser = HtmlBrowserEngine.Firefox, VideoPath = recording ? Path.Combine(fixture.Root, "recording.webm") : null };
        var expected = new PlaywrightException("process did exit: exitCode=255; rejected profile");

        var actual = await Assert.ThrowsAsync<PlaywrightException>(() => HtmlBrowser.LaunchWithCacheRepairAsync<int>(options, launched => {
            Assert.Equal(slot, launched);
            throw expected;
        }));

        Assert.Same(expected, actual);
        Assert.Equal(1, fixture.Installs);
        Assert.True(HtmlBrowser.VerifyCacheSlot(slot, true, out _));
        Assert.False(File.Exists(Path.Combine(slot, "launch-failed.txt")));
    }

    [Fact]
    public async Task CorruptionDuringLaunchRequiresDriverRestartBeforeTheSingleRetry() {
        using var fixture = new CacheFixture();
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        string original = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox);
        int calls = 0;
        await Assert.ThrowsAsync<HtmlBrowser.BrowserCacheRepairedException>(() => HtmlBrowser.LaunchWithCacheRepairAsync<int>(new HtmlBrowserLaunchOptions { Browser = HtmlBrowserEngine.Firefox }, slot => {
            if (++calls == 1) {
                Assert.Equal(original, slot);
                File.WriteAllText(HtmlBrowser.CacheFilePath(original, HtmlBrowser.ReadCacheManifest(original).Runtimes[0].Executable), "evil");
                throw new PlaywrightException("process did exit: exitCode=255");
            }
            throw new InvalidOperationException("A retry on the old driver must not run.");
        }));
        Assert.Equal(1, calls);
        string repaired = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox);
        Assert.NotEqual(original, repaired);
        var expected = new PlaywrightException("process did exit: exitCode=255");
        var actual = await Assert.ThrowsAsync<PlaywrightException>(() => HtmlBrowser.LaunchWithCacheRepairAsync<int>(
            new HtmlBrowserLaunchOptions { Browser = HtmlBrowserEngine.Firefox }, slot => {
                Assert.Equal(repaired, slot);
                throw expected;
            }, allowRepair: false));
        Assert.Same(expected, actual);
        Assert.Equal(2, fixture.Installs);
    }

    [Fact]
    public async Task RecordingFailureRepairsItsAuxiliaryCompatibleSlotAndPreservesTheOtherFallback() {
        using var fixture = new CacheFixture();
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        string recordingSlot = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox);
        fixture.Revision = "101";
        fixture.AuxiliaryRevision = "101";
        fixture.WriteDriver();
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        string other = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox);
        fixture.Revision = "102";
        fixture.AuxiliaryRevision = "100";
        fixture.WriteDriver();
        Assert.Equal(other, HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox));
        int calls = 0;
        await Assert.ThrowsAsync<HtmlBrowser.BrowserCacheRepairedException>(() => HtmlBrowser.LaunchWithCacheRepairAsync<int>(new HtmlBrowserLaunchOptions {
            Browser = HtmlBrowserEngine.Firefox, VideoPath = Path.Combine(fixture.Root, "recording.webm")
        }, slot => {
            if (++calls == 1) {
                Assert.Equal(recordingSlot, slot);
                File.WriteAllText(HtmlBrowser.CacheFilePath(recordingSlot, HtmlBrowser.ReadCacheManifest(recordingSlot).Runtimes[0].Executable), "evil");
                throw new PlaywrightException("process did exit: exitCode=255");
            }
            throw new InvalidOperationException("A retry on the old driver must not run.");
        }));
        Assert.Equal(1, calls);
        string repaired = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox);
        Assert.NotEqual(recordingSlot, repaired);
        Assert.NotEqual(other, repaired);
        Assert.True(File.Exists(Path.Combine(recordingSlot, "launch-failed.txt")));
        Assert.True(HtmlBrowser.VerifyCacheSlot(other, true, out _));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("directory")]
    public async Task InvalidWriterRecordReportsPromptlyWithoutOverwritingUnknownOwnership(string metadata) {
        using var fixture = new CacheFixture();
        if (metadata == "directory") Directory.CreateDirectory(fixture.Lock + ".writer.json");
        else File.WriteAllText(fixture.Lock + ".writer.json", metadata);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => HtmlBrowser.AcquireInstallationFileLockAsync());
        Assert.Contains("writer", failure.Message);
        if (metadata == "directory") Assert.True(Directory.Exists(fixture.Lock + ".writer.json"));
        else Assert.Equal(metadata, File.ReadAllText(fixture.Lock + ".writer.json"));
        var report = await HtmlBrowser.TestBrowserCacheAsync(HtmlBrowserEngine.Firefox, repair: true);
        Assert.Equal(3, report.ExitCode);
        Assert.Contains(report.Revisions, item => item.Browser == "installer" && item.State == "incomplete");
        Assert.Equal(0, fixture.Installs);
    }
}
