using HtmlTinkerX;
using System.IO;
using Xunit;

namespace HtmlTinkerX.Tests;

/// <summary>
/// Tests list parsing functionality in <see cref="HtmlParser"/>.
/// </summary>
public class HtmlParserListTests {
    private static string GetSampleListHtml() {
        var baseDir = AppContext.BaseDirectory;
        var path = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Documents", "sample_lists.html"));
        return File.ReadAllText(path);
    }

    [Fact]
    /// <summary>
    /// Parses simple lists using AngleSharp.
    /// </summary>
    public void ParseListsWithAngleSharp_ReturnsItems() {
        string html = GetSampleListHtml();
        var lists = HtmlParser.ParseListsWithAngleSharp(html, " ");
        Assert.Equal(2, lists.Count);
        Assert.Equal(new[] { "Item1", "Item2" }, lists[0]);
        Assert.Equal(new[] { "First", "Second" }, lists[1]);
    }

    [Fact]
    /// <summary>
    /// Parses lists using HtmlAgilityPack.
    /// </summary>
    public void ParseListsWithHtmlAgilityPack_ReturnsItems() {
        string html = GetSampleListHtml();
        var lists = HtmlParser.ParseListsWithHtmlAgilityPack(html, " ");
        Assert.Equal(2, lists.Count);
        Assert.Equal(new[] { "Item1", "Item2" }, lists[0]);
        Assert.Equal(new[] { "First", "Second" }, lists[1]);
    }

    [Fact]
    /// <summary>
    /// Parses lists with AngleSharp and returns metadata.
    /// </summary>
    public void ParseListsWithAngleSharpDetailed_ReturnsMetadata() {
        string html = GetSampleListHtml();
        var lists = HtmlParser.ParseListsWithAngleSharpDetailed(html, " ");
        Assert.Equal(2, lists.Count);
        Assert.Equal(0, lists[0].Metadata.ListIndex);
        Assert.Equal(2, lists[0].Items.Count);
    }

    [Fact]
    /// <summary>
    /// Parses lists with HtmlAgilityPack and returns metadata.
    /// </summary>
    public void ParseListsWithHtmlAgilityPackDetailed_ReturnsMetadata() {
        string html = GetSampleListHtml();
        var lists = HtmlParser.ParseListsWithHtmlAgilityPackDetailed(html, " ");
        Assert.Equal(2, lists.Count);
        Assert.Equal(1, lists[1].Metadata.ListIndex);
        Assert.Equal(2, lists[1].Items.Count);
    }

    [Fact]
    public void ParseListsWithAngleSharpDetailed_NullHtml_Throws() {
        Assert.Throws<ArgumentNullException>(() => HtmlParser.ParseListsWithAngleSharpDetailed(null, " "));
    }
}