using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Process diagnostics")]
public sealed partial class HtmlBrowserCacheResilienceTests {
    [Theory]
    [InlineData("ffmpeg")]
    [InlineData("winldd")]
    public async Task AuxiliaryOnlyVersionBumpRequiresANewExactGeneration(string auxiliary) {
        if (auxiliary == "winldd" && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        using var fixture = new CacheFixture();
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        string previous = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox);
        if (auxiliary == "ffmpeg") fixture.AuxiliaryRevision = "101";
        else fixture.WindowsRevision = "101";
        fixture.WriteDriver();
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        string current = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox);
        Assert.NotEqual(previous, current);
        Assert.Equal(2, fixture.Installs);
        Assert.Contains(HtmlBrowser.ReadCacheManifest(current).Runtimes, runtime => runtime.Name == "firefox" && runtime.Directory == "firefox-100");
        Assert.Contains(HtmlBrowser.ReadCacheManifest(current).Runtimes, runtime => runtime.Name == auxiliary && runtime.Directory == auxiliary + "-101");
    }

    [Fact]
    public async Task OfflineFallbackCannotRecordWithAnIncompatibleAuxiliaryRevision() {
        using var fixture = new CacheFixture();
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        string previous = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox);
        fixture.AuxiliaryRevision = "101";
        fixture.WriteDriver();
        Environment.SetEnvironmentVariable("HTMLTINKERX_BROWSER_OFFLINE", "1");
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        Assert.Equal(previous, HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => HtmlBrowser.OpenSessionForNavigationAsync(new HtmlBrowserLaunchOptions {
            Browser = HtmlBrowserEngine.Firefox, VideoPath = Path.Combine(fixture.Root, "recording.webm")
        }));
        Assert.Contains("FFmpeg", error.Message);
        Assert.True(HtmlBrowser.VerifyCacheSlot(previous, true, out _));
    }
    [Fact]
    public void MacAppBundleIcuResourcesAreVerifiedAtTheirFrameworkLocation() {
        using var fixture = new CacheFixture();
        string slot = Path.Combine(fixture.Root, "mac-image");
        string binary = "chromium-100/Chrome.app/Contents/MacOS/Chrome";
        string icu = "chromium-100/Chrome.app/Contents/Frameworks/Chromium.framework/Resources/icudtl.dat";
        string ffmpeg = "ffmpeg-100/ffmpeg-mac";
        foreach (string path in new[] { binary, icu, ffmpeg }) {
            string file = Path.Combine(slot, path);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "fixture data");
        }
        HtmlBrowser.WriteCacheManifest(slot, HtmlBrowserEngine.Chromium, new List<HtmlBrowser.CacheRuntime> {
            new() { Name = "chromium", Directory = "chromium-100", Executable = binary },
            new() { Name = "ffmpeg", Directory = "ffmpeg-100", Executable = ffmpeg }
        });
        Assert.True(HtmlBrowser.VerifyCacheSlot(slot, true, out _));
        File.Delete(Path.Combine(slot, icu));
        Assert.False(HtmlBrowser.VerifyCacheSlot(slot, false, out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HermeticGenerationsSurviveDriverReplacementAndFailedDriverDownload(bool failedDownload) {
        using var fixture = new CacheFixture();
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", "0");
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        string previous = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox);
        Assert.StartsWith(Path.Combine(fixture.Root, "driver", ".htmltinkerx-browsers"), previous);
        string driver = Path.Combine(fixture.Root, "driver", ".playwright");
        File.WriteAllText(Path.Combine(driver, ".version"), "0.0.1");
        var factory = HtmlBrowser.HttpClientFactory;
        using var handler = new HtmlBrowserDriverPackageTests.FakeHandler(HtmlBrowserDriverPackageTests.CreateDriverPackage(
            "{\"browsers\":[{\"name\":\"firefox\",\"revision\":\"100\"},{\"name\":\"ffmpeg\",\"revision\":\"100\"},{\"name\":\"winldd\",\"revision\":\"100\"}]}"));
        try {
            HtmlBrowser.HttpClientFactory = () => failedDownload ? throw new HttpRequestException("offline network") : new HttpClient(handler, false);
            if (failedDownload) await Assert.ThrowsAsync<HttpRequestException>(() => HtmlBrowser.EnsureDriverInstalledAsync());
            else await HtmlBrowser.EnsureDriverInstalledAsync();
            Assert.True(Directory.Exists(driver));
            Assert.True(HtmlBrowser.VerifyCacheSlot(previous, true, out _));
            Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(previous))!, "install.jsonl")));
            Assert.Equal(previous, HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox));
            Assert.Empty(Directory.GetDirectories(Path.Combine(fixture.Root, "driver"), ".playwright-previous-*"));
        } finally { HtmlBrowser.HttpClientFactory = factory; }
    }

    [Fact]
    public async Task OfflineModeNeverRequestsADriverDownload() {
        using var fixture = new CacheFixture();
        string version = Path.Combine(fixture.Root, "driver", ".playwright", ".version");
        File.WriteAllText(version, "0.0.1");
        Environment.SetEnvironmentVariable("HTMLTINKERX_BROWSER_OFFLINE", "1");
        var factory = HtmlBrowser.HttpClientFactory;
        int requests = 0;
        try {
            HtmlBrowser.HttpClientFactory = () => { requests++; throw new InvalidOperationException("Network must remain unused."); };
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox));
            Assert.Contains("offline", error.Message);
            Assert.Equal(0, requests);
            Assert.Equal("0.0.1", File.ReadAllText(version));
        } finally { HtmlBrowser.HttpClientFactory = factory; }
    }
    [Theory]
    [InlineData(null)]
    [InlineData("{not json")]
    [InlineData("{}")]
    public void InstalledDriverVersionUsesOneActionableErrorContract(string? content) {
        using var fixture = new CacheFixture();
        string package = Path.Combine(fixture.Root, "driver", ".playwright", "package", "package.json");
        if (content != null) File.WriteAllText(package, content);
        var error = Assert.Throws<InvalidDataException>(() => HtmlBrowser.GetInstalledDriverVersion());
        if (content == "{}") Assert.Null(error.InnerException);
        else Assert.NotNull(error.InnerException);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SessionCancellationDoesNotAbandonOrRaceTheSharedInstall(bool persistent) {
        using var fixture = new CacheFixture();
        var original = HtmlBrowser.PlaywrightFactory;
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        HtmlBrowser.CacheStageInstaller = async (slot, engine) => { entered.SetResult(true); await release.Task; await fixture.StageAsync(slot, engine); };
        HtmlBrowser.PlaywrightFactory = () => throw new InvalidOperationException("Canceled session must not launch later.");
        using var cancellation = new CancellationTokenSource();
        Task<HtmlBrowserSession> opening = HtmlBrowser.OpenSessionAsync("about:blank", new HtmlBrowserLaunchOptions {
            UserDataDirectory = persistent ? Path.Combine(fixture.Root, "profile") : null
        }, cancellation.Token);
        try {
            await entered.Task;
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening);
        } finally {
            release.SetResult(true);
            await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);
            HtmlBrowser.PlaywrightFactory = original;
        }
        Assert.Equal(1, fixture.Installs);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CrossProcessLockWaitsAndRecoversOnlyAfterOwnerExits(bool killed, bool orphanedWriter) {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        using var fixture = new CacheFixture();
        string command = (orphanedWriter ? "" : "$handle=[IO.File]::Open($env:HTMLTINKERX_INSTALL_LOCK_PATH,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None);")
            + "[Console]::WriteLine('locked');[Console]::ReadLine()|Out-Null;" + (orphanedWriter ? "" : "$handle.Dispose()");
        var start = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -Command \"" + command + "\"") {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        using Process owner = Process.Start(start)!;
        try {
            Assert.Equal("locked", await owner.StandardOutput.ReadLineAsync());
            File.WriteAllText(fixture.Lock + (orphanedWriter ? ".writer.json" : ".owner.json"), JsonSerializer.Serialize(new HtmlBrowserInstallOwner {
                ProcessId = owner.Id, ProcessStartUtc = owner.StartTime.ToUniversalTime(), Command = "powershell cache-lock fixture"
            }));
            Task install = HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
            var report = await HtmlBrowser.TestBrowserCacheAsync(HtmlBrowserEngine.Firefox);
            Assert.True(report.LockHeld);
            Assert.Equal(3, report.ExitCode);
            Assert.Equal(owner.Id, report.LockOwner!.ProcessId);
            Assert.Equal(0, fixture.Installs);
            Assert.False(install.IsCompleted);
            if (killed) owner.Kill();
            else await owner.StandardInput.WriteLineAsync("release");
            await Task.Run(() => owner.WaitForExit());
            await install;
            Assert.Equal(1, fixture.Installs);
        } finally { if (!owner.HasExited) { owner.Kill(); owner.WaitForExit(); } }
    }

    [Fact]
    public async Task BulkCleanupCannotRemoveVerifiedGenerationsOrInstallHistory() {
        using var fixture = new CacheFixture();
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        string slot = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox);
        var result = HtmlBrowserCacheCleaner.CleanCache(new[] { new HtmlBrowserCacheCleaner.CacheLocation { Path = fixture.Cache } });
        Assert.False(result.Success);
        Assert.True(HtmlBrowser.VerifyCacheSlot(slot, true, out _));
        Assert.True(File.Exists(Path.Combine(fixture.Cache, ".htmltinkerx", "install.jsonl")));
    }
    [Fact]
    public async Task MissingIcuRepairsBeforeLaunchAndKeepsOtherRevisions() {
        using var fixture = new CacheFixture();
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);
        string previous = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Chromium);
        string icu = Directory.GetFiles(previous, "icudtl.dat", SearchOption.AllDirectories).First();
        File.Delete(icu);
        Assert.False(HtmlBrowser.VerifyCacheSlot(previous, false, out string diagnosis));
        Assert.Contains("icudtl.dat", diagnosis);
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);
        string replacement = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Chromium);
        Assert.NotEqual(previous, replacement);
        Assert.True(HtmlBrowser.VerifyCacheSlot(replacement, true, out _));
        Assert.True(Directory.Exists(previous));
        Assert.Equal(2, fixture.Installs);
    }

    [Fact]
    public async Task WindowsPublicationWaitsForReleasedDirectoryHandleAndReusesGeneration() {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        using var fixture = new CacheFixture();
        SafeFileHandle? lease = null;
        Task release = Task.CompletedTask;
        HtmlBrowser.CacheSmokeTest = (slot, _) => {
            lease = OpenPromotionDirectory(slot, 1, (uint)(FileShare.Read | FileShare.Write), IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
            if (lease.IsInvalid) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            release = Task.Run(async () => { await Task.Delay(TimeSpan.FromSeconds(8)); lease.Dispose(); });
            return Task.CompletedTask;
        };
        try {
            await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);
            string generation = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Chromium);
            Assert.True(HtmlBrowser.VerifyCacheSlot(generation, true, out _));
            await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);
            Assert.Equal(generation, HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Chromium));
            Assert.Equal(1, fixture.Installs);
        } finally {
            lease?.Dispose();
            await release;
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle OpenPromotionDirectory(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

    [Fact]
    public async Task AtomicPublicationHidesStagingUntilSmokeSucceeds() {
        using var fixture = new CacheFixture();
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        string previous = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        HtmlBrowser.CacheSmokeTest = async (_, _) => { entered.SetResult(true); await finish.Task; };
        Task repair = HtmlBrowser.RepairInstallationAsync(HtmlBrowserEngine.Firefox);
        await entered.Task;
        Assert.Equal(previous, HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox));
        Assert.Single(Directory.GetDirectories(Path.Combine(fixture.Cache, ".htmltinkerx", "versions")));
        finish.SetResult(true);
        await repair;
        Assert.NotEqual(previous, HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox));
        Assert.True(Directory.Exists(previous));
    }

    [Fact]
    public async Task ConcurrentInstallersPublishOnceAndIntactLaunchDoesNotWait() {
        using var fixture = new CacheFixture();
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var install = HtmlBrowser.CacheStageInstaller!;
        HtmlBrowser.CacheStageInstaller = async (path, engine) => { entered.TrySetResult(true); await finish.Task; await install(path, engine); };
        Task first = HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);
        await entered.Task;
        Task second = HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);
        Task intact = HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        Assert.True(intact.IsCompleted);
        Assert.False(second.IsCompleted);
        finish.SetResult(true);
        await Task.WhenAll(first, second, intact);
        Assert.Equal(2, fixture.Installs); // one Firefox generation and one shared Chromium installation
    }

    [Fact]
    public async Task DeadOwnerMetadataDoesNotPreventLockRecovery() {
        using var fixture = new CacheFixture();
        File.WriteAllText(fixture.Lock + ".owner.json", JsonSerializer.Serialize(new HtmlBrowserInstallOwner {
            ProcessId = int.MaxValue, ProcessStartUtc = DateTime.UtcNow.AddDays(-1), Command = "dead installer"
        }));
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        var report = await HtmlBrowser.TestBrowserCacheAsync(HtmlBrowserEngine.Firefox);
        Assert.Equal(Process.GetCurrentProcess().Id, report.LockOwner!.ProcessId);
        Assert.False(report.LockHeld);
        Assert.Equal(0, report.ExitCode);
    }

    [Fact]
    public async Task VersionBumpPreservesOtherConsumersLinksAndVerifiedFallback() {
        using var fixture = new CacheFixture();
        string links = Path.Combine(fixture.Cache, ".links");
        Directory.CreateDirectory(links);
        File.WriteAllText(Path.Combine(links, "other-consumer"), "another repository's driver");
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        string previous = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox);
        fixture.Revision = "101";
        fixture.WriteDriver();
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        string current = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox);
        Assert.NotEqual(previous, current);
        Assert.True(HtmlBrowser.VerifyCacheSlot(previous, true, out _));
        Assert.Equal("another repository's driver", File.ReadAllText(Path.Combine(links, "other-consumer")));
        // Playwright's ordinary GC only considers browser-name-prefixed directories, not .htmltinkerx.
        Assert.StartsWith(Path.Combine(fixture.Cache, ".htmltinkerx"), previous);
        Assert.Contains("firefox-101", File.ReadAllText(Path.Combine(current, "runtime-plan.json")));
    }

    [Fact]
    public async Task OfflineVersionBumpFallsBackWithoutDeletingAnything() {
        using var fixture = new CacheFixture();
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        string previous = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox);
        fixture.Revision = "101";
        fixture.WriteDriver();
        Environment.SetEnvironmentVariable("HTMLTINKERX_BROWSER_OFFLINE", "1");
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        Assert.Equal(previous, HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox));
        Assert.Equal(1, fixture.Installs);
        var report = await HtmlBrowser.TestBrowserCacheAsync(HtmlBrowserEngine.Firefox);
        Assert.Equal(4, report.ExitCode);
        Assert.Contains(report.RecentInstalls, line => line.Contains("repair-failed") && line.Contains("offline"));
        Assert.True(HtmlBrowser.VerifyCacheSlot(previous, true, out _));
    }

    [Fact]
    public async Task OfflineEmptyCacheGivesActionableErrorAndNoDeletion() {
        using var fixture = new CacheFixture();
        Environment.SetEnvironmentVariable("HTMLTINKERX_BROWSER_OFFLINE", "1");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox));
        Assert.Contains("offline", error.Message);
        Assert.Contains("No working revisions were deleted", error.Message);
        Assert.Equal(0, fixture.Installs);
        Assert.False(Directory.Exists(Path.Combine(fixture.Cache, ".htmltinkerx", "versions")));
    }

    [Fact]
    public async Task FailedSmokeTestRetainsPreviousGenerationAndLogsFailure() {
        using var fixture = new CacheFixture();
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        string previous = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox);
        HtmlBrowser.CacheSmokeTest = (_, _) => throw new IOException("Firefox exit code 255");
        await HtmlBrowser.RepairInstallationAsync(HtmlBrowserEngine.Firefox);
        Assert.Equal(previous, HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox));
        Assert.Single(Directory.GetDirectories(Path.Combine(fixture.Cache, ".htmltinkerx", "versions")));
        Assert.Empty(Directory.GetDirectories(Path.Combine(fixture.Cache, ".htmltinkerx"), "staging-*"));
    }

    [Fact]
    public async Task DoctorDetectsSameSizeCorruptionAndReportsOwnerSpaceAndHistory() {
        using var fixture = new CacheFixture();
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        string slot = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox);
        string binary = HtmlBrowser.CacheFilePath(slot, HtmlBrowser.ReadCacheManifest(slot).Runtimes.Single(runtime => runtime.Name == "firefox").Executable);
        File.WriteAllText(binary, "evil"); // same size as the staged executable
        Assert.True(HtmlBrowser.VerifyCacheSlot(slot, false, out _));
        var report = await HtmlBrowser.TestBrowserCacheAsync(HtmlBrowserEngine.Firefox);
        Assert.Equal(2, report.ExitCode);
        Assert.Contains(report.Revisions, revision => revision.State == "incomplete" && revision.Message.Contains("SHA-256"));
        Assert.True(report.AvailableBytes > 0);
        Assert.NotNull(report.LockOwner);
        Assert.Contains(report.RecentInstalls, line => line.Contains("verified") && line.Contains("Added"));
    }

    [Theory]
    [InlineData("Invalid file descriptor to ICU data")]
    [InlineData("process did exit: exitCode=255")]
    [InlineData("Executable doesn't exist at cache/firefox")]
    public void KnownLaunchSignaturesAreCacheFailures(string message) => Assert.True(HtmlBrowser.IsCacheLaunchFailure(message));

    internal sealed class CacheFixture : IDisposable {
        private readonly Dictionary<string, string?> _environment = new();
        private readonly Func<string, HtmlBrowserEngine, Task>? _stage = HtmlBrowser.CacheStageInstaller;
        private readonly Func<string, HtmlBrowserEngine, Task>? _smoke = HtmlBrowser.CacheSmokeTest;
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "htmltinkerx-cache-test-" + Guid.NewGuid().ToString("N"));
        public string Cache => Path.Combine(Root, "cache");
        public string Lock => Path.Combine(Root, "install.lock");
        public string Revision { get; set; } = "100";
        public string? AuxiliaryRevision { get; set; }
        public string WindowsRevision { get; set; } = "100";
        public int Installs { get; private set; }

        public CacheFixture() {
            Directory.CreateDirectory(Root);
            Set("PLAYWRIGHT_BROWSERS_PATH", Cache);
            Set("PLAYWRIGHT_DRIVER_SEARCH_PATH", Path.Combine(Root, "driver"));
            Set("HTMLTINKERX_INSTALL_LOCK_PATH", Lock);
            Set("HTMLTINKERX_BROWSER_OFFLINE", null);
            Set("PLAYWRIGHT_HOST_PLATFORM_OVERRIDE", null);
            WriteDriver();
            HtmlBrowser.CacheStageInstaller = StageAsync;
            HtmlBrowser.CacheSmokeTest = (_, _) => Task.CompletedTask;
        }

        private void Set(string name, string? value) { _environment[name] = Environment.GetEnvironmentVariable(name); Environment.SetEnvironmentVariable(name, value); }

        public void WriteDriver() {
            string driver = Path.Combine(Root, "driver", ".playwright");
            string node = Path.Combine(driver, "node", PlatformExtensions.GetCurrentPlatform().ToPlatformId());
            string package = Path.Combine(driver, "package");
            Directory.CreateDirectory(node);
            Directory.CreateDirectory(package);
            File.WriteAllText(Path.Combine(node, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node"), "node");
            File.WriteAllText(Path.Combine(package, "cli.js"), "cli");
            File.WriteAllText(Path.Combine(driver, ".version"), typeof(Microsoft.Playwright.Playwright).Assembly.GetName().Version!.ToString(3));
            File.WriteAllText(Path.Combine(package, "browsers.json"), JsonSerializer.Serialize(new {
                browsers = new[] { "chromium", "chromium-headless-shell", "firefox", "webkit", "ffmpeg", "winldd" }.Select(name => new {
                    name, revision = name == "ffmpeg" ? AuxiliaryRevision ?? Revision : name == "winldd" ? WindowsRevision : Revision
                })
            }));
        }

        public Task StageAsync(string slot, HtmlBrowserEngine engine) {
            Installs++;
            return CreateStageAsync(slot, engine, Revision, AuxiliaryRevision, WindowsRevision);
        }

        internal static Task CreateStageAsync(string slot, HtmlBrowserEngine engine, string revision, string? auxiliaryRevision = null, string? windowsRevision = null) {
            var names = engine == HtmlBrowserEngine.Chromium ? new List<string> { "chromium", "chromium-headless-shell", "ffmpeg" } : new List<string> { engine.ToString().ToLowerInvariant(), "ffmpeg" };
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) names.Add("winldd");
            var runtimes = new List<HtmlBrowser.CacheRuntime>();
            foreach (string name in names) {
                string directory = name.Replace('-', '_') + "-" + (name == "ffmpeg" ? auxiliaryRevision ?? revision : name == "winldd" ? windowsRevision ?? revision : revision);
                Directory.CreateDirectory(Path.Combine(slot, directory));
                string binary = Path.Combine(directory, "browser.exe");
                File.WriteAllText(Path.Combine(slot, binary), "good");
                if (name.StartsWith("chromium")) File.WriteAllText(Path.Combine(slot, directory, "icudtl.dat"), "ICU data");
                File.WriteAllText(Path.Combine(slot, directory, "INSTALLATION_COMPLETE"), "");
                runtimes.Add(new HtmlBrowser.CacheRuntime { Name = name, Directory = directory, Executable = binary });
            }
            File.WriteAllText(Path.Combine(slot, "runtime-plan.json"), JsonSerializer.Serialize(runtimes));
            return Task.CompletedTask;
        }

        public void Dispose() {
            HtmlBrowser.CacheStageInstaller = _stage;
            HtmlBrowser.CacheSmokeTest = _smoke;
            foreach (var pair in _environment) Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            Directory.Delete(Root, true);
        }
    }
}
