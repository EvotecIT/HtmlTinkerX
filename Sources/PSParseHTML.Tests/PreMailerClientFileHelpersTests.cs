using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace PSParseHTML.Tests;

public class PreMailerClientFileHelpersTests
{
    private const string HtmlContent = "<html><head><style>p{color:red}</style></head><body><p>Hello</p></body></html>";

    [Fact]
    public async Task MoveCssInlineFromFile_RemovesStyleElements()
    {
        var options = new PreMailerOptions { RemoveStyleElements = true };
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".html");
        File.WriteAllText(path, HtmlContent);
        try
        {
            PreMailerResult result = await PreMailerClient.MoveCssInlineFromFile(path, options);
            Assert.DoesNotContain("<style", result.Html, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task MoveCssInlineFromFileAsync_BehavesSameAsSync()
    {
        var options = new PreMailerOptions { RemoveStyleElements = true };
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".html");
        File.WriteAllText(path, HtmlContent);
        try
        {
            PreMailerResult syncResult = await PreMailerClient.MoveCssInlineFromFile(path, options);
            PreMailerResult asyncResult = await PreMailerClient.MoveCssInlineFromFileAsync(path, options);
            Assert.Equal(syncResult.Html, asyncResult.Html);
            Assert.DoesNotContain("<style", asyncResult.Html, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
