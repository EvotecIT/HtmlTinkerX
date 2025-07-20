using HtmlTinkerX.JavaScriptBeautifier;
using Xunit;

namespace PSParseHTML.Tests;

/// <summary>
/// Tests for JavaScript beautifier indentation options.
/// </summary>
public class JavaScriptBeautifierIndentationTests {
    [Fact]
    public void TestTabs() {
        var beautifier = new Beautifier();
        beautifier.Opts.IndentWithTabs = true;

        string result = beautifier.Beautify("{tabs()}");

        Assert.Equal("{\n\ttabs()\n}", result);
    }

    [Fact]
    public void TestFunctionIndent() {
        var beautifier = new Beautifier();
        beautifier.Opts.IndentWithTabs = true;
        beautifier.Opts.KeepArrayIndentation = true;

        string result = beautifier.Beautify("var foo = function(){ bar() }();");

        Assert.Equal("var foo = function() {\n\tbar()\n}();", result);
    }
}