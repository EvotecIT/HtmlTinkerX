using System.IO;
using System.Linq;
using Xunit;

namespace PSParseHTML.Tests;

public class ConvertFromHtmlTableTests {
    private static string GetAzureStatusHtmlFromSources() {
        var baseDir = AppContext.BaseDirectory;
        var path = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Documents", "azure_status.html"));
        return File.ReadAllText(path);
    }

    private static string GetAzureStatusHtmlFromTests() {
        var baseDir = AppContext.BaseDirectory;
        var path = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "Tests", "Documents", "azure_status.html"));
        return File.ReadAllText(path);
    }

    [Fact]
    public void ConvertFromHtmlTable_WithHtmlAgilityPack_ReturnsFirstRows() {
        string html = GetAzureStatusHtmlFromSources();
        var tables = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(html, false, null, null, true, false, true, "--");
        var dataTables = tables.Where(t => t.Metadata.RowCount > 1).ToList();

        Assert.True(dataTables.Count >= 7);
        foreach (var table in dataTables.Take(7)) {
            Assert.True(table.Data.Count >= 2);
            Assert.Equal(table.Metadata.ColumnCount, table.Data[0].Count);
            Assert.Equal(table.Metadata.ColumnCount, table.Data[1].Count);
        }

        var first = dataTables[0];
        Assert.Contains("NonRegional", first.Metadata.Headers);
        Assert.DoesNotContain("*Non-Regional", first.Metadata.Headers);
        Assert.Equal("--", first.Data[0]["NonRegional"]);
    }

    [Fact]
    public void ConvertFromHtmlTable_WithAngleSharp_ReturnsFirstRows() {
        string html = GetAzureStatusHtmlFromTests();
        var tables = HtmlParser.ParseTablesWithAngleSharpDetailed(html, null, null, true, false, true, "--");
        var dataTables = tables.Where(t => t.Metadata.RowCount > 1).ToList();

        Assert.True(dataTables.Count >= 7);
        foreach (var table in dataTables.Take(7)) {
            Assert.True(table.Data.Count >= 2);
            Assert.Equal(table.Metadata.ColumnCount, table.Data[0].Count);
            Assert.Equal(table.Metadata.ColumnCount, table.Data[1].Count);
        }

        var first = dataTables[0];
        Assert.Contains("NonRegional", first.Metadata.Headers);
        Assert.DoesNotContain("*Non-Regional", first.Metadata.Headers);
        Assert.Equal("--", first.Data[0]["NonRegional"]);
    }
}
