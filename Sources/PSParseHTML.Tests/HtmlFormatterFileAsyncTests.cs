using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlFormatterFileAsyncTests
{
    private static string GetPath(string name)
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Documents", name));
    }

    [Fact]
    public async Task FormatJavaScriptFileAsync_ReturnsFormattedScript()
    {
        string path = GetPath("sample_script.js");
        string expected = "function x() {\n    return 1;\n};";
        string result = await HtmlFormatter.FormatJavaScriptFileAsync(path);
        Assert.Equal(expected.Replace("\r\n", "\n"), result.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task FormatCssFileAsync_ReturnsFormattedCss()
    {
        string path = GetPath("sample_style.css");
        string expected = ".foo { color: rgba(255, 0, 0, 1) }\n.bar { margin: 0; padding: 0 }";
        string result = await HtmlFormatter.FormatCssFileAsync(path);
        Assert.Equal(expected.Replace("\r\n", "\n"), result.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task FormatHtmlFileAsync_ReturnsFormattedHtml()
    {
        string path = GetPath("sample_markup.html");
        string expected = string.Join("\n", new[]
        {
            "<html>",
            "    <body>",
            "        <div>",
            "            <p>Hi</p>",
            "        </div>",
            "    </body>",
            "</html>"
        });
        string result = await HtmlFormatter.FormatHtmlFileAsync(path);
        Assert.Equal(expected.Replace("\r\n", "\n"), result.Replace("\r\n", "\n"));
    }
}
