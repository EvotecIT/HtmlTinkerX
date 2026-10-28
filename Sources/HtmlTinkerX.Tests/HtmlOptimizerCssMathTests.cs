using HtmlTinkerX;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlOptimizerCssMathTests {
    [Fact]
    public void OptimizeCss_PreservesNestedTerminalFontSize() {
        const string css = ".frame { font-size: min(var(--max), max(var(--fit), var(--min) - clamp(0px, (var(--fit) - var(--min)) * 1000, 1px) * 1000)); }";

        Assert.Equal(css, HtmlOptimizer.OptimizeCss(css));
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
    [InlineData("--length: 0px; width: calc(var(--length) + 1px)")]
    [InlineData("--angle: 0deg; rotate: calc(var(--angle) + 1deg)")]
    [InlineData("--length: 0px")]
    [InlineData("width: c\\61 lc(1px + 0px)")]
    public void OptimizeCss_PreservesPotentialMathAndCustomValues(string declaration) {
        string css = ".example { " + declaration + "; }";
        Assert.Equal(css, HtmlOptimizer.OptimizeCss(css));
    }

    [Fact]
    public void OptimizeCss_StillMinifiesOrdinaryStylesheets() {
        Assert.Equal(".example{margin:0;color:#f00}", HtmlOptimizer.OptimizeCss(".example { margin: 0px; color: red; }"));
    }

    [Fact]
    public void OptimizeHtml_PreservesEmbeddedAndInlineMath() {
        const string html = "<style>.x { width: min(0px, 1px); }</style><div style=\"width: clamp(0px, 2px, 3px)\"></div>";
        var result = HtmlOptimizer.OptimizeHtml(html, cssDecodeEscapes: false);

        Assert.Contains("min(0px, 1px)", result);
        Assert.Contains("clamp(0px, 2px, 3px)", result);
    }

    [Fact]
    public void OptimizeHtml_PreservesMathInEntityEncodedAttributes() {
        const string html = "<div style=\"width: clamp&#40;0px, 2px + 1px, 3px&#41;\"></div>";
        var result = HtmlOptimizer.OptimizeHtml(html, cssDecodeEscapes: false);

        Assert.Contains("0px, 2px + 1px, 3px", result);
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
    public void OptimizeHtml_PreservesMathInTableFragments(string html) {
        var result = HtmlOptimizer.OptimizeHtml(html, cssDecodeEscapes: false);
        Assert.Contains("0px", result);
    }
}
