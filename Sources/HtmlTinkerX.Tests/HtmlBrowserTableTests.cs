using HtmlTinkerX;
using System.IO;
using Xunit;

namespace PSParseHTML.Tests;

/// <summary>
/// Tests parsing of tables without header rows using <see cref="HtmlParser"/>.
/// </summary>
public class HtmlBrowserTableTests {
    private static string GetHtml() {
        var baseDir = AppContext.BaseDirectory;
        var path = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Documents", "headless_table.html"));
        return File.ReadAllText(path);
    }

    [Fact]
    /// <summary>
    /// Ensures tables without headers get default column names.
    /// </summary>
    public void ParseHeadlessTable_AddsDefaultHeaders() {
        string html = GetHtml();
        var tables = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(html, false, null, null, true);
        Assert.True(tables.Count >= 1);
        Assert.True(tables[0].Data.Count > 0);
        Assert.Equal("Column1", tables[0].Metadata.Headers[0]);
    }

    [Fact]
    /// <summary>
    /// Verifies detection of multiple tables on a page.
    /// </summary>
    public void ParseHeadlessTable_DetectsAllTables() {
        string html = GetHtml();
        var tables = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(html, false, null, null, true);

        Assert.Equal(4, tables.Count);

        Assert.Equal("Data64-bit", tables[0].Data[0]["Column3"]);
        Assert.Equal("Source", tables[2].Data[0]["Column1"]);
        Assert.Equal("D:", tables[2].Data[0]["Column2"]);
        Assert.Contains("Column3", tables[3].Metadata.Headers);
    }
}