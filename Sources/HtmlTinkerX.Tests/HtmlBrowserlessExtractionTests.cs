using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlBrowserlessExtractionTests {
    [Fact]
    public async Task DiscoverAsync_FindsStaticAppStateAndExtractsRecords() {
        const string html = """
<!doctype html>
<html>
<head>
<script id="__NEXT_DATA__" type="application/json">
{
  "props": {
    "pageProps": {
      "products": [
        { "id": "p1", "name": "Alpha", "price": 10 },
        { "id": "p2", "name": "Beta", "price": 20 }
      ]
    }
  }
}
</script>
</head>
<body><main>Loading</main></body>
</html>
""";

        var sources = await HtmlBrowserlessExtraction.DiscoverAsync(
            html,
            new HtmlBrowserlessDiscoveryOptions {
                BaseUri = new Uri("https://example.org/products"),
                DirectOnly = true
            });

        HtmlBrowserlessDataSource appState = Assert.Single(sources, source => source.Kind == "AppState");
        Assert.True(appState.CanExtractDirectly);
        Assert.False(appState.RequiresHttpFetch);

        HtmlBrowserlessExtractionResult result = await HtmlBrowserlessExtraction.ExtractAsync(appState);

        Assert.True(result.Success);
        Assert.Equal("Browserless", result.Mode);
        Assert.Contains(result.Items, item => item.Name == "Alpha" && item.Path.EndsWith("products[0]", StringComparison.Ordinal));
        Assert.Contains(result.Items, item => item.Name == "Beta" && item.Path.EndsWith("products[1]", StringComparison.Ordinal));
        Assert.Contains(result.Evidence, evidence => evidence.Contains("No browser runtime", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExtractAsync_DoesNotFetchEndpointUnlessAllowed() {
        HtmlBrowserlessDataSource source = new() {
            Kind = "ApiEndpoint",
            Name = "Products",
            Method = "GET",
            ResolvedUrl = "https://example.org/api/products",
            RequiresHttpFetch = true,
            CanExtractDirectly = true
        };

        HtmlBrowserlessExtractionResult result = await HtmlBrowserlessExtraction.ExtractAsync(source);

        Assert.False(result.Success);
        Assert.Empty(result.Requests);
        Assert.Contains(result.Warnings, warning => warning.Contains("HTTP fetch was not allowed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExtractAsync_FetchesLowRiskGetEndpointWhenAllowed() {
        HtmlBrowserlessDataSource source = new() {
            Kind = "ApiEndpoint",
            Name = "Products",
            Method = "GET",
            PageUrl = "https://example.org/products",
            ResolvedUrl = "https://example.org/api/products",
            RequiresHttpFetch = true,
            CanExtractDirectly = true
        };

        using HttpClient client = new(new StaticJsonHandler("""
{
  "items": [
    { "id": "p1", "name": "Alpha" },
    { "id": "p2", "name": "Beta" }
  ]
}
"""));

        HtmlBrowserlessExtractionResult result = await HtmlBrowserlessExtraction.ExtractAsync(
            source,
            new HtmlBrowserlessExtractionOptions {
                AllowHttpFetch = true,
                IncludeRawContent = true
            },
            client);

        Assert.True(result.Success);
        Assert.Single(result.Requests);
        Assert.True(result.Requests[0].Success);
        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, item => item.Name == "Alpha");
        Assert.Contains("items", result.RawContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtractAsync_DoesNotFetchEndpointWhenOriginCannotBeProven() {
        HtmlBrowserlessDataSource source = new() {
            Kind = "ApiEndpoint",
            Name = "Products",
            Method = "GET",
            ResolvedUrl = "https://example.org/api/products",
            RequiresHttpFetch = true,
            CanExtractDirectly = true
        };

        using CountingHandler handler = new("""
{
  "items": [
    { "id": "p1", "name": "Alpha" }
  ]
}
""");
        using HttpClient client = new(handler);

        HtmlBrowserlessExtractionResult result = await HtmlBrowserlessExtraction.ExtractAsync(
            source,
            new HtmlBrowserlessExtractionOptions {
                AllowHttpFetch = true
            },
            client);

        Assert.False(result.Success);
        Assert.Equal(0, handler.RequestCount);
        Assert.Contains(result.Warnings, warning => warning.Contains("cannot be proven same-origin", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExtractAsync_PropagatesCallerCancellation() {
        HtmlBrowserlessDataSource source = new() {
            Kind = "ApiEndpoint",
            Name = "Products",
            Method = "GET",
            PageUrl = "https://example.org/products",
            ResolvedUrl = "https://example.org/api/products",
            RequiresHttpFetch = true,
            CanExtractDirectly = true
        };

        using CancellationTokenSource cts = new();
        using HttpClient client = new(new CancelingHandler(cts));

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            HtmlBrowserlessExtraction.ExtractAsync(
                source,
                new HtmlBrowserlessExtractionOptions {
                    AllowHttpFetch = true
                },
                client,
                cts.Token));
    }

    [Fact]
    public async Task ExtractAsync_TruncatesFetchedResponsesByBytes() {
        HtmlBrowserlessDataSource source = new() {
            Kind = "ApiEndpoint",
            Name = "Products",
            Method = "GET",
            PageUrl = "https://example.org/products",
            ResolvedUrl = "https://example.org/api/products",
            RequiresHttpFetch = true,
            CanExtractDirectly = true
        };

        using HttpClient client = new(new TextHandler("abcdef"));

        HtmlBrowserlessExtractionResult result = await HtmlBrowserlessExtraction.ExtractAsync(
            source,
            new HtmlBrowserlessExtractionOptions {
                AllowHttpFetch = true,
                IncludeRawContent = true,
                MaxResponseBytes = 3
            },
            client);

        Assert.Equal("abc", result.RawContent);
        Assert.Contains(result.Warnings, warning => warning.Contains("3 bytes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RecipeRoundTrip_CanExtractStaticPayloadWhenRawContentIncluded() {
        HtmlBrowserlessDataSource source = new() {
            Kind = "JsonLd",
            Name = "ProductGraph",
            Type = "Product",
            PageUrl = "https://example.org/product",
            Selector = "script[type='application/ld+json']",
            RawContent = """
[
  { "@type": "Product", "name": "Alpha" },
  { "@type": "Product", "name": "Beta" }
]
""",
            CanExtractDirectly = true
        };

        HtmlBrowserlessExtractionRecipe recipe = HtmlBrowserlessExtraction.CreateRecipe(source, includeRawContent: true);
        string json = HtmlBrowserlessExtraction.SerializeRecipe(recipe);
        HtmlBrowserlessExtractionRecipe imported = HtmlBrowserlessExtraction.DeserializeRecipe(json);

        HtmlBrowserlessExtractionResult result = await HtmlBrowserlessExtraction.ExtractRecipeAsync(imported);

        Assert.True(result.Success);
        Assert.Equal(2, result.Items.Count);
        Assert.DoesNotContain(result.Warnings, warning => warning.Contains("RawContent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RecipeRoundTrip_PreservesEndpointRiskMetadata() {
        HtmlBrowserlessDataSource source = new() {
            Kind = "ApiEndpoint",
            Name = "ExternalProducts",
            Type = "LinkedEndpoint",
            Method = "GET",
            ResolvedUrl = "https://api.example.net/products",
            RequiresHttpFetch = true,
            CanExtractDirectly = false,
            RiskLevel = HtmlApiEndpointRiskLevel.Medium,
            IsExternal = true,
            RequiresAuthenticationHint = true
        };

        HtmlBrowserlessExtractionRecipe recipe = HtmlBrowserlessExtraction.CreateRecipe(source);
        string json = HtmlBrowserlessExtraction.SerializeRecipe(recipe);
        HtmlBrowserlessExtractionRecipe imported = HtmlBrowserlessExtraction.DeserializeRecipe(json);

        HtmlBrowserlessExtractionResult result = await HtmlBrowserlessExtraction.ExtractRecipeAsync(
            imported,
            new HtmlBrowserlessExtractionOptions {
                AllowHttpFetch = true,
                AllowMediumRiskEndpoints = true
            });

        Assert.False(result.Success);
        Assert.Empty(result.Requests);
        Assert.Contains(result.Warnings, warning => warning.Contains("External endpoint", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class StaticJsonHandler : HttpMessageHandler {
        private readonly string content;

        public StaticJsonHandler(string content) {
            this.content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            HttpResponseMessage response = new(HttpStatusCode.OK) {
                Content = new StringContent(content)
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            return Task.FromResult(response);
        }
    }

    private sealed class CountingHandler : HttpMessageHandler {
        private readonly string content;

        public CountingHandler(string content) {
            this.content = content;
        }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            RequestCount++;
            HttpResponseMessage response = new(HttpStatusCode.OK) {
                Content = new StringContent(content)
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            return Task.FromResult(response);
        }
    }

    private sealed class CancelingHandler : HttpMessageHandler {
        private readonly CancellationTokenSource cts;

        public CancelingHandler(CancellationTokenSource cts) {
            this.cts = cts;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            cts.Cancel();
            return Task.FromCanceled<HttpResponseMessage>(cancellationToken);
        }
    }

    private sealed class TextHandler : HttpMessageHandler {
        private readonly string content;

        public TextHandler(string content) {
            this.content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            HttpResponseMessage response = new(HttpStatusCode.OK) {
                Content = new StringContent(content)
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
            return Task.FromResult(response);
        }
    }
}
