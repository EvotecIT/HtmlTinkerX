#region License
// MIT License
//
// Copyright (c) 2018 Denis Ivanov
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
#endregion

using System.Collections;
using System.Runtime.InteropServices;

namespace Tests {
    using Jsbeautifier;
    using NUnit.Framework;

    [TestFixture]
    public class TestJSBeautifier {
        private Beautifier beautifier;

        public TestJSBeautifier() {
            beautifier = new Beautifier();

            beautifier.Opts.IndentSize = 4;
            beautifier.Opts.IndentChar = ' ';
            beautifier.Opts.PreserveNewlines = true;
            beautifier.Opts.JslintHappy = false;
            beautifier.Opts.KeepArrayIndentation = false;
            beautifier.Opts.BraceStyle = BraceStyle.Collapse;
            beautifier.Flags.IndentationLevel = 0;
            beautifier.Opts.BreakChainedMethods = false;
        }

        [TestCase("\"\\\\s\"", ExpectedResult = "\"\\\\s\"")]
        [TestCase("\'\\\\s\'", ExpectedResult = "\'\\\\s\'")]
        [TestCase("'\\\\\\s'", ExpectedResult = "'\\\\\\s'")]
        [TestCase("'\\s'", ExpectedResult = "'\\s'")]
        [TestCase("\"•\"", ExpectedResult = "\"•\"")]
        [TestCase("\"-\"", ExpectedResult = "\"-\"")]
        [TestCase("\"\\x41\\x42\\x43\\x01\"", ExpectedResult = "\"\\x41\\x42\\x43\\x01\"")]
        [TestCase("\"\\u2022\"", ExpectedResult = "\"\\u2022\"")]
        [TestCase(@"a = /\s+/", ExpectedResult = @"a = /\s+/")]
        [TestCase("\"\\u2022\";a = /\\s+/;\"\\x41\\x42\\x43\\x01\".match(/\\x41/);", ExpectedResult = "\"\\u2022\";\na = /\\s+/;\n\"\\x41\\x42\\x43\\x01\".match(/\\x41/);")]
        [TestCase("\"\\x22\\x27\",\'\\x22\\x27\',\"\\x5c\",\'\\x5c\',\"\\xff and \\xzz\",\"unicode \\u0000 \\u0022 \\u0027 \\u005c \\uffff \\uzzzz\"", ExpectedResult = "\"\\x22\\x27\", \'\\x22\\x27\', \"\\x5c\", \'\\x5c\', \"\\xff and \\xzz\", \"unicode \\u0000 \\u0022 \\u0027 \\u005c \\uffff \\uzzzz\"")]
        [TestCase("a = /\\s+/", ExpectedResult = "a = /\\s+/")]
        public string TestUnescape(string code) {
            return beautifier.Beautify(code);
        }

        [Test]
        public void TestBeautifier() {
            Assert.That(beautifier.Beautify(""), Is.EqualTo(""));
            Assert.That(beautifier.Beautify("return .5"), Is.EqualTo("return .5"));
            Assert.That(beautifier.Beautify("    return .5"), Is.EqualTo("    return .5"));
            Assert.That(beautifier.Beautify("a        =          1"), Is.EqualTo("a = 1"));
            Assert.That(beautifier.Beautify("a=1"), Is.EqualTo("a = 1"));
            Assert.That(beautifier.Beautify("a();\n\nb();"), Is.EqualTo("a();\n\nb();"));
            Assert.That(beautifier.Beautify("var a = 1 var b = 2"), Is.EqualTo("var a = 1\nvar b = 2"));
            Assert.That(beautifier.Beautify("var a=1, b=c[d], e=6;"), Is.EqualTo("var a = 1,\n    b = c[d],\n    e = 6;"));
            Assert.That(beautifier.Beautify("a = \" 12345 \""), Is.EqualTo("a = \" 12345 \""));
            Assert.That(beautifier.Beautify("a = ' 12345 '"), Is.EqualTo("a = ' 12345 '"));
            Assert.That(beautifier.Beautify("if (a == 1) b = 2;"), Is.EqualTo("if (a == 1) b = 2;"));
            Assert.That(beautifier.Beautify("if(1){2}else{3}"), Is.EqualTo("if (1) {\n    2\n} else {\n    3\n}"));
            Assert.That(beautifier.Beautify("if (foo) bar();\nelse\ncar();"), Is.EqualTo("if (foo) bar();\nelse car();"));
            Assert.That(beautifier.Beautify("if(1||2);"), Is.EqualTo("if (1 || 2);"));
            Assert.That(beautifier.Beautify("(a==1)||(b==2)"), Is.EqualTo("(a == 1) || (b == 2)"));
            Assert.That(beautifier.Beautify("if(1||2);"), Is.EqualTo("if (1 || 2);"));
            Assert.That(beautifier.Beautify("(a==1)||(b==2)"), Is.EqualTo("(a == 1) || (b == 2)"));
            Assert.That(beautifier.Beautify("var a = 1 if (2) 3;"), Is.EqualTo("var a = 1\nif (2) 3;"));
            Assert.That(beautifier.Beautify("a = a + 1"), Is.EqualTo("a = a + 1"));
            Assert.That(beautifier.Beautify("a = a == 1"), Is.EqualTo("a = a == 1"));
            Assert.That(beautifier.Beautify("/12345[^678]*9+/.match(a)"), Is.EqualTo("/12345[^678]*9+/.match(a)"));
            Assert.That(beautifier.Beautify("a /= 5"), Is.EqualTo("a /= 5"));
            Assert.That(beautifier.Beautify("a = 0.5 * 3"), Is.EqualTo("a = 0.5 * 3"));
            Assert.That(beautifier.Beautify("a *= 10.55"), Is.EqualTo("a *= 10.55"));
            Assert.That(beautifier.Beautify("a < .5"), Is.EqualTo("a < .5"));
            Assert.That(beautifier.Beautify("a <= .5"), Is.EqualTo("a <= .5"));
            Assert.That(beautifier.Beautify("a<.5"), Is.EqualTo("a < .5"));
            Assert.That(beautifier.Beautify("a<=.5"), Is.EqualTo("a <= .5"));
            Assert.That(beautifier.Beautify("a = 0xff;"), Is.EqualTo("a = 0xff;"));
            Assert.That(beautifier.Beautify("a=0xff+4"), Is.EqualTo("a = 0xff + 4"));
            Assert.That(beautifier.Beautify("a = [1, 2, 3, 4]"), Is.EqualTo("a = [1, 2, 3, 4]"));
            Assert.That(beautifier.Beautify("F*(g/=f)*g+b"), Is.EqualTo("F * (g /= f) * g + b"));
            Assert.That(beautifier.Beautify("a.b({c:d})"), Is.EqualTo("a.b({\n    c: d\n})"));
            Assert.That(beautifier.Beautify("a.b\n(\n{\nc:\nd\n}\n)"), Is.EqualTo("a.b({\n    c: d\n})"));
            Assert.That(beautifier.Beautify("a=!b"), Is.EqualTo("a = !b"));
            Assert.That(beautifier.Beautify("a?b:c"), Is.EqualTo("a ? b : c"));
            Assert.That(beautifier.Beautify("a?1:2"), Is.EqualTo("a ? 1 : 2"));
            Assert.That(beautifier.Beautify("a?(b):c"), Is.EqualTo("a ? (b) : c"));
            Assert.That(beautifier.Beautify("x={a:1,b:w==\"foo\"?x:y,c:z}"), Is.EqualTo("x = {\n    a: 1,\n    b: w == \"foo\" ? x : y,\n    c: z\n}"));
            Assert.That(beautifier.Beautify("x=a?b?c?d:e:f:g;"), Is.EqualTo("x = a ? b ? c ? d : e : f : g;"));
            Assert.That(beautifier.Beautify("x=a?b?c?d:{e1:1,e2:2}:f:g;"), Is.EqualTo("x = a ? b ? c ? d : {\n    e1: 1,\n    e2: 2\n} : f : g;"));
            Assert.That(beautifier.Beautify("function void(void) {}"), Is.EqualTo("function void(void) {}"));
            Assert.That(beautifier.Beautify("if(!a)foo();"), Is.EqualTo("if (!a) foo();"));
            Assert.That(beautifier.Beautify("a=~a"), Is.EqualTo("a = ~a"));
            Assert.That(beautifier.Beautify("a;/*comment*/b;"), Is.EqualTo("a; /*comment*/\nb;"));
            Assert.That(beautifier.Beautify("a;/* comment */b;"), Is.EqualTo("a; /* comment */\nb;"));
            Assert.That(beautifier.Beautify("a;/*\ncomment\n*/b;"), Is.EqualTo("a;\n/*\ncomment\n*/\nb;"));
            Assert.That(beautifier.Beautify("a;/**\n* javadoc\n*/b;"), Is.EqualTo("a;\n/**\n * javadoc\n */\nb;"));
            Assert.That(beautifier.Beautify("a;/**\n\nno javadoc\n*/b;"), Is.EqualTo("a;\n/**\n\nno javadoc\n*/\nb;"));
            Assert.That(beautifier.Beautify("a;/*\n* javadoc\n*/b;"), Is.EqualTo("a;\n/*\n * javadoc\n */\nb;"));
            Assert.That(beautifier.Beautify("if(a)break;"), Is.EqualTo("if (a) break;"));
            Assert.That(beautifier.Beautify("if(a){break}"), Is.EqualTo("if (a) {\n    break\n}"));
            Assert.That(beautifier.Beautify("if((a))foo();"), Is.EqualTo("if ((a)) foo();"));
            Assert.That(beautifier.Beautify("for(var i=0;;)"), Is.EqualTo("for (var i = 0;;)"));
            Assert.That(beautifier.Beautify("a++;"), Is.EqualTo("a++;"));
            Assert.That(beautifier.Beautify("for(;;i++)"), Is.EqualTo("for (;; i++)"));
            Assert.That(beautifier.Beautify("for(;;++i)"), Is.EqualTo("for (;; ++i)"));
            Assert.That(beautifier.Beautify("return(1)"), Is.EqualTo("return (1)"));
            Assert.That(beautifier.Beautify("try{a();}catch(b){c();}finally{d();}"), Is.EqualTo("try {\n    a();\n} catch (b) {\n    c();\n} finally {\n    d();\n}"));
            Assert.That(beautifier.Beautify("(xx)()"), Is.EqualTo("(xx)()"));
            Assert.That(beautifier.Beautify("a[1]()"), Is.EqualTo("a[1]()"));
            Assert.That(beautifier.Beautify("if(a){b();}else if(c) foo();"), Is.EqualTo("if (a) {\n    b();\n} else if (c) foo();"));
            Assert.That(beautifier.Beautify("switch(x) {case 0: case 1: a(); break; default: break}"), Is.EqualTo("switch (x) {\n    case 0:\n    case 1:\n        a();\n        break;\n    default:\n        break\n}"));
            Assert.That(beautifier.Beautify("switch(x){case -1:break;case !y:break;}"), Is.EqualTo("switch (x) {\n    case -1:\n        break;\n    case !y:\n        break;\n}"));
            Assert.That(beautifier.Beautify("a !== b"), Is.EqualTo("a !== b"));
            Assert.That(beautifier.Beautify("if (a) b(); else c();"), Is.EqualTo("if (a) b();\nelse c();"));
            Assert.That(beautifier.Beautify("// comment\n(function something() {})"), Is.EqualTo("// comment\n(function something() {})"));
            Assert.That(beautifier.Beautify("{\n\n    x();\n\n}"), Is.EqualTo("{\n\n    x();\n\n}"));
            Assert.That(beautifier.Beautify("if (a in b) foo();"), Is.EqualTo("if (a in b) foo();"));
            Assert.That(beautifier.Beautify("var a, b"), Is.EqualTo("var a, b"));
            Assert.That(beautifier.Beautify("{a:1, b:2}"), Is.EqualTo("{\n    a: 1,\n    b: 2\n}"));
            Assert.That(beautifier.Beautify("a={1:[-1],2:[+1]}"), Is.EqualTo("a = {\n    1: [-1],\n    2: [+1]\n}"));
            Assert.That(beautifier.Beautify("var l = {\'a\':\'1\', \'b\':\'2\'}"), Is.EqualTo("var l = {\n    'a': '1',\n    'b': '2'\n}"));
            Assert.That(beautifier.Beautify("if (template.user[n] in bk) foo();"), Is.EqualTo("if (template.user[n] in bk) foo();"));
            Assert.That(beautifier.Beautify("{{}/z/}"), Is.EqualTo("{\n    {}\n    /z/\n}"));
            Assert.That(beautifier.Beautify("return 45"), Is.EqualTo("return 45"));
            Assert.That(beautifier.Beautify("If[1]"), Is.EqualTo("If[1]"));
            Assert.That(beautifier.Beautify("Then[1]"), Is.EqualTo("Then[1]"));
            Assert.That(beautifier.Beautify("a = 1e10"), Is.EqualTo("a = 1e10"));
            Assert.That(beautifier.Beautify("a = 1.3e10"), Is.EqualTo("a = 1.3e10"));
            Assert.That(beautifier.Beautify("a = 1.3e-10"), Is.EqualTo("a = 1.3e-10"));
            Assert.That(beautifier.Beautify("a = -1.3e-10"), Is.EqualTo("a = -1.3e-10"));
            Assert.That(beautifier.Beautify("a = 1e-10"), Is.EqualTo("a = 1e-10"));
            Assert.That(beautifier.Beautify("a = e - 10"), Is.EqualTo("a = e - 10"));
            Assert.That(beautifier.Beautify("a = 11-10"), Is.EqualTo("a = 11 - 10"));
            Assert.That(beautifier.Beautify("a = 1;// comment"), Is.EqualTo("a = 1; // comment"));
            Assert.That(beautifier.Beautify("a = 1; // comment"), Is.EqualTo("a = 1; // comment"));
            Assert.That(beautifier.Beautify("a = 1;\n // comment"), Is.EqualTo("a = 1;\n// comment"));
            Assert.That(beautifier.Beautify("a = [-1, -1, -1]"), Is.EqualTo("a = [-1, -1, -1]"));

            Assert.That(beautifier.Beautify("o = [{a:b},{c:d}]"), Is.EqualTo("o = [{\n    a: b\n}, {\n    c: d\n}]"));

            Assert.That(beautifier.Beautify("if (a) {\n    do();\n}"), Is.EqualTo("if (a) {\n    do();\n}"));
            Assert.That(beautifier.Beautify("if\n(a)\nb();"), Is.EqualTo("if (a) b();"));
            Assert.That(beautifier.Beautify("if (a) {\n// comment\n}else{\n// comment\n}"), Is.EqualTo("if (a) {\n    // comment\n} else {\n    // comment\n}"));
            Assert.That(beautifier.Beautify("if (a) {\n// comment\n// comment\n}"), Is.EqualTo("if (a) {\n    // comment\n    // comment\n}"));
            Assert.That(beautifier.Beautify("if (a) b() else c();"), Is.EqualTo("if (a) b()\nelse c();"));
            Assert.That(beautifier.Beautify("if (a) b() else if c() d();"), Is.EqualTo("if (a) b()\nelse if c() d();"));

            Assert.That(beautifier.Beautify("{}"), Is.EqualTo("{}"));
            Assert.That(beautifier.Beautify("{\n\n}"), Is.EqualTo("{\n\n}"));
            Assert.That(beautifier.Beautify("do { a(); } while ( 1 );"), Is.EqualTo("do {\n    a();\n} while (1);"));
            Assert.That(beautifier.Beautify("do {} while (1);"), Is.EqualTo("do {} while (1);"));
            Assert.That(beautifier.Beautify("do {\n} while (1);"), Is.EqualTo("do {} while (1);"));
            Assert.That(beautifier.Beautify("do {\n\n} while (1);"), Is.EqualTo("do {\n\n} while (1);"));
            Assert.That(beautifier.Beautify("var a = x(a, b, c)"), Is.EqualTo("var a = x(a, b, c)"));
            Assert.That(beautifier.Beautify("delete x if (a) b();"), Is.EqualTo("delete x\nif (a) b();"));
            Assert.That(beautifier.Beautify("delete x[x] if (a) b();"), Is.EqualTo("delete x[x]\nif (a) b();"));
            Assert.That(beautifier.Beautify("for(var a=1,b=2)"), Is.EqualTo("for (var a = 1, b = 2)"));
            Assert.That(beautifier.Beautify("for(var a=1,b=2,c=3)"), Is.EqualTo("for (var a = 1, b = 2, c = 3)"));
            Assert.That(beautifier.Beautify("for(var a=1,b=2,c=3;d<3;d++)"), Is.EqualTo("for (var a = 1, b = 2, c = 3; d < 3; d++)"));
            Assert.That(beautifier.Beautify("function x(){(a||b).c()}"), Is.EqualTo("function x() {\n    (a || b).c()\n}"));
            Assert.That(beautifier.Beautify("function x(){return - 1}"), Is.EqualTo("function x() {\n    return -1\n}"));
            Assert.That(beautifier.Beautify("function x(){return ! a}"), Is.EqualTo("function x() {\n    return !a\n}"));

            Assert.That(beautifier.Beautify("settings = $.extend({},defaults,settings);"), Is.EqualTo("settings = $.extend({}, defaults, settings);"));
            Assert.That(beautifier.Beautify("{xxx;}()"), Is.EqualTo("{\n    xxx;\n}()"));
            Assert.That(beautifier.Beautify("a = 'a'\nb = 'b'"), Is.EqualTo("a = 'a'\nb = 'b'"));
            Assert.That(beautifier.Beautify("a = /reg/exp"), Is.EqualTo("a = /reg/exp"));
            Assert.That(beautifier.Beautify("a = /reg/"), Is.EqualTo("a = /reg/"));
            Assert.That(beautifier.Beautify("/abc/.test()"), Is.EqualTo("/abc/.test()"));
            Assert.That(beautifier.Beautify("/abc/i.test()"), Is.EqualTo("/abc/i.test()"));
            Assert.That(beautifier.Beautify("{/abc/i.test()}"), Is.EqualTo("{\n    /abc/i.test()\n}"));
            Assert.That(beautifier.Beautify("var x=(a)/a;"), Is.EqualTo("var x = (a) / a;"));

            Assert.That(beautifier.Beautify("x != -1"), Is.EqualTo("x != -1"));

            Assert.That(beautifier.Beautify("for (; s-->0;)"), Is.EqualTo("for (; s-- > 0;)"));
            Assert.That(beautifier.Beautify("for (; s++>0;)"), Is.EqualTo("for (; s++ > 0;)"));
            Assert.That(beautifier.Beautify("a = s++>s--;"), Is.EqualTo("a = s++ > s--;"));
            Assert.That(beautifier.Beautify("a = s++>--s;"), Is.EqualTo("a = s++ > --s;"));

            Assert.That(beautifier.Beautify("{x=#1=[]}"), Is.EqualTo("{\n    x = #1=[]\n}"));
            Assert.That(beautifier.Beautify("{a:#1={}}"), Is.EqualTo("{\n    a: #1={}\n}"));
            Assert.That(beautifier.Beautify("{a:#1#}"), Is.EqualTo("{\n    a: #1#\n}"));
            Assert.That(beautifier.Beautify("\"incomplete-string"), Is.EqualTo("\"incomplete-string"));
            Assert.That(beautifier.Beautify("'incomplete-string"), Is.EqualTo("'incomplete-string"));
            Assert.That(beautifier.Beautify("/incomplete-regex"), Is.EqualTo("/incomplete-regex"));
            Assert.That(beautifier.Beautify("{a:1},{a:2}"), Is.EqualTo("{\n    a: 1\n}, {\n    a: 2\n}"));
            Assert.That(beautifier.Beautify("var ary=[{a:1}, {a:2}];"), Is.EqualTo("var ary = [{\n    a: 1\n}, {\n    a: 2\n}];"));
            Assert.That(beautifier.Beautify("{a:#1"), Is.EqualTo("{\n    a: #1"));

            Assert.That(beautifier.Beautify("{a:#"), Is.EqualTo("{\n    a: #"));
            Assert.That(beautifier.Beautify("}}}"), Is.EqualTo("}\n}\n}"));
            Assert.That(beautifier.Beautify("<!--\nvoid();\n// -->"), Is.EqualTo("<!--\nvoid();\n// -->"));
            Assert.That(beautifier.Beautify("a=/regexp"), Is.EqualTo("a = /regexp"));
            Assert.That(beautifier.Beautify("{a:#1=[],b:#1#,c:#999999#}"), Is.EqualTo("{\n    a: #1=[],\n    b: #1#,\n    c: #999999#\n}"));
            Assert.That(beautifier.Beautify("a = 1e+2"), Is.EqualTo("a = 1e+2"));
            Assert.That(beautifier.Beautify("a = 1e-2"), Is.EqualTo("a = 1e-2"));
            Assert.That(beautifier.Beautify("do{x()}while(a>1)"), Is.EqualTo("do {\n    x()\n} while (a > 1)"));
            Assert.That(beautifier.Beautify("x(); /reg/exp.match(something)"), Is.EqualTo("x();\n/reg/exp.match(something)"));
            Assert.That(beautifier.Beautify("something();("), Is.EqualTo("something();\n("));

            Assert.That(beautifier.Beautify("#!she/bangs, she bangs\nf=1"), Is.EqualTo("#!she/bangs, she bangs\n\nf = 1"));
            Assert.That(beautifier.Beautify("#!she/bangs, she bangs\n\nf=1"), Is.EqualTo("#!she/bangs, she bangs\n\nf = 1"));
            Assert.That(beautifier.Beautify("#!she/bangs, she bangs\n\n/* comment */"), Is.EqualTo("#!she/bangs, she bangs\n\n/* comment */"));
            Assert.That(beautifier.Beautify("#!she/bangs, she bangs\n\n\n/* comment */"), Is.EqualTo("#!she/bangs, she bangs\n\n\n/* comment */"));
            Assert.That(beautifier.Beautify("#"), Is.EqualTo("#"));
            Assert.That(beautifier.Beautify("#!"), Is.EqualTo("#!"));
            Assert.That(beautifier.Beautify("function namespace::something()"), Is.EqualTo("function namespace::something()"));
            Assert.That(beautifier.Beautify("<!--\nsomething();\n-->"), Is.EqualTo("<!--\nsomething();\n-->"));
            Assert.That(beautifier.Beautify("<!--\nif(i<0){bla();}\n-->"), Is.EqualTo("<!--\nif (i < 0) {\n    bla();\n}\n-->"));
            Assert.That(beautifier.Beautify("{foo();--bar;}"), Is.EqualTo("{\n    foo();\n    --bar;\n}"));
            Assert.That(beautifier.Beautify("{foo();++bar;}"), Is.EqualTo("{\n    foo();\n    ++bar;\n}"));

            Assert.That(beautifier.Beautify("{--bar;}"), Is.EqualTo("{\n    --bar;\n}"));
            Assert.That(beautifier.Beautify("{++bar;}"), Is.EqualTo("{\n    ++bar;\n}"));
            Assert.That(beautifier.Beautify("a(/abc\\/\\/def/);b()"), Is.EqualTo("a(/abc\\/\\/def/);\nb()"));
            Assert.That(beautifier.Beautify("a(/a[b\\[\\]c]d/);b()"), Is.EqualTo("a(/a[b\\[\\]c]d/);\nb()"));
            Assert.That(beautifier.Beautify("a(/a[b\\["), Is.EqualTo("a(/a[b\\["));
            Assert.That(beautifier.Beautify("a(/[a/b]/);b()"), Is.EqualTo("a(/[a/b]/);\nb()"));
            Assert.That(beautifier.Beautify("a=[[1,2],[4,5],[7,8]]"), Is.EqualTo("a = [\n    [1, 2],\n    [4, 5],\n    [7, 8]\n]"));
            Assert.That(beautifier.Beautify("a=[a[1],b[4],c[d[7]]]"), Is.EqualTo("a = [a[1], b[4], c[d[7]]]"));
            Assert.That(beautifier.Beautify("[1,2,[3,4,[5,6],7],8]"), Is.EqualTo("[1, 2, [3, 4, [5, 6], 7], 8]"));
            Assert.That(beautifier.Beautify("[[[\"1\",\"2\"],[\"3\",\"4\"]],[[\"5\",\"6\",\"7\"],[\"8\",\"9\",\"0\"]],[[\"1\",\"2\",\"3\"],[\"4\",\"5\",\"6\",\"7\"],[\"8\",\"9\",\"0\"]]]"), Is.EqualTo("[\n    [\n        [\"1\", \"2\"],\n        [\"3\", \"4\"]\n    ],\n    [\n        [\"5\", \"6\", \"7\"],\n        [\"8\", \"9\", \"0\"]\n    ],\n    [\n        [\"1\", \"2\", \"3\"],\n        [\"4\", \"5\", \"6\", \"7\"],\n        [\"8\", \"9\", \"0\"]\n    ]\n]"));
            Assert.That(beautifier.Beautify("{[x()[0]];indent;}"), Is.EqualTo("{\n    [x()[0]];\n    indent;\n}"));
            Assert.That(beautifier.Beautify("return ++i"), Is.EqualTo("return ++i"));
            Assert.That(beautifier.Beautify("return !!x"), Is.EqualTo("return !!x"));

            Assert.That(beautifier.Beautify("return !x"), Is.EqualTo("return !x"));
            Assert.That(beautifier.Beautify("return [1,2]"), Is.EqualTo("return [1, 2]"));
            Assert.That(beautifier.Beautify("return;"), Is.EqualTo("return;"));
            Assert.That(beautifier.Beautify("return\nfunc"), Is.EqualTo("return\nfunc"));
            Assert.That(beautifier.Beautify("catch(e)"), Is.EqualTo("catch (e)"));
            Assert.That(beautifier.Beautify("var a=1,b={foo:2,bar:3},{baz:4,wham:5},c=4;"), Is.EqualTo("var a = 1,\n    b = {\n        foo: 2,\n        bar: 3\n    }, {\n        baz: 4,\n        wham: 5\n    }, c = 4;"));
            Assert.That(beautifier.Beautify("var a=1,b={foo:2,bar:3},{baz:4,wham:5},\nc=4;"), Is.EqualTo("var a = 1,\n    b = {\n        foo: 2,\n        bar: 3\n    }, {\n        baz: 4,\n        wham: 5\n    },\n    c = 4;"));
            Assert.That(beautifier.Beautify("function x(/*int*/ start, /*string*/ foo)"), Is.EqualTo("function x( /*int*/ start, /*string*/ foo)"));
            Assert.That(beautifier.Beautify("/**\n* foo\n*/"), Is.EqualTo("/**\n * foo\n */"));
            Assert.That(beautifier.Beautify("{\n/**\n* foo\n*/\n}"), Is.EqualTo("{\n    /**\n     * foo\n     */\n}"));
            Assert.That(beautifier.Beautify("var a,b,c=1,d,e,f=2;"), Is.EqualTo("var a, b, c = 1,\n    d, e, f = 2;"));

            Assert.That(beautifier.Beautify("var a,b,c=[],d,e,f=2;"), Is.EqualTo("var a, b, c = [],\n    d, e, f = 2;"));
            Assert.That(beautifier.Beautify("function() {\n    var a, b, c, d, e = [],\n        f;\n}"), Is.EqualTo("function() {\n    var a, b, c, d, e = [],\n        f;\n}"));
            Assert.That(beautifier.Beautify("do/regexp/;\nwhile(1);"), Is.EqualTo("do /regexp/;\nwhile (1);"));
            Assert.That(beautifier.Beautify("var a = a,\na;\nb = {\nb\n}"), Is.EqualTo("var a = a,\n    a;\nb = {\n    b\n}"));
            Assert.That(beautifier.Beautify("var a = a,\n    /* c */\n    b;"), Is.EqualTo("var a = a,\n    /* c */\n    b;"));
            Assert.That(beautifier.Beautify("var a = a,\n    // c\n    b;"), Is.EqualTo("var a = a,\n    // c\n    b;"));
            Assert.That(beautifier.Beautify("foo.(\"bar\");"), Is.EqualTo("foo.(\"bar\");"));
            Assert.That(beautifier.Beautify("if (a) a()\nelse b()\nnewline()"), Is.EqualTo("if (a) a()\nelse b()\nnewline()"));
            Assert.That(beautifier.Beautify("if (a) a()\nnewline()"), Is.EqualTo("if (a) a()\nnewline()"));
            Assert.That(beautifier.Beautify("a=typeof(x)"), Is.EqualTo("a = typeof(x)"));
            Assert.That(beautifier.Beautify("var a = function() {\n    return null;\n},\nb = false;"), Is.EqualTo("var a = function() {\n    return null;\n},\nb = false;"));
            Assert.That(beautifier.Beautify("var a = function() {\n    func1()\n}"), Is.EqualTo("var a = function() {\n    func1()\n}"));
            Assert.That(beautifier.Beautify("var a = function() {\n    func1()\n}\nvar b = function() {\n    func2()\n}"), Is.EqualTo("var a = function() {\n    func1()\n}\nvar b = function() {\n    func2()\n}"));

            beautifier.Opts.JslintHappy = true;

            Assert.That(beautifier.Beautify("x();\n\nfunction(){}"), Is.EqualTo("x();\n\nfunction () {}"));
            Assert.That(beautifier.Beautify("function () {\n    var a, b, c, d, e = [],\n        f;\n}"), Is.EqualTo("function () {\n    var a, b, c, d, e = [],\n        f;\n}"));
            Assert.That(beautifier.Beautify("// comment 1\n(function()"), Is.EqualTo("// comment 1\n(function ()"));
            Assert.That(beautifier.Beautify("var o1=$.extend(a);function(){alert(x);}"), Is.EqualTo("var o1 = $.extend(a);\n\nfunction () {\n    alert(x);\n}"));
            Assert.That(beautifier.Beautify("a=typeof(x)"), Is.EqualTo("a = typeof (x)"));

            beautifier.Opts.JslintHappy = false;

            Assert.That(beautifier.Beautify("// comment 2\n(function()"), Is.EqualTo("// comment 2\n(function()"));
            Assert.That(beautifier.Beautify("var a2, b2, c2, d2 = 0, c = function() {}, d = '';"), Is.EqualTo("var a2, b2, c2, d2 = 0,\n    c = function() {}, d = '';"));
            Assert.That(beautifier.Beautify("var a2, b2, c2, d2 = 0, c = function() {},\nd = '';"), Is.EqualTo("var a2, b2, c2, d2 = 0,\n    c = function() {},\n    d = '';"));
            Assert.That(beautifier.Beautify("var o2=$.extend(a);function(){alert(x);}"), Is.EqualTo("var o2 = $.extend(a);\n\nfunction() {\n    alert(x);\n}"));
            Assert.That(beautifier.Beautify("{\"x\":[{\"a\":1,\"b\":3},7,8,8,8,8,{\"b\":99},{\"a\":11}]}"), Is.EqualTo("{\n    \"x\": [{\n        \"a\": 1,\n        \"b\": 3\n    },\n    7, 8, 8, 8, 8, {\n        \"b\": 99\n    }, {\n        \"a\": 11\n    }]\n}"));
            Assert.That(beautifier.Beautify("{\"1\":{\"1a\":\"1b\"},\"2\"}"), Is.EqualTo("{\n    \"1\": {\n        \"1a\": \"1b\"\n    },\n    \"2\"\n}"));
            Assert.That(beautifier.Beautify("{a:{a:b},c}"), Is.EqualTo("{\n    a: {\n        a: b\n    },\n    c\n}"));
            Assert.That(beautifier.Beautify("{[y[a]];keep_indent;}"), Is.EqualTo("{\n    [y[a]];\n    keep_indent;\n}"));
            Assert.That(beautifier.Beautify("if (x) {y} else { if (x) {y}}"), Is.EqualTo("if (x) {\n    y\n} else {\n    if (x) {\n        y\n    }\n}"));
            Assert.That(beautifier.Beautify("if (foo) one()\ntwo()\nthree()"), Is.EqualTo("if (foo) one()\ntwo()\nthree()"));
            Assert.That(beautifier.Beautify("if (1 + foo() && bar(baz()) / 2) one()\ntwo()\nthree()"), Is.EqualTo("if (1 + foo() && bar(baz()) / 2) one()\ntwo()\nthree()"));
            Assert.That(beautifier.Beautify("if (1 + foo() && bar(baz()) / 2) one();\ntwo();\nthree();"), Is.EqualTo("if (1 + foo() && bar(baz()) / 2) one();\ntwo();\nthree();"));

            beautifier.Opts.IndentSize = 1;
            beautifier.Opts.IndentChar = ' ';

            Assert.That(beautifier.Beautify("{ one_char() }"), Is.EqualTo("{\n one_char()\n}"));
            Assert.That(beautifier.Beautify("var a,b=1,c=2"), Is.EqualTo("var a, b = 1,\n c = 2"));

            beautifier.Opts.IndentSize = 4;

            Assert.That(beautifier.Beautify("{ one_char() }"), Is.EqualTo("{\n    one_char()\n}"));

            beautifier.Opts.IndentSize = 1;
            beautifier.Opts.IndentChar = '\t';

            Assert.That(beautifier.Beautify("{ one_char() }"), Is.EqualTo("{\n\tone_char()\n}"));
            Assert.That(beautifier.Beautify("x = a ? b : c; x;"), Is.EqualTo("x = a ? b : c;\nx;"));

            beautifier.Opts.IndentSize = 4;
            beautifier.Opts.IndentChar = ' ';
            beautifier.Opts.PreserveNewlines = false;

            Assert.That(beautifier.Beautify("var\na=dont_preserve_newlines;"), Is.EqualTo("var a = dont_preserve_newlines;"));
            Assert.That(beautifier.Beautify("function foo() {\n    return 1;\n}\n\nfunction foo() {\n    return 1;\n}"), Is.EqualTo("function foo() {\n    return 1;\n}\n\nfunction foo() {\n    return 1;\n}"));
            Assert.That(beautifier.Beautify("function foo() {\n    return 1;\n}\nfunction foo() {\n    return 1;\n}"), Is.EqualTo("function foo() {\n    return 1;\n}\n\nfunction foo() {\n    return 1;\n}"));
            Assert.That(beautifier.Beautify("function foo() {\n    return 1;\n}\n\n\nfunction foo() {\n    return 1;\n}"), Is.EqualTo("function foo() {\n    return 1;\n}\n\nfunction foo() {\n    return 1;\n}"));

            beautifier.Opts.PreserveNewlines = true;

            Assert.That(beautifier.Beautify("var\na=do_preserve_newlines;"), Is.EqualTo("var\na = do_preserve_newlines;"));
            Assert.That(beautifier.Beautify("// a\n// b\n\n// c\n// d"), Is.EqualTo("// a\n// b\n\n// c\n// d"));
            Assert.That(beautifier.Beautify("if (foo) //  comment\n{\n    bar();\n}"), Is.EqualTo("if (foo) //  comment\n{\n    bar();\n}"));

            beautifier.Opts.KeepArrayIndentation = true;

            Assert.That(beautifier.Beautify("a = ['a', 'b', 'c',\n    'd', 'e', 'f']"), Is.EqualTo("a = ['a', 'b', 'c',\n    'd', 'e', 'f']"));
            Assert.That(beautifier.Beautify("a = ['a', 'b', 'c',\n    'd', 'e', 'f',\n        'g', 'h', 'i']"), Is.EqualTo("a = ['a', 'b', 'c',\n    'd', 'e', 'f',\n        'g', 'h', 'i']"));
            Assert.That(beautifier.Beautify("a = ['a', 'b', 'c',\n        'd', 'e', 'f',\n            'g', 'h', 'i']"), Is.EqualTo("a = ['a', 'b', 'c',\n        'd', 'e', 'f',\n            'g', 'h', 'i']"));
            Assert.That(beautifier.Beautify("var x = [{}\n]"), Is.EqualTo("var x = [{}\n]"));
            Assert.That(beautifier.Beautify("var x = [{foo:bar}\n]"), Is.EqualTo("var x = [{\n    foo: bar\n}\n]"));
            Assert.That(beautifier.Beautify("a = ['something',\n'completely',\n'different'];\nif (x);"), Is.EqualTo("a = ['something',\n'completely',\n'different'];\nif (x);"));
            Assert.That(beautifier.Beautify("a = ['a','b','c']"), Is.EqualTo("a = ['a', 'b', 'c']"));
            Assert.That(beautifier.Beautify("a = ['a',   'b','c']"), Is.EqualTo("a = ['a', 'b', 'c']"));
            Assert.That(beautifier.Beautify("x = [{'a':0}]"), Is.EqualTo("x = [{\n    'a': 0\n}]"));
            Assert.That(beautifier.Beautify("{a([[a1]], {b;});}"), Is.EqualTo("{\n    a([[a1]], {\n        b;\n    });\n}"));
            Assert.That(beautifier.Beautify("a = //comment\n/regex/;"), Is.EqualTo("a = //comment\n/regex/;"));
            Assert.That(beautifier.Beautify("/*\n * X\n */"), Is.EqualTo("/*\n * X\n */"));
            Assert.That(beautifier.Beautify("/*\r\n * X\r\n */"), Is.EqualTo("/*\n * X\n */"));
            Assert.That(beautifier.Beautify("if (a)\n{\nb;\n}\nelse\n{\nc;\n}"), Is.EqualTo("if (a) {\n    b;\n} else {\n    c;\n}"));
            Assert.That(beautifier.Beautify("var a = new function();"), Is.EqualTo("var a = new function();"));
            Assert.That(beautifier.Beautify("new function"), Is.EqualTo("new function"));

            beautifier.Opts.BraceStyle = BraceStyle.Expand;

            Assert.That(beautifier.Beautify("throw {}"), Is.EqualTo("throw {}"));
            Assert.That(beautifier.Beautify("throw {\n    foo;\n}"), Is.EqualTo("throw {\n    foo;\n}"));
            Assert.That(beautifier.Beautify("if (a)\n{\nb;\n}\nelse\n{\nc;\n}"), Is.EqualTo("if (a)\n{\n    b;\n}\nelse\n{\n    c;\n}"));
            Assert.That(beautifier.Beautify("foo {"), Is.EqualTo("foo\n{"));
            Assert.That(beautifier.Beautify("return;\n{"), Is.EqualTo("return;\n{"));
            Assert.That(beautifier.Beautify("if (a)\n{\nb;\n}\nelse\n{\nc;\n}"), Is.EqualTo("if (a)\n{\n    b;\n}\nelse\n{\n    c;\n}"));
            Assert.That(beautifier.Beautify("var foo = {}"), Is.EqualTo("var foo = {}"));
            Assert.That(beautifier.Beautify("if (a)\n{\nb;\n}\nelse\n{\nc;\n}"), Is.EqualTo("if (a)\n{\n    b;\n}\nelse\n{\n    c;\n}"));
            Assert.That(beautifier.Beautify("if (foo) {"), Is.EqualTo("if (foo)\n{"));
            Assert.That(beautifier.Beautify("foo {"), Is.EqualTo("foo\n{"));
            Assert.That(beautifier.Beautify("return {"), Is.EqualTo("return {"));
            Assert.That(beautifier.Beautify("return /* inline comment */ {"), Is.EqualTo("return /* inline comment */ {"));
            Assert.That(beautifier.Beautify("return /* inline comment */ {"), Is.EqualTo("return /* inline comment */ {"));
            Assert.That(beautifier.Beautify("return /* inline comment */ {"), Is.EqualTo("return /* inline comment */ {"));
            Assert.That(beautifier.Beautify("return;\n{"), Is.EqualTo("return;\n{"));

            beautifier.Opts.BraceStyle = BraceStyle.Collapse;

            Assert.That(beautifier.Beautify("if (a)\n{\nb;\n}\nelse\n{\nc;\n}"), Is.EqualTo("if (a) {\n    b;\n} else {\n    c;\n}"));
            Assert.That(beautifier.Beautify("if (foo) {"), Is.EqualTo("if (foo) {"));
            Assert.That(beautifier.Beautify("foo {"), Is.EqualTo("foo {"));
            Assert.That(beautifier.Beautify("return {"), Is.EqualTo("return {"));
            Assert.That(beautifier.Beautify("return;\n{"), Is.EqualTo("return; {"));
            Assert.That(beautifier.Beautify("if (foo) bar();\nelse break"), Is.EqualTo("if (foo) bar();\nelse break"));
            Assert.That(beautifier.Beautify("function x() {\n    foo();\n}zzz"), Is.EqualTo("function x() {\n    foo();\n}\nzzz"));
            Assert.That(beautifier.Beautify("a: do {} while (); xxx"), Is.EqualTo("a: do {} while ();\nxxx"));

            beautifier.Opts.BraceStyle = BraceStyle.EndExpand;

            Assert.That(beautifier.Beautify("if(1){2}else{3}"), Is.EqualTo("if (1) {\n    2\n}\nelse {\n    3\n}"));
            Assert.That(beautifier.Beautify("try{a();}catch(b){c();}finally{d();}"), Is.EqualTo("try {\n    a();\n}\ncatch (b) {\n    c();\n}\nfinally {\n    d();\n}"));
            Assert.That(beautifier.Beautify("if(a){b();}else if(c) foo();"), Is.EqualTo("if (a) {\n    b();\n}\nelse if (c) foo();"));
            Assert.That(beautifier.Beautify("if (a) {\n// comment\n}else{\n// comment\n}"), Is.EqualTo("if (a) {\n    // comment\n}\nelse {\n    // comment\n}"));
            Assert.That(beautifier.Beautify("if (x) {y} else { if (x) {y}}"), Is.EqualTo("if (x) {\n    y\n}\nelse {\n    if (x) {\n        y\n    }\n}"));
            Assert.That(beautifier.Beautify("if (a)\n{\nb;\n}\nelse\n{\nc;\n}"), Is.EqualTo("if (a) {\n    b;\n}\nelse {\n    c;\n}"));
            Assert.That(beautifier.Beautify("if (foo) {}\nelse /regex/.test();"), Is.EqualTo("if (foo) {}\nelse /regex/.test();"));
            Assert.That(beautifier.Beautify("a = <?= external() ?> ;"), Is.EqualTo("a = <?= external() ?> ;"));
            Assert.That(beautifier.Beautify("a = <%= external() %> ;"), Is.EqualTo("a = <%= external() %> ;"));
            Assert.That(beautifier.Beautify("roo = {\n    /*\n    ****\n      FOO\n    ****\n    */\n    BAR: 0\n};"), Is.EqualTo("roo = {\n    /*\n    ****\n      FOO\n    ****\n    */\n    BAR: 0\n};"));
            Assert.That(beautifier.Beautify("if (zz) {\n    // ....\n}\n(function"), Is.EqualTo("if (zz) {\n    // ....\n}\n(function"));

            beautifier.Opts.PreserveNewlines = true;

            Assert.That(beautifier.Beautify("var a = 42; // foo\n\nvar b;"), Is.EqualTo("var a = 42; // foo\n\nvar b;"));
            Assert.That(beautifier.Beautify("var a = 42; // foo\n\n\nvar b;"), Is.EqualTo("var a = 42; // foo\n\n\nvar b;"));
            Assert.That(beautifier.Beautify("\"foo\"\"bar\"\"baz\""), Is.EqualTo("\"foo\"\n\"bar\"\n\"baz\""));
            Assert.That(beautifier.Beautify("'foo''bar''baz'"), Is.EqualTo("'foo'\n'bar'\n'baz'"));
            Assert.That(beautifier.Beautify("{\n    get foo() {}\n}"), Is.EqualTo("{\n    get foo() {}\n}"));
            Assert.That(beautifier.Beautify("{\n    var a = get\n    foo();\n}"), Is.EqualTo("{\n    var a = get\n    foo();\n}"));
            Assert.That(beautifier.Beautify("{\n    set foo() {}\n}"), Is.EqualTo("{\n    set foo() {}\n}"));
            Assert.That(beautifier.Beautify("{\n    var a = set\n    foo();\n}"), Is.EqualTo("{\n    var a = set\n    foo();\n}"));
            Assert.That(beautifier.Beautify("var x = {\n    get function()\n}"), Is.EqualTo("var x = {\n    get function()\n}"));
            Assert.That(beautifier.Beautify("var x = {\n    set function()\n}"), Is.EqualTo("var x = {\n    set function()\n}"));
            Assert.That(beautifier.Beautify("var x = set\n\nfunction() {}"), Is.EqualTo("var x = set\n\nfunction() {}"));
            Assert.That(beautifier.Beautify("<!-- foo\nbar();\n-->"), Is.EqualTo("<!-- foo\nbar();\n-->"));
            Assert.That(beautifier.Beautify("<!-- dont crash"), Is.EqualTo("<!-- dont crash"));
            Assert.That(beautifier.Beautify("for () /abc/.test()"), Is.EqualTo("for () /abc/.test()"));
            Assert.That(beautifier.Beautify("if (k) /aaa/m.test(v) && l();"), Is.EqualTo("if (k) /aaa/m.test(v) && l();"));
            Assert.That(beautifier.Beautify("switch (true) {\n    case /swf/i.test(foo):\n        bar();\n}"), Is.EqualTo("switch (true) {\n    case /swf/i.test(foo):\n        bar();\n}"));
            Assert.That(beautifier.Beautify("createdAt = {\n    type: Date,\n    default: Date.now\n}"), Is.EqualTo("createdAt = {\n    type: Date,\n    default: Date.now\n}"));
            Assert.That(beautifier.Beautify("switch (createdAt) {\n    case a:\n        Date,\n    default:\n        Date.now\n}"), Is.EqualTo("switch (createdAt) {\n    case a:\n        Date,\n    default:\n        Date.now\n}"));
            Assert.That(beautifier.Beautify("foo = {\n    x: y, // #44\n    w: z // #44\n}"), Is.EqualTo("foo = {\n    x: y, // #44\n    w: z // #44\n}"));
            Assert.That(beautifier.Beautify("return function();"), Is.EqualTo("return function();"));
            Assert.That(beautifier.Beautify("var a = function();"), Is.EqualTo("var a = function();"));
            Assert.That(beautifier.Beautify("var a = 5 + function();"), Is.EqualTo("var a = 5 + function();"));
            Assert.That(beautifier.Beautify("{\n    foo // something\n    ,\n    bar // something\n    baz\n}"), Is.EqualTo("{\n    foo // something\n    ,\n    bar // something\n    baz\n}"));
            Assert.That(beautifier.Beautify("function a(a) {} function b(b) {} function c(c) {}"), Is.EqualTo("function a(a) {}\nfunction b(b) {}\nfunction c(c) {}"));
            Assert.That(beautifier.Beautify("3.*7;"), Is.EqualTo("3. * 7;"));
            Assert.That(beautifier.Beautify("import foo.*;"), Is.EqualTo("import foo.*;"));
            Assert.That(beautifier.Beautify("function f(a: a, b: b)"), Is.EqualTo("function f(a: a, b: b)"));
            Assert.That(beautifier.Beautify("foo(a, function() {})"), Is.EqualTo("foo(a, function() {})"));
            Assert.That(beautifier.Beautify("foo(a, /regex/)"), Is.EqualTo("foo(a, /regex/)"));
            Assert.That(beautifier.Beautify("if (foo) // comment\nbar();"), Is.EqualTo("if (foo) // comment\nbar();"));
            Assert.That(beautifier.Beautify("if (foo) // comment\n(bar());"), Is.EqualTo("if (foo) // comment\n(bar());"));
            Assert.That(beautifier.Beautify("if (foo) // comment\n(bar());"), Is.EqualTo("if (foo) // comment\n(bar());"));
            Assert.That(beautifier.Beautify("if (foo) // comment\n/asdf/;"), Is.EqualTo("if (foo) // comment\n/asdf/;"));
            Assert.That(beautifier.Beautify("/* foo */\n\"x\""), Is.EqualTo("/* foo */\n\"x\""));
            Assert.That(beautifier.Beautify("var a = 'foo' +\n    'bar';"), Is.EqualTo("var a = 'foo' +\n    'bar';"));
            Assert.That(beautifier.Beautify("var a = \"foo\" +\n    \"bar\";"), Is.EqualTo("var a = \"foo\" +\n    \"bar\";"));

            beautifier.Opts.BreakChainedMethods = false;
            beautifier.Opts.PreserveNewlines = false;

            Assert.That(beautifier.Beautify("foo\n.bar()\n.baz().cucumber(fat)"), Is.EqualTo("foo.bar().baz().cucumber(fat)"));
            Assert.That(beautifier.Beautify("foo\n.bar()\n.baz().cucumber(fat); foo.bar().baz().cucumber(fat)"), Is.EqualTo("foo.bar().baz().cucumber(fat);\nfoo.bar().baz().cucumber(fat)"));
            Assert.That(beautifier.Beautify("foo\n.bar()\n.baz().cucumber(fat)\n foo.bar().baz().cucumber(fat)"), Is.EqualTo("foo.bar().baz().cucumber(fat)\nfoo.bar().baz().cucumber(fat)"));
            Assert.That(beautifier.Beautify("this\n.something = foo.bar()\n.baz().cucumber(fat)"), Is.EqualTo("this.something = foo.bar().baz().cucumber(fat)"));
            Assert.That(beautifier.Beautify("this.something.xxx = foo.moo.bar()"), Is.EqualTo("this.something.xxx = foo.moo.bar()"));
            Assert.That(beautifier.Beautify("this\n.something\n.xxx = foo.moo\n.bar()"), Is.EqualTo("this.something.xxx = foo.moo.bar()"));

            beautifier.Opts.BreakChainedMethods = false;
            beautifier.Opts.PreserveNewlines = true;

            Assert.That(beautifier.Beautify("foo\n.bar()\n.baz().cucumber(fat)"), Is.EqualTo("foo\n    .bar()\n    .baz().cucumber(fat)"));
            Assert.That(beautifier.Beautify("foo\n.bar()\n.baz().cucumber(fat); foo.bar().baz().cucumber(fat)"), Is.EqualTo("foo\n    .bar()\n    .baz().cucumber(fat);\nfoo.bar().baz().cucumber(fat)"));
            Assert.That(beautifier.Beautify("foo\n.bar()\n.baz().cucumber(fat)\n foo.bar().baz().cucumber(fat)"), Is.EqualTo("foo\n    .bar()\n    .baz().cucumber(fat)\nfoo.bar().baz().cucumber(fat)"));
            Assert.That(beautifier.Beautify("this\n.something = foo.bar()\n.baz().cucumber(fat)"), Is.EqualTo("this\n    .something = foo.bar()\n    .baz().cucumber(fat)"));
            Assert.That(beautifier.Beautify("this.something.xxx = foo.moo.bar()"), Is.EqualTo("this.something.xxx = foo.moo.bar()"));
            Assert.That(beautifier.Beautify("this\n.something\n.xxx = foo.moo\n.bar()"), Is.EqualTo("this\n    .something\n    .xxx = foo.moo\n    .bar()"));

            beautifier.Opts.BreakChainedMethods = true;
            beautifier.Opts.PreserveNewlines = false;

            Assert.That(beautifier.Beautify("foo\n.bar()\n.baz().cucumber(fat)"), Is.EqualTo("foo.bar()\n    .baz()\n    .cucumber(fat)"));
            Assert.That(beautifier.Beautify("foo\n.bar()\n.baz().cucumber(fat); foo.bar().baz().cucumber(fat)"), Is.EqualTo("foo.bar()\n    .baz()\n    .cucumber(fat);\nfoo.bar()\n    .baz()\n    .cucumber(fat)"));
            Assert.That(beautifier.Beautify("foo\n.bar()\n.baz().cucumber(fat)\n foo.bar().baz().cucumber(fat)"), Is.EqualTo("foo.bar()\n    .baz()\n    .cucumber(fat)\nfoo.bar()\n    .baz()\n    .cucumber(fat)"));
            Assert.That(beautifier.Beautify("this\n.something = foo.bar()\n.baz().cucumber(fat)"), Is.EqualTo("this.something = foo.bar()\n    .baz()\n    .cucumber(fat)"));
            Assert.That(beautifier.Beautify("this.something.xxx = foo.moo.bar()"), Is.EqualTo("this.something.xxx = foo.moo.bar()"));
            Assert.That(beautifier.Beautify("this\n.something\n.xxx = foo.moo\n.bar()"), Is.EqualTo("this.something.xxx = foo.moo.bar()"));

            beautifier.Opts.BreakChainedMethods = true;
            beautifier.Opts.PreserveNewlines = true;

            Assert.That(beautifier.Beautify("foo\n.bar()\n.baz().cucumber(fat)"), Is.EqualTo("foo\n    .bar()\n    .baz()\n    .cucumber(fat)"));
            Assert.That(beautifier.Beautify("foo\n.bar()\n.baz().cucumber(fat); foo.bar().baz().cucumber(fat)"), Is.EqualTo("foo\n    .bar()\n    .baz()\n    .cucumber(fat);\nfoo.bar()\n    .baz()\n    .cucumber(fat)"));
            Assert.That(beautifier.Beautify("foo\n.bar()\n.baz().cucumber(fat)\n foo.bar().baz().cucumber(fat)"), Is.EqualTo("foo\n    .bar()\n    .baz()\n    .cucumber(fat)\nfoo.bar()\n    .baz()\n    .cucumber(fat)"));
            Assert.That(beautifier.Beautify("this\n.something = foo.bar()\n.baz().cucumber(fat)"), Is.EqualTo("this\n    .something = foo.bar()\n    .baz()\n    .cucumber(fat)"));
            Assert.That(beautifier.Beautify("this\n.something\n.xxx = foo.moo\n.bar()"), Is.EqualTo("this\n    .something\n    .xxx = foo.moo\n    .bar()"));

            beautifier.Opts.BreakChainedMethods = false;
            beautifier.Opts.PreserveNewlines = false;
            beautifier.Opts.WrapLineLength = 0;

            Assert.That(
                beautifier.Beautify(
                    "foo.bar().baz().cucumber((fat && \"sassy\") || (leans\n&& mean));\n" +
                    "Test_very_long_variable_name_this_should_never_wrap\n.but_this_can\n" +
                    "if (wraps_can_occur && inside_an_if_block) that_is_\n.okay();"
                ),
                Is.EqualTo(
                    "foo.bar().baz().cucumber((fat && \"sassy\") || (leans && mean));\n" +
                    "Test_very_long_variable_name_this_should_never_wrap.but_this_can\n" +
                    "if (wraps_can_occur && inside_an_if_block) that_is_.okay();"
                )
            );

            beautifier.Opts.WrapLineLength = 70;

            Assert.That(
                beautifier.Beautify(
                    "foo.bar().baz().cucumber((fat && \"sassy\") || (leans\n&& mean));\n" +
                    "Test_very_long_variable_name_this_should_never_wrap\n.but_this_can\n" +
                    "if (wraps_can_occur && inside_an_if_block) that_is_\n.okay();"
                ),
                Is.EqualTo(
                    "foo.bar().baz().cucumber((fat && \"sassy\") || (leans && mean));\n" +
                    "Test_very_long_variable_name_this_should_never_wrap.but_this_can\n" +
                    "if (wraps_can_occur && inside_an_if_block) that_is_.okay();"
                )
            );

            beautifier.Opts.WrapLineLength = 40;

            Assert.That(
                beautifier.Beautify(
                    "foo.bar().baz().cucumber((fat && \"sassy\") || (leans\n&& mean));\n" +
                    "Test_very_long_variable_name_this_should_never_wrap\n.but_this_can\n" +
                    "if (wraps_can_occur && inside_an_if_block) that_is_\n.okay();"
                ),
                Is.EqualTo(
                    "foo.bar().baz().cucumber((fat &&\n" +
                    "    \"sassy\") || (leans && mean));\n" +
                    "Test_very_long_variable_name_this_should_never_wrap\n" +
                    "    .but_this_can\n" +
                    "if (wraps_can_occur &&\n" +
                    "    inside_an_if_block) that_is_.okay();"
                )
            );

            beautifier.Opts.WrapLineLength = 41;

            Assert.That(
                beautifier.Beautify(
                    "foo.bar().baz().cucumber((fat && \"sassy\") || (leans\n&& mean));\n" +
                    "Test_very_long_variable_name_this_should_never_wrap\n.but_this_can\n" +
                    "if (wraps_can_occur && inside_an_if_block) that_is_\n.okay();"
                ),
                Is.EqualTo(
                    "foo.bar().baz().cucumber((fat && \"sassy\") ||\n" +
                    "    (leans && mean));\n" +
                    "Test_very_long_variable_name_this_should_never_wrap\n" +
                    "    .but_this_can\n" +
                    "if (wraps_can_occur &&\n" +
                    "    inside_an_if_block) that_is_.okay();"
                )
            );

            beautifier.Opts.WrapLineLength = 45;

            Assert.That(
                beautifier.Beautify(
                    "{\n" +
                    "    foo.bar().baz().cucumber((fat && \"sassy\") || (leans\n&& mean));\n" +
                    "    Test_very_long_variable_name_this_should_never_wrap\n.but_this_can\n" +
                    "    if (wraps_can_occur && inside_an_if_block) that_is_\n.okay();\n" +
                    "}"
                ),
                Is.EqualTo(
                    "{\n" +
                    "    foo.bar().baz().cucumber((fat && \"sassy\") ||\n" +
                    "        (leans && mean));\n" +
                    "    Test_very_long_variable_name_this_should_never_wrap\n" +
                    "        .but_this_can\n" +
                    "    if (wraps_can_occur &&\n" +
                    "        inside_an_if_block) that_is_.okay();\n" +
                    "}"
                )
            );

            beautifier.Opts.PreserveNewlines = true;
            beautifier.Opts.WrapLineLength = 0;

            Assert.That(
                beautifier.Beautify(
                    "foo.bar().baz().cucumber((fat && \"sassy\") || (leans\n&& mean));\n" +
                    "Test_very_long_variable_name_this_should_never_wrap\n.but_this_can\n" +
                    "if (wraps_can_occur && inside_an_if_block) that_is_\n.okay();"
                ),
                Is.EqualTo(
                    "foo.bar().baz().cucumber((fat && \"sassy\") || (leans && mean));\n" +
                    "Test_very_long_variable_name_this_should_never_wrap\n" +
                    "    .but_this_can\n" +
                    "if (wraps_can_occur && inside_an_if_block) that_is_\n" +
                    "    .okay();"
                )
            );

            beautifier.Opts.WrapLineLength = 70;

            Assert.That(
                beautifier.Beautify(
                    "foo.bar().baz().cucumber((fat && \"sassy\") || (leans\n&& mean));\n" +
                    "Test_very_long_variable_name_this_should_never_wrap\n.but_this_can\n" +
                    "if (wraps_can_occur && inside_an_if_block) that_is_\n.okay();"
                ),
                Is.EqualTo(
                    "foo.bar().baz().cucumber((fat && \"sassy\") || (leans && mean));\n" +
                    "Test_very_long_variable_name_this_should_never_wrap\n" +
                    "    .but_this_can\n" +
                    "if (wraps_can_occur && inside_an_if_block) that_is_\n" +
                    "    .okay();"
                )
            );

            beautifier.Opts.WrapLineLength = 40;

            Assert.That(
                beautifier.Beautify(
                    "foo.bar().baz().cucumber((fat && \"sassy\") || (leans\n&& mean));\n" +
                    "Test_very_long_variable_name_this_should_never_wrap\n.but_this_can\n" +
                    "if (wraps_can_occur && inside_an_if_block) that_is_\n.okay();"
                ),

                Is.EqualTo(
                    "foo.bar().baz().cucumber((fat &&\n" +
                    "    \"sassy\") || (leans && mean));\n" +
                    "Test_very_long_variable_name_this_should_never_wrap\n" +
                    "    .but_this_can\n" +
                    "if (wraps_can_occur &&\n" +
                    "    inside_an_if_block) that_is_\n" +
                    "    .okay();"
                )
            );

            beautifier.Opts.WrapLineLength = 41;

            Assert.That(
                beautifier.Beautify(
                    "foo.bar().baz().cucumber((fat && \"sassy\") || (leans\n&& mean));\n" +
                    "Test_very_long_variable_name_this_should_never_wrap\n.but_this_can\n" +
                    "if (wraps_can_occur && inside_an_if_block) that_is_\n.okay();"
                ),
                Is.EqualTo(
                    "foo.bar().baz().cucumber((fat && \"sassy\") ||\n" +
                    "    (leans && mean));\n" +
                    "Test_very_long_variable_name_this_should_never_wrap\n" +
                    "    .but_this_can\n" +
                    "if (wraps_can_occur &&\n" +
                    "    inside_an_if_block) that_is_\n" +
                    "    .okay();"
                )
            );

            beautifier.Opts.WrapLineLength = 45;

            Assert.That(
                beautifier.Beautify(
                    "{\n" +
                    "    foo.bar().baz().cucumber((fat && \"sassy\") || (leans\n&& mean));\n" +
                    "    Test_very_long_variable_name_this_should_never_wrap\n.but_this_can\n" +
                    "    if (wraps_can_occur && inside_an_if_block) that_is_\n.okay();\n" +
                    "}"
                ),
                Is.EqualTo(
                    "{\n" +
                    "    foo.bar().baz().cucumber((fat && \"sassy\") ||\n" +
                    "        (leans && mean));\n" +
                    "    Test_very_long_variable_name_this_should_never_wrap\n" +
                    "        .but_this_can\n" +
                    "    if (wraps_can_occur &&\n" +
                    "        inside_an_if_block) that_is_\n" +
                    "        .okay();\n" +
                    "}"
                )
            );

            beautifier.Opts.WrapLineLength = 0;

            beautifier.Opts.PreserveNewlines = false;

            Assert.That(beautifier.Beautify("var a =\nfoo"), Is.EqualTo("var a = foo"));
            Assert.That(beautifier.Beautify("var a = {\n\"a\":1,\n\"b\":2}"), Is.EqualTo("var a = {\n    \"a\": 1,\n    \"b\": 2\n}"));
            Assert.That(beautifier.Beautify("var a = {\n'a':1,\n'b':2}"), Is.EqualTo("var a = {\n    'a': 1,\n    'b': 2\n}"));
            Assert.That(beautifier.Beautify("var a = /*i*/ \"b\";"), Is.EqualTo("var a = /*i*/ \"b\";"));
            Assert.That(beautifier.Beautify("var a = /*i*/\n\"b\";"), Is.EqualTo("var a = /*i*/ \"b\";"));
            Assert.That(beautifier.Beautify("var a = /*i*/\nb;"), Is.EqualTo("var a = /*i*/ b;"));
            Assert.That(beautifier.Beautify("{\n\n\n\"x\"\n}"), Is.EqualTo("{\n    \"x\"\n}"));
            Assert.That(beautifier.Beautify("if(a &&\nb\n||\nc\n||d\n&&\ne) e = f"), Is.EqualTo("if (a && b || c || d && e) e = f"));
            Assert.That(beautifier.Beautify("if(a &&\n(b\n||\nc\n||d)\n&&\ne) e = f"), Is.EqualTo("if (a && (b || c || d) && e) e = f"));
            Assert.That(beautifier.Beautify("\n\n\"x\""), Is.EqualTo("\"x\""));

            beautifier.Opts.PreserveNewlines = true;
            Assert.That(beautifier.Beautify("var a =\nfoo"), Is.EqualTo("var a =\n    foo"));
            Assert.That(beautifier.Beautify("var a = {\n\"a\":1,\n\"b\":2}"), Is.EqualTo("var a = {\n    \"a\": 1,\n    \"b\": 2\n}"));
            Assert.That(beautifier.Beautify("var a = {\n'a':1,\n'b':2}"), Is.EqualTo("var a = {\n    'a': 1,\n    'b': 2\n}"));
            Assert.That(beautifier.Beautify("var a = /*i*/ \"b\";"), Is.EqualTo("var a = /*i*/ \"b\";"));
            Assert.That(beautifier.Beautify("var a = /*i*/\n\"b\";"), Is.EqualTo("var a = /*i*/\n    \"b\";"));
            Assert.That(beautifier.Beautify("var a = /*i*/\nb;"), Is.EqualTo("var a = /*i*/\n    b;"));
            Assert.That(beautifier.Beautify("{\n\n\n\"x\"\n}"), Is.EqualTo("{\n\n\n    \"x\"\n}"));
            Assert.That(beautifier.Beautify("if(a &&\nb\n||\nc\n||d\n&&\ne) e = f"), Is.EqualTo("if (a &&\n    b ||\n    c || d &&\n    e) e = f"));
            Assert.That(beautifier.Beautify("if(a &&\n(b\n||\nc\n||d)\n&&\ne) e = f"), Is.EqualTo("if (a &&\n    (b ||\n    c || d) &&\n    e) e = f"));
            Assert.That(beautifier.Beautify("\n\n\"x\""), Is.EqualTo("\"x\""));
        }

        [Test]
        public void TestNewLineBug() {
            Assert.That(beautifier.Beautify("function foo()\n{\n\n}\n\n\n\nfunction bar()\n{\n\n}\n"), Is.EqualTo("function foo() {\n\n}\n\n\n\nfunction bar() {\n\n}"));
        }
    }
}