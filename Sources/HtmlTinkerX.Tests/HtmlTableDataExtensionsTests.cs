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
    public void ToDataTable_NullTableThrows() {
        Assert.Throws<ArgumentNullException>(() => HtmlTableDataExtensions.ToDataTable(null!));
    }
}
