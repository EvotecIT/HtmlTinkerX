using HtmlTinkerX;
using NUglify.Html;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlOptimizerCssMathTests {
    [Fact]
    public void OptimizeCss_PreservesNestedTerminalFontSize() {
        const string css = ".frame { font-size: min(var(--max), max(var(--fit), var(--min) - clamp(0px, (var(--fit) - var(--min)) * 1000, 1px) * 1000)); }";

        string result = HtmlOptimizer.OptimizeCss(css);
        Assert.Contains("min(var(--max), max(var(--fit), var(--min) - clamp(0px, (var(--fit) - var(--min)) * 1000, 1px) * 1000))", result);
        Assert.True(result.Length < css.Length);
    }

    [Theory]
    [InlineData("width: min(0px, max(1px, 2px))")]
    [InlineData("width: calc(1px + min(0px, 2px))")]
    [InlineData("width: round(nearest, 0%, 1%)")]
    [InlineData("width: mod(0px, 1px)")]
    [InlineData("width: rem(0px, 1px)")]
    [InlineData("width: abs(0px)")]
    [InlineData("opacity: sign(0px)")]
    [InlineData("opacity: sin(0deg)")]
    [InlineData("animation-delay: max(0s, 1s)")]
    [InlineData("width: c\\61 lc(1px + 0px)")]
    public void OptimizeCss_PreservesPotentialMathAndCustomValues(string declaration) {
        string css = ".example { " + declaration + "; }";
        string value = declaration.Substring(declaration.IndexOf(':') + 1).Trim();
        Assert.Contains(value, HtmlOptimizer.OptimizeCss(css));
    }

    [Fact]
    public void OptimizeCss_MinifiesAroundMathAndCustomProperties() {
        const string css = ":root { --length: 0px; --angle: 0deg; } .example { margin: 0px; color: red; width: clamp(0px, var(--length) + 1px, 10px); }";
        string result = HtmlOptimizer.OptimizeCss(css);
        Assert.Contains("--length: 0px", result);
        Assert.Contains("--angle: 0deg", result);
        Assert.Contains("clamp(0px, var(--length) + 1px, 10px)", result);
        Assert.Contains("margin:0", result);
        Assert.Contains("color:#f00", result);
        Assert.True(result.Length < css.Length);
    }

    [Fact]
    public void OptimizeCss_PreservesEscapedNamesAndCustomValueTokens() {
        const string css = ":root { \\2d \\2d length: 0px; --content: one\\;two; } .x { width: c\\61 lc(0px + 1px); margin: 0px; }";
        string result = HtmlOptimizer.OptimizeCss(css);
        Assert.Contains(" 0px", result);
        Assert.Contains("one\\;two", result);
        Assert.Contains("c\\61 lc(0px + 1px)", result);
        Assert.Contains("margin:0", result);
    }

    [Fact]
    public void OptimizeCss_PreservesCustomPropertiesAfterNestedRules() {
        const string css = ".a { & .b { color: red; } --length: 0px; width: calc(var(--length) + 1px); margin: 0px; }";
        string result = HtmlOptimizer.OptimizeCss(css);
        Assert.Contains("--length: 0px", result);
        Assert.Contains("calc(var(--length) + 1px)", result);
        Assert.Contains("margin:0", result);
    }

    [Fact]
    public void OptimizeCss_PreservesMathWithCrLfEscapeTerminator() {
        const string css = ".a { width: c\\61\r\nlc(0px + 1px); margin: 0px; }";
        string result = HtmlOptimizer.OptimizeCss(css);
        Assert.Contains("c\\61\r\nlc(0px + 1px)", result);
        Assert.Contains("margin:0", result);
    }

    [Fact]
    public void OptimizeCss_StillMinifiesOrdinaryStylesheets() {
        Assert.Equal(".example{margin:0;color:#f00}", HtmlOptimizer.OptimizeCss(".example { margin: 0px; color: red; }"));
    }

    [Fact]
    public void OptimizeHtml_PreservesEmbeddedAndInlineMath() {
        const string html = "<style>.x { width: min(0px, 1px); color: red; }</style><div style=\"width: clamp(0px, 2px, 3px); margin: 0px\"></div>";
        var result = HtmlOptimizer.OptimizeHtml(html, cssDecodeEscapes: false);

        Assert.Contains("min(0px, 1px)", result);
        Assert.Contains("clamp(0px, 2px, 3px)", result);
        Assert.Contains("color:#f00", result);
        Assert.Contains("margin:0", result);
    }

    [Fact]
    public void OptimizeHtml_PreservesMathInEntityEncodedAttributes() {
        const string html = "<div style=\"width: clamp&#40;0px, 2px + 1px, 3px&#41;\"></div>";
        var result = HtmlOptimizer.OptimizeHtml(html, cssDecodeEscapes: false);

        Assert.Contains("0px, 2px + 1px, 3px", result);
    }

    [Fact]
    public void OptimizeHtml_PreservesMathInNamedEntityEncodedAttributes() {
        const string html = "<div style=\"width: min&lpar;0px, 1px&rpar;; margin: 0px\"></div>";
        string result = HtmlOptimizer.OptimizeHtml(html, cssDecodeEscapes: false);
        Assert.Contains("min(0px, 1px)", result);
        Assert.Contains("margin:0", result);
    }

    [Fact]
    public void OptimizeHtml_MinifiesCssWhenOnlyOtherContentContainsGuardCharacters() {
        const string html = "<!-- note --><style>body { color: red; }</style><script>f();</script><div>One &amp; two</div>";
        var result = HtmlOptimizer.OptimizeHtml(html, cssDecodeEscapes: false);

        Assert.Contains("body{color:#f00}", result);
    }

    [Theory]
    [InlineData("<tr style=\"width: min(0px, 1px)\"><td>Example</td></tr>")]
    [InlineData("<td style=\"width: min&#40;0px, 1px&#41;\">Example</td>")]
    [InlineData("<col style=\"width: min(0px, 1px)\">")]
    [InlineData("<table></table><tr style=\"width: min(0px, 1px)\"><td>Example</td></tr>")]
    public void OptimizeHtml_PreservesMathInTableFragments(string html) {
        var result = HtmlOptimizer.OptimizeHtml(html, cssDecodeEscapes: false);
        Assert.Contains("0px", result);
    }

    [Fact]
    public void OptimizeHtml_PrettyPrintingPreservesCssStringContinuations() {
        const string html = "<style>.x { content: 'a\\\nb'; width: min(0px, 1px); }</style>";
        HtmlSettings settings = HtmlOptimizer.CreateDefaultHtmlSettings();
        settings.PrettyPrint = true;
        string result = HtmlOptimizer.OptimizeHtml(html, settings);
        Assert.Contains("'ab'", result);
        Assert.Contains("min(0px, 1px)", result);
    }

    [Fact]
    public void OptimizeHtml_StillRemovesScriptsOnMalformedInput() {
        HtmlSettings settings = HtmlOptimizer.CreateDefaultHtmlSettings();
        settings.RemoveJavaScript = true;
        string result = HtmlOptimizer.OptimizeHtml("<script>alert(1)</script><", settings);
        Assert.DoesNotContain("<script>", result);
    }
}
