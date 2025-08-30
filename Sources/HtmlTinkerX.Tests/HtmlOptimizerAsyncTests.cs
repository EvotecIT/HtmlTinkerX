using HtmlTinkerX;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

/// <summary>
/// Tests asynchronous methods of <see cref="HtmlOptimizer"/>.
/// </summary>
public class HtmlOptimizerAsyncTests {
    [Fact]
    public async Task OptimizeHtmlAsync_TreatAsDocumentMinifiesContent() {
        const string input = "<html><!--c--><body> <p>Hi</p></body></html>";
        string result = await HtmlOptimizer.OptimizeHtmlAsync(input, false, treatAsDocument: true, removeComments: true);
        Assert.Equal("<html><body><p>Hi</p></body></html>", result);
    }

    [Fact]
    public async Task OptimizeHtmlAsync_DoesNotWrapFragmentsByDefault() {
        string result = await HtmlOptimizer.OptimizeHtmlAsync("<tr></tr>", false);
        Assert.Equal("<tr></tr>", result);
    }
}