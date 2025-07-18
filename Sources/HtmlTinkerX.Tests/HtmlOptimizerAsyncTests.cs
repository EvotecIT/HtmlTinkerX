using HtmlTinkerX;
using System.Threading.Tasks;
using Xunit;

namespace PSParseHTML.Tests;

/// <summary>
/// Tests asynchronous methods of <see cref="HtmlOptimizer"/>.
/// </summary>
public class HtmlOptimizerAsyncTests {
    [Fact]
    public async Task OptimizeHtmlAsync_MinifiesContent() {
        const string input = "<html><!--c--><body> <p>Hi</p></body></html>";
        string result = await HtmlOptimizer.OptimizeHtmlAsync(input, false);
        Assert.Equal("<html><body><p>Hi</p></body></html>", result);
    }
}