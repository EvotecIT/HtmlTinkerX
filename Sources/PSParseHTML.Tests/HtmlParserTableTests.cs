using System.Collections.Generic;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlParserTableTests {
    private const string SimpleTable = "<table><tr><th>A</th><th>B</th></tr><tr><td>1</td><td>2</td></tr></table>";

    [Fact]
    public void ParseTablesWithAngleSharp_ReturnsData() {
        var tables = HtmlParser.ParseTablesWithAngleSharp(SimpleTable);
        Assert.Single(tables);
        Assert.Single(tables[0]);
        Assert.Equal("1", tables[0][0]["A"]);
        Assert.Equal("2", tables[0][0]["B"]);
    }

    [Fact]
    public void ParseTablesWithHtmlAgilityPack_ReturnsData() {
        var tables = HtmlParser.ParseTablesWithHtmlAgilityPack(SimpleTable);
        Assert.Single(tables);
        Assert.Single(tables[0]);
        Assert.Equal("1", tables[0][0]["A"]);
        Assert.Equal("2", tables[0][0]["B"]);
    }

    [Fact]
    public void ParseTablesWithHtmlAgilityPack_Reverse() {
        const string html = "<table><tr><th>K</th><td>V</td></tr><tr><th>X</th><td>Y</td></tr></table>";
        var tables = HtmlParser.ParseTablesWithHtmlAgilityPack(html, true);
        Assert.Single(tables);
        Assert.Single(tables[0]);
        var row = tables[0][0];
        Assert.Equal("V", row["K"]);
        Assert.Equal("Y", row["X"]);
    }
}
