using OfficeIMO.Html;

namespace HtmlTinkerX.Tests;

public class HtmlPageReaderTests {
    [Fact]
    public void Read_ReturnsOfficeImoStyleSemanticObjectsAndResolvedWebObjects() {
        const string html = """
            <!doctype html>
            <html lang="en">
            <head><title>Quarterly status</title></head>
            <body>
              <main>
                <h1>Quarterly status</h1>
                <p>Services remained healthy.</p>
                <ul><li>North region</li><li>South region</li></ul>
                <table>
                  <caption>Availability</caption>
                  <thead><tr><th>Service</th><th>Percent</th></tr></thead>
                  <tbody><tr><td>API</td><td>99.9</td></tr></tbody>
                </table>
                <a href="details">Read details</a>
                <img src="status.png" alt="Status chart">
              </main>
            </body>
            </html>
            """;

        HtmlPageDocument page = HtmlPageReader.Read(
            html,
            new HtmlPageReaderOptions {
                SourceUri = new Uri("https://example.org/reports/q1/"),
                BaseUri = new Uri("https://example.org/reports/q1/"),
                IncludeCollections = false
            });

        Assert.Equal("Quarterly status", page.Title);
        Assert.Equal("en", page.Language);
        Assert.Contains(page.Headings, heading => heading.Text == "Quarterly status" && heading.Level == 1);
        Assert.Contains(page.Paragraphs, paragraph => paragraph.Text == "Services remained healthy.");
        Assert.Single(page.Lists);
        HtmlSemanticTable table = Assert.Single(page.Tables);
        Assert.Equal("Availability", table.Caption);
        Assert.Equal(2, table.Rows.Count);
        Assert.Contains(page.Links, link =>
            link.Text == "Read details"
            && link.Url == "https://example.org/reports/q1/details"
            && link.RawUrl == "details");
        Assert.Contains(page.Resources, resource => resource.AlternateText == "Status chart");
        Assert.Contains("# Quarterly status", page.Markdown);
        Assert.Same(page.SemanticDocument, page.Content.SemanticDocument);
        Assert.NotNull(page.LogicalDocument);
    }

    [Fact]
    public void Read_InfersDistinctRepeatedCollectionsAndProjectsNamedValues() {
        const string html = """
            <html>
            <head><base href="https://shop.example/products/"></head>
            <body>
              <main class="catalog">
                <article class="product-card">
                  <a class="product-link" href="one"><h2 class="product-title">Gold One</h2></a>
                  <span class="product-price">10 PLN</span>
                </article>
                <article class="product-card">
                  <a class="product-link" href="two"><h2 class="product-title">Gold Two</h2></a>
                  <span class="product-price">20 PLN</span>
                </article>
                <article class="product-card">
                  <a class="product-link" href="three"><h2 class="product-title">Gold Three</h2></a>
                  <span class="product-price">30 PLN</span>
                </article>
              </main>
            </body>
            </html>
            """;

        HtmlPageDocument page = HtmlPageReader.Read(
            html,
            new HtmlPageReaderOptions {
                SourceUri = new Uri("https://shop.example/catalog"),
                BaseUri = new Uri("https://shop.example/catalog"),
                CollectionLimit = 5
            });

        HtmlPageCollection products = Assert.Single(
            page.Collections,
            collection => collection.Items.Count == 3
                && collection.Fields.Any(field => field.Name == "Title")
                && collection.Fields.Any(field => field.Attribute == "href"));
        Assert.Equal("Product Cards", products.Name);
        Assert.Equal("Gold One", products.Items[0]["Title"]);
        Assert.Equal("10 PLN", products.Items[0]["Price"]);
        Assert.Equal("https://shop.example/products/one", products.Items[0]["ProductLink"]);
        Assert.DoesNotContain(
            page.Collections,
            other => other.Index != products.Index
                && other.Items.Count == products.Items.Count
                && other.Items[0].Values.SequenceEqual(products.Items[0].Values));
    }

    [Fact]
    public void Read_UsesArticleObjectsEvenWhenNoRepeatedCollectionExists() {
        const string html = """
            <article>
              <h1>One long-form article</h1>
              <p>First paragraph.</p>
              <p>Second paragraph.</p>
              <blockquote>A quoted observation.</blockquote>
              <pre><code>Get-HtmlPage -Content $html</code></pre>
            </article>
            """;

        HtmlPageDocument page = HtmlPageReader.Read(html);

        Assert.Single(page.Headings);
        Assert.Equal(2, page.Paragraphs.Count);
        Assert.Contains(page.Blocks, block => block.Kind == HtmlSemanticBlockKind.Quote);
        Assert.Contains(page.Blocks, block => block.Kind == HtmlSemanticBlockKind.Code);
        Assert.Empty(page.Collections);
    }

    [Fact]
    public void Read_CollapsesWrapperAndCardCandidatesWithTheSameRecordIdentities() {
        const string html = """
            <main>
              <div class="grid-item">
                <article class="product-card"><a class="product-link" href="/one"><h2>One</h2></a><span class="price">10</span></article>
              </div>
              <div class="grid-item">
                <article class="product-card"><a class="product-link" href="/two"><h2>Two</h2></a><span class="price">20</span></article>
              </div>
            </main>
            """;

        HtmlPageDocument page = HtmlPageReader.Read(
            html,
            new HtmlPageReaderOptions {
                BaseUri = new Uri("https://example.org/")
            });

        Assert.Single(
            page.Collections,
            collection => collection.Count == 2
                && collection.Items[0]["ProductLink"]?.ToString() == "https://example.org/one");
    }

    [Fact]
    public void Read_UsesSemanticFieldNamesAndPrefersRealLazyLoadedImages() {
        const string html = """
            <main>
              <article class="product_pod">
                <a href="/one"><h3>One</h3></a>
                <img src="data:image/gif;base64,placeholder" data-srcset="/images/one-small.jpg 80w, /images/one.jpg 160w" alt="One">
                <p class="price_color">10</p>
              </article>
              <article class="product_pod">
                <a href="/two"><h3>Two</h3></a>
                <img src="data:image/gif;base64,placeholder" data-srcset="/images/two-small.jpg 80w, /images/two.jpg 160w" alt="Two">
                <p class="price_color">20</p>
              </article>
            </main>
            """;

        HtmlPageDocument page = HtmlPageReader.Read(
            html,
            new HtmlPageReaderOptions {
                BaseUri = new Uri("https://example.org/")
            });

        HtmlPageCollection products = Assert.Single(
            page.Collections,
            collection => collection.Name == "Product Pods");
        Assert.Equal("One", products.Items[0]["Title"]);
        Assert.Equal("10", products.Items[0]["Price"]);
        Assert.Equal("https://example.org/images/one.jpg", products.Items[0]["Image"]);
    }

    [Fact]
    public void Read_CanDisableCollectionInferenceWithoutLosingSemanticContent() {
        const string html = """
            <main>
              <article class="result"><h2>One</h2><p>First</p></article>
              <article class="result"><h2>Two</h2><p>Second</p></article>
            </main>
            """;

        HtmlPageDocument page = HtmlPageReader.Read(
            html,
            new HtmlPageReaderOptions {
                IncludeCollections = false
            });

        Assert.Equal(2, page.Headings.Count);
        Assert.Equal(2, page.Paragraphs.Count);
        Assert.Empty(page.Collections);
    }

    [Fact]
    public void Read_RejectsUnboundedCollectionCounts() {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            HtmlPageReader.Read(
                "<p>Bounded.</p>",
                new HtmlPageReaderOptions {
                    CollectionLimit = 101
                }));

        Assert.Equal("CollectionLimit", exception.ParamName);
    }
}
