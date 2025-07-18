using HtmlTinkerX;
using System.IO;
using Xunit;

namespace PSParseHTML.Tests;

/// <summary>
/// Tests for file-based formatting helpers in <see cref="HtmlFormatter"/>.
/// </summary>
public class HtmlFormatterFileTests {
    private static string GetPath(string name) => TestHelpers.GetDocumentPath(name);

    [Fact]
    /// <summary>
    /// Verifies that JavaScript files are properly formatted.
    /// </summary>
    public void FormatJavaScriptFile_ReturnsFormattedScript() {
        string path = GetPath("sample_script.js");
        string expected = "function x() {\n    return 1;\n};";
        string result = HtmlFormatter.FormatJavaScriptFile(path);
        TestHelpers.EqualIgnoringLineEndings(expected, result);
    }

    [Fact]
    /// <summary>
    /// Ensures that CSS files are formatted with expected spacing.
    /// </summary>
    public void FormatCssFile_ReturnsFormattedCss() {
        string path = GetPath("sample_style.css");
        string expected = ".foo { color: rgba(255, 0, 0, 1) }\n.bar { margin: 0; padding: 0 }";
        string result = HtmlFormatter.FormatCssFile(path);
        TestHelpers.EqualIgnoringLineEndings(expected, result);
    }

    [Fact]
    /// <summary>
    /// Checks that HTML files are formatted with indentation.
    /// </summary>
    public void FormatHtmlFile_ReturnsFormattedHtml() {
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
        TestHelpers.EqualIgnoringLineEndings(expected, result);
    }
}