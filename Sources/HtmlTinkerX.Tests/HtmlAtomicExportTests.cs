using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlAtomicExportTests {
    [Fact]
    public async Task SaveResultAsync_LockedPageJsonlPreservesPreviousPageDatasets() {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT) return;
        string directory = Path.Combine(Path.GetTempPath(), "HtmlAtomicExportTests", Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlResult result = new() {
                StartUrl = "https://example.test/",
                Pages = new[] { new HtmlCrawlPage { Url = "https://example.test/", Title = "Previous title" } }
            };
            await HtmlCrawler.SaveResultAsync(result, directory);
            string jsonlPath = Path.Combine(directory, "pages.jsonl");
            string csvPath = Path.Combine(directory, "pages.csv");
            byte[] previousJsonl = File.ReadAllBytes(jsonlPath);
            byte[] previousCsv = File.ReadAllBytes(csvPath);
            byte[] previousManifest = File.ReadAllBytes(result.ManifestPath!);
            result.Pages[0].Title = "Replacement title";
            using (FileStream lockedJsonl = new(jsonlPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                await Assert.ThrowsAnyAsync<IOException>(() => HtmlCrawler.SaveResultAsync(result, directory));
                Assert.Equal(previousJsonl, File.ReadAllBytes(jsonlPath));
                Assert.Equal(previousCsv, File.ReadAllBytes(csvPath));
                Assert.Equal(previousManifest, File.ReadAllBytes(result.ManifestPath!));
                Assert.Empty(Directory.GetFiles(directory, "*.tmp", SearchOption.AllDirectories));
            }
        } finally {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task WriteAtomicallyAsync_DoesNotPublishCanceledOrFailedExports(bool cancel, bool existingFile) {
        string directory = Path.Combine(Path.GetTempPath(), "HtmlAtomicExportTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "export.json");
        if (existingFile) File.WriteAllText(path, "previous export");
        using CancellationTokenSource cancellation = new();
        try {
            Task writing = HtmlUtilities.WriteAtomicallyAsync(path, async (stream, token) => {
                byte[] partial = Encoding.UTF8.GetBytes("{\"partial\":");
                await stream.WriteAsync(partial, 0, partial.Length, token);
                if (cancel) cancellation.Cancel();
                else throw new InvalidDataException("The export producer failed.");
            }, cancellation.Token);
            if (cancel) {
                OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writing);
                Assert.Equal(cancellation.Token, error.CancellationToken);
            } else await Assert.ThrowsAsync<InvalidDataException>(() => writing);
            if (existingFile) Assert.Equal("previous export", File.ReadAllText(path));
            else Assert.False(File.Exists(path));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }
}
