using PSParseHTML;
using Xunit;

namespace PSParseHTML.Tests;

public class PreMailerClientTests
{
    private const string HtmlWithMediaQuery = "<html><head><style>h1{color:red;}@media(max-width:600px){h1{font-size:14px;}}</style></head><body><h1>Hello</h1></body></html>";

    [Fact]
    public void MoveCssInline_RemovesStyleElements_WhenEnabled()
    {
        var options = new PreMailerOptions { RemoveStyleElements = true };
        PreMailerResult result = PreMailerClient.MoveCssInline(HtmlWithMediaQuery, options);
        Assert.DoesNotContain("<style", result.Html, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MoveCssInline_PreservesStyleElements_WhenDisabled()
    {
        var options = new PreMailerOptions { RemoveStyleElements = false };
        PreMailerResult result = PreMailerClient.MoveCssInline(HtmlWithMediaQuery, options);
        Assert.Contains("<style", result.Html, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MoveCssInline_PreservesMediaQueries_WhenRequested()
    {
        var options = new PreMailerOptions
        {
            RemoveStyleElements = true,
            PreserveMediaQueries = true
        };
        PreMailerResult result = PreMailerClient.MoveCssInline(HtmlWithMediaQuery, options);
        Assert.Contains("@media", result.Html, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<style", result.Html, System.StringComparison.OrdinalIgnoreCase);
    }
}
