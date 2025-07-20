using HtmlTinkerX;
using System;
using System.IO;
using Xunit;

namespace HtmlTinkerX.Tests;

/// <summary>
/// Tests synchronous optimization methods on files.
/// </summary>
public class HtmlOptimizerFileTests {
    [Fact]
    /// <summary>
    /// Minifies CSS content read from a file.
    /// </summary>
    public void OptimizeCssFile_MinifiesContent() {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".css");
        File.WriteAllText(path, "body { color: red; }");
        try {
            string result = HtmlOptimizer.OptimizeCssFile(path);
            Assert.Equal("body{color:#f00}", result);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    /// <summary>
    /// Minifies HTML markup read from a file.
    /// </summary>
    public void OptimizeHtmlFile_MinifiesContent() {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".html");
        File.WriteAllText(path, "<html><!--c--><body> <p>Hi</p></body></html>");
        try {
            string result = HtmlOptimizer.OptimizeHtmlFile(path, false);
            Assert.Equal("<html><body><p>Hi</p></body></html>", result);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    /// <summary>
    /// Minifies JavaScript content read from a file.
    /// </summary>
    public void OptimizeJavaScriptFile_MinifiesInput() {
        const string formatted = "(function() {\n    function main() {\n        var tabButtons = [].slice.call(document.querySelectorAll('ul.tab-nav li a.buttonTab'));\n        tabButtons.map(function(button) {\n            button.addEventListener('click', function() {\n                document.querySelector('li a.active.buttonTab').classList.remove('active');\n                button.classList.add('active');\n                document.querySelector('.tab-pane.active').classList.remove('active');\n                document.querySelector(button.getAttribute('href')).classList.add('active');\n            });\n        });\n    }\n    if (document.readyState !== 'loading') {\n        main();\n    } else {\n        document.addEventListener('DOMContentLoaded', main);\n    }\n})();";
        const string expected = "(function(){function n(){var n=[].slice.call(document.querySelectorAll(\"ul.tab-nav li a.buttonTab\"));n.map(function(n){n.addEventListener(\"click\",function(){document.querySelector(\"li a.active.buttonTab\").classList.remove(\"active\");n.classList.add(\"active\");document.querySelector(\".tab-pane.active\").classList.remove(\"active\");document.querySelector(n.getAttribute(\"href\")).classList.add(\"active\")})})}document.readyState!==\"loading\"?n():document.addEventListener(\"DOMContentLoaded\",n)})()";
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".js");
        File.WriteAllText(path, formatted);
        try {
            string result = HtmlOptimizer.OptimizeJavaScriptFile(path);
            Assert.Equal(expected, result);
        } finally {
            File.Delete(path);
        }
    }
}