using System;
using System.IO;
using System.Threading.Tasks;
using PSParseHTML;
using Xunit;

namespace PSParseHTML.Tests;

public class PreMailerClientAsyncTests
{
    private const string HtmlWithMediaQuery = "<html><head><style>h1{color:red;}@media(max-width:600px){h1{font-size:14px;}}</style></head><body><h1>Hello</h1></body></html>";

    [Fact]
    public async Task MoveCssInlineAsync_RemovesStyleElements_WhenEnabled()
    {
        var options = new PreMailerOptions { RemoveStyleElements = true };
        PreMailerResult result = await PreMailerClient.MoveCssInlineAsync(HtmlWithMediaQuery, options);
        Assert.DoesNotContain("<style", result.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MoveCssInlineFromFileAsync_ProcessesFile()
    {
        var options = new PreMailerOptions { RemoveStyleElements = true };
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".html");
        await File.WriteAllTextAsync(path, HtmlWithMediaQuery);
        try
        {
            PreMailerResult result = await PreMailerClient.MoveCssInlineFromFileAsync(path, options);
            Assert.DoesNotContain("<style", result.Html, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
