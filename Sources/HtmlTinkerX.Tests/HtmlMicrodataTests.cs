using HtmlTinkerX;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlMicrodataTests {
    [Fact]
    public void ParseMicrodataItems_ReturnsExpected() {
        const string html = "<div itemscope itemtype=\"https://schema.org/Person\"><span itemprop=\"name\">Jane Doe</span></div>";
        var items = HtmlParser.ParseMicrodataItems(html);
        Assert.Single(items);
        Assert.Equal("https://schema.org/Person", items[0].Type);
        Assert.Equal("Jane Doe", items[0].Properties["name"][0]);
    }

    [Fact]
    public void ValidateMicrodataItems_ReturnsMismatches() {
        const string html = "<div itemscope itemtype=\"https://schema.org/Person\"><span itemprop=\"foo\">bar</span></div>";
        var items = HtmlParser.ParseMicrodataItems(html);
        var mismatches = HtmlParser.ValidateMicrodataItems(items);
        Assert.Single(mismatches);
        Assert.Equal("https://schema.org/Person", mismatches[0].Type);
        Assert.Contains("foo", mismatches[0].Properties);
    }
}