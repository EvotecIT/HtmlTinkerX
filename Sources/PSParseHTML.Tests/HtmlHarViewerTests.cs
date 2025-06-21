using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlHarViewerTests {
    private static string GetHarPath() {
        var baseDir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "Tests", "Documents", "sample.har"));
    }

    [Fact]
    public async Task BuildViewerHtml_ReturnsHtml() {
        Har har = await HtmlHarViewer.ReadHarAsync(GetHarPath());
        string html = HtmlHarViewer.BuildViewerHtml(har);
        Assert.Contains("<table>", html);
    }
}
