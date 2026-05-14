using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlTableDataExtensionsTests {
    [Fact]
    public void ToDataTable_UsesMetadataHeadersAndCanInferTypes() {
        const string html = """
<table id="sales-report">
  <thead><tr><th>Name</th><th>Amount</th><th>Active</th></tr></thead>
  <tbody>
    <tr><td>North</td><td>12.50</td><td>true</td></tr>
    <tr><td>South</td><td>7.25</td><td>false</td></tr>
  </tbody>
</table>
""";

        HtmlTableResult table = HtmlParser.ParseTablesWithAngleSharpDetailed(html).Single();
        DataTable dataTable = table.ToDataTable(inferTypes: true);

        Assert.Equal("sales_report", dataTable.TableName);
        Assert.Equal(3, dataTable.Columns.Count);
        Assert.Equal(typeof(string), dataTable.Columns["Name"]!.DataType);
        Assert.Equal(typeof(decimal), dataTable.Columns["Amount"]!.DataType);
        Assert.Equal(typeof(bool), dataTable.Columns["Active"]!.DataType);
        Assert.Equal(2, dataTable.Rows.Count);
        Assert.Equal("North", dataTable.Rows[0]["Name"]);
        Assert.Equal(12.50m, dataTable.Rows[0]["Amount"]);
        Assert.Equal(false, dataTable.Rows[1]["Active"]);
    }

    [Fact]
    public void ToDataSet_CreatesUniqueTablesForAllParsedHtmlTables() {
        const string html = """
<table id="dup"><tr><th>Name</th></tr><tr><td>One</td></tr></table>
<table id="dup"><tr><th>Name</th></tr><tr><td>Two</td></tr></table>
""";

        DataSet dataSet = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(html).ToDataSet();

        Assert.Equal("HtmlTables", dataSet.DataSetName);
        Assert.Equal(2, dataSet.Tables.Count);
        Assert.Equal("dup", dataSet.Tables[0]!.TableName);
        Assert.Equal("dup2", dataSet.Tables[1]!.TableName);
        Assert.Equal("Two", dataSet.Tables[1]!.Rows[0]["Name"]);
    }

    [Fact]
    public void ParseFileAndStreamTables_ReturnDetailedTableModels() {
        const string html = """
<table class="inventory">
  <tr><th>Name</th><th>Count</th></tr>
  <tr><td>Widget</td><td>4</td></tr>
</table>
""";

        string filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".html");
        try {
            File.WriteAllText(filePath, html, Encoding.UTF8);

            HtmlTableResult fileTable = HtmlParser.ParseFileTablesWithHtmlAgilityPackDetailed(filePath).Single();
            Assert.Equal("inventory", fileTable.Metadata.Classes);
            Assert.Equal("Widget", fileTable.Data[0]["Name"]);

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(html));
            HtmlTableResult streamTable = HtmlParser.ParseStreamTablesWithAngleSharpDetailed(stream).Single();
            Assert.Equal("inventory", streamTable.Metadata.Classes);
            Assert.Equal("4", streamTable.Data[0]["Count"]);
        } finally {
            if (File.Exists(filePath)) {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void ParseTablesDetailed_CanIncludeLinkUrlColumns() {
        const string html = """
<table id="links">
  <tr><th>Name</th><th>Owner</th></tr>
  <tr><td><a href="https://example.com/a">Alpha</a></td><td>Team A</td></tr>
  <tr><td><a href="/beta">Beta</a><a href="/beta/details">Details</a></td><td>Team B</td></tr>
</table>
""";

        HtmlTableResult table = HtmlParser.ParseTablesWithAngleSharpDetailed(html, includeLinkUrls: true).Single();
        DataTable dataTable = table.ToDataTable();

        Assert.Contains("NameUrl", table.Metadata.Headers);
        Assert.Equal("Alpha", dataTable.Rows[0]["Name"]);
        Assert.Equal("https://example.com/a", dataTable.Rows[0]["NameUrl"]);
        Assert.Equal("/beta; /beta/details", dataTable.Rows[1]["NameUrl"]);
    }

    [Fact]
    public void ParseTablesDetailed_NormalizesLinkUrlColumnsAcrossRows() {
        const string html = """
<table id="links">
  <tr><th>Name</th><th>Owner</th></tr>
  <tr><td>Alpha</td><td>Team A</td></tr>
  <tr><td><a href="https://example.com/b">Beta</a></td><td>Team B</td></tr>
</table>
""";

        HtmlTableResult table = HtmlParser.ParseTablesWithAngleSharpDetailed(html, includeLinkUrls: true).Single();
        DataTable dataTable = table.ToDataTable();

        Assert.Contains("NameUrl", table.Metadata.Headers);
        Assert.True(table.Data[0].ContainsKey("NameUrl"));
        Assert.Null(table.Data[0]["NameUrl"]);
        Assert.Equal(DBNull.Value, dataTable.Rows[0]["NameUrl"]);
        Assert.Equal("https://example.com/b", dataTable.Rows[1]["NameUrl"]);
    }

    [Fact]
    public void ParseTablesDetailed_UsesDistinctLinkUrlColumnsForEmptyHeaders() {
        const string html = """
<table>
  <tr><th></th><th></th></tr>
  <tr><td><a href="/one">One</a></td><td><a href="/two">Two</a></td></tr>
</table>
""";

        HtmlTableResult table = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(html, includeLinkUrls: true).Single();

        Assert.Contains("Column1Url", table.Metadata.Headers);
        Assert.Contains("Column2Url", table.Metadata.Headers);
        Assert.Equal("/one", table.Data[0]["Column1Url"]);
        Assert.Equal("/two", table.Data[0]["Column2Url"]);
    }

    [Fact]
    public void ParseTablesDetailed_UsesOnlyDirectCaptionForNestedTables() {
        const string html = """
<table id="outer">
  <tr><td>
    <table id="inner"><caption>Nested caption</caption><tr><td>Inside</td></tr></table>
  </td></tr>
</table>
""";

        HtmlTableResult outer = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(html).Single(table => table.Metadata.Id == "outer");

        Assert.Equal(string.Empty, outer.Metadata.Caption);
    }

    [Fact]
    public void SelectTables_FiltersByIndexIdClassCaptionAndHeader() {
        const string html = """
<table id="summary" class="report compact"><caption>Summary data</caption><tr><th>Name</th></tr><tr><td>One</td></tr></table>
<table id="details" class="report wide"><caption>Detailed data</caption><tr><th>Owner</th></tr><tr><td>Team</td></tr></table>
""";

        var tables = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(html);

        Assert.Single(tables.SelectTables(new HtmlTableSelectionOptions { TableIndexes = { 1 } }));
        Assert.Equal("summary", tables.SelectTables(new HtmlTableSelectionOptions { Id = "summary" }).Single().Metadata.Id);
        Assert.Equal("details", tables.SelectTables(new HtmlTableSelectionOptions { ClassName = "wide" }).Single().Metadata.Id);
        Assert.Equal("summary", tables.SelectTables(new HtmlTableSelectionOptions { CaptionContains = "summary" }).Single().Metadata.Id);
        Assert.Equal("details", tables.SelectTables(new HtmlTableSelectionOptions { Header = "Owner" }).Single().Metadata.Id);
    }

    [Fact]
    public void SelectTables_RespectsCaseSensitiveMatching() {
        const string html = """
<table id="Summary" class="Report"><caption>Summary Data</caption><tr><th>Owner</th></tr><tr><td>Team</td></tr></table>
""";

        var tables = HtmlParser.ParseTablesWithHtmlAgilityPackDetailed(html);

        Assert.Single(tables.SelectTables(new HtmlTableSelectionOptions { Id = "summary" }));
        Assert.Empty(tables.SelectTables(new HtmlTableSelectionOptions { Id = "summary", IgnoreCase = false }));
        Assert.Empty(tables.SelectTables(new HtmlTableSelectionOptions { ClassName = "report", IgnoreCase = false }));
        Assert.Empty(tables.SelectTables(new HtmlTableSelectionOptions { CaptionContains = "summary", IgnoreCase = false }));
        Assert.Empty(tables.SelectTables(new HtmlTableSelectionOptions { Header = "owner", IgnoreCase = false }));
        Assert.Single(tables.SelectTables(new HtmlTableSelectionOptions { Id = "Summary", ClassName = "Report", CaptionContains = "Summary", Header = "Owner", IgnoreCase = false }));
    }

    [Fact]
    public void SelectTables_EmptySelectionOptionsReturnAllTables() {
        const string html = """
<table id="one"><tr><th>Name</th></tr><tr><td>One</td></tr></table>
<table id="two"><tr><th>Name</th></tr><tr><td>Two</td></tr></table>
""";

        var tables = HtmlParser.ParseTablesWithAngleSharpDetailed(html);
        var selected = tables.SelectTables(new HtmlTableSelectionOptions {
            Id = " ",
            ClassName = "\t",
            CaptionContains = "\r\n",
            Header = string.Empty,
            TableIndexes = { -1 }
        });

        Assert.Equal(2, selected.Count);

        selected = tables.SelectTables(new HtmlTableSelectionOptions {
            TableIndexes = null!
        });

        Assert.Equal(2, selected.Count);
    }

    [Fact]
    public void ToDataTable_NullTableThrows() {
        Assert.Throws<ArgumentNullException>(() => HtmlTableDataExtensions.ToDataTable(null!));
    }
}
