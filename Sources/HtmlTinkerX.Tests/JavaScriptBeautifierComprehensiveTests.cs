using HtmlTinkerX.JavaScriptBeautifier;
using Xunit;

namespace HtmlTinkerX.Tests;

/// <summary>
/// Comprehensive tests for JavaScript beautifier covering all major functionality.
/// Converted from the original 700+ line NUnit test suite.
/// </summary>
public class JavaScriptBeautifierComprehensiveTests {
    private Beautifier CreateBeautifier() {
        var beautifier = new Beautifier();
        beautifier.Opts.IndentSize = 4;
        beautifier.Opts.IndentChar = ' ';
        beautifier.Opts.PreserveNewlines = true;
        beautifier.Opts.JslintHappy = false;
        beautifier.Opts.KeepArrayIndentation = false;
        beautifier.Opts.BraceStyle = BraceStyle.Collapse;
        beautifier.Opts.BreakChainedMethods = false;
        return beautifier;
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
    public void TestStringAndRegexEscaping(string input, string expected) {
        var beautifier = CreateBeautifier();
        string result = beautifier.Beautify(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("return .5", "return .5")]
    [InlineData("    return .5", "    return .5")]
    [InlineData("a        =          1", "a = 1")]
    [InlineData("a=1", "a = 1")]
    [InlineData("a();\n\nb();", "a();\n\nb();")]
    [InlineData("var a = 1 var b = 2", "var a = 1\nvar b = 2")]
    [InlineData("var a=1, b=c[d], e=6;", "var a = 1,\n    b = c[d],\n    e = 6;")]
    [InlineData("a = \" 12345 \"", "a = \" 12345 \"")]
    [InlineData("a = ' 12345 '", "a = ' 12345 '")]
    public void TestBasicFormatting(string input, string expected) {
        var beautifier = CreateBeautifier();
        string result = beautifier.Beautify(input);
        Assert.Equal(expected, result);
    }
 [Theory]
    [InlineData("if (a == 1) b = 2;", "if (a == 1) b = 2;")]
    [InlineData("if(1){2}else{3}", "if (1) {\n    2\n} else {\n    3\n}")]
    [InlineData("if (foo) bar();\nelse\ncar();", "if (foo) bar();\nelse car();")]
    [InlineData("if(1||2);", "if (1 || 2);")]
    [InlineData("(a==1)||(b==2)", "(a == 1) || (b == 2)")]
    [InlineData("var a = 1 if (2) 3;", "var a = 1\nif (2) 3;")]
    [InlineData("if(a)break;", "if (a) break;")]
    [InlineData("if(a){break}", "if (a) {\n    break\n}")]
    [InlineData("if((a))foo();", "if ((a)) foo();")]
    [InlineData("if(!a)foo();", "if (!a) foo();")]
    public void TestConditionalStatements(string input, string expected) {
        var beautifier = CreateBeautifier();
        string result = beautifier.Beautify(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("for(var i=0;;)", "for (var i = 0;;)")]
    [InlineData("for(;;i++)", "for (;; i++)")]
    [InlineData("for(;;++i)", "for (;; ++i)")]
    [InlineData("for(var a=1,b=2)", "for (var a = 1, b = 2)")]
    [InlineData("for(var a=1,b=2,c=3)", "for (var a = 1, b = 2, c = 3)")]
    [InlineData("for(var a=1,b=2,c=3;d<3;d++)", "for (var a = 1, b = 2, c = 3; d < 3; d++)")]
    [InlineData("for (; s-->0;)", "for (; s-- > 0;)")]
    [InlineData("for (; s++>0;)", "for (; s++ > 0;)")]
    public void TestLoopStatements(string input, string expected) {
        var beautifier = CreateBeautifier();
        string result = beautifier.Beautify(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("a = a + 1", "a = a + 1")]
    [InlineData("a = a == 1", "a = a == 1")]
    [InlineData("a /= 5", "a /= 5")]
    [InlineData("a = 0.5 * 3", "a = 0.5 * 3")]
    [InlineData("a *= 10.55", "a *= 10.55")]
    [InlineData("a < .5", "a < .5")]
    [InlineData("a <= .5", "a <= .5")]
    [InlineData("a<.5", "a < .5")]
    [InlineData("a<=.5", "a <= .5")]
    [InlineData("a=!b", "a = !b")]
    [InlineData("a = ~a", "a = ~a")]
    [InlineData("a !== b", "a !== b")]
    [InlineData("x != -1", "x != -1")]
    public void TestOperators(string input, string expected) {
        var beautifier = CreateBeautifier();
        string result = beautifier.Beautify(input);
        Assert.Equal(expected, result);
    }
  [Theory]
    [InlineData("a = 0xff;", "a = 0xff;")]
    [InlineData("a=0xff+4", "a = 0xff + 4")]
    [InlineData("a = 1e10", "a = 1e10")]
    [InlineData("a = 1.3e10", "a = 1.3e10")]
    [InlineData("a = 1.3e-10", "a = 1.3e-10")]
    [InlineData("a = -1.3e-10", "a = -1.3e-10")]
    [InlineData("a = 1e-10", "a = 1e-10")]
    [InlineData("a = e - 10", "a = e - 10")]
    [InlineData("a = 11-10", "a = 11 - 10")]
    [InlineData("a = 1e+2", "a = 1e+2")]
    [InlineData("a = 1e-2", "a = 1e-2")]
    public void TestNumericLiterals(string input, string expected) {
        var beautifier = CreateBeautifier();
        string result = beautifier.Beautify(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("a = [1, 2, 3, 4]", "a = [1, 2, 3, 4]")]
    [InlineData("a={1:[-1],2:[+1]}", "a = {\n    1: [-1],\n    2: [+1]\n}")]
    [InlineData("var l = {'a':'1', 'b':'2'}", "var l = {\n    'a': '1',\n    'b': '2'\n}")]
    [InlineData("{a:1, b:2}", "{\n    a: 1,\n    b: 2\n}")]
    [InlineData("o = [{a:b},{c:d}]", "o = [{\n    a: b\n}, {\n    c: d\n}]")]
    [InlineData("a = [-1, -1, -1]", "a = [-1, -1, -1]")]
    [InlineData("a=[[1,2],[4,5],[7,8]]", "a = [\n    [1, 2],\n    [4, 5],\n    [7, 8]\n]")]
    [InlineData("a=[a[1],b[4],c[d[7]]]", "a = [a[1], b[4], c[d[7]]]")]
    [InlineData("[1,2,[3,4,[5,6],7],8]", "[1, 2, [3, 4, [5, 6], 7], 8]")]
    public void TestArraysAndObjects(string input, string expected) {
        var beautifier = CreateBeautifier();
        string result = beautifier.Beautify(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("F*(g/=f)*g+b", "F * (g /= f) * g + b")]
    [InlineData("a.b({c:d})", "a.b({\n    c: d\n})")]
    [InlineData("a.b\n(\n{\nc:\nd\n}\n)", "a.b({\n    c: d\n})")]
    [InlineData("settings = $.extend({},defaults,settings);", "settings = $.extend({}, defaults, settings);")]
    [InlineData("(xx)()", "(xx)()")]
    [InlineData("a[1]()", "a[1]()")]
    public void TestFunctionCalls(string input, string expected) {
        var beautifier = CreateBeautifier();
        string result = beautifier.Beautify(input);
        Assert.Equal(expected, result);
    }
   [Theory]
    [InlineData("a?b:c", "a ? b : c")]
    [InlineData("a?1:2", "a ? 1 : 2")]
    [InlineData("a?(b):c", "a ? (b) : c")]
    [InlineData("x={a:1,b:w==\"foo\"?x:y,c:z}", "x = {\n    a: 1,\n    b: w == \"foo\" ? x : y,\n    c: z\n}")]
    [InlineData("x=a?b?c?d:e:f:g;", "x = a ? b ? c ? d : e : f : g;")]
    [InlineData("x=a?b?c?d:{e1:1,e2:2}:f:g;", "x = a ? b ? c ? d : {\n    e1: 1,\n    e2: 2\n} : f : g;")]
    [InlineData("a = s++>s--;", "a = s++ > s--;")]
    [InlineData("a = s++>--s;", "a = s++ > --s;")]
    public void TestTernaryOperators(string input, string expected) {
        var beautifier = CreateBeautifier();
        string result = beautifier.Beautify(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("a;/*comment*/b;", "a; /*comment*/\nb;")]
    [InlineData("a;/* comment */b;", "a; /* comment */\nb;")]
    [InlineData("a;/*\ncomment\n*/b;", "a;\n/*\ncomment\n*/\nb;")]
    [InlineData("a;/**\n* javadoc\n*/b;", "a;\n/**\n * javadoc\n */\nb;")]
    [InlineData("a;/**\n\nno javadoc\n*/b;", "a;\n/**\n\nno javadoc\n*/\nb;")]
    [InlineData("a;/*\n* javadoc\n*/b;", "a;\n/*\n * javadoc\n */\nb;")]
    [InlineData("a = 1;// comment", "a = 1; // comment")]
    [InlineData("a = 1; // comment", "a = 1; // comment")]
    [InlineData("a = 1;\n // comment", "a = 1;\n// comment")]
    [InlineData("// comment\n(function something() {})", "// comment\n(function something() {})")]
    public void TestComments(string input, string expected) {
        var beautifier = CreateBeautifier();
        string result = beautifier.Beautify(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("return(1)", "return (1)")]
    [InlineData("return ++i", "return ++i")]
    [InlineData("return !!x", "return !!x")]
    [InlineData("return !x", "return !x")]
    [InlineData("return [1,2]", "return [1, 2]")]
    [InlineData("return;", "return;")]
    [InlineData("return\nfunc", "return\nfunc")]
    [InlineData("return 45", "return 45")]
    public void TestReturnStatements(string input, string expected) {
        var beautifier = CreateBeautifier();
        string result = beautifier.Beautify(input);
        Assert.Equal(expected, result);
    }
   [Theory]
    [InlineData("try{a();}catch(b){c();}finally{d();}", "try {\n    a();\n} catch (b) {\n    c();\n} finally {\n    d();\n}")]
    [InlineData("catch(e)", "catch (e)")]
    [InlineData("switch(x) {case 0: case 1: a(); break; default: break}", "switch (x) {\n    case 0:\n    case 1:\n        a();\n        break;\n    default:\n        break\n}")]
    [InlineData("switch(x){case -1:break;case !y:break;}", "switch (x) {\n    case -1:\n        break;\n    case !y:\n        break;\n}")]
    public void TestExceptionHandlingAndSwitch(string input, string expected) {
        var beautifier = CreateBeautifier();
        string result = beautifier.Beautify(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("function void(void) {}", "function void(void) {}")]
    [InlineData("function x(){(a||b).c()}", "function x() {\n    (a || b).c()\n}")]
    [InlineData("function x(){return - 1}", "function x() {\n    return -1\n}")]
    [InlineData("function x(){return ! a}", "function x() {\n    return !a\n}")]
    [InlineData("function namespace::something()", "function namespace::something()")]
    public void TestFunctions(string input, string expected) {
        var beautifier = CreateBeautifier();
        string result = beautifier.Beautify(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("/12345[^678]*9+/.match(a)", "/12345[^678]*9+/.match(a)")]
    [InlineData("a = /reg/exp", "a = /reg/exp")]
    [InlineData("a = /reg/", "a = /reg/")]
    [InlineData("/abc/.test()", "/abc/.test()")]
    [InlineData("/abc/i.test()", "/abc/i.test()")]
    [InlineData("{/abc/i.test()}", "{\n    /abc/i.test()\n}")]
    [InlineData("var x=(a)/a;", "var x = (a) / a;")]
    [InlineData("do/regexp/;\nwhile(1);", "do /regexp/;\nwhile (1);")]
    [InlineData("a=/regexp", "a = /regexp")]
    public void TestRegularExpressions(string input, string expected) {
        var beautifier = CreateBeautifier();
        string result = beautifier.Beautify(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("{}", "{}")]
    [InlineData("{\n\n}", "{\n\n}")]
    [InlineData("{\n\n    x();\n\n}", "{\n\n    x();\n\n}")]
    [InlineData("{xxx;}()", "{\n    xxx;\n}()")]
    [InlineData("{{}/z/}", "{\n    {}\n    /z/\n}")]
    [InlineData("}}}", "}\n}\n}")]
    public void TestBlockStatements(string input, string expected) {
        var beautifier = CreateBeautifier();
        string result = beautifier.Beautify(input);
        Assert.Equal(expected, result);
    }
   [Theory]
    [InlineData("do { a(); } while ( 1 );", "do {\n    a();\n} while (1);")]
    [InlineData("do {} while (1);", "do {} while (1);")]
    [InlineData("do {\n} while (1);", "do {} while (1);")]
    [InlineData("do {\n\n} while (1);", "do {\n\n} while (1);")]
    [InlineData("do{x()}while(a>1)", "do {\n    x()\n} while (a > 1)")]
    public void TestDoWhileLoops(string input, string expected) {
        var beautifier = CreateBeautifier();
        string result = beautifier.Beautify(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("var a, b", "var a, b")]
    [InlineData("var a=1,b=2,c=3", "var a = 1,\n    b = 2,\n    c = 3")]
    [InlineData("var a,b,c=1,d,e,f=2;", "var a, b, c = 1,\n    d, e, f = 2;")]
    [InlineData("var a,b,c=[],d,e,f=2;", "var a, b, c = [],\n    d, e, f = 2;")]
    [InlineData("var a = a,\na;\nb = {\nb\n}", "var a = a,\n    a;\nb = {\n    b\n}")]
    public void TestVariableDeclarations(string input, string expected) {
        var beautifier = CreateBeautifier();
        string result = beautifier.Beautify(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("a++;", "a++;")]
    [InlineData("{--bar;}", "{\n    --bar;\n}")]
    [InlineData("{++bar;}", "{\n    ++bar;\n}")]
    [InlineData("{foo();--bar;}", "{\n    foo();\n    --bar;\n}")]
    [InlineData("{foo();++bar;}", "{\n    foo();\n    ++bar;\n}")]
    public void TestIncrementDecrement(string input, string expected) {
        var beautifier = CreateBeautifier();
        string result = beautifier.Beautify(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TestBraceStyleExpand() {
        var beautifier = CreateBeautifier();
        beautifier.Opts.BraceStyle = BraceStyle.Expand;

        string result = beautifier.Beautify("if(1){2}else{3}");

        Assert.Equal("if (1)\n{\n    2\n}\nelse\n{\n    3\n}", result);
    }

    [Fact]
    public void TestBraceStyleEndExpand() {
        var beautifier = CreateBeautifier();
        beautifier.Opts.BraceStyle = BraceStyle.EndExpand;

        string result = beautifier.Beautify("if(1){2}else{3}");

        Assert.Equal("if (1) {\n    2\n}\nelse {\n    3\n}", result);
    }
 [Fact]
    public void TestJslintHappyMode() {
        var beautifier = CreateBeautifier();
        beautifier.Opts.JslintHappy = true;

        string result1 = beautifier.Beautify("function(){}");
        Assert.Equal("function () {}", result1);

        string result2 = beautifier.Beautify("a=typeof(x)");
        Assert.Equal("a = typeof (x)", result2);
    }

    [Fact]
    public void TestKeepArrayIndentation() {
        var beautifier = CreateBeautifier();
        beautifier.Opts.KeepArrayIndentation = true;

        string result = beautifier.Beautify("a = ['a', 'b', 'c',\n    'd', 'e', 'f']");
        Assert.Equal("a = ['a', 'b', 'c',\n    'd', 'e', 'f']", result);

        string result2 = beautifier.Beautify("a = ['a', 'b', 'c',\n    'd', 'e', 'f',\n        'g', 'h', 'i']");
        Assert.Equal("a = ['a', 'b', 'c',\n    'd', 'e', 'f',\n        'g', 'h', 'i']", result2);
    }

    [Fact]
    public void TestPreserveNewlines() {
        var beautifier = CreateBeautifier();
        beautifier.Opts.PreserveNewlines = true;

        string result = beautifier.Beautify("var\na=do_preserve_newlines;");
        Assert.Equal("var\na = do_preserve_newlines;", result);

        beautifier.Opts.PreserveNewlines = false;
        string result2 = beautifier.Beautify("var\na=dont_preserve_newlines;");
        Assert.Equal("var a = dont_preserve_newlines;", result2);
    }

    [Fact]
    public void TestCustomIndentSize() {
        var beautifier = CreateBeautifier();
        beautifier.Opts.IndentSize = 1;

        string result = beautifier.Beautify("{ one_char() }");
        Assert.Equal("{\n one_char()\n}", result);

        string result2 = beautifier.Beautify("var a,b=1,c=2");
        Assert.Equal("var a, b = 1,\n c = 2", result2);
    }

    [Fact]
    public void TestTabIndentation() {
        var beautifier = CreateBeautifier();
        beautifier.Opts.IndentWithTabs = true;

        string result = beautifier.Beautify("{ one_char() }");
        Assert.Equal("{\n\tone_char()\n}", result);

        string result2 = beautifier.Beautify("x = a ? b : c; x;");
        Assert.Equal("x = a ? b : c;\nx;", result2);
    }

    [Theory]
    [InlineData("\"incomplete-string", "\"incomplete-string")]
    [InlineData("'incomplete-string", "'incomplete-string")]
    [InlineData("/incomplete-regex", "/incomplete-regex")]
    [InlineData("{a:#1", "{\n    a: #1")]
    [InlineData("{a:#", "{\n    a: #")]
    public void TestIncompleteTokens(string input, string expected) {
        var beautifier = CreateBeautifier();
        string result = beautifier.Beautify(input);
        Assert.Equal(expected, result);
    }
}