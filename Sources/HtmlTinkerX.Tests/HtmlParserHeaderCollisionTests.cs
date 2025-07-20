using HtmlTinkerX;
using Xunit;

namespace HtmlTinkerX.Tests;

/// <summary>
/// Tests header collision handling when parsing tables.
/// </summary>
public class HtmlParserHeaderCollisionTests {
    private const string DuplicateHeaders = "<table><tr><th>A*</th><th>A#</th><th>A!</th><th>B</th></tr><tr><td>1</td><td>2</td><td>3</td><td>4</td></tr></table>";

    [Fact]
    /// <summary>
    /// Ensures duplicate headers are made unique when using AngleSharp.
    /// </summary>
    public void ParseTablesWithAngleSharpDetailed_UniqueHeaders() {
        var tables = HtmlParser.ParseTablesWithAngleSharpDetailed(DuplicateHeaders, null, null, true, false, true, null);
        var result = tables[0];
        Assert.Equal(new[] { "A", "A1", "A2", "B" }, result.Metadata.Headers);
        var row = result.Data[0];
        Assert.Equal("1", row["A"]);
        Assert.Equal("2", row["A1"]);
        Assert.Equal("3", row["A2"]);
        Assert.Equal("4", row["B"]);
    }

    [Fact]
    /// <summary>
    /// Ensures duplicate headers are made unique when using HtmlAgilityPack.
    /// </summary>
    public void ParseTablesWithHtmlAgilityPackDetailed_UniqueHeaders() {
        var tables = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(DuplicateHeaders, false, null, null, true, false, true, null);
        var result = tables[0];
        Assert.Equal(new[] { "A", "A1", "A2", "B" }, result.Metadata.Headers);
        var row = result.Data[0];
        Assert.Equal("1", row["A"]);
        Assert.Equal("2", row["A1"]);
        Assert.Equal("3", row["A2"]);
        Assert.Equal("4", row["B"]);
    }
}