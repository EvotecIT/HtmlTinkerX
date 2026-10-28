using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Process diagnostics")]
public sealed class HtmlBrowserCacheDownloaderTests {
    [Fact]
    public async Task RealDownloaderHandlesInterruptedKilledCorruptAndMirrorTransfers() {
        string root = Path.Combine(Path.GetTempPath(), "htmltinkerx-download-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string script = Path.Combine(root, "downloader.cjs");
        try {
            using (Stream source = typeof(HtmlBrowser).Assembly.GetManifestResourceStream("HtmlTinkerX.Playwright.Scripts.HtmlBrowserCache.cjs")!)
            using (FileStream target = File.Create(script)) source.CopyTo(target);
            string zip = Path.Combine(root, "valid.zip");
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(archive.CreateEntry("payload.txt", CompressionLevel.NoCompression).Open())) writer.Write("browser archive data");
            string package = Path.Combine(AppContext.BaseDirectory, ".playwright", "package");
            string node = Path.Combine(AppContext.BaseDirectory, ".playwright", "node", PlatformExtensions.GetCurrentPlatform().ToPlatformId(),
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "node.exe" : "node");
            var start = new ProcessStartInfo(node, "--test --test-isolation=none --test-timeout=20000 \"" + Path.Combine(AppContext.BaseDirectory, "BrowserCacheDownloader.test.cjs") + "\"") {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
            };
            start.EnvironmentVariables["HTMLTINKERX_TEST_DOWNLOADER"] = script;
            start.EnvironmentVariables["HTMLTINKERX_TEST_PACKAGE"] = package;
            start.EnvironmentVariables["HTMLTINKERX_TEST_ZIP"] = zip;
            start.EnvironmentVariables["NO_PROXY"] = "127.0.0.1,localhost";
            using Process process = Process.Start(start)!;
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> errors = process.StandardError.ReadToEndAsync();
            if (!await Task.Run(() => process.WaitForExit(60000))) {
#if NETFRAMEWORK
                using var terminate = Process.Start(new ProcessStartInfo("taskkill", $"/PID {process.Id} /T /F") { UseShellExecute = false, CreateNoWindow = true });
                terminate?.WaitForExit();
#else
                process.Kill(entireProcessTree: true);
#endif
                throw new TimeoutException("Downloader contract tests exceeded 60 seconds. " + await output + await errors);
            }
            Assert.True(process.ExitCode == 0, (await output) + (await errors));
            Assert.Contains("pass 5", await output);
        } finally { Directory.Delete(root, true); }
    }
}
