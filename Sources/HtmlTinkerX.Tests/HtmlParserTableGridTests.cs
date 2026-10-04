using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlParserTableGridTests {
    public static IEnumerable<object[]> ParserModes() {
        foreach (bool agilityPack in new[] { false, true }) {
            foreach (bool detailed in new[] { false, true }) {
                yield return new object[] { agilityPack, detailed };
            }
        }
    }

    private static List<List<Dictionary<string, string?>>> Parse(string html, bool agilityPack, bool detailed, HtmlTableParseLimits? limits = null, bool links = false) {
        if (detailed) {
            var tables = agilityPack
                ? HtmlParserFromTable.ParseTablesWithHtmlAgilityPackDetailed(html, false, null, null, false, false, false, null, HtmlCellTextFormat.Compact, links, limits)
                : HtmlParserFromTable.ParseTablesWithAngleSharpDetailed(html, null, null, false, false, false, null, HtmlCellTextFormat.Compact, links, limits);
            return tables.Select(table => table.Data).ToList();
        }
        return agilityPack
            ? HtmlParserFromTable.ParseTablesWithHtmlAgilityPack(html, false, null, null, false, limits)
            : HtmlParserFromTable.ParseTablesWithAngleSharp(html, null, null, false, limits);
    }

    [Theory]
    [MemberData(nameof(ParserModes))]
    public void NestedTables_KeepTheirOwnRowsAndCells(bool agilityPack, bool detailed) {
        const string html = "<table><tr><td>Before<table><tr><th>Key</th><th>Value</th></tr><tr><td>X</td><td>1</td></tr></table>After</td><td>Outer</td></tr><tr><td>Last</td><td>2</td></tr></table>";
        var tables = Parse(html, agilityPack, detailed);
        Assert.Equal(2, tables.Count);
        Assert.Equal(2, tables[0].Count);
        Assert.Equal(new[] { "Column1", "Column2" }, tables[0][0].Keys);
        Assert.Equal("Outer", tables[0][0]["Column2"]);
        Assert.Equal("Last", tables[0][1]["Column1"]);
        Assert.Equal("2", tables[0][1]["Column2"]);
        Assert.Single(tables[1]);
        Assert.Equal("X", tables[1][0]["Key"]);
        Assert.Equal("1", tables[1][0]["Value"]);
    }

    [Theory]
    [MemberData(nameof(ParserModes))]
    public void RowSpans_EndAtTheirRowGroupIncludingZero(bool agilityPack, bool detailed) {
        foreach (string span in new[] { "0", "65534", "999999999999999999999" }) {
            string html = $"<table><thead><tr><th>A</th><th>B</th></tr></thead><tbody><tr><td rowspan='{span}'>Group</td><td>1</td></tr><tr><td>2</td></tr></tbody><tbody><tr><td>Next</td><td>3</td></tr></tbody></table>";
            var rows = Assert.Single(Parse(html, agilityPack, detailed));
            Assert.Equal(3, rows.Count);
            Assert.Equal("Group", rows[1]["A"]);
            Assert.Equal("2", rows[1]["B"]);
            Assert.Equal("Next", rows[2]["A"]);
            Assert.Equal("3", rows[2]["B"]);
        }
    }

    [Theory]
    [MemberData(nameof(ParserModes))]
    public void EmptyRows_ReceiveActiveRowSpan(bool agilityPack, bool detailed) {
        const string html = "<table><tr><th>A</th></tr><tbody><tr><td rowspan='0'>Group</td></tr><tr></tr></tbody></table>";
        var rows = Assert.Single(Parse(html, agilityPack, detailed));
        Assert.Equal(2, rows.Count);
        Assert.Equal("Group", rows[1]["A"]);
    }

    [Theory]
    [MemberData(nameof(ParserModes))]
    public void ColumnSpans_NormalizeInvalidValuesAndClampAtHtmlMaximum(bool agilityPack, bool detailed) {
        foreach (string span in new[] { "0", "-1", "invalid" }) {
            string html = $"<table><tr><td colspan='{span}'>First</td><td>Second</td></tr></table>";
            var row = Assert.Single(Assert.Single(Parse(html, agilityPack, detailed)));
            Assert.Equal(2, row.Count);
            Assert.Equal("First", row["Column1"]);
            Assert.Equal("Second", row["Column2"]);
        }
        foreach (string span in new[] { "5000", "2147483647", "999999999999999999999" }) {
            string html = $"<table><tr><td colspan='{span}'>Value</td></tr></table>";
            var row = Assert.Single(Assert.Single(Parse(html, agilityPack, detailed)));
            Assert.Equal(1000, row.Count);
            Assert.All(row.Values, value => Assert.Equal("Value", value));
        }
    }

    [Theory]
    [MemberData(nameof(ParserModes))]
    public void ExpandedHeaders_KeepEveryColumn(bool agilityPack, bool detailed) {
        const string html = "<table><tr><th colspan='2'>A</th><th>A1</th><th></th><th>3</th></tr><tr><td>1</td><td>2</td><td>3</td><td>4</td><td>5</td></tr></table>";
        var row = Assert.Single(Assert.Single(Parse(html, agilityPack, detailed)));
        Assert.Equal(5, row.Count);
        Assert.Equal(new[] { "1", "2", "3", "4", "5" }, row.Values);
        Assert.Equal("3", row["A1"]);
        Assert.Equal("4", row["3"]);
        Assert.Equal("5", row["31"]);
        var reordered = Assert.Single(Assert.Single(Parse("<table><tr><th>1</th><th></th></tr><tr><td>Named</td><td>Empty</td></tr></table>", agilityPack, detailed)));
        Assert.Equal(2, reordered.Count);
        Assert.Equal("Empty", reordered["1"]);
        Assert.Equal("Named", reordered["11"]);
    }

    [Theory]
    [MemberData(nameof(ParserModes))]
    public void ParseLimits_RejectWideAndAggregateExpandedResults(bool agilityPack, bool detailed) {
        const string table = "<table><tr><th>A</th><th>B</th></tr><tr><td>1</td><td>2</td></tr></table>";
        Assert.Throws<InvalidDataException>(() => Parse(table, agilityPack, detailed, new HtmlTableParseLimits { MaximumColumns = 1 }));
        Assert.Throws<InvalidDataException>(() => Parse(table + table, agilityPack, detailed, new HtmlTableParseLimits { MaximumRows = 3 }));
        Assert.Throws<InvalidDataException>(() => Parse(table + table, agilityPack, detailed, new HtmlTableParseLimits { MaximumExpandedCells = 3 }));
        Assert.Equal(2, Parse(table + table, agilityPack, detailed, new HtmlTableParseLimits { MaximumExpandedCells = 4 }).Count);
        Assert.Throws<InvalidDataException>(() => Parse("<table><tr><td colspan='1000'>Value</td></tr></table>", agilityPack, detailed, new HtmlTableParseLimits { MaximumExpandedCells = 999 }));
    }

    [Theory]
    [MemberData(nameof(ParserModes))]
    public void DataAttributeOverrides_UpdateTheBudgetedHeader(bool agilityPack, bool detailed) {
        foreach (string attribute in new[] { "category", "severity" }) {
            foreach (string header in new[] { attribute, attribute.ToUpperInvariant() }) {
                foreach (string display in new[] { "", "display" }) {
                    string html = $"<table><tr><th>{header}</th></tr><tr data-{attribute}='x'><td>{display}</td></tr></table>";
                    var limits = new HtmlTableParseLimits { MaximumColumns = 1, MaximumExpandedCells = 1 };
                    var row = Assert.Single(Assert.Single(Parse(html, agilityPack, detailed, limits)));
                    Assert.Equal(header, Assert.Single(row.Keys));
                    // The simple HAP parser fills empty cells; the other paths also override populated cells.
                    Assert.Equal(agilityPack && !detailed && display.Length > 0 ? display : "x", row[header]);
                    if (detailed) {
                        var table = Assert.Single(agilityPack
                            ? HtmlParserFromTable.ParseTablesWithHtmlAgilityPackDetailed(html, false, null, null, false, false, false, null, HtmlCellTextFormat.Compact, false, limits)
                            : HtmlParserFromTable.ParseTablesWithAngleSharpDetailed(html, null, null, false, false, false, null, HtmlCellTextFormat.Compact, false, limits));
                        Assert.Equal(new[] { header }, table.Metadata.Headers);
                        Assert.Equal(table.Metadata.Headers.Count, row.Count);
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LinkColumns_AreIncludedInColumnAndCellBudgets(bool agilityPack) {
        const string html = "<table><tr><th>A</th></tr><tr><td><a href='/one'>One</a></td></tr><tr><td>Two</td></tr></table>";
        Assert.Throws<InvalidDataException>(() => Parse(html, agilityPack, true, new HtmlTableParseLimits { MaximumColumns = 1 }, links: true));
        Assert.Throws<InvalidDataException>(() => Parse(html, agilityPack, true, new HtmlTableParseLimits { MaximumExpandedCells = 3 }, links: true));
        var rows = Assert.Single(Parse(html, agilityPack, true, new HtmlTableParseLimits { MaximumExpandedCells = 4 }, links: true));
        Assert.Equal("/one", rows[0]["AUrl"]);
        Assert.Null(rows[1]["AUrl"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedHeaderAndFooter_MetadataOnlyDescribesOwnedRows(bool agilityPack) {
        const string html = "<table id='outer'><tbody><tr><td><table><thead><tr><th>Inner</th></tr></thead><tbody><tr><td>Value</td></tr></tbody></table></td></tr></tbody><tfoot><tr><td>Footer</td></tr></tfoot></table>";
        var tables = agilityPack
            ? HtmlParserFromTable.ParseTablesWithHtmlAgilityPackDetailed(html, skipFooter: true)
            : HtmlParserFromTable.ParseTablesWithAngleSharpDetailed(html, skipFooter: true);
        Assert.Equal(2, tables.Count);
        Assert.Equal("outer", tables[0].Metadata.Id);
        Assert.Equal(1, tables[0].Metadata.RowCount);
        Assert.Equal(new[] { "Column1" }, tables[0].Metadata.Headers);
        Assert.Single(tables[0].Data);
    }

    [Fact]
    public void ReverseTable_UsesOwnedRowsAndLimitsOutputColumns() {
        const string html = "<table><tr><th>First</th><td>V<table><tr><th>Nested</th><td>N</td></tr></table></td></tr><tr><th>Second</th><td>W</td></tr></table>";
        var tables = HtmlParserFromTable.ParseTablesWithHtmlAgilityPack(html, true);
        Assert.Equal(2, tables.Count);
        Assert.Equal(new[] { "First", "Second" }, tables[0][0].Keys);
        Assert.Throws<InvalidDataException>(() => HtmlParserFromTable.ParseTablesWithHtmlAgilityPack(html, true, null, null, false, new HtmlTableParseLimits { MaximumColumns = 1 }));
        Assert.Throws<InvalidDataException>(() => HtmlParserFromTable.ParseTablesWithHtmlAgilityPackDetailed(html, true, null, null, false, false, false, null, HtmlCellTextFormat.Compact, false, new HtmlTableParseLimits { MaximumColumns = 1 }));
    }

    [Fact]
    public void ReverseTable_LinkColumnCannotOverwriteALaterNamedField() {
        const string html = "<table><tr><th>Site</th><td><a href='/actual'>Link</a></td></tr><tr><th>SiteUrl</th><td>Field</td></tr></table>";
        var table = Assert.Single(HtmlParserFromTable.ParseTablesWithHtmlAgilityPackDetailed(html, true, null, null, false, false, false, null, HtmlCellTextFormat.Compact, true));
        Assert.Equal("Link", table.Data[0]["Site"]);
        Assert.Equal("Field", table.Data[0]["SiteUrl"]);
        Assert.Equal("/actual", table.Data[0]["SiteUrl2"]);
    }
}
