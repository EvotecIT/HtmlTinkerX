using AngleSharp.Html.Parser;
using PSParseHTML;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlParserExtensionsTests {
    [Fact]
    public void GetElements_ByTag_ReturnsMatchingElements() {
        const string html = "<html><body><p>First</p><p>Second</p></body></html>";
        var elements = HtmlParserExtensions.GetElements(html, tag: "p").ToArray();
        Assert.Equal(2, elements.Length);
        Assert.Equal("First", elements[0].TextContent);
    }

    [Fact]
    public void GetElements_ByClass_ReturnsMatchingElements() {
        const string html = "<div><span class='info'>A</span><span>B</span></div>";
        var elements = HtmlParserExtensions.GetElements(html, className: "info").ToArray();
        Assert.Single(elements);
        Assert.Equal("A", elements[0].TextContent);
    }

    [Fact]
    public async Task GetElements_FromUrl_ByTag() {
        using var client = new HttpClient();
        string html = await HttpContentHelper.GetStringWithProperEncodingAsync(
            client,
            "https://developer.mozilla.org/en-US/docs/Web/HTML/Element/em");

        var elements = HtmlParserExtensions.GetElements(html, tag: "em");
        Assert.NotEmpty(elements);
    }
}
