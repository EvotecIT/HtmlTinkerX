using HtmlTinkerX;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace PSParseHTML.Tests;

/// <summary>
/// Tests for <see cref="HtmlHarViewer"/> helper methods.
/// </summary>
public class HtmlHarViewerTests {
    private static string GetHarPath() {
        var baseDir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "Tests", "Documents", "sample.har"));
    }

    private static string GetMinimalHarPath() {
        var baseDir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "Tests", "Documents", "minimal.har"));
    }

    [Fact]
    /// <summary>
    /// Generates HTML viewer content from a HAR file.
    /// </summary>
    public async Task BuildViewerHtml_ReturnsHtml() {
        Har har = await HtmlHarViewer.ReadHarAsync(GetHarPath());
        string html = HtmlHarViewer.BuildViewerHtml(har);
        Assert.Contains("<table>", html);
    }

    [Fact]
    /// <summary>
    /// Reads a minimal HAR file and populates entries.
    /// </summary>
    public async Task ReadHarAsync_PopulatesEntries() {
        Har har = await HtmlHarViewer.ReadHarAsync(GetMinimalHarPath());
        Assert.NotNull(har.Log);
        Assert.NotNull(har.Log!.Entries);
        Assert.NotEmpty(har.Log.Entries);
    }

    [Fact]
    /// <summary>
    /// Verifies invalid JSON causes <see cref="InvalidDataException"/>.
    /// </summary>
    public async Task ReadHarAsync_InvalidJsonThrows() {
        string path = Path.GetTempFileName();
        try {
            await File.WriteAllTextAsync(path, "{ invalid ");
            await Assert.ThrowsAsync<InvalidDataException>(() => HtmlHarViewer.ReadHarAsync(path));
        } finally {
            File.Delete(path);
        }
    }
}