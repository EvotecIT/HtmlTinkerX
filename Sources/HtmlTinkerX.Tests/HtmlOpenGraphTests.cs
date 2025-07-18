using HtmlTinkerX;
using System.IO;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlOpenGraphTests {
    private static string GetSampleHtml() {
        var baseDir = AppContext.BaseDirectory;
        var path = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Documents", "sample_open_graph.html"));
        return File.ReadAllText(path);
    }

    [Fact]
    public void ParseOpenGraph_ReturnsExpected() {
        string html = GetSampleHtml();
        var og = HtmlParser.ParseOpenGraph(html);
        Assert.Equal("Open Graph Title", og.Properties["title"][0]);
        Assert.Equal("https://example.com/img.png", og.Properties["image"][0]);
    }
}
