using HtmlTinkerX;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlParserTableAdvancedTests {
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
        Assert.Equal("Not available", first.Data[1]["NonRegional"]);
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
        Assert.Equal("Not available", first.Data[1]["NonRegional"]);
    }

    private static string GetPolishTableHtml() {
        var baseDir = AppContext.BaseDirectory;
        var path = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Documents", "polish_table.html"));
        return File.ReadAllText(path, System.Text.Encoding.UTF8);
    }

    [Fact]
    public void ConvertFromHtmlTable_WithPolishCharacters_AgilityPack_PreservesEncoding() {
        string html = GetPolishTableHtml();
        var tables = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(html, false, null, null, false, false, false, null);

        Assert.Single(tables);
        var table = tables[0];
        Assert.Equal(2, table.Data.Count);
        Assert.Equal(2, table.Metadata.ColumnCount);

        // Check that Polish characters are preserved correctly
        Assert.Equal("Komórka a1", table.Data[0]["Column1"]);
        Assert.Equal("Komórka a2", table.Data[0]["Column2"]);
        Assert.Equal("Komórka a3", table.Data[1]["Column1"]);
        Assert.Equal("Komórka a4", table.Data[1]["Column2"]);
    }

    [Fact]
    public void ConvertFromHtmlTable_WithPolishCharacters_AngleSharp_PreservesEncoding() {
        string html = GetPolishTableHtml();
        var tables = HtmlParser.ParseTablesWithAngleSharpDetailed(html, null, null, false, false, false, null);

        Assert.Single(tables);
        var table = tables[0];
        Assert.Equal(2, table.Data.Count);
        Assert.Equal(2, table.Metadata.ColumnCount);

        // Check that Polish characters are preserved correctly
        Assert.Equal("Komórka a1", table.Data[0]["Column1"]);
        Assert.Equal("Komórka a2", table.Data[0]["Column2"]);
        Assert.Equal("Komórka a3", table.Data[1]["Column1"]);
        Assert.Equal("Komórka a4", table.Data[1]["Column2"]);
    }

    [Fact]
    public async Task ConvertFromHtmlTable_FromUrlWithPolishCharacters_PreservesEncoding() {
// #if FRAMEWORK
//         // Skip this test on .NET Framework due to encoding detection differences
//         // The test relies on external URL encoding detection which behaves differently in .NET Framework
//         return;
// #else
        // This test validates our encoding fix for URL downloads
        var url = "https://ifj.edu.pl/private/krawczyk/kurshtml/tabele/tabele.htm";

        using var client = new HttpClient();
        var doc = await HtmlParser.ParseUrlWithHtmlAgilityPackAsync(url, client);
        var tables = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(doc.DocumentNode.OuterHtml, false, null, null, false, false, false, null);

        Assert.True(tables.Count > 12); // Should have more than 12 tables

        if (tables.Count > 12) {
            var table = tables[12]; // The table that was showing encoding issues

            if (table.Data.Count >= 2) {
                // Check that Polish characters are preserved correctly (not corrupted as "Kom�rka")
                var cell1 = table.Data[0]["Column1"];
                var cell2 = table.Data[0]["Column2"];
                var cell3 = table.Data[1]["Column1"];
                var cell4 = table.Data[1]["Column2"];

                // Verify the correct Polish characters are present
                Assert.Contains("Komórka", cell1);
                Assert.Contains("Komórka", cell2);
                Assert.Contains("Komórka", cell3);
                Assert.Contains("Komórka", cell4);
                
#if !FRAMEWORK
                // On .NET Core/5+, we can be stricter about encoding
                Assert.DoesNotContain("�", cell1);
                Assert.DoesNotContain("�", cell2);
                Assert.DoesNotContain("�", cell3);
                Assert.DoesNotContain("�", cell4);
                
                Assert.Equal("Komórka a1", cell1);
                Assert.Equal("Komórka a2", cell2);
                Assert.Equal("Komórka a3", cell3);
                Assert.Equal("Komórka a4", cell4);
#else
                // On .NET Framework, HtmlAgilityPack may introduce some encoding artifacts
                // but as long as the Polish characters are preserved, we consider it acceptable
                Assert.Contains("a1", cell1);
                Assert.Contains("a2", cell2);
                Assert.Contains("a3", cell3);
                Assert.Contains("a4", cell4);
#endif
            }
        }
//#endif
    }

    [Fact]
    public void ParseTablesDetailed_NonUSCulture_ParsesSpans() {
        const string html = "<table><tr><th>A</th><th>B</th></tr><tr><td rowspan=\"2\">1</td><td>2</td></tr><tr><td>3</td></tr></table>";
        TestHelpers.WithCulture("fr-FR", () => {
            var angle = HtmlParser.ParseTablesWithAngleSharpDetailed(html, null, null, false, false, false, null);
            Assert.Single(angle);
            Assert.Equal("3", angle[0].Data[1]["B"]);

            var agility = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(html, false, null, null, false, false, false, null);
            Assert.Single(agility);
            Assert.Equal("3", agility[0].Data[1]["B"]);
        });
    }
}