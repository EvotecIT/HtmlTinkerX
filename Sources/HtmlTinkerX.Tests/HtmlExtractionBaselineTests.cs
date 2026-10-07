using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX.Tests;

public class HtmlExtractionBaselineTests {
    private static HtmlBrowserlessExtractionRecipe DomRecipe() => HtmlBrowserlessExtraction.CreateDomRecipe(
        "article.product", new Dictionary<string, HtmlDomFieldDefinition> {
            ["Name"] = new() { Selector = "h2", Required = true },
            ["Price"] = new() { Selector = ".price", DataType = typeof(decimal), Required = true },
            ["Note"] = new() { Selector = ".note" }
        }, new HtmlDomExtractionReportOptions { MinimumItemCount = 1, MaximumItemCount = 20 });

    private static string Products(int count = 8, bool note = true, string price = "12.50") => string.Concat(
        Enumerable.Range(0, count).Select(i => $"<article class='product'><h2 class='name'>Product {i}</h2>"
            + $"<a href='/item/{i}'>View</a><img src='/image/{i}.png'><span class='price'>{price}</span>"
            + "<span class='rating'>Five stars</span><time>Today</time>"
            + (note ? "<p class='note'>available-secret-value</p>" : string.Empty) + "</article>"));

    [Fact]
    public void AcceptedDomBaseline_PreservesValuesPrivacyAndSurvivesJson() {
        HtmlBrowserlessExtractionRecipe original = DomRecipe();
        HtmlBrowserlessExtractionRecipe accepted = HtmlBrowserlessExtraction.CaptureDomRecipeBaseline(original, Products());
        string json = HtmlBrowserlessExtraction.SerializeRecipe(accepted);
        var loaded = HtmlBrowserlessExtraction.DeserializeRecipe(json);
        var result = HtmlBrowserlessExtraction.ExtractDomRecipe(loaded, Products(price: "987.65"));

        Assert.Null(original.Baseline);
        Assert.DoesNotContain("available-secret-value", json);
        Assert.DoesNotContain("Product 0", json);
        Assert.DoesNotContain("12.50", json);
        Assert.True(result.Success);
        Assert.False(result.DriftReport!.HasChanges);
        Assert.Equal(987.65m, result.DomReport!.Records[0].Values["Price"]);
    }

    [Fact]
    public void AcceptedDomBaseline_DetectsOptionalFieldLossEvenWhenQualityRulesPass() {
        HtmlBrowserlessExtractionRecipe recipe = DomRecipe();
        Assert.True(HtmlBrowserlessExtraction.ExtractDomRecipe(recipe, Products(note: false)).Success);
        var accepted = HtmlBrowserlessExtraction.CaptureDomRecipeBaseline(recipe, Products());
        var result = HtmlBrowserlessExtraction.ExtractDomRecipe(accepted, Products(note: false));

        Assert.True(result.DomReport!.IsValid);
        Assert.False(result.Success);
        Assert.True(result.DriftReport!.ShapeChanged);
        Assert.Contains(result.Warnings, warning => warning.Contains("baseline"));
    }

    [Fact]
    public void AcceptedDomBaseline_ReportsCompatibleCountGrowthWithoutLearningIt() {
        var accepted = HtmlBrowserlessExtraction.CaptureDomRecipeBaseline(DomRecipe(), Products(7));
        var result = HtmlBrowserlessExtraction.ExtractDomRecipe(accepted, Products(8));

        Assert.True(result.Success);
        Assert.True(result.DriftReport!.ItemCountChanged);
        Assert.True(result.DriftReport.IsCompatible);
        Assert.Equal(7, accepted.Baseline!.ItemCount);
        Assert.Equal(8, result.DriftReport.CurrentItemCount);
    }

    [Fact]
    public void AcceptedDomBaseline_DetectsMissingSourceHiddenByAnOptionalDefault() {
        var recipe = DomRecipe();
        recipe.DomProperties!["Note"].DefaultValue = "fallback";
        var accepted = HtmlBrowserlessExtraction.CaptureDomRecipeBaseline(recipe, Products());
        var current = HtmlBrowserlessExtraction.ExtractDomRecipe(accepted, Products(note: false));

        Assert.Equal("fallback", current.DomReport!.Records[0].Values["Note"]);
        Assert.True(current.DomReport.IsValid);
        Assert.True(current.DomReport.DefaultedFieldCount > 0);
        Assert.False(current.Success);
        Assert.True(current.DriftReport!.ShapeChanged);
    }

    [Fact]
    public void AcceptedDomBaseline_DetectsExtraMatchesEvenWhenTheSelectedValueIsUnchanged() {
        var accepted = HtmlBrowserlessExtraction.CaptureDomRecipeBaseline(DomRecipe(), Products());
        var current = HtmlBrowserlessExtraction.ExtractDomRecipe(accepted,
            Products().Replace("</article>", "<span class='price'>99.00</span></article>"));

        Assert.Equal(12.50m, current.DomReport!.Records[0].Values["Price"]);
        Assert.True(current.DomReport.IsValid);
        Assert.False(current.Success);
        Assert.True(current.DriftReport!.ShapeChanged);
    }

    [Fact]
    public void AcceptedDomBaseline_DetectsExistingDiscoveryConfidenceReduction() {
        string html = Products();
        var accepted = HtmlBrowserlessExtraction.CaptureDomRecipeBaseline(DomRecipe(), html);
        var result = HtmlBrowserlessExtraction.ExtractDomRecipe(accepted, "<nav>" + html + "</nav>");

        Assert.Equal("High", accepted.Baseline!.CollectionConfidence);
        Assert.False(result.DriftReport!.ShapeChanged);
        Assert.True(result.DomReport!.IsValid);
        Assert.True(result.DriftReport.ConfidenceReduced);
        Assert.Equal("Medium", result.DriftReport.CurrentConfidence);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task AcceptedJsonBaseline_DetectsChangedTypesAndSourcePaths() {
        var source = new HtmlBrowserlessDataSource {
            Kind = "AppState", RawContent = "{\"items\":[{\"name\":\"sensitive-product\",\"price\":12.5}]}"
        };
        var recipe = HtmlBrowserlessExtraction.CreateRecipe(source, includeRawContent: true);
        var accepted = HtmlBrowserlessExtraction.CaptureRecipeBaseline(recipe, await HtmlBrowserlessExtraction.ExtractRecipeAsync(recipe));
        accepted.RawContent = "{\"items\":[{\"price\":24,\"name\":\"changed-value\"},{\"name\":\"another\",\"price\":15}]}";
        var changedValues = await HtmlBrowserlessExtraction.ExtractRecipeAsync(accepted);
        Assert.True(changedValues.Success);
        Assert.False(changedValues.DriftReport!.ShapeChanged);
        Assert.True(changedValues.DriftReport.ItemCountChanged);
        Assert.DoesNotContain("sensitive-product", string.Join("", accepted.Baseline!.ItemShapes));

        accepted.RawContent = "{\"records\":[{\"name\":\"changed-value\",\"price\":\"24\"}]}";
        var changedShape = await HtmlBrowserlessExtraction.ExtractRecipeAsync(accepted);
        Assert.False(changedShape.Success);
        Assert.True(changedShape.DriftReport!.ShapeChanged);
        Assert.NotEmpty(changedShape.DriftReport.AddedShapes);
        Assert.NotEmpty(changedShape.DriftReport.MissingShapes);
    }

    [Fact]
    public async Task RecipeWithoutBaseline_PreservesExistingJsonExtraction() {
        var recipe = HtmlBrowserlessExtraction.CreateRecipe(new HtmlBrowserlessDataSource {
            Kind = "AppState", RawContent = "{\"items\":[{\"name\":\"item\",\"price\":\"12.5\"}]}"
        }, includeRawContent: true);
        var result = await HtmlBrowserlessExtraction.ExtractRecipeAsync(recipe);
        Assert.True(result.Success);
        Assert.Null(result.DriftReport);
        Assert.DoesNotContain("Baseline", HtmlBrowserlessExtraction.SerializeRecipe(recipe));
    }

    [Fact]
    public async Task AcceptedEndpointBaseline_ChecksTheFetchedResponse() {
        using var client = new HttpClient(new JsonHandler());
        var source = new HtmlBrowserlessDataSource {
            Kind = "ApiEndpoint", PageUrl = "https://example.org/catalog", ResolvedUrl = "https://example.org/items",
            Method = "GET", RequiresHttpFetch = true, CanExtractDirectly = true
        };
        var recipe = HtmlBrowserlessExtraction.CreateRecipe(source);
        var options = new HtmlBrowserlessExtractionOptions { AllowHttpFetch = true };
        var initial = await HtmlBrowserlessExtraction.ExtractRecipeAsync(recipe, options, client);
        var accepted = HtmlBrowserlessExtraction.CaptureRecipeBaseline(recipe, initial);
        var current = await HtmlBrowserlessExtraction.ExtractRecipeAsync(accepted, options, client);

        Assert.False(current.Success);
        Assert.True(current.DriftReport!.ShapeChanged);
        Assert.Single(current.Requests);
        Assert.Single(current.Items);
        Assert.Empty(current.RawContent);
    }

    [Fact]
    public void BaselineCapture_RejectsInvalidOrEmptyAcceptedExtraction() {
        var recipe = DomRecipe();
        Assert.Throws<ArgumentException>(() => HtmlBrowserlessExtraction.CaptureDomRecipeBaseline(recipe, ""));
        Assert.Throws<ArgumentException>(() => HtmlBrowserlessExtraction.CaptureDomRecipeBaseline(recipe,
            "<article class='product'><h2>Missing price</h2></article>"));
    }

    [Fact]
    public async Task BaselineCapture_BoundsValidButDeepStructuredData() {
        string nested = "\"leaf\"";
        for (int i = 0; i < 35; i++) nested = "{\"child\":" + nested + "}";
        var recipe = HtmlBrowserlessExtraction.CreateRecipe(new HtmlBrowserlessDataSource {
            Kind = "AppState", RawContent = "{\"name\":\"item\",\"nested\":" + nested + "}"
        }, includeRawContent: true);
        var extracted = await HtmlBrowserlessExtraction.ExtractRecipeAsync(recipe);
        Assert.True(extracted.Success);
        var error = Assert.Throws<InvalidDataException>(() => HtmlBrowserlessExtraction.CaptureRecipeBaseline(recipe, extracted));
        Assert.Contains("depth 32", error.Message);
        Assert.Null(recipe.Baseline);
    }

    [Theory]
    [InlineData(2, "Dom", "High")]
    [InlineData(1, "AppState", "High")]
    [InlineData(1, "Dom", "Unknown")]
    public void PortableBaseline_RejectsUnsupportedOrMismatchedMetadata(int version, string kind, string confidence) {
        var recipe = DomRecipe();
        recipe.Baseline = new HtmlExtractionBaseline {
            Version = version, SourceKind = kind, CollectionConfidence = confidence, ItemCount = 1, ItemShapes = new[] { "shape" }
        };
        Assert.Throws<ArgumentException>(() => HtmlBrowserlessExtraction.SerializeRecipe(recipe));
        Assert.Throws<ArgumentException>(() => HtmlBrowserlessExtraction.ExtractDomRecipe(recipe, Products()));
    }

    [Theory]
    [InlineData("depth", 1, "depth 32")]
    [InlineData("nodes", 1, "4096 nodes")]
    [InlineData("shapes", 257, "256 distinct")]
    public async Task BaselineComparison_ReturnsRecordsWhenCurrentShapeExceedsLimits(string limit, int expectedItems, string error) {
        var recipe = HtmlBrowserlessExtraction.CreateRecipe(new HtmlBrowserlessDataSource {
            Kind = "AppState", RawContent = "{\"name\":\"accepted\"}"
        }, includeRawContent: true);
        var accepted = HtmlBrowserlessExtraction.CaptureRecipeBaseline(recipe,
            await HtmlBrowserlessExtraction.ExtractRecipeAsync(recipe));
        if (limit == "depth") {
            string nested = "\"leaf\"";
            for (int i = 0; i < 35; i++) nested = "{\"child\":" + nested + "}";
            accepted.RawContent = "{\"name\":\"current\",\"nested\":" + nested + "}";
        } else if (limit == "nodes") {
            accepted.RawContent = "{" + string.Join(",", Enumerable.Range(0, 4100).Select(i => $"\"field{i}\":1")) + "}";
        } else {
            accepted.RawContent = "[" + string.Join(",", Enumerable.Range(0, 257).Select(i => $"{{\"field{i}\":1}}")) + "]";
        }
        var current = await HtmlBrowserlessExtraction.ExtractRecipeAsync(accepted);

        Assert.False(current.Success);
        Assert.Equal(expectedItems, current.Items.Count);
        Assert.NotNull(current.DriftReport);
        Assert.False(current.DriftReport!.IsCompatible);
        Assert.Contains(current.Warnings, warning => warning.Contains(error));
        Assert.Contains(error, current.DriftReport.ComparisonError);
    }

    [Fact]
    public void BaselineConfidence_IgnoresReorderingOfHeterogeneousValidRecords() {
        var recipe = DomRecipe();
        recipe.MaximumItemCount = 100;
        string rich = Products(20);
        string sparse = string.Concat(Enumerable.Range(0, 20).Select(i =>
            $"<article class='product'><h2 class='name'>Sparse {i}</h2><span class='price'>12.50</span></article>"));
        var accepted = HtmlBrowserlessExtraction.CaptureDomRecipeBaseline(recipe, rich + sparse);
        var current = HtmlBrowserlessExtraction.ExtractDomRecipe(accepted, sparse + rich);

        Assert.True(current.DomReport!.IsValid);
        Assert.False(current.DriftReport!.ShapeChanged);
        Assert.Equal(accepted.Baseline!.CollectionConfidence, current.DriftReport.CurrentConfidence);
        Assert.True(current.Success);
    }

    [Fact]
    public async Task BaselineComparison_DetectsResponseEnvelopeChangesOutsideRecords() {
        var recipe = HtmlBrowserlessExtraction.CreateRecipe(new HtmlBrowserlessDataSource {
            Kind = "AppState", RawContent = "{\"items\":[{\"name\":\"A\"}],\"paging\":{\"next\":\"private-token\"}}"
        }, includeRawContent: true);
        var accepted = HtmlBrowserlessExtraction.CaptureRecipeBaseline(recipe,
            await HtmlBrowserlessExtraction.ExtractRecipeAsync(recipe));
        accepted.RawContent = "{\"items\":[{\"name\":\"B\"}],\"paging\":\"broken\"}";
        var current = await HtmlBrowserlessExtraction.ExtractRecipeAsync(accepted);

        Assert.Single(current.Items);
        Assert.False(current.Success);
        Assert.False(current.DriftReport!.IsCompatible);
        Assert.True(current.DriftReport.ResponseShapeChanged);
        Assert.DoesNotContain("private-token", string.Join("", accepted.Baseline!.ItemShapes));
        Assert.DoesNotContain("private-token", accepted.Baseline.ResponseShape);
    }

    [Fact]
    public async Task AcceptedEndpointBaseline_CapturesEnvelopeWithoutRetainingRawResponse() {
        using var client = new HttpClient(new JsonHandler(envelopeOnly: true));
        var recipe = HtmlBrowserlessExtraction.CreateRecipe(new HtmlBrowserlessDataSource {
            Kind = "ApiEndpoint", PageUrl = "https://example.org/catalog", ResolvedUrl = "https://example.org/items", Method = "GET"
        });
        var options = new HtmlBrowserlessExtractionOptions { AllowHttpFetch = true };
        var initial = await HtmlBrowserlessExtraction.ExtractRecipeAsync(recipe, options, client);
        Assert.Empty(initial.RawContent);
        var accepted = HtmlBrowserlessExtraction.CaptureRecipeBaseline(recipe, initial);
        var current = await HtmlBrowserlessExtraction.ExtractRecipeAsync(accepted, options, client);

        Assert.False(current.Success);
        Assert.Empty(current.RawContent);
        Assert.Empty(accepted.RawContent);
        Assert.Empty(current.DriftReport!.AddedShapes);
        Assert.Empty(current.DriftReport.MissingShapes);
        Assert.True(current.DriftReport.ResponseShapeChanged);
        Assert.DoesNotContain("private-token", HtmlBrowserlessExtraction.SerializeRecipe(accepted));
    }

    [Theory]
    [InlineData(31)]
    [InlineData(32)]
    public async Task BaselineDescriptorSerialization_PreservesJsonAtSupportedDepth(int depth) {
        string json = "{}";
        for (int i = 0; i < depth; i++) json = "{\"child\":" + json + "}";
        var recipe = HtmlBrowserlessExtraction.CreateRecipe(new HtmlBrowserlessDataSource {
            Kind = "AppState", RawContent = json
        }, includeRawContent: true);
        var initial = await HtmlBrowserlessExtraction.ExtractRecipeAsync(recipe);
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(Assert.Single(initial.Items).Value);
        var accepted = HtmlBrowserlessExtraction.CaptureRecipeBaseline(recipe, initial);
        var current = await HtmlBrowserlessExtraction.ExtractRecipeAsync(accepted);
        Assert.True(current.Success);
        Assert.False(current.DriftReport!.HasChanges);
    }

    [Fact]
    public void BaselineConfidence_IgnoresOrdinaryTextLengthChanges() {
        string Cards(string name) => string.Concat(Enumerable.Range(0, 8).Select(i =>
            $"<article class='product'><h2 class='name'>{name}</h2><span class='price'>12.50</span>"
            + $"<a href='/item/{i}'>View</a><span class='stock'>In stock</span></article>"));
        var recipe = DomRecipe();
        var accepted = HtmlBrowserlessExtraction.CaptureDomRecipeBaseline(recipe, Cards("Short name"));
        var current = HtmlBrowserlessExtraction.ExtractDomRecipe(accepted, Cards(new string('x', 300)));

        Assert.True(current.DomReport!.IsValid);
        Assert.False(current.DriftReport!.ShapeChanged);
        Assert.Equal(accepted.Baseline!.CollectionConfidence, current.DriftReport.CurrentConfidence);
        Assert.True(current.Success);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(101)]
    public void BaselineConfidence_LeavesAllowedRecordCountsToDeclaredRules(int currentCount) {
        string Cards(int count) => string.Concat(Enumerable.Range(0, count).Select(i =>
            $"<article class='product'><h2 class='name'>Item {i}</h2><span class='price'>12.50</span>"
            + $"<a href='/item/{i}'>View</a><span class='stock'>In stock</span></article>"));
        var recipe = DomRecipe();
        recipe.MaximumItemCount = 200;
        var accepted = HtmlBrowserlessExtraction.CaptureDomRecipeBaseline(recipe, Cards(8));
        var current = HtmlBrowserlessExtraction.ExtractDomRecipe(accepted, Cards(currentCount));

        Assert.True(current.DomReport!.IsValid);
        Assert.True(current.DriftReport!.ItemCountChanged);
        Assert.False(current.DriftReport.ShapeChanged);
        Assert.False(current.DriftReport.ConfidenceReduced);
        Assert.True(current.Success);
    }

    private sealed class JsonHandler : HttpMessageHandler {
        private int calls;
        private readonly bool envelopeOnly;
        internal JsonHandler(bool envelopeOnly = false) => this.envelopeOnly = envelopeOnly;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            bool initial = Interlocked.Increment(ref calls) == 1;
            string json = initial
                ? "{\"items\":[{\"name\":\"item\",\"price\":12.5}]}"
                : "{\"items\":[{\"name\":\"item\",\"price\":\"12.5\"}]}";
            if (envelopeOnly) json = initial
                ? "{\"items\":[{\"name\":\"A\"}],\"paging\":{\"next\":\"private-token\"}}"
                : "{\"items\":[{\"name\":\"B\"}],\"paging\":\"broken\"}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                RequestMessage = request, Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
