using PSParseHTML;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlParserTextTests {
    [Fact]
    public void ConvertToText_ReturnsExpectedText() {
        const string html = "<html><body><p>Hello <b>world</b></p></body></html>";
        string result = HtmlParserToText.ConvertToText(html).Trim();
        Assert.Equal("Hello world", result);
    }
}
