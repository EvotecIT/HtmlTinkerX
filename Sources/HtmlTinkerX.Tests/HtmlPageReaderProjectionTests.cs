namespace HtmlTinkerX.Tests;

public class HtmlPageReaderProjectionTests {
    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void Read_OptionalProjectionsRetainSemanticContent(bool readable, bool markdown, bool webData) {
        const string html = "<html lang='en'><head><title>Projection test</title><base href='https://example.org/reports/'></head>"
            + "<body><main><h1>Projection test</h1><p>Services remained healthy throughout the quarter. "
            + "The operations team reviewed availability, response times and capacity for all regions.</p>"
            + "<table><tr><th>Service</th><th>Status</th></tr><tr><td>API</td><td>Healthy</td></tr></table>"
            + "<a href='details'>Details</a><img src='status.png' alt='Status chart'>"
            + "<form action='save'><input name='q'></form></main></body></html>";
        HtmlPageDocument page = HtmlPageReader.Read(html, new HtmlPageReaderOptions {
            IncludeReadableText = readable, IncludeMarkdown = markdown,
            IncludeWebData = webData, IncludeCollections = false
        });

        Assert.Equal("Projection test", page.Title);
        Assert.Equal("en", page.Language);
        Assert.Equal(html, page.Html);
        Assert.Equal("https://example.org/reports/", page.EffectiveBaseUrl);
        Assert.Single(page.Headings);
        Assert.Contains(page.Paragraphs, paragraph => paragraph.Text.Contains("Services remained healthy"));
        Assert.Single(page.Tables);
        Assert.Contains(page.Resources, resource => resource.AlternateText == "Status chart");
        Assert.Equal(readable, !string.IsNullOrWhiteSpace(page.ReadableText.Text));
        if (!readable) Assert.Equal(0, page.ReadableText.CandidateCount);
        Assert.Equal(markdown, !string.IsNullOrWhiteSpace(page.Markdown));
        Assert.Equal(webData, page.Links.Count > 0);
        Assert.Equal(webData, page.Forms.Count > 0);
        Assert.Equal(webData, page.Assets.Count > 0);
        if (webData) Assert.Contains(page.Links, link => link.Url == "https://example.org/reports/details");
        Assert.Empty(page.Collections);
    }

    [Fact]
    public void Read_CanInferCollectionsWithoutOtherWebData() {
        const string html = "<main><article class='product-card'><a class='product-link' href='one'><h2>One</h2></a><span class='price'>10</span></article>"
            + "<article class='product-card'><a class='product-link' href='two'><h2>Two</h2></a><span class='price'>20</span></article></main>";
        HtmlPageDocument page = HtmlPageReader.Read(html, new HtmlPageReaderOptions {
            IncludeWebData = false, IncludeReadableText = false, IncludeMarkdown = false,
            BaseUri = new Uri("https://example.org/catalog/")
        });

        Assert.Empty(page.Links);
        Assert.Empty(page.Forms);
        Assert.Empty(page.Assets);
        HtmlPageCollection collection = Assert.Single(page.Collections, item => item.Count == 2);
        Assert.Equal("One", collection.Items[0]["Title"]);
        Assert.Equal("https://example.org/catalog/one", collection.Items[0]["ProductLink"]);
        Assert.Equal(2, page.Headings.Count);
    }
}
