using System;
using System.Collections.Generic;

namespace HtmlTinkerX.Examples;

/// <summary>
/// Demonstrates extracting JSON-LD data into a typed model.
/// </summary>
public static class JsonLdExtractionExample {
    /// <summary>Executes the example logic.</summary>
    public static void Run() {
        const string html = @"<html>
<head>
<script type=""application/ld+json"">
{
  ""@context"": ""https://schema.org"",
  ""@type"": ""Product"",
  ""name"": ""Widget""
}
</script>
</head>
<body></body>
</html>";

        List<Product> products = HtmlParser.ParseJsonLd<Product>(html);
        foreach (Product product in products) {
            Console.WriteLine(product.Name);
        }
    }

    private class Product {
        public string? Name { get; set; }
    }
}