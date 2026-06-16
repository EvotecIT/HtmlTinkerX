using System;
using System.Linq;
using System.Threading.Tasks;

namespace HtmlTinkerX.Examples;

/// <summary>
/// Demonstrates browserless-first extraction without launching Playwright.
/// </summary>
public static class BrowserlessExtractionExample {
    private const string Html = """
<!doctype html>
<html>
<head>
<title>Browserless extraction story</title>
<script id="__NEXT_DATA__" type="application/json">
{
  "props": {
    "pageProps": {
      "products": [
        { "id": "p1", "name": "HtmlTinkerX guide", "category": "Docs" },
        { "id": "p2", "name": "Browserless recipe", "category": "Automation" }
      ]
    }
  }
}
</script>
<script>
fetch('/api/products', { method: 'GET' });
</script>
</head>
<body>
<main>Products are hydrated from embedded state.</main>
</body>
</html>
""";

    /// <summary>
    /// Runs the browserless extraction example.
    /// </summary>
    public static async Task RunAsync() {
        var sources = await HtmlBrowserlessExtraction.DiscoverAsync(
            Html,
            new HtmlBrowserlessDiscoveryOptions {
                BaseUri = new Uri("https://example.org/products"),
                DirectOnly = true
            });

        HtmlBrowserlessDataSource source = sources.First(item => item.Kind == "AppState");
        HtmlBrowserlessExtractionResult result = await HtmlBrowserlessExtraction.ExtractAsync(source);
        HtmlBrowserlessExtractionRecipe recipe = HtmlBrowserlessExtraction.CreateRecipe(source, includeRawContent: true);
        HtmlBrowserlessExtractionResult recipeResult = await HtmlBrowserlessExtraction.ExtractRecipeAsync(recipe);

        Console.WriteLine($"Browserless mode: {result.Mode}");
        Console.WriteLine($"Source: {source.Kind} / {source.Name}");
        Console.WriteLine($"Items: {string.Join(", ", result.Items.Select(item => item.Name))}");
        Console.WriteLine($"Recipe items: {recipeResult.Items.Count}");
        Console.WriteLine("Browser started: false");
    }
}
