using PSParseHTML;
using Xunit;
using System.Threading.Tasks;

namespace PSParseHTML.Tests;

public class HtmlOptimizerAsyncTests {
    [Fact]
    public async Task OptimizeHtmlAsync_MinifiesContent() {
        const string input = "<html><!--c--><body> <p>Hi</p></body></html>";
        string result = await HtmlOptimizer.OptimizeHtmlAsync(input, false);
        Assert.Equal("<html><body><p>Hi</p></body></html>", result);
    }
}
