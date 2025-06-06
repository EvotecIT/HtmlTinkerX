using PSParseHTML;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlOptimizerTests {
    [Fact]
    public void OptimizeCss_MinifiesContent() {
        const string css = "body { color: red; }";
        string result = HtmlOptimizer.OptimizeCss(css);
        Assert.Equal("body{color:#f00}", result);
    }
}
