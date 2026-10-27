using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Process diagnostics")]
public sealed class HtmlBrowserRecordingRepairTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RecordingSavesVideoWithAnIntactOrRepairedDriver(bool persistent, bool corrupt) {
        await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Firefox);
        string source = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Firefox);
        string root = Path.Combine(Path.GetTempPath(), "htmltinkerx-recording-repair-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string? originalCache = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        string? originalLock = Environment.GetEnvironmentVariable("HTMLTINKERX_INSTALL_LOCK_PATH");
        var originalStage = HtmlBrowser.CacheStageInstaller;
        int stages = 0;
        int launches = 0;
        string? rejected = null;
        string video = Path.Combine(root, "recording.webm");
        try {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", Path.Combine(root, "cache"));
            Environment.SetEnvironmentVariable("HTMLTINKERX_INSTALL_LOCK_PATH", Path.Combine(root, "install.lock"));
            HtmlBrowser.CacheStageInstaller = (slot, _) => {
                stages++;
                // Reuse real verified runtime bytes; the controlled repair needs no second browser download.
                foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)) {
                    string relative = file.Substring(source.Length + 1);
                    if (relative == HtmlBrowser.CacheManifestName || relative == "launch-failed.txt") continue;
                    string target = Path.Combine(slot, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(file, target);
#if !NETFRAMEWORK
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) File.SetUnixFileMode(target, File.GetUnixFileMode(file));
#endif
                }
                return Task.CompletedTask;
            };
            using var boundary = HtmlBrowser.OverrideCacheLaunchStarting(slot => {
                string ffmpeg = HtmlBrowser.CacheFilePath(slot, HtmlBrowser.ReadCacheManifest(slot).Runtimes.Single(runtime => runtime.Name == "ffmpeg").Executable);
                if (++launches == 1 && corrupt) {
                    rejected = slot;
                    File.Delete(ffmpeg); // Driver registry already captured this generation.
                } else {
                    if (corrupt) Assert.NotEqual(rejected, slot);
                    Assert.True(File.Exists(ffmpeg));
                }
            });
            await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync("about:blank", new HtmlBrowserLaunchOptions {
                Browser = HtmlBrowserEngine.Firefox, UserDataDirectory = persistent ? Path.Combine(root, "profile") : null, VideoPath = video,
                ViewportWidth = 160, ViewportHeight = 120
            });
            await session.Page.SetContentAsync("<body style='background:teal'>Repaired recorder</body>");
            await session.Page.ScreenshotAsync();
            await HtmlBrowser.StopVideoRecordingAsync(session);
            Assert.Equal(corrupt ? 2 : 1, launches);
            Assert.Equal(corrupt ? 2 : 1, stages);
            Assert.True(File.Exists(video));
            Assert.True(new FileInfo(video).Length > 0);
            if (corrupt) Assert.True(File.Exists(Path.Combine(rejected!, "launch-failed.txt")));
        } finally {
            HtmlBrowser.CacheStageInstaller = originalStage;
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", originalCache);
            Environment.SetEnvironmentVariable("HTMLTINKERX_INSTALL_LOCK_PATH", originalLock);
            for (int attempt = 0; ; attempt++) {
                try { Directory.Delete(root, true); break; }
                catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && attempt < 20) {
                    await Task.Delay(250); // Just-closed browser processes can briefly retain executable handles.
                }
            }
        }
    }
}
