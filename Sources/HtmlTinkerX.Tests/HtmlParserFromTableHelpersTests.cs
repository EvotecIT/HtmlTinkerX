using HtmlTinkerX;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlParserFromTableHelpersTests {
    [Fact]
    public void ReadTableMetadata_ReturnsExpectedInfo() {
        const string html = "<table id='t1'><tr><th>A</th><th>B</th></tr><tr><td>1</td><td>2</td></tr></table>";
        var doc = HtmlParser.ParseWithAngleSharp(html);
        var table = doc.QuerySelector("table")!;
        var (metadata, rows, start) = HtmlParserFromTable.ReadTableMetadata(table, 0, null, false, false);
        Assert.Equal(2, metadata.ColumnCount);
        Assert.Equal(new[] { "A", "B" }, metadata.Headers);
        Assert.Equal(2, metadata.RowCount);
        Assert.Equal(1, start);
        Assert.Equal("t1", metadata.Id);
    }

    [Fact]
    public void ParseTableRows_ReturnsRows() {
        const string html = "<table><tr><th>A</th><th>B</th></tr><tr><td>1</td><td>2</td></tr></table>";
        var doc = HtmlParser.ParseWithAngleSharp(html);
        var table = doc.QuerySelector("table")!;
        var (meta, rows, start) = HtmlParserFromTable.ReadTableMetadata(table, 0, null, false, false);
        var data = HtmlParserFromTable.ParseTableRows(rows, start, meta.Headers, null, false, null, HtmlCellTextFormat.Compact);
        Assert.Single(data);
        var row = data[0];
        Assert.Equal("1", row["A"]);
        Assert.Equal("2", row["B"]);
    }

    [Fact]
    public void ReadTableMetadata_NoThead_DoesNotThrow() {
        const string html = "<table><tbody><tr><td>1</td><td>2</td></tr></tbody></table>";
        var doc = HtmlParser.ParseWithAngleSharp(html);
        var table = doc.QuerySelector("table")!;
        var (meta, rows, _) = HtmlParserFromTable.ReadTableMetadata(table, 0, null, false, false);
        Assert.Equal(2, meta.ColumnCount);
        Assert.Equal(new[] { "Column1", "Column2" }, meta.Headers);
        Assert.Equal(1, meta.RowCount);
    }
}
