using HtmlTinkerX;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlParserTableSkipFooterTests {
    [Fact]
    public void ParseTablesWithAngleSharpDetailed_SkipFooter_OmitsTfootRows() {
        const string html = "<table><tr><th>A</th></tr><tr><td>1</td></tr><tfoot><tr><td>2</td></tr></tfoot></table>";
        var tables = HtmlParser.ParseTablesWithAngleSharpDetailed(html, null, null, false, true, false, null);
        Assert.Single(tables);
        var table = tables[0];
        Assert.Single(table.Data);
        Assert.Equal("1", table.Data[0].Values["A"]);
    }

    [Fact]
    public void ParseTablesWithHtmlAgilityPackDetailed_SkipFooter_OmitsTfootRows() {
        const string html = "<table><tr><th>A</th></tr><tr><td>1</td></tr><tfoot><tr><td>2</td></tr></tfoot></table>";
        var tables = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(html, false, null, null, false, true, false, null);
        Assert.Single(tables);
        var table = tables[0];
        Assert.Single(table.Data);
        Assert.Equal("1", table.Data[0].Values["A"]);
    }
}