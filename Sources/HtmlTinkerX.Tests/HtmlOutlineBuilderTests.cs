using HtmlTinkerX;
using System.IO;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlOutlineBuilderTests {
    private static string GetOutlineHtml() {
        var baseDir = AppContext.BaseDirectory;
        var path = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Documents", "outline.html"));
        return File.ReadAllText(path);
    }

    [Fact]
    public void Build_ReturnsHierarchy() {
        string html = GetOutlineHtml();
        var outline = HtmlOutlineBuilder.Build(html, HtmlParserEngine.AgilityPack);
        Assert.Equal(2, outline.Count);
        Assert.Equal("Section 1", outline[0].Title);
        Assert.Equal(2, outline[0].Children.Count);
        Assert.Equal("Subsection 1.1", outline[0].Children[0].Title);
        Assert.Single(outline[0].Children[0].Children);
        Assert.Equal("Detail 1.1.1", outline[0].Children[0].Children[0].Title);
        Assert.Equal("Section 2", outline[1].Title);
    }

    [Theory]
    [InlineData(HtmlParserEngine.AgilityPack)]
    [InlineData(HtmlParserEngine.AngleSharp)]
    public void Build_SkipsMalformedHeadings(HtmlParserEngine engine) {
        string html = "<h1>Good</h1><hX>Bad</hX><h1>Also Good</h1>";
        var outline = HtmlOutlineBuilder.Build(html, engine);
        Assert.Equal(2, outline.Count);
        Assert.Equal("Good", outline[0].Title);
        Assert.Equal("Also Good", outline[1].Title);
    }
}