using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HtmlTinkerX.Tests;

public class HtmlDomValueSelectorTests {
    private const string PageHtml = """
        <!doctype html>
        <html>
        <head><base href="https://shop.example/catalog/"></head>
        <body>
          <nav><a href="/">Home</a> <a href="/about">About us</a></nav>
          <ul class="products">
            <li class="product" data-id="1"><h2>Gold <em>One</em></h2><a class="more" href="one">Details</a><img src="one.png" alt="Gold one"></li>
            <li class="product" data-id="2"><h2>Gold Two</h2><a class="more" href="two">Details</a><img src="two.png" alt=""></li>
            <li class="product" data-id="3"><h2>Silver Three</h2><a class="more" href="three">Details</a><img src="three.png"></li>
            <li class="banner">Sale ends soon</li>
          </ul>
          <a class="pager" href="?page=2">Next page</a>
          <footer><ul><li>Contact</li><li>Imprint</li></ul></footer>
        </body>
        </html>
        """;

    [Fact]
    public void SelectValues_ReturnsOwnTextAndAttributeValues() {
        Assert.Equal(new[] { "Gold", "Gold Two", "Silver Three" }, HtmlDomExtraction.SelectValues(PageHtml, "li.product h2::text"));
        Assert.Equal(new[] { "Gold", "One", "Details", "Gold Two" }, HtmlDomExtraction.SelectValues(PageHtml, "li.product *::text").Take(4));
        Assert.Equal(HtmlDomExtraction.SelectValues(PageHtml, "li.product *::text"), HtmlDomExtraction.SelectValues(PageHtml, "li.product ::text"));
        Assert.Equal(
            new[] { "https://shop.example/catalog/one", "https://shop.example/catalog/two", "https://shop.example/catalog/three" },
            HtmlDomExtraction.SelectValues(PageHtml, "a.more::attr(href)"));
        Assert.Equal(new[] { "Gold one", "" }, HtmlDomExtraction.SelectValues(PageHtml, "img::attr(alt)"));
        Assert.Equal(new[] { "1" }, HtmlDomExtraction.SelectValues(PageHtml, "li::attr(data-id)", first: true));
    }

    [Theory]
    [InlineData("a::text span", "last part")]
    [InlineData("a::attr(href)::text", "last part")]
    [InlineData("nav a, li::text", "selector list")]
    [InlineData("a::attr()", "one attribute")]
    [InlineData("::text", "element selector before")]
    [InlineData("li", "must end with ::text")]
    public void SelectValues_RejectsSelectorsThatCannotSelectValues(string selector, string expectedMessage) {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => HtmlDomExtraction.SelectValues(PageHtml, selector));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectElements_PointsValueSelectorsToSelectValues() {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => HtmlDomExtraction.SelectElements(PageHtml, "a::attr(href)"));

        Assert.Contains("SelectValues", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_FieldSelectorsCanEndWithTextOrAttribute() {
        var properties = new Dictionary<string, HtmlDomFieldDefinition> {
            ["Title"] = new() { Selector = "h2::text" },
            ["Link"] = new() { Selector = "a.more::attr(href)" },
            ["Id"] = new() { Selector = "::attr(data-id)" }
        };

        IReadOnlyList<HtmlDomExtractionRecord> records = HtmlDomExtraction.Extract(PageHtml, "li.product", properties);

        Assert.Equal(new object?[] { "Gold", "https://shop.example/catalog/one", "1" },
            new[] { records[0].Values["Title"], records[0].Values["Link"], records[0].Values["Id"] });
        Assert.Equal("Silver Three", records[2].Values["Title"]);
    }

    [Theory]
    [InlineData("next page", HtmlTextMatch.Exact, null, false, "a:Next page")]
    [InlineData("Next Page", HtmlTextMatch.Exact, null, true, "")]
    [InlineData("gold", HtmlTextMatch.Contains, "h2", false, "h2:Gold One|h2:Gold Two")]
    [InlineData(@"^(Gold|Silver) T", HtmlTextMatch.Regex, null, false, "h2:Gold Two|h2:Silver Three")]
    [InlineData("Details", HtmlTextMatch.Exact, "li:nth-child(2) a", false, "a:Details")]
    public void FindByText_MatchesOwnText(string text, HtmlTextMatch match, string? selector, bool caseSensitive, string expected) {
        IReadOnlyList<IElement> elements = HtmlDomExtraction.FindByText(PageHtml, text, match, selector, caseSensitive);

        string actual = string.Join("|", elements.Select(static element => element.LocalName + ":" + element.TextContent.Trim()));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FindSimilar_ReturnsListSiblingsAndSkipsOtherShapes() {
        IDocument document = HtmlParser.ParseWithAngleSharp(PageHtml);

        IReadOnlyList<IElement> similar = HtmlDomExtraction.FindSimilar(document.QuerySelector("li.product")!);

        Assert.Equal(new[] { "2", "3" }, similar.Select(static element => element.GetAttribute("data-id")));
    }
}
