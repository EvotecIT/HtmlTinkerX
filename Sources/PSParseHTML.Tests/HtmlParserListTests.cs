using System.IO;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlParserListTests {
    private static string GetSampleListHtml() {
        var baseDir = AppContext.BaseDirectory;
        var path = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Documents", "sample_lists.html"));
        return File.ReadAllText(path);
    }

    [Fact]
    public void ParseListsWithAngleSharp_ReturnsItems() {
        string html = GetSampleListHtml();
        var lists = HtmlParser.ParseListsWithAngleSharp(html);
        Assert.Equal(2, lists.Count);
        Assert.Equal(new[] { "Item1", "Item2" }, lists[0]);
        Assert.Equal(new[] { "First", "Second" }, lists[1]);
    }

    [Fact]
    public void ParseListsWithHtmlAgilityPack_ReturnsItems() {
        string html = GetSampleListHtml();
        var lists = HtmlParser.ParseListsWithHtmlAgilityPack(html);
        Assert.Equal(2, lists.Count);
        Assert.Equal(new[] { "Item1", "Item2" }, lists[0]);
        Assert.Equal(new[] { "First", "Second" }, lists[1]);
    }
}
