using HtmlTinkerX.JavaScriptBeautifier;
using Xunit;

namespace HtmlTinkerX.Tests;

/// <summary>
/// Tests for JavaScript beautifier core functionality.
/// </summary>
public class JavaScriptBeautifierTests {
    private readonly Beautifier _beautifier;

    public JavaScriptBeautifierTests() {
        _beautifier = new Beautifier();
        _beautifier.Opts.IndentSize = 4;
        _beautifier.Opts.IndentChar = ' ';
        _beautifier.Opts.PreserveNewlines = true;
        _beautifier.Opts.JslintHappy = false;
        _beautifier.Opts.KeepArrayIndentation = false;
        _beautifier.Opts.BraceStyle = BraceStyle.Collapse;
        _beautifier.Opts.BreakChainedMethods = false;
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("return .5", "return .5")]
    [InlineData("    return .5", "    return .5")]
    [InlineData("a        =          1", "a = 1")]
    [InlineData("a=1", "a = 1")]
    [InlineData("var a = 1 var b = 2", "var a = 1\nvar b = 2")]
    [InlineData("var a=1, b=c[d], e=6;", "var a = 1,\n    b = c[d],\n    e = 6;")]
    [InlineData("a = \" 12345 \"", "a = \" 12345 \"")]
    [InlineData("a = ' 12345 '", "a = ' 12345 '")]
    [InlineData("if (a == 1) b = 2;", "if (a == 1) b = 2;")]
    [InlineData("if(1){2}else{3}", "if (1) {\n    2\n} else {\n    3\n}")]
    [InlineData("if(1||2);", "if (1 || 2);")]
    [InlineData("(a==1)||(b==2)", "(a == 1) || (b == 2)")]
    public void TestBasicFormatting(string input, string expected) {
        string result = _beautifier.Beautify(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("\"\\\\s\"", "\"\\\\s\"")]
    [InlineData("'\\\\s'", "'\\\\s'")]
    [InlineData("'\\\\\\s'", "'\\\\\\s'")]
    [InlineData("'\\s'", "'\\s'")]
    [InlineData("\"•\"", "\"•\"")]
    [InlineData("\"-\"", "\"-\"")]
    [InlineData("\"\\x41\\x42\\x43\\x01\"", "\"\\x41\\x42\\x43\\x01\"")]
    [InlineData("\"\\u2022\"", "\"\\u2022\"")]
    [InlineData(@"a = /\s+/", @"a = /\s+/")]
    public void TestStringAndRegexHandling(string input, string expected) {
        string result = _beautifier.Beautify(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TestUnescapeHexSequences() {
        var beautifier = new Beautifier();
        beautifier.Opts.UnescapeStrings = true;

        string result = beautifier.Beautify("'\\x41\\u0042'");

        Assert.Equal("'AB'", result);
    }

    [Theory]
    [InlineData("function test(){var x=1;if(x>0){return x;}}",
                "function test() {\n    var x = 1;\n    if (x > 0) {\n        return x;\n    }\n}")]
    [InlineData("try{a();}catch(b){c();}finally{d();}",
                "try {\n    a();\n} catch (b) {\n    c();\n} finally {\n    d();\n}")]
    [InlineData("switch(x) {case 0: case 1: a(); break; default: break}",
                "switch (x) {\n    case 0:\n    case 1:\n        a();\n        break;\n    default:\n        break\n}")]
    public void TestComplexStructures(string input, string expected) {
        string result = _beautifier.Beautify(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TestBraceStyleExpand() {
        var beautifier = new Beautifier();
        beautifier.Opts.BraceStyle = BraceStyle.Expand;

        string result = beautifier.Beautify("if(1){2}else{3}");

        Assert.Equal("if (1)\n{\n    2\n}\nelse\n{\n    3\n}", result);
    }

    [Fact]
    public void TestBraceStyleEndExpand() {
        var beautifier = new Beautifier();
        beautifier.Opts.BraceStyle = BraceStyle.EndExpand;

        string result = beautifier.Beautify("if(1){2}else{3}");

        Assert.Equal("if (1) {\n    2\n}\nelse {\n    3\n}", result);
    }

    [Fact]
    public void TestCustomIndentSize() {
        var beautifier = new Beautifier();
        beautifier.Opts.IndentSize = 2;

        string result = beautifier.Beautify("if(1){2}");

        Assert.Equal("if (1) {\n  2\n}", result);
    }

    [Fact]
    public void TestTabIndentation() {
        var beautifier = new Beautifier();
        beautifier.Opts.IndentWithTabs = true;

        string result = beautifier.Beautify("if(1){2}");

        Assert.Equal("if (1) {\n\t2\n}", result);
    }

    [Theory]
    [InlineData("a = [1, 2, 3, 4]", "a = [1, 2, 3, 4]")]
    [InlineData("a={1:[-1],2:[+1]}", "a = {\n    1: [-1],\n    2: [+1]\n}")]
    [InlineData("var l = {'a':'1', 'b':'2'}", "var l = {\n    'a': '1',\n    'b': '2'\n}")]
    [InlineData("o = [{a:b},{c:d}]", "o = [{\n    a: b\n}, {\n    c: d\n}]")]
    public void TestObjectAndArrayFormatting(string input, string expected) {
        string result = _beautifier.Beautify(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("a;/*comment*/b;", "a; /*comment*/\nb;")]
    [InlineData("a;/* comment */b;", "a; /* comment */\nb;")]
    [InlineData("a;/*\ncomment\n*/b;", "a;\n/*\ncomment\n*/\nb;")]
    [InlineData("a = 1;// comment", "a = 1; // comment")]
    [InlineData("a = 1; // comment", "a = 1; // comment")]
    public void TestCommentHandling(string input, string expected) {
        string result = _beautifier.Beautify(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("a = 1e10", "a = 1e10")]
    [InlineData("a = 1.3e10", "a = 1.3e10")]
    [InlineData("a = 1.3e-10", "a = 1.3e-10")]
    [InlineData("a = -1.3e-10", "a = -1.3e-10")]
    [InlineData("a = 1e-10", "a = 1e-10")]
    [InlineData("a = e - 10", "a = e - 10")]
    public void TestScientificNotation(string input, string expected) {
        string result = _beautifier.Beautify(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TestJslintHappyMode() {
        var beautifier = new Beautifier();
        beautifier.Opts.JslintHappy = true;

        string result = beautifier.Beautify("function(){}");

        Assert.Equal("function () {}", result);
    }

    [Fact]
    public void TestKeepArrayIndentation() {
        var beautifier = new Beautifier();
        beautifier.Opts.KeepArrayIndentation = true;

        string result = beautifier.Beautify("a = ['a', 'b', 'c',\n    'd', 'e', 'f']");

        Assert.Equal("a = ['a', 'b', 'c',\n    'd', 'e', 'f']", result);
    }

    [Fact]
    public void TestPreserveNewlines() {
        var beautifier = new Beautifier();
        beautifier.Opts.PreserveNewlines = true;

        string result = beautifier.Beautify("var\na=do_preserve_newlines;");

        Assert.Equal("var\na = do_preserve_newlines;", result);
    }

    [Fact]
    public void TestDisablePreserveNewlines() {
        var beautifier = new Beautifier();
        beautifier.Opts.PreserveNewlines = false;

        string result = beautifier.Beautify("var\na=dont_preserve_newlines;");

        Assert.Equal("var a = dont_preserve_newlines;", result);
    }
}