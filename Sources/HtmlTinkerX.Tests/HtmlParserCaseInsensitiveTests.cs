using System.Collections.Generic;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlParserCaseInsensitiveTests {
    [Fact]
    public void AngleSharp_ReplacesHeadersAndContent_CaseInsensitive() {
        const string html = "<table><tr><th>HEADER</th></tr><tr><td>value</td></tr></table>";
        var tables = HtmlParser.ParseTablesWithAngleSharpDetailed(
            html,
            new Dictionary<string, string> { ["VALUE"] = "replaced" },
            new Dictionary<string, string> { ["header"] = "H" });

        Assert.Single(tables);
        var table = tables[0];
        Assert.Single(table.Metadata.Headers);
        Assert.Equal("H", table.Metadata.Headers[0]);
        Assert.Equal("replaced", table.Data[0]["H"]);
    }

    [Fact]
    public void HtmlAgilityPack_ReplacesHeadersAndContent_CaseInsensitive() {
        const string html = "<table><tr><th>HEADER</th></tr><tr><td>VALUE</td></tr></table>";
        var tables = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(
            html,
            replaceContent: new Dictionary<string, string> { ["value"] = "replaced" },
            replaceHeaders: new Dictionary<string, string> { ["header"] = "H" });

        Assert.Single(tables);
        var table = tables[0];
        Assert.Single(table.Metadata.Headers);
        Assert.Equal("H", table.Metadata.Headers[0]);
        Assert.Equal("replaced", table.Data[0]["H"]);
    }
}
