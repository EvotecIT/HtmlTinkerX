using System;
using System.Collections.Generic;
using System.Globalization;

namespace HtmlTinkerX.Tests;

public class HtmlDomTypedExtractionTests {
    [Fact]
    public void Extract_ConvertsFieldsUsingTheirDeclaredTypesAndCulture() {
        const string html = """
            <article data-count='42' data-total='9223372036854775807' data-price='1234,50'
                     data-active='TRUE' data-day='monday' data-date='2026-10-05T12:30:00+02:00'></article>
            """;
        Dictionary<string, HtmlDomFieldDefinition> fields = new() {
            ["Count"] = new() { Attribute = "data-count", DataType = typeof(int) },
            ["Total"] = new() { Attribute = "data-total", DataType = typeof(long) },
            ["Price"] = new() { Attribute = "data-price", DataType = typeof(decimal), Culture = "pl-PL" },
            ["Active"] = new() { Attribute = "data-active", DataType = typeof(bool) },
            ["Day"] = new() { Attribute = "data-day", DataType = typeof(DayOfWeek) },
            ["Date"] = new() { Attribute = "data-date", DataType = typeof(DateTimeOffset) }
        };

        HtmlDomExtractionRecord record = Assert.Single(HtmlDomExtraction.Extract(html, "article", fields));
        Assert.Equal(42, Assert.IsType<int>(record.Values["Count"]));
        Assert.Equal(long.MaxValue, Assert.IsType<long>(record.Values["Total"]));
        Assert.Equal(1234.50m, Assert.IsType<decimal>(record.Values["Price"]));
        Assert.True(Assert.IsType<bool>(record.Values["Active"]));
        Assert.Equal(DayOfWeek.Monday, Assert.IsType<DayOfWeek>(record.Values["Day"]));
        Assert.Equal(new DateTimeOffset(2026, 10, 5, 12, 30, 0, TimeSpan.FromHours(2)),
            Assert.IsType<DateTimeOffset>(record.Values["Date"]));
    }

    [Fact]
    public void Extract_DefaultConversionIgnoresTheHostCultureAndTimeZone() {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(1234.5m, Read("1,234.5", new() { DataType = typeof(decimal) }));
            Assert.Equal(new DateTimeOffset(2026, 10, 5, 12, 30, 0, TimeSpan.Zero),
                Read("2026-10-05T12:30:00", new() { DataType = typeof(DateTimeOffset) }));
            Assert.Equal(new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero),
                Read("31.12.2026", new() { DataType = typeof(DateTimeOffset), Culture = "pl-PL" }));
        } finally {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(typeof(int), "2147483648")]
    [InlineData(typeof(long), "9223372036854775808")]
    [InlineData(typeof(decimal), "79228162514264337593543950336")]
    [InlineData(typeof(bool), "yes")]
    [InlineData(typeof(DateTimeOffset), "private-invalid-date")]
    [InlineData(typeof(DayOfWeek), "Unknown")]
    [InlineData(typeof(DayOfWeek), "1")]
    public void Extract_InvalidDataReportsFieldAndItemWithoutEchoingTheValue(Type type, string value) {
        FormatException error = Assert.Throws<FormatException>(() => Read(value, new() { DataType = type }));
        Assert.Contains("Field", error.Message, StringComparison.Ordinal);
        Assert.Contains("item 0", error.Message, StringComparison.Ordinal);
        Assert.Contains(type.Name, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(value, error.Message, StringComparison.Ordinal);
        Assert.Null(error.InnerException);
    }

    [Fact]
    public void Extract_OnlyConvertsTheSelectedValueUnlessAllIsRequested() {
        const string html = "<article><span>12</span><span>invalid</span></article>";
        HtmlDomFieldDefinition field = new() { Selector = "span", DataType = typeof(int) };
        Dictionary<string, HtmlDomFieldDefinition> fields = new() { ["Count"] = field };
        Assert.Equal(12, Assert.Single(HtmlDomExtraction.Extract(html, "article", fields)).Values["Count"]);

        field.All = true;
        Assert.Throws<FormatException>(() => HtmlDomExtraction.Extract(html, "article", fields));
        object? result = Read("<span>12</span><span>24</span>", field);
        Assert.Equal(new object?[] { 12, 24 }, Assert.IsType<object?[]>(result));
    }

    [Fact]
    public void Extract_PreservesLegacyDefaultsAndConvertsTypedDefaults() {
        object sentinel = new();
        Assert.Same(sentinel, Read("", new() { Selector = ".missing", DefaultValue = sentinel }));
        Assert.Equal(12.5m, Read("", new() {
            Selector = ".missing", DataType = typeof(decimal), DefaultValue = "12,5", Culture = "pl-PL"
        }));
        Assert.Equal(12m, Read("", new() { Selector = ".missing", DataType = typeof(decimal), DefaultValue = 12 }));
        Assert.Null(Read("", new() { Selector = ".missing", DataType = typeof(int?) }));
        Assert.Equal(42, Read("42", new() { DataType = typeof(int?) }));
        Assert.Empty(Assert.IsType<object?[]>(Read("", new() {
            Selector = ".missing", DataType = typeof(int), DefaultValue = "invalid", All = true
        })));
        Assert.Throws<FormatException>(() => Read("", new() {
            Selector = ".missing", DataType = typeof(decimal), DefaultValue = "invalid"
        }));
    }

    [Fact]
    public void Extract_EmptyValuesCanUseDefaultsOrFailRequiredValidation() {
        Assert.Equal("", Read("   ", new()));
        HtmlDomFieldDefinition field = new() {
            DataType = typeof(int), TreatEmptyAsMissing = true, DefaultValue = 7
        };
        Assert.Equal(7, Read("   ", field));
        field.Required = true;
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Read("   ", field));
        Assert.Contains("Field", error.Message, StringComparison.Ordinal);
        Assert.Contains("item 0", error.Message, StringComparison.Ordinal);

        field.Required = false;
        field.Selector = "span";
        Assert.Equal(42, Read("<span></span><span>42</span>", field));
        field.Attribute = "data-value";
        Assert.Equal(42, Read("<span data-value='   '></span><span data-value='42'></span>", field));
    }

    [Fact]
    public void Extract_ValidatesConversionConfigurationEvenWhenNoItemsMatch() {
        Dictionary<string, HtmlDomFieldDefinition> fields = new() {
            ["Value"] = new() { DataType = typeof(Uri) }
        };
        ArgumentException typeError = Assert.Throws<ArgumentException>(() =>
            HtmlDomExtraction.Extract("", "article", fields));
        Assert.Contains("Value", typeError.Message, StringComparison.Ordinal);
        fields["Value"] = new() { DataType = typeof(decimal), Culture = "!" };
        ArgumentException cultureError = Assert.Throws<ArgumentException>(() =>
            HtmlDomExtraction.Extract("", "article", fields));
        Assert.Contains("Value", cultureError.Message, StringComparison.Ordinal);
    }

    private static object? Read(string value, HtmlDomFieldDefinition definition) =>
        Assert.Single(HtmlDomExtraction.Extract("<article>" + value + "</article>", "article",
            new Dictionary<string, HtmlDomFieldDefinition> { ["Field"] = definition })).Values["Field"];
}