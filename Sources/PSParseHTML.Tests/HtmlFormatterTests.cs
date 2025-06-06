using PSParseHTML;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlFormatterTests {
    [Fact]
    public void FormatJavaScript_ReturnsFormattedScript() {
        const string js = "(function(){function main(){var tabButtons = [].slice.call(document.querySelectorAll(\"ul.tab-nav li a.buttonTab\"));tabButtons.map(function(button){button.addEventListener(\"click\",function(){document .querySelector(\"li a.active.buttonTab\") .classList.remove(\"active\");button.classList.add(\"active\");document .querySelector(\".tab-pane.active\") .classList.remove(\"active\");document .querySelector(button.getAttribute(\"href\")) .classList.add(\"active\")})})}if(document.readyState!== \"loading\"){main()}else{document.addEventListener(\"DOMContentLoaded\",main)}})();";
        const string expected = "(function() {\n    function main() {\n        var tabButtons = [].slice.call(document.querySelectorAll(\"ul.tab-nav li a.buttonTab\"));\n        tabButtons.map(function(button) {\n            button.addEventListener(\"click\", function() {\n                document.querySelector(\"li a.active.buttonTab\").classList.remove(\"active\");\n                button.classList.add(\"active\");\n                document.querySelector(\".tab-pane.active\").classList.remove(\"active\");\n                document.querySelector(button.getAttribute(\"href\")).classList.add(\"active\")\n            })\n        })\n    }\n    if (document.readyState !== \"loading\") {\n        main()\n    } else {\n        document.addEventListener(\"DOMContentLoaded\", main)\n    }\n})();";

        string result = HtmlFormatter.FormatJavaScript(js);
        Assert.Equal(expected.Replace("\r\n", "\n"), result.Replace("\r\n", "\n"));
    }
}
