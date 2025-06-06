using PSParseHTML;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlOptimizerTests {
    [Fact]
    public void OptimizeHtml_MinifiesContent() {
        const string input = "<html><!--c--><body> <p>Hi</p></body></html>";
        string result = HtmlOptimizer.OptimizeHtml(input, false);
        Assert.Equal("<html><body><p>Hi</p></body></html>", result);
    }

    [Fact]
    public void OptimizeHtml_PreservesMotw() {
        const string input = "<!-- saved from url=(0014)about:internet --><html><body>test</body></html>";
        string result = HtmlOptimizer.OptimizeHtml(input, false);
        Assert.StartsWith("<!-- saved from url=(0014)about:internet -->", result);
    }
}
