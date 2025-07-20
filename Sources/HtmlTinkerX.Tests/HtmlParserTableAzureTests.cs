using HtmlTinkerX;
using System.IO;
using System.Linq;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlParserTableAzureTests {
    private static string GetAzureStatusHtml() {
        var baseDir = AppContext.BaseDirectory;
        var path = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Documents", "azure_status.html"));
        return File.ReadAllText(path);
    }

    [Fact]
    public void ParseAzureStatus_WithHtmlAgilityPack_ReturnsTables() {
        string html = GetAzureStatusHtml();
        var tables = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(html);
        var dataTables = tables.Where(t => t.Metadata.RowCount > 1).ToList();

        Assert.True(dataTables.Count >= 7);
        foreach (var t in dataTables) {
            Assert.True(t.Metadata.ColumnCount >= 4);
            Assert.Contains("Products and services", t.Metadata.Headers);
        }

        var first = dataTables[0];
        Assert.Contains("*Non-Regional", first.Metadata.Headers);
        Assert.Contains("East US", first.Metadata.Headers);
        Assert.Equal("Compute", first.Data[0].Values["Products and services"]);
    }

    [Fact]
    public void ParseAzureStatus_WithAngleSharp_ReturnsTables() {
        string html = GetAzureStatusHtml();
        var tables = HtmlParser.ParseTablesWithAngleSharpDetailed(html);
        var dataTables = tables.Where(t => t.Metadata.RowCount > 1).ToList();

        Assert.True(dataTables.Count >= 7);
        foreach (var t in dataTables) {
            Assert.True(t.Metadata.ColumnCount >= 4);
            Assert.Contains("Products and services", t.Metadata.Headers);
        }

        var first = dataTables[0];
        Assert.Contains("*Non-Regional", first.Metadata.Headers);
        Assert.Contains("East US", first.Metadata.Headers);
        Assert.Equal("Compute", first.Data[0].Values["Products and services"]);
    }
}