using PSParseHTML;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlUtilitiesTests {
    [Fact]
    public void ConvertToText_ReturnsExpectedText() {
        const string html = "<html><body><p>Hello <b>world</b></p></body></html>";
        string result = HtmlUtilities.ConvertToText(html).Trim();
        Assert.Equal("Hello world", result);
    }
}
