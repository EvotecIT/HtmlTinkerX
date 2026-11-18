using HtmlTinkerX;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Playwright collection")]
public class HtmlBrowserTracingTests {
    [Fact]
    public async Task StartAndStopTracingAsync_CreatesReadableTraceWithRecordedAction() {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string file = Path.Combine(dir, "trace.zip");
        HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync("about:blank", new HtmlBrowserLaunchOptions {
            Browser = HtmlBrowserEngine.Chromium,
            Headless = true
        });
        try {
            await HtmlBrowser.StartTracingAsync(session);
            await session.Page.SetContentAsync("<button id=\"trace-probe\">Trace action</button>");
            await session.Page.Locator("#trace-probe").ClickAsync();
            await HtmlBrowser.StopTracingAsync(session, file);

            Assert.True(Directory.Exists(dir));
            using var archive = new ZipArchive(File.OpenRead(file), ZipArchiveMode.Read);
            Assert.Contains(archive.Entries, entry => entry.FullName.EndsWith(".network", StringComparison.Ordinal));
            var actions = Assert.Single(archive.Entries, entry => entry.FullName.EndsWith(".trace", StringComparison.Ordinal));
            using var reader = new StreamReader(actions.Open());
            Assert.Contains("trace-probe", reader.ReadToEnd());
        } finally {
            await session.DisposeAsync();
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
