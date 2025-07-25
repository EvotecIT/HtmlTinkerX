using HtmlTinkerX;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class FormatHtmlInlineCssAsyncTests {
    private const string Html = "<html><head><style>p{color:red}</style></head><body><p>Hello</p></body></html>";

    [Fact]
    public async Task FormatHtmlInlineCssAsync_RemovesStyleElements() {
        var options = new PreMailerOptions { RemoveStyleElements = true };
        string result = await HtmlFormatter.FormatHtmlInlineCssAsync(Html, options, CancellationToken.None);
        Assert.DoesNotContain("<style", result, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("style=", result, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FormatHtmlInlineCssAsync_CanceledToken_Throws() {
        using CancellationTokenSource cts = new();
        cts.Cancel();
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            HtmlFormatter.FormatHtmlInlineCssAsync(Html, null, cts.Token));
    }

    [Fact]
    public async Task FormatHtmlInlineCssAsync_InvalidCssUrl_ReturnsHtml() {
        const string invalidUrlHtml =
            "<html><head><link rel='stylesheet' href='http://localhost:1/style.css'></head><body><p>Hello</p></body></html>";
        var options = new PreMailerOptions { DownloadRemoteCss = true };
        string result = await HtmlFormatter.FormatHtmlInlineCssAsync(
            invalidUrlHtml,
            options,
            CancellationToken.None);
        Assert.Contains("<p>Hello</p>", result, System.StringComparison.OrdinalIgnoreCase);
    }
}
