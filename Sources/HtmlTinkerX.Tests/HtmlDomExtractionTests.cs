namespace HtmlTinkerX.Tests;

public class HtmlDomExtractionTests {
    private const string ProductHtml = """
        <!doctype html>
        <html>
        <head><base href="https://shop.example/products/"></head>
        <body>
          <main class="catalog">
            <article class="product product-card" data-id="1">
              <a class="product-link" href="one"><span class="product-title">Gold One</span></a>
              <span class="product-price" data-type="sell">10 &amp; 50 PLN</span>
              <span class="product-price" data-type="buy">9 PLN</span>
            </article>
            <article class="product product-card" data-id="2">
              <a class="product-link" href="two"><span class="product-title">Gold Two</span></a>
              <span class="product-price" data-type="sell">20 PLN</span>
              <span class="product-price" data-type="buy">18 PLN</span>
            </article>
            <article class="product product-card" data-id="3">
              <a class="product-link" href="three"><span class="product-title">Gold Three</span></a>
              <span class="product-price" data-type="buy">27 PLN</span>
            </article>
          </main>
        </body>
        </html>
        """;

    [Fact]
    public void Extract_ProjectsTextAttributesDefaultsAndResolvedUrls() {
        Dictionary<string, HtmlDomFieldDefinition> properties = new(StringComparer.OrdinalIgnoreCase) {
            ["Name"] = new() { Selector = ".product-title" },
            ["SellPrice"] = new() {
                Selector = ".product-price[data-type='sell']",
                DefaultValue = "Unavailable"
            },
            ["Link"] = new() {
                Selector = ".product-link",
                Attribute = "href"
            }
        };

        IReadOnlyList<HtmlDomExtractionRecord> records = HtmlDomExtraction.Extract(
            ProductHtml,
            ".product-card",
            properties,
            new Uri("https://fallback.example/"));

        Assert.Equal(3, records.Count);
        Assert.Equal("Gold One", records[0].Values["Name"]);
        Assert.Equal("10 & 50 PLN", records[0].Values["SellPrice"]);
        Assert.Equal("https://shop.example/products/one", records[0].Values["Link"]);
        Assert.Equal("Unavailable", records[2].Values["SellPrice"]);
    }

    [Fact]
    public void Extract_ThrowsWhenARequiredFieldIsMissing() {
        Dictionary<string, HtmlDomFieldDefinition> properties = new(StringComparer.OrdinalIgnoreCase) {
            ["SellPrice"] = new() {
                Selector = ".product-price[data-type='sell']",
                Required = true
            }
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            HtmlDomExtraction.Extract(ProductHtml, ".product-card", properties));

        Assert.Contains("item 2", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SellPrice", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectElements_UsesCssSelectorsAndReportsInvalidSelectors() {
        IReadOnlyList<AngleSharp.Dom.IElement> matches =
            HtmlDomExtraction.SelectElements(ProductHtml, ".product-title");

        Assert.Equal(3, matches.Count);
        Assert.Equal("Gold One", matches[0].TextContent);
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            HtmlDomExtraction.SelectElements(ProductHtml, "[broken"));
        Assert.Contains("[broken", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DiscoverSelectors_FindsRepeatedCardsFieldsLinksAndSuggestedCommand() {
        IReadOnlyList<HtmlDomSelectorCandidate> candidates = HtmlDomExtraction.DiscoverSelectors(
            ProductHtml,
            "Gold",
            new Uri("https://shop.example/catalog"),
            limit: 5);

        HtmlDomSelectorCandidate product = Assert.Single(
            candidates,
            candidate => candidate.Selector == "article.product-card");
        Assert.Equal(3, product.MatchCount);
        Assert.Contains(product.Fields, field => field.Name == "Title");
        Assert.Contains(product.Fields, field =>
            field.Name == "ProductLink"
            && field.Attribute == "href"
            && field.SampleValues.Contains("https://shop.example/products/one"));
        Assert.Contains(product.Fields, field =>
            field.Name == "SellPrice"
            && field.Selector.Contains("data-type='sell'", StringComparison.Ordinal));
        Assert.Contains("Select-HtmlData", product.SuggestedCommand, StringComparison.Ordinal);
        Assert.Contains("-ItemSelector", product.SuggestedCommand, StringComparison.Ordinal);
        Assert.Contains("SellPrice", product.SuggestedCommand, StringComparison.Ordinal);
    }

    [Fact]
    public void DiscoverSelectors_MatchesDescendantUrlsAndKeepsSharedClassWithModifiers() {
        const string html = """
            <main>
              <article class="product-card sku-one">
                <a class="brand-link" href="/brands/acme">Acme</a>
                <a class="product-overlay-link" href="/products/one">Product one</a>
              </article>
              <article class="product-card sku-two">
                <a class="brand-link" href="/brands/acme">Acme</a>
                <a class="product-overlay-link" href="/products/two">Product two</a>
              </article>
            </main>
            """;

        HtmlDomSelectorCandidate candidate = Assert.Single(
            HtmlDomExtraction.DiscoverSelectors(
                html,
                "/products/two",
                new Uri("https://shop.example/catalog"),
                limit: 5),
            item => item.Selector == "article.product-card");

        Assert.Contains(candidate.Fields, field => field.Name == "ProductLink");
        Assert.Contains(candidate.Fields, field => field.Name == "BrandLink");
        Assert.Contains("ProductLink", candidate.SuggestedCommand, StringComparison.Ordinal);
        Assert.Contains("BrandLink", candidate.SuggestedCommand, StringComparison.Ordinal);
    }

    [Fact]
    public void DiscoverSelectors_AcceptsClasslessRowsWithLinkedText() {
        const string html = """
            <ul>
              <li><a href="/products/one">Product one</a></li>
              <li><a href="/products/two">Product two</a></li>
            </ul>
            """;

        HtmlDomSelectorCandidate row = Assert.Single(
            HtmlDomExtraction.DiscoverSelectors(html, "Product", new Uri("https://shop.example/"), limit: 5),
            candidate => candidate.Selector == "li");

        Assert.Contains(row.Fields, field => field.Attribute == "href");
        Assert.Contains(row.Fields, field => field.Name == "LinkText");
    }

    [Fact]
    public void DiscoverSelectors_ReplaysFileAndBaseUrlSource() {
        HtmlDomCommandSource source = new() {
            Path = @"C:\snapshots\catalog.html",
            BaseUri = new Uri("https://shop.example/catalog/")
        };

        HtmlDomSelectorCandidate product = Assert.Single(
            HtmlDomExtraction.DiscoverSelectors(
                ProductHtml,
                "Gold",
                source.BaseUri,
                limit: 5,
                commandSource: source),
            candidate => candidate.Selector == "article.product-card");

        Assert.Contains("-Path 'C:\\snapshots\\catalog.html'", product.SuggestedCommand, StringComparison.Ordinal);
        Assert.Contains("-BaseUrl 'https://shop.example/catalog/'", product.SuggestedCommand, StringComparison.Ordinal);
        Assert.DoesNotContain("-Url", product.SuggestedCommand, StringComparison.Ordinal);
        Assert.True(product.SuggestedCommandIsReplayable);
        Assert.Equal(string.Empty, product.SuggestedCommandNote);
    }

    [Fact]
    public void DiscoverSelectors_ReplaysRequestSpecificUrlOptionsWithoutEmbeddingSecrets() {
        HtmlDomCommandSource source = new() {
            Url = new Uri("https://shop.example/catalog"),
            UserAgent = "Scraper's Agent",
            Proxy = "http://proxy.example:8080",
            UsesHeaders = true,
            UsesProxyCredential = true
        };

        HtmlDomSelectorCandidate product = Assert.Single(
            HtmlDomExtraction.DiscoverSelectors(
                ProductHtml,
                "Gold",
                source.Url,
                limit: 5,
                commandSource: source),
            candidate => candidate.Selector == "article.product-card");

        Assert.Contains("-Url 'https://shop.example/catalog'", product.SuggestedCommand, StringComparison.Ordinal);
        Assert.Contains("-UserAgent 'Scraper''s Agent'", product.SuggestedCommand, StringComparison.Ordinal);
        Assert.Contains("-Proxy 'http://proxy.example:8080'", product.SuggestedCommand, StringComparison.Ordinal);
        Assert.Contains("-ProxyCredential $ProxyCredential", product.SuggestedCommand, StringComparison.Ordinal);
        Assert.Contains("-Header $Header", product.SuggestedCommand, StringComparison.Ordinal);
        Assert.False(product.SuggestedCommandIsReplayable);
        Assert.Contains("$ProxyCredential", product.SuggestedCommandNote, StringComparison.Ordinal);
        Assert.Contains("$Header", product.SuggestedCommandNote, StringComparison.Ordinal);
    }

    [Fact]
    public void DiscoverSelectors_UsesSecureTemplateForSensitiveUrl() {
        HtmlDomCommandSource source = new() {
            Url = new Uri("https://shop.example/catalog?access_token=secret")
        };

        HtmlDomSelectorCandidate product = Assert.Single(
            HtmlDomExtraction.DiscoverSelectors(
                ProductHtml,
                "Gold",
                source.Url,
                limit: 5,
                commandSource: source),
            candidate => candidate.Selector == "article.product-card");

        Assert.Contains("-Url $Url", product.SuggestedCommand, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", product.SuggestedCommand, StringComparison.Ordinal);
        Assert.False(product.SuggestedCommandIsReplayable);
        Assert.Contains("$Url", product.SuggestedCommandNote, StringComparison.Ordinal);
    }

    [Fact]
    public void DiscoverSelectors_DistinguishesUnqualifiedSiblingLinks() {
        const string html = """
            <main>
              <article class="card"><a href="/brand/one">Brand one</a><a href="/product/one">Product one</a></article>
              <article class="card"><a href="/brand/two">Brand two</a><a href="/product/two">Product two</a></article>
            </main>
            """;

        HtmlDomSelectorCandidate card = Assert.Single(
            HtmlDomExtraction.DiscoverSelectors(
                html,
                "Product",
                new Uri("https://shop.example/"),
                limit: 5),
            candidate => candidate.Selector == "article.card");
        HtmlDomSelectorFieldCandidate[] links = card.Fields
            .Where(field => field.Attribute == "href")
            .ToArray();

        Assert.Equal(2, links.Length);
        Assert.Contains(links, field => field.Selector == "a:nth-of-type(1)");
        Assert.Contains(links, field => field.Selector == "a:nth-of-type(2)");
        Assert.Contains("Link2", card.SuggestedCommand, StringComparison.Ordinal);
    }
}
