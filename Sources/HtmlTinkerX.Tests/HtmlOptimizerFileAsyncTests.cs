using HtmlTinkerX;

namespace HtmlTinkerX.Tests;

/// <summary>
/// Tests asynchronous optimization routines on files.
/// </summary>
public class HtmlOptimizerFileAsyncTests {
    [Fact]
    /// <summary>
    /// Ensures that CSS files are minified asynchronously.
    /// </summary>
    public async Task OptimizeCssFileAsync_MinifiesContent() {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".css");
#if FRAMEWORK
        await WriteAllTextAsync(path, "body { color: red; }");
#else
        await File.WriteAllTextAsync(path, "body { color: red; }");
#endif
        try {
            string result = await HtmlOptimizer.OptimizeCssFileAsync(path);
            Assert.Equal("body{color:#f00}", result);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    /// <summary>
    /// Asynchronously minifies HTML files.
    /// </summary>
    public async Task OptimizeHtmlFileAsync_MinifiesContent() {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".html");
#if FRAMEWORK
        await WriteAllTextAsync(path, "<html><!--c--><body> <p>Hi</p></body></html>");
#else
        await File.WriteAllTextAsync(path, "<html><!--c--><body> <p>Hi</p></body></html>");
#endif
        try {
            string result = await HtmlOptimizer.OptimizeHtmlFileAsync(path, false, treatAsDocument: true, removeComments: true);
            Assert.Equal("<html><body><p>Hi</p></body></html>", result);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    /// <summary>
    /// Asynchronously minifies JavaScript files.
    /// </summary>
    public async Task OptimizeJavaScriptFileAsync_MinifiesInput() {
        const string formatted = "(function() {\n    function main() {\n        var tabButtons = [].slice.call(document.querySelectorAll('ul.tab-nav li a.buttonTab'));\n        tabButtons.map(function(button) {\n            button.addEventListener('click', function() {\n                document.querySelector('li a.active.buttonTab').classList.remove('active');\n                button.classList.add('active');\n                document.querySelector('.tab-pane.active').classList.remove('active');\n                document.querySelector(button.getAttribute('href')).classList.add('active');\n            });\n        });\n    }\n    if (document.readyState !== 'loading') {\n        main();\n    } else {\n        document.addEventListener('DOMContentLoaded', main);\n    }\n})();";
        const string expected = "(function(){function n(){var n=[].slice.call(document.querySelectorAll(\"ul.tab-nav li a.buttonTab\"));n.map(function(n){n.addEventListener(\"click\",function(){document.querySelector(\"li a.active.buttonTab\").classList.remove(\"active\");n.classList.add(\"active\");document.querySelector(\".tab-pane.active\").classList.remove(\"active\");document.querySelector(n.getAttribute(\"href\")).classList.add(\"active\")})})}document.readyState!==\"loading\"?n():document.addEventListener(\"DOMContentLoaded\",n)})()";
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".js");
#if FRAMEWORK
        await WriteAllTextAsync(path, formatted);
#else
        await File.WriteAllTextAsync(path, formatted);
#endif
        try {
            string result = await HtmlOptimizer.OptimizeJavaScriptFileAsync(path);
            Assert.Equal(expected, result);
        } finally {
            File.Delete(path);
        }
    }
}