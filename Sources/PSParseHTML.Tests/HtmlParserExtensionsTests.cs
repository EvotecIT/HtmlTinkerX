using System.Linq;
using AngleSharp.Html.Parser;
using PSParseHTML;
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
}
