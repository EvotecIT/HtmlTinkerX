using HtmlTinkerX;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlOptimizerTests {
    [Fact]
    public void OptimizeCss_MinifiesContent() {
        const string css = "body { color: red; }";
        string result = HtmlOptimizer.OptimizeCss(css);
        Assert.Equal("body{color:#f00}", result);
    }

    [Fact]
    public void OptimizeHtml_MinifiesContent() {
        const string input = "<html><!--c--><body> <p>Hi</p></body></html>";
        string result = HtmlOptimizer.OptimizeHtml(input, false);
        Assert.Equal("<html><body><p>Hi</p></body></html>", result);
    }

    [Fact]
    public void OptimizeHtml_PreservesMotw() {
        const string input = "<!-- saved from url=(0014)about:internet --><html><body>test</body></html>";
        string result = HtmlOptimizer.OptimizeHtml(input, false);
        Assert.StartsWith("<!-- saved from url=(0014)about:internet -->", result);
    }

    [Fact]
    public void OptimizeJavaScript_MinifiesInput() {
        const string formatted = "(function() {\n    function main() {\n        var tabButtons = [].slice.call(document.querySelectorAll('ul.tab-nav li a.buttonTab'));\n        tabButtons.map(function(button) {\n            button.addEventListener('click', function() {\n                document.querySelector('li a.active.buttonTab').classList.remove('active');\n                button.classList.add('active');\n                document.querySelector('.tab-pane.active').classList.remove('active');\n                document.querySelector(button.getAttribute('href')).classList.add('active');\n            });\n        });\n    }\n    if (document.readyState !== 'loading') {\n        main();\n    } else {\n        document.addEventListener('DOMContentLoaded', main);\n    }\n})();";

        const string expected = "(function(){function n(){var n=[].slice.call(document.querySelectorAll(\"ul.tab-nav li a.buttonTab\"));n.map(function(n){n.addEventListener(\"click\",function(){document.querySelector(\"li a.active.buttonTab\").classList.remove(\"active\");n.classList.add(\"active\");document.querySelector(\".tab-pane.active\").classList.remove(\"active\");document.querySelector(n.getAttribute(\"href\")).classList.add(\"active\")})})}document.readyState!==\"loading\"?n():document.addEventListener(\"DOMContentLoaded\",n)})()";

        string result = HtmlOptimizer.OptimizeJavaScript(formatted);
        Assert.Equal(expected, result);
    }
}
