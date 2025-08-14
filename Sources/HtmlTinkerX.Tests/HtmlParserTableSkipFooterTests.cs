using HtmlTinkerX;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlParserTableSkipFooterTests {
    [Fact]
    public void ParseTablesWithAngleSharpDetailed_SkipFooter_OmitsTfootRows() {
        const string html = "<table><tr><th>A</th></tr><tr><td>1</td></tr><tfoot><tr><td>2</td></tr></tfoot></table>";
        var tables = HtmlParser.ParseTablesWithAngleSharpDetailed(html, null, null, false, true, false, null);
        Assert.Single(tables);
        var table = tables[0];
        Assert.Single(table.Data);
        Assert.Equal("1", table.Data[0]["A"]);
    }

    [Fact]
    public void ParseTablesWithHtmlAgilityPackDetailed_SkipFooter_OmitsTfootRows() {
        const string html = "<table><tr><th>A</th></tr><tr><td>1</td></tr><tfoot><tr><td>2</td></tr></tfoot></table>";
        var tables = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(html, false, null, null, false, true, false, null);
        Assert.Single(tables);
        var table = tables[0];
        Assert.Single(table.Data);
        Assert.Equal("1", table.Data[0]["A"]);
    }

    [Fact]
    public void ParseTablesWithDetailedParsers_NonUSCulture_ParsesSpans() {
        const string html = "<table><tr><th>A</th><th>B</th></tr><tr><td colspan=\"2\">1</td></tr><tfoot><tr><td colspan=\"2\">2</td></tr></tfoot></table>";
        TestHelpers.WithCulture("fr-FR", () => {
            var angle = HtmlParser.ParseTablesWithAngleSharpDetailed(html, null, null, false, true, false, null);
            Assert.Single(angle);
            Assert.Single(angle[0].Data);
            Assert.Equal("1", angle[0].Data[0]["A"]);
            Assert.Equal("1", angle[0].Data[0]["B"]);

            var agility = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(html, false, null, null, false, true, false, null);
            Assert.Single(agility);
            Assert.Single(agility[0].Data);
            Assert.Equal("1", agility[0].Data[0]["A"]);
            Assert.Equal("1", agility[0].Data[0]["B"]);
        });
    }
}