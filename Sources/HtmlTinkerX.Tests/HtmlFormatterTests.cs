using HtmlTinkerX;
using HtmlTinkerX.JavaScriptBeautifier;
using Xunit;

namespace HtmlTinkerX.Tests;

/// <summary>
/// Tests for in-memory formatting methods in <see cref="HtmlFormatter"/>.
/// </summary>
public class HtmlFormatterTests {
    [Fact]
    /// <summary>
    /// Formats JavaScript text using default options.
    /// </summary>
    public void FormatJavaScript_ReturnsFormattedScript() {
        const string js =
            "(function(){function main(){var tabButtons = [].slice.call(document.querySelectorAll(\"ul.tab-nav li a.buttonTab\"));tabButtons.map(function(button){button.addEventListener(\"click\",function(){document .querySelector(\"li a.active.buttonTab\") .classList.remove(\"active\");button.classList.add(\"active\");document .querySelector(\".tab-pane.active\") .classList.remove(\"active\");document .querySelector(button.getAttribute(\"href\")) .classList.add(\"active\")})})}if(document.readyState!== \"loading\"){main()}else{document.addEventListener(\"DOMContentLoaded\",main)}})();";
        const string expected =
            "(function() {\n    function main() {\n        var tabButtons = [].slice.call(document.querySelectorAll(\"ul.tab-nav li a.buttonTab\"));\n        tabButtons.map(function(button) {\n            button.addEventListener(\"click\", function() {\n                document.querySelector(\"li a.active.buttonTab\").classList.remove(\"active\");\n                button.classList.add(\"active\");\n                document.querySelector(\".tab-pane.active\").classList.remove(\"active\");\n                document.querySelector(button.getAttribute(\"href\")).classList.add(\"active\")\n            })\n        })\n    }\n    if (document.readyState !== \"loading\") {\n        main()\n    } else {\n        document.addEventListener(\"DOMContentLoaded\", main)\n    }\n})();";

        string result = HtmlFormatter.FormatJavaScript(js);
        TestHelpers.EqualIgnoringLineEndings(expected, result);
    }

    [Fact]
    /// <summary>
    /// Formats JavaScript using custom <see cref="BeautifierOptions"/>.
    /// </summary>
    public void FormatJavaScript_RespectsOptions() {
        const string js = "function x(){return 1;};";
        BeautifierOptions opts = new BeautifierOptions() { IndentSize = 2, BraceStyle = BraceStyle.Expand };
        string expected = "function x()\n{\n  return 1;\n};";

        string result = HtmlFormatter.FormatJavaScript(js, opts);
        TestHelpers.EqualIgnoringLineEndings(expected, result);
    }

    [Fact]
    public void FormatJavaScript_BreaksStatementCommaSequences() {
        const string js = "! function(n) { n.value = \"text\", n.value2 = \"hello world\", n.value3 = \"foo\" }";
        const string expected =
            "! function(n) {\n    n.value = \"text\",\n    n.value2 = \"hello world\",\n    n.value3 = \"foo\"\n}";

        string result = HtmlFormatter.FormatJavaScript(js);
        TestHelpers.EqualIgnoringLineEndings(expected, result);
    }

    [Fact]
    public void FormatJavaScript_WrapLineLengthBreaksBeforeArrayStringArgument() {
        const string js = "! function(n, r, e) { (r = e(2)(!1)).push([n.i, 'my really long string', \"\"]), n.exports = r }";
        BeautifierOptions opts = new BeautifierOptions { WrapLineLength = 40 };
        const string expected =
            "! function(n, r, e) {\n    (r = e(2)(!1)).push([n.i,\n        'my really long string', \"\"]),\n        n.exports = r\n}";

        string result = HtmlFormatter.FormatJavaScript(js, opts);
        TestHelpers.EqualIgnoringLineEndings(expected, result);
    }

    [Fact]
    public void FormatJavaScript_SplitsLongStringLiteralsWhenRequested() {
        const string js = "var payload='abcdefghijkl';";
        BeautifierOptions opts = new BeautifierOptions {
            SplitLongStringLiterals = true,
            MaxStringLiteralLength = 4
        };
        const string expected =
            "var payload = 'abcd' +\n    'efgh' +\n    'ijkl';";

        string result = HtmlFormatter.FormatJavaScript(js, opts);
        TestHelpers.EqualIgnoringLineEndings(expected, result);
    }

    [Fact]
    public void FormatJavaScript_SplitLongStringKeepsIssueScenarioBelowEditorLimit() {
        string js = $"! function(n, r, e) {{ (r = e(2)(!1)).push([n.i, '{new string('x', 2600)}', \"\"]), n.exports = r }}";
        BeautifierOptions opts = new BeautifierOptions {
            SplitLongStringLiterals = true
        };

        string result = HtmlFormatter.FormatJavaScript(js, opts);
        int maxLineLength = result
            .Replace("\r\n", "\n")
            .Split('\n')
            .Max(line => line.Length);

        Assert.True(maxLineLength < 2500, $"Expected all lines below 2500 columns, longest line was {maxLineLength}.");
    }

    [Fact]
    public void FormatJavaScript_DoesNotSplitInsideEscapeSequences() {
        const string js = "var payload='ab\\'cdef';";
        BeautifierOptions opts = new BeautifierOptions {
            SplitLongStringLiterals = true,
            MaxStringLiteralLength = 4
        };
        const string expected =
            "var payload = 'ab\\'' +\n    'cdef';";

        string result = HtmlFormatter.FormatJavaScript(js, opts);
        TestHelpers.EqualIgnoringLineEndings(expected, result);
    }

    [Fact]
    /// <summary>
    /// Formats minified CSS text.
    /// </summary>
    public void FormatCss_FormatsMinifiedCss() {
        const string content = ".tabsWrapper{text-align:center;margin:10px auto;font-family:\"Roboto\", sans-serif!important}.tabs{margin-top:10px;font-size:15px;padding:0;list-style:none;background:rgba(255, 255, 255, 1);box-shadow:0 5px 20px rgba(0, 0, 0, 0.1);border-radius:4px;position:relative}.tabs .round{border-radius:4px}.tabs a{text-decoration:none;color:rgba(119, 119, 119, 1);text-transform:uppercase;padding:10px 20px;display:inline-block;position:relative;z-index:1;transition-duration:0.6s}.tabs a.active{color:rgba(255, 255, 255, 1)}.tabs a i{margin-right:5px}.tabs .selector{display:none;height:100%;position:absolute;left:0;top:0;right:0;bottom:0;z-index:1;border-radius:4px}.tabs-content{display:none}.tabs-content.active{display:block}";
        string expected = string.Join("\n", new[] {
            ".tabsWrapper { text-align: center; margin: 10px auto; font-family: \"Roboto\", sans-serif !important }",
            ".tabs { margin-top: 10px; font-size: 15px; padding: 0; list-style: none; background: rgba(255, 255, 255, 1); box-shadow: 0 5px 20px rgba(0, 0, 0, 0.1); border-radius: 4px; position: relative }",
            ".tabs .round { border-radius: 4px }",
            ".tabs a { text-decoration: none; color: rgba(119, 119, 119, 1); text-transform: uppercase; padding: 10px 20px; display: inline-block; position: relative; z-index: 1; transition-duration: 0.6s }",
            ".tabs a.active { color: rgba(255, 255, 255, 1) }",
            ".tabs a i { margin-right: 5px }",
            ".tabs .selector { display: none; height: 100%; position: absolute; left: 0; top: 0; right: 0; bottom: 0; z-index: 1; border-radius: 4px }",
            ".tabs-content { display: none }",
            ".tabs-content.active { display: block }"
        });

        string result = HtmlFormatter.FormatCss(content);
        TestHelpers.EqualIgnoringLineEndings(expected, result);
    }

    [Fact]
    /// <summary>
    /// Formats minified HTML markup.
    /// </summary>
    public void FormatHtml_FormatsMinifiedHtml() {
        const string input = "<html><body><div><p>Test</p></div></body></html>";
        const string expected = "<html>\n    <body>\n        <div>\n            <p>Test</p>\n        </div>\n    </body>\n</html>";

        string result = HtmlFormatter.FormatHtml(input);
        TestHelpers.EqualIgnoringLineEndings(expected, result);
    }
}
