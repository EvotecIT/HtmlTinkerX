using System.Threading.Tasks;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlParserTests {
    [Fact]
    public void ParseWithAngleSharp_FromString() {
        const string html = "<html><body><p>Test</p></body></html>";
        var doc = HtmlParser.ParseWithAngleSharp(html);
        Assert.Equal("html", doc.DocumentElement.NodeName.ToLower());
    }

    [Fact]
    public void ParseWithHtmlAgilityPack_FromString() {
        const string html = "<html><body><p>Test</p></body></html>";
        var doc = HtmlParser.ParseWithHtmlAgilityPack(html);
        Assert.Equal("#document", doc.DocumentNode.Name.ToLower());
    }

    [Fact]
    public async Task ParseUrlWithAngleSharpAsync_FromExample() {
        var doc = await HtmlParser.ParseUrlWithAngleSharpAsync("https://example.com");
        Assert.Contains("Example Domain", doc.Title);
    }

    [Fact]
    public async Task ParseUrlWithHtmlAgilityPackAsync_FromExample() {
        var doc = await HtmlParser.ParseUrlWithHtmlAgilityPackAsync("https://example.com");
        Assert.NotNull(doc.DocumentNode);
    }
}
