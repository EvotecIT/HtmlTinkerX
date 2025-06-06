using PSParseHTML;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlFormatterTests {
    [Fact]
    public void FormatJavaScript_ReturnsFormattedScript() {
        const string js =
            "(function(){function main(){var tabButtons = [].slice.call(document.querySelectorAll(\"ul.tab-nav li a.buttonTab\"));tabButtons.map(function(button){button.addEventListener(\"click\",function(){document .querySelector(\"li a.active.buttonTab\") .classList.remove(\"active\");button.classList.add(\"active\");document .querySelector(\".tab-pane.active\") .classList.remove(\"active\");document .querySelector(button.getAttribute(\"href\")) .classList.add(\"active\")})})}if(document.readyState!== \"loading\"){main()}else{document.addEventListener(\"DOMContentLoaded\",main)}})();";
        const string expected =
            "(function() {\n    function main() {\n        var tabButtons = [].slice.call(document.querySelectorAll(\"ul.tab-nav li a.buttonTab\"));\n        tabButtons.map(function(button) {\n            button.addEventListener(\"click\", function() {\n                document.querySelector(\"li a.active.buttonTab\").classList.remove(\"active\");\n                button.classList.add(\"active\");\n                document.querySelector(\".tab-pane.active\").classList.remove(\"active\");\n                document.querySelector(button.getAttribute(\"href\")).classList.add(\"active\")\n            })\n        })\n    }\n    if (document.readyState !== \"loading\") {\n        main()\n    } else {\n        document.addEventListener(\"DOMContentLoaded\", main)\n    }\n})();";

        string result = HtmlFormatter.FormatJavaScript(js);
        Assert.Equal(expected.Replace("\r\n", "\n"), result.Replace("\r\n", "\n"));
    }

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
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatHtml_FormatsMinifiedHtml() {
        const string input = "<html><body><div><p>Test</p></div></body></html>";
        const string expected = "<html>\n    <body>\n        <div>\n            <p>Test</p>\n        </div>\n    </body>\n</html>";

        string result = HtmlFormatter.FormatHtml(input);
        Assert.Equal(expected.Replace("\r\n", "\n"), result.Replace("\r\n", "\n"));
    }
}
