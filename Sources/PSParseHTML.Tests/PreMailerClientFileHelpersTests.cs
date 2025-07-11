using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace PSParseHTML.Tests;

public class PreMailerClientFileHelpersTests
{
    private const string HtmlContent = "<html><head><style>p{color:red}</style></head><body><p>Hello</p></body></html>";

    [Fact]
    public void MoveCssInlineFromFile_RemovesStyleElements()
    {
        var options = new PreMailerOptions { RemoveStyleElements = true };
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".html");
        File.WriteAllText(path, HtmlContent);
        try
        {
            PreMailerResult result = PreMailerClient.MoveCssInlineFromFile(path, options);
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
            PreMailerResult syncResult = PreMailerClient.MoveCssInlineFromFile(path, options);
            PreMailerResult asyncResult = await PreMailerClient.MoveCssInlineFromFileAsync(path, options);
            Assert.Equal(syncResult.Html, asyncResult.Html);
            Assert.DoesNotContain("<style", asyncResult.Html, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MoveCssInlineFromFile_RelativePath()
    {
        string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".html");
        File.WriteAllText(file, HtmlContent);
        string relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), file);
        try
        {
            PreMailerResult result = PreMailerClient.MoveCssInlineFromFile(relative, null);
            Assert.Contains("Hello", result.Html, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void MoveCssInlineFromFile_EnvironmentVariablePath()
    {
        string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".html");
        File.WriteAllText(file, HtmlContent);
        Environment.SetEnvironmentVariable("HTML_FILE_TEST", file);
        try
        {
            PreMailerResult result = PreMailerClient.MoveCssInlineFromFile("%HTML_FILE_TEST%", null);
            Assert.Contains("Hello", result.Html, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(file);
            Environment.SetEnvironmentVariable("HTML_FILE_TEST", null);
        }
    }
}
