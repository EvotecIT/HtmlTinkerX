using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace HtmlTinkerX.Tests;

public class HtmlBrowserlessDomRecipeTests {
    [Fact]
    public void SavedRecipe_PreservesTypesDefaultsCultureAndProvenance() {
        var date = new DateTimeOffset(2026, 10, 5, 12, 30, 0, TimeSpan.FromHours(2));
        var fields = new Dictionary<string, HtmlDomFieldDefinition> {
            ["Price"] = new() { Selector = "b", DataType = typeof(decimal?), Culture = "pl-PL", MaximumValueCount = 1 },
            ["Day"] = new() { Selector = ".day", DataType = typeof(DayOfWeek), DefaultValue = DayOfWeek.Monday },
            ["Date"] = new() { Selector = ".date", DataType = typeof(DateTimeOffset), DefaultValue = date },
            ["Count"] = new() { Selector = ".count", DefaultValue = 7 },
            ["Link"] = new() { Selector = "a", Attribute = "href", ResolveUrl = true, Required = true }
        };
        var recipe = HtmlBrowserlessExtraction.CreateDomRecipe("article", fields,
            new HtmlDomExtractionReportOptions { MinimumItemCount = 1, MaximumItemCount = 2 },
            new Uri("https://example.org/catalog/"));
        fields["Price"].Selector = ".changed-by-caller";
        string json = HtmlBrowserlessExtraction.SerializeRecipe(recipe);
        var loaded = HtmlBrowserlessExtraction.DeserializeRecipe(json);
        var result = HtmlBrowserlessExtraction.ExtractDomRecipe(loaded, "<article><b>1234,50</b><a href='item'>Link</a></article>");

        Assert.Contains("enum:System.DayOfWeek", json);
        Assert.DoesNotContain("System.Private.CoreLib", json);
        Assert.DoesNotContain("mscorlib", json);
        Assert.True(result.Success);
        Assert.Empty(result.RawContent);
        Assert.Empty(result.Requests);
        Assert.Empty(result.Source!.Warnings);
        Assert.True(result.Source.CanExtractDirectly);
        var report = Assert.IsType<HtmlDomExtractionReport>(result.DomReport);
        Assert.Equal("https://example.org/catalog/", report.BaseUri!.AbsoluteUri);
        Assert.Equal(1, report.MinimumItemCount);
        Assert.Equal(2, report.MaximumItemCount);
        var record = Assert.Single(report.Records);
        Assert.Equal(1234.50m, Assert.IsType<decimal>(record.Values["Price"]));
        Assert.Equal(DayOfWeek.Monday, Assert.IsType<DayOfWeek>(record.Values["Day"]));
        Assert.Equal(date, Assert.IsType<DateTimeOffset>(record.Values["Date"]));
        Assert.Equal(7, Assert.IsType<int>(record.Values["Count"]));
        Assert.Equal("https://example.org/catalog/item", record.Values["Link"]);
        Assert.Equal("b", Assert.Single(report.Fields, field => field.PropertyName == "Price").Selector);
        Assert.Equal("pl-PL", Assert.Single(report.Fields, field => field.PropertyName == "Price").Culture);
        Assert.Same(record.Values, Assert.Single(result.Items).Value);
    }

    [Theory]
    [InlineData("<article><span>Name</span></article>", HtmlDomFieldStatus.RequiredMissing)]
    [InlineData("<article><span>Name</span><b>private-invalid-price</b></article>", HtmlDomFieldStatus.InvalidValue)]
    [InlineData("<article><span>Name</span><b>12</b><b>24</b></article>", HtmlDomFieldStatus.TooManyValues)]
    public void ReusedRecipe_DetectsChangedFieldsWithoutLosingValidNeighbors(string html, HtmlDomFieldStatus status) {
        var recipe = CreatePriceRecipe();
        var initial = HtmlBrowserlessExtraction.ExtractDomRecipe(recipe, "<article><span>Name</span><b>12</b></article>");
        var changed = HtmlBrowserlessExtraction.ExtractDomRecipe(recipe, html);

        Assert.True(initial.Success);
        Assert.False(changed.Success);
        Assert.False(changed.DomReport!.IsValid);
        Assert.Equal("Name", Assert.Single(changed.DomReport.Records).Values["Name"]);
        Assert.Null(changed.DomReport.Records[0].Values["Price"]);
        Assert.Equal(status, Assert.Single(changed.DomReport.Fields, field => field.PropertyName == "Price").Status);
        Assert.DoesNotContain("private-invalid-price", changed.DomReport.Fields.Single(field => field.PropertyName == "Price").Error!);
        Assert.NotEmpty(changed.Warnings);
    }

    [Fact]
    public void ReusedRecipe_DetectsEmptyAndExcessiveDatasets() {
        var recipe = CreatePriceRecipe();
        var empty = HtmlBrowserlessExtraction.ExtractDomRecipe(recipe, "<main></main>");
        var excessive = HtmlBrowserlessExtraction.ExtractDomRecipe(recipe,
            "<article><span>A</span><b>12</b></article><article><span>B</span><b>24</b></article>", true);

        Assert.False(empty.Success);
        Assert.False(empty.DomReport!.ItemCountIsValid);
        Assert.Empty(empty.Items);
        Assert.False(excessive.Success);
        Assert.False(excessive.DomReport!.ItemCountIsValid);
        Assert.Equal(2, excessive.Items.Count);
        Assert.Contains("<article>", excessive.RawContent);
    }

    [Fact]
    public async Task DomRecipe_RequiresExplicitCurrentHtmlAndDoesNotBecomeAnEndpoint() {
        var recipe = CreatePriceRecipe();
        recipe.ResolvedUrl = "https://example.org/should-not-fetch";
        await Assert.ThrowsAsync<ArgumentException>(() => HtmlBrowserlessExtraction.ExtractRecipeAsync(recipe,
            new HtmlBrowserlessExtractionOptions { AllowHttpFetch = true }));
        var staticRecipe = new HtmlBrowserlessExtractionRecipe { SourceKind = "AppState", RawContent = "{\"name\":\"Alpha\"}" };
        Assert.Throws<ArgumentException>(() => HtmlBrowserlessExtraction.ExtractDomRecipe(staticRecipe, "<main></main>"));
        string json = HtmlBrowserlessExtraction.SerializeRecipe(staticRecipe);
        Assert.DoesNotContain("domProperties", json);
        Assert.DoesNotContain("minimumItemCount", json);
        var loaded = HtmlBrowserlessExtraction.DeserializeRecipe(json);
        Assert.True((await HtmlBrowserlessExtraction.ExtractRecipeAsync(loaded)).Success);
    }

    [Theory]
    [InlineData("\"version\": 2", typeof(ArgumentException))]
    [InlineData("\"selector\": \"[\"", typeof(AngleSharp.Dom.DomException))]
    [InlineData("\"pageUrl\": \"relative\"", typeof(ArgumentException))]
    public void Import_RejectsInvalidDomRecipeConfiguration(string extra, Type exceptionType) {
        string json = "{\"sourceKind\":\"Dom\",\"selector\":\"article\",\"domProperties\":{\"Price\":{\"selector\":\"b\",\"dataType\":\"Decimal\"}}," + extra + "}";
        Assert.Throws(exceptionType, () => HtmlBrowserlessExtraction.DeserializeRecipe(json));
    }

    [Theory]
    [InlineData("enum:")]
    [InlineData("enum:System.DayOfWeek, System.Private.CoreLib")]
    [InlineData("System.IO.FileInfo")]
    [InlineData("enum:System.String")]
    [InlineData("enum:Missing.Application.Enum")]
    public void Import_RejectsUnknownOrNonEnumTypeNames(string typeName) {
        string json = "{\"sourceKind\":\"Dom\",\"selector\":\"article\",\"domProperties\":{\"Value\":{\"dataType\":\"" + typeName + "\"}}}";
        Assert.Throws<JsonException>(() => HtmlBrowserlessExtraction.DeserializeRecipe(json));
    }

    [Fact]
    public void Import_RejectsCaseCollisionsRatherThanOverwritingARecordField() {
        const string json = "{\"sourceKind\":\"Dom\",\"selector\":\"article\",\"domProperties\":{\"Name\":{\"selector\":\"b\"},\"name\":{\"selector\":\"span\"}}}";
        var error = Assert.Throws<ArgumentException>(() => HtmlBrowserlessExtraction.DeserializeRecipe(json));
        Assert.Contains("unique", error.Message);
    }

    [Fact]
    public void Saving_RejectsArbitraryDefaultObjectsAndUndefinedEnumDefaults() {
        var fields = new Dictionary<string, HtmlDomFieldDefinition> { ["Value"] = new() { DefaultValue = new object() } };
        Assert.Throws<JsonException>(() => HtmlBrowserlessExtraction.CreateDomRecipe("article", fields));
        fields["Value"].DefaultValue = (DayOfWeek)999;
        Assert.Throws<JsonException>(() => HtmlBrowserlessExtraction.CreateDomRecipe("article", fields));
        fields["Value"].DefaultValue = "private-malformed-default";
        string json = HtmlBrowserlessExtraction.SerializeRecipe(HtmlBrowserlessExtraction.CreateDomRecipe("article", fields));
        json = json.Replace("\"defaultValueType\": \"String\"", "\"defaultValueType\": \"Int32\"");
        var error = Assert.Throws<JsonException>(() => HtmlBrowserlessExtraction.DeserializeRecipe(json));
        Assert.DoesNotContain("private-malformed-default", error.Message);
    }

    [Fact]
    public void SavedScalarDefaults_PreserveRuntimeTypesAndPrecision() {
        var defaults = new Dictionary<string, object> {
            ["Float"] = 1.2345678f, ["Double"] = Math.PI, ["Decimal"] = 1234567890.123456789m,
            ["UtcDate"] = new DateTime(2026, 10, 5, 12, 30, 0, DateTimeKind.Utc),
            ["Boolean"] = true, ["Text"] = string.Empty
        };
        var fields = defaults.ToDictionary(pair => pair.Key,
            pair => new HtmlDomFieldDefinition { Selector = ".absent", DefaultValue = pair.Value });
        var recipe = HtmlBrowserlessExtraction.CreateDomRecipe("article", fields);
        var report = HtmlBrowserlessExtraction.ExtractDomRecipe(recipe, "<article></article>").DomReport!;
        foreach (var pair in defaults) {
            object? observed = report.Records[0].Values[pair.Key];
            Assert.IsType(pair.Value.GetType(), observed);
            Assert.Equal(pair.Value, observed);
        }
        Assert.Equal(DateTimeKind.Utc, ((DateTime)report.Records[0].Values["UtcDate"]!).Kind);
    }

    private static HtmlBrowserlessExtractionRecipe CreatePriceRecipe() => HtmlBrowserlessExtraction.CreateDomRecipe(
        "article", new Dictionary<string, HtmlDomFieldDefinition> {
            ["Name"] = new() { Selector = "span", Required = true },
            ["Price"] = new() { Selector = "b", DataType = typeof(decimal), Required = true, MaximumValueCount = 1 }
        }, new HtmlDomExtractionReportOptions { MinimumItemCount = 1, MaximumItemCount = 1 });
}