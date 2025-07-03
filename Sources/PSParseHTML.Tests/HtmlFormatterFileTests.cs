using System.IO;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlFormatterFileTests
{
    private static string GetPath(string name)
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Documents", name));
    }

    [Fact]
    public void FormatJavaScriptFile_ReturnsFormattedScript()
    {
        string path = GetPath("sample_script.js");
        string expected = "function x() {\n    return 1;\n};";
        string result = HtmlFormatter.FormatJavaScriptFile(path);
        Assert.Equal(expected.Replace("\r\n", "\n"), result.Replace("\r\n", "\n"));
    }

    [Fact]
    public void FormatCssFile_ReturnsFormattedCss()
    {
        string path = GetPath("sample_style.css");
        string expected = ".foo { color: rgba(255, 0, 0, 1) }\n.bar { margin: 0; padding: 0 }";
        string result = HtmlFormatter.FormatCssFile(path);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatHtmlFile_ReturnsFormattedHtml()
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
        string result = HtmlFormatter.FormatHtmlFile(path);
        Assert.Equal(expected.Replace("\r\n", "\n"), result.Replace("\r\n", "\n"));
    }
}
