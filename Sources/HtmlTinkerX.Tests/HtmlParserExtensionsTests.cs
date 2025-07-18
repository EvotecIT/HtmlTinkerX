using AngleSharp.Html.Parser;
using HtmlTinkerX;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using HtmlParserExtensions = HtmlTinkerX.HtmlParserExtensions;

namespace PSParseHTML.Tests;

/// <summary>
/// Tests extension methods for <see cref="HtmlParser"/>.
/// </summary>
public class HtmlParserExtensionsTests {
    [Fact]
    /// <summary>
    /// Retrieves elements filtered by tag name.
    /// </summary>
    public void GetElements_ByTag_ReturnsMatchingElements() {
        const string html = "<html><body><p>First</p><p>Second</p></body></html>";
        var elements = HtmlParserExtensions.GetElements(html, tag: "p").ToArray();
        Assert.Equal(2, elements.Length);
        Assert.Equal("First", elements[0].TextContent);
    }

    [Fact]
    /// <summary>
    /// Retrieves elements filtered by CSS class.
    /// </summary>
    public void GetElements_ByClass_ReturnsMatchingElements() {
        const string html = "<div><span class='info'>A</span><span>B</span></div>";
        var elements = HtmlParserExtensions.GetElements(html, className: "info").ToArray();
        Assert.Single(elements);
        Assert.Equal("A", elements[0].TextContent);
    }

    [Fact]
    /// <summary>
    /// Downloads a page and selects elements by tag.
    /// </summary>
    public async Task GetElements_FromUrl_ByTag() {
        using var client = new HttpClient();
        string html = await HtmlUtilities.GetStringWithProperEncodingAsync(
            client,
            "https://developer.mozilla.org/en-US/docs/Web/HTML/Element/em");

        var elements = HtmlParserExtensions.GetElements(html, tag: "em");
        Assert.NotEmpty(elements);
    }

    [Fact]
    /// <summary>
    /// Retrieves an element using its id attribute.
    /// </summary>
    public void GetElements_ById_ReturnsMatchingElement() {
        const string html = "<div><span id='special'>A</span><span>B</span></div>";
        var elements = HtmlParserExtensions.GetElements(html, id: "special").ToArray();
        Assert.Single(elements);
        Assert.Equal("A", elements[0].TextContent);
    }

    [Fact]
    /// <summary>
    /// Retrieves an element using its name attribute.
    /// </summary>
    public void GetElements_ByName_ReturnsMatchingElement() {
        const string html = "<form><input name='field1'/><input name='field2'/></form>";
        var elements = HtmlParserExtensions.GetElements(html, name: "field1").ToArray();
        Assert.Single(elements);
    }

    [Fact]
    /// <summary>
    /// Validates combined selection filters produce correct counts.
    /// </summary>
    public void GetElements_ByClassTagIdName_CombinedCounts() {
        const string html = "<div id='box' class='wrapper'><span class='wrapper' name='x'>T</span></div>";
        Assert.Single(HtmlParserExtensions.GetElements(html, tag: "span"));
        Assert.Equal(2, HtmlParserExtensions.GetElements(html, className: "wrapper").Count());
        Assert.Single(HtmlParserExtensions.GetElements(html, id: "box"));
        Assert.Single(HtmlParserExtensions.GetElements(html, name: "x"));
    }
}