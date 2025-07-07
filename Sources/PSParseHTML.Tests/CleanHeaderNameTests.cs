using Xunit;

namespace PSParseHTML.Tests;

/// <summary>
/// Unit tests for <see cref="HtmlUtilities.CleanHeaderName"/>.
/// </summary>
public class CleanHeaderNameTests {
    [Theory]
    [InlineData("Header-Name", "HeaderName")]
    [InlineData("[Header]", "Header")]
    [InlineData("A&B", "AandB")]
    [InlineData("#Price ($)!", "Price")]
    public void CleanHeaderName_ReturnsExpected(string input, string expected) {
        string result = HtmlParser.CleanHeaderName(input);
        Assert.Equal(expected, result);
    }
}
