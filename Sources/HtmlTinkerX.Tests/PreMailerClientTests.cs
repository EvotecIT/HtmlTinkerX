using HtmlTinkerX;
using System.Threading;
using Xunit;

namespace PSParseHTML.Tests;

public class PreMailerClientTests
{
    private const string HtmlWithMediaQuery = "<html><head><style>h1{color:red;}@media(max-width:600px){h1{font-size:14px;}}</style></head><body><h1>Hello</h1></body></html>";

    [Fact]
    public async Task MoveCssInline_RemovesStyleElements_WhenEnabled()
    {
        var options = new PreMailerOptions { RemoveStyleElements = true };
        PreMailerResult result = await PreMailerClient.MoveCssInlineAsync(HtmlWithMediaQuery, options, CancellationToken.None);
        Assert.DoesNotContain("<style", result.Html, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MoveCssInline_PreservesStyleElements_WhenDisabled()
    {
        var options = new PreMailerOptions { RemoveStyleElements = false };
        PreMailerResult result = await PreMailerClient.MoveCssInlineAsync(HtmlWithMediaQuery, options, CancellationToken.None);
        Assert.Contains("<style", result.Html, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MoveCssInline_PreservesMediaQueries_WhenRequested()
    {
        var options = new PreMailerOptions
        {
            RemoveStyleElements = true,
            PreserveMediaQueries = true
        };
        PreMailerResult result = await PreMailerClient.MoveCssInlineAsync(HtmlWithMediaQuery, options, CancellationToken.None);
        Assert.Contains("@media", result.Html, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<style", result.Html, System.StringComparison.OrdinalIgnoreCase);
    }
}
