using HtmlTinkerX;
using System.IO;
using Xunit;

namespace HtmlTinkerX.Tests;

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
        Assert.Equal("Open Graph Title", og.Properties.Find(p => p.Name == "title")?.Values[0]);
        Assert.Equal("https://example.com/img.png", og.Properties.Find(p => p.Name == "image")?.Values[0]);
    }

    [Fact]
    public void ParseOpenGraph_NullHtml_Throws() {
        Assert.Throws<ArgumentNullException>(() => HtmlParser.ParseOpenGraph(null!));
    }
}
