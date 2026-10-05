using System;
using System.Collections.Generic;
using System.Text.Json;

namespace HtmlTinkerX.Tests;

public class HtmlDomExtractionReportTests {
    [Fact]
    public void Report_ContinuesAfterDataErrorsAndKeepsValidValuesAndFieldProvenance() {
        const string html = "<article><b>bad-private-price</b><span>First</span></article><article><b>12,5</b><span>Second</span></article>";
        Dictionary<string, HtmlDomFieldDefinition> fields = new() {
            ["Price"] = new() { Selector = "b", DataType = typeof(decimal), Culture = "pl-PL" },
            ["Name"] = new() { Selector = "span", Required = true }
        };

        HtmlDomExtractionReport report = HtmlDomExtraction.ExtractReport(html, "article", fields);

        Assert.False(report.IsValid);
        Assert.Equal(2, report.ItemCount);
        Assert.Equal(1, report.InvalidFieldCount);
        Assert.Null(report.Records[0].Values["Price"]);
        Assert.Equal("First", report.Records[0].Values["Name"]);
        Assert.Equal(12.5m, report.Records[1].Values["Price"]);
        Assert.Equal("Second", report.Records[1].Values["Name"]);
        HtmlDomFieldDiagnostic error = report.Fields[0];
        Assert.Equal(HtmlDomFieldStatus.InvalidValue, error.Status);
        Assert.Equal(0, error.ItemIndex);
        Assert.Equal("Price", error.PropertyName);
        Assert.Equal("b", error.Selector);
        Assert.Equal(typeof(decimal).FullName, error.DataType);
        Assert.Equal("pl-PL", error.Culture);
        Assert.Equal(1, error.MatchCount);
        Assert.Equal(1, error.ValueCount);
        Assert.Contains("item 0", error.Error!);
        Assert.DoesNotContain("bad-private-price", JsonSerializer.Serialize(report), StringComparison.Ordinal);
        Assert.Throws<FormatException>(() => HtmlDomExtraction.Extract(html, "article", fields));
    }

    [Fact]
    public void Report_DistinguishesRequiredMissingOptionalMissingAndConvertedDefaults() {
        Dictionary<string, HtmlDomFieldDefinition> fields = new() {
            ["Required"] = new() { Selector = "b", Required = true, TreatEmptyAsMissing = true, DefaultValue = 7 },
            ["Default"] = new() { Selector = "b", TreatEmptyAsMissing = true, DataType = typeof(int), DefaultValue = "7" },
            ["Missing"] = new() { Selector = ".absent" },
            ["All"] = new() { Selector = ".absent", All = true, DefaultValue = "ignored" }
        };

        HtmlDomExtractionReport report = HtmlDomExtraction.ExtractReport("<article><b> </b></article>", "article", fields);

        Assert.Equal(1, report.InvalidFieldCount);
        Assert.Equal(1, report.DefaultedFieldCount);
        Assert.Equal(4, report.MissingFieldCount);
        Assert.Equal(HtmlDomFieldStatus.RequiredMissing, report.Fields[0].Status);
        Assert.Equal(1, report.Fields[0].MatchCount);
        Assert.Equal(0, report.Fields[0].ValueCount);
        Assert.Equal(HtmlDomFieldStatus.Defaulted, report.Fields[1].Status);
        Assert.Equal(7, report.Records[0].Values["Default"]);
        Assert.Equal(HtmlDomFieldStatus.Missing, report.Fields[2].Status);
        Assert.Null(report.Records[0].Values["Missing"]);
        Assert.Empty(Assert.IsType<object?[]>(report.Records[0].Values["All"]));
    }

    [Theory]
    [InlineData(2, null, HtmlDomFieldStatus.TooFewValues)]
    [InlineData(null, 0, HtmlDomFieldStatus.TooManyValues)]
    public void ValueCountBoundsApplyToReportsAndStrictExtraction(int? minimum, int? maximum, HtmlDomFieldStatus expected) {
        Dictionary<string, HtmlDomFieldDefinition> fields = new() {
            ["Count"] = new() { Selector = "span", DataType = typeof(int), MinimumValueCount = minimum, MaximumValueCount = maximum }
        };
        const string html = "<article><span>12</span><span> </span></article>";
        fields["Count"].TreatEmptyAsMissing = true;

        HtmlDomExtractionReport report = HtmlDomExtraction.ExtractReport(html, "article", fields);

        Assert.False(report.IsValid);
        HtmlDomFieldDiagnostic field = Assert.Single(report.Fields);
        Assert.Equal(expected, field.Status);
        Assert.Equal(2, field.MatchCount);
        Assert.Equal(1, field.ValueCount);
        Assert.Equal(minimum, field.MinimumValueCount);
        Assert.Equal(maximum, field.MaximumValueCount);
        Assert.Null(report.Records[0].Values["Count"]);
        Assert.Throws<InvalidOperationException>(() => HtmlDomExtraction.Extract(html, "article", fields));
    }

    [Fact]
    public void Report_ItemCountBoundsDetectAnEmptyOrExpandedDataset() {
        Dictionary<string, HtmlDomFieldDefinition> fields = new() { ["Name"] = new() };
        HtmlDomExtractionReportOptions options = new() { MinimumItemCount = 1, MaximumItemCount = 1 };
        HtmlDomExtractionReport empty = HtmlDomExtraction.ExtractReport("", "article", fields, options);
        Assert.False(empty.IsValid);
        Assert.False(empty.ItemCountIsValid);
        Assert.Equal(0, empty.ItemCount);
        Assert.Empty(empty.Fields);
        Assert.False(HtmlDomExtraction.ExtractReport("<article></article><article></article>", "article", fields, options).IsValid);
        Assert.True(HtmlDomExtraction.ExtractReport("<article>One</article>", "article", fields, options).IsValid);
        Assert.True(HtmlDomExtraction.ExtractReport("", "article", fields).IsValid);
    }

    [Fact]
    public void Report_RecordsTheEffectiveUrlAndAttributeSource() {
        Uri page = new("https://example.org/products/index.html");
        Dictionary<string, HtmlDomFieldDefinition> fields = new() {
            ["Link"] = new() { Selector = "a", Attribute = "href" }
        };
        HtmlDomExtractionReport report = HtmlDomExtraction.ExtractReport(
            "<base href='/catalog/'><article><a href='item'>Item</a></article>", "article", fields, baseUri: page);

        Assert.True(report.IsValid);
        Assert.Equal(new Uri("https://example.org/catalog/"), report.BaseUri);
        Assert.Equal("https://example.org/catalog/item", report.Records[0].Values["Link"]);
        Assert.Equal("a", report.Fields[0].Selector);
        Assert.Equal("href", report.Fields[0].Attribute);
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(null, -1)]
    [InlineData(2, 1)]
    public void InvalidCountConfigurationThrowsEvenForAnEmptyDataset(int? minimum, int? maximum) {
        Dictionary<string, HtmlDomFieldDefinition> fields = new() { ["Count"] = new() };
        Assert.Throws<ArgumentException>(() => HtmlDomExtraction.ExtractReport("", "article", fields,
            new() { MinimumItemCount = minimum, MaximumItemCount = maximum }));
        fields["Count"].MinimumValueCount = minimum;
        fields["Count"].MaximumValueCount = maximum;
        Assert.Throws<ArgumentException>(() => HtmlDomExtraction.ExtractReport("", "article", fields));
        Assert.Throws<ArgumentException>(() => HtmlDomExtraction.Extract("", "article", fields));
    }

    [Fact]
    public void Report_RejectsInvalidConversionConfigurationAndFieldSelectors() {
        Dictionary<string, HtmlDomFieldDefinition> fields = new() { ["Count"] = new() { DataType = typeof(Uri) } };
        Assert.Throws<ArgumentException>(() => HtmlDomExtraction.ExtractReport("", "article", fields));
        fields["Count"] = new() { Selector = "[" };
        Assert.Throws<ArgumentException>(() => HtmlDomExtraction.ExtractReport("<article></article>", "article", fields));
        Assert.Throws<ArgumentException>(() => HtmlDomExtraction.ExtractReport("", "article", fields));
        fields["Count"] = new() { ValueKind = "Unsupported" };
        Assert.Throws<ArgumentException>(() => HtmlDomExtraction.ExtractReport("", "article", fields));
    }
}