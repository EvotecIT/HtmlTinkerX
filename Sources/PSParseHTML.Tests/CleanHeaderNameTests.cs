using Xunit;

namespace PSParseHTML.Tests;

public class CleanHeaderNameTests {
    [Theory]
    [InlineData("Header-Name", "Header_Name")]
    [InlineData("[Header]", "Header")]
    [InlineData("A&B", "AB")]
    [InlineData("#Price ($)!", "Price")]
    public void CleanHeaderName_ReturnsExpected(string input, string expected) {
        string result = HtmlParser.CleanHeaderName(input);
        Assert.Equal(expected, result);
    }
}
