using System.IO;
using Xunit;

namespace PSParseHTML.Tests;

public class HeadlessTableTests {
    private static string GetHtml() {
        var baseDir = AppContext.BaseDirectory;
        var path = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Documents", "headless_table.html"));
        return File.ReadAllText(path);
    }

    [Fact]
    public void ParseHeadlessTable_AddsDefaultHeaders() {
        string html = GetHtml();
        var tables = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(html, false, null, null, true);
        Assert.True(tables.Count >= 1);
        Assert.True(tables[0].Data.Count > 0);
        Assert.Equal("Column1", tables[0].Metadata.Headers[0]);
    }
}
