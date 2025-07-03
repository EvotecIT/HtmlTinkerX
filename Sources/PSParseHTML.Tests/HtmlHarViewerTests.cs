using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace PSParseHTML.Tests;

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
    public async Task BuildViewerHtml_ReturnsHtml() {
        Har har = await HtmlHarViewer.ReadHarAsync(GetHarPath());
        string html = HtmlHarViewer.BuildViewerHtml(har);
        Assert.Contains("<table>", html);
    }

    [Fact]
    public async Task ReadHarAsync_PopulatesEntries() {
        Har har = await HtmlHarViewer.ReadHarAsync(GetMinimalHarPath());
        Assert.NotNull(har.Log);
        Assert.NotNull(har.Log!.Entries);
        Assert.NotEmpty(har.Log.Entries);
    }

    [Fact]
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
