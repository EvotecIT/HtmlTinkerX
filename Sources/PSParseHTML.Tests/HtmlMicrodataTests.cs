using Xunit;

namespace PSParseHTML.Tests;

public class HtmlMicrodataTests {
    [Fact]
    public void ParseMicrodataItems_ReturnsExpected() {
        const string html = "<div itemscope itemtype=\"https://schema.org/Person\"><span itemprop=\"name\">Jane Doe</span></div>";
        var items = HtmlParser.ParseMicrodataItems(html);
        Assert.Single(items);
        Assert.Equal("https://schema.org/Person", items[0].Type);
        Assert.Equal("Jane Doe", items[0].Properties["name"][0]);
    }
}
