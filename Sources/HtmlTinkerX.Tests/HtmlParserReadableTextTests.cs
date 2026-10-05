using HtmlTinkerX;

namespace HtmlTinkerX.Tests;

public class HtmlParserReadableTextTests {
    [Theory]
    [InlineData("pliki")]
    [InlineData("plików")]
    public void ExtractReadableText_RemovesConsentSignalWhenPhraseStartsInAnotherSubtree(string prefix) {
        string html = "<span>" + prefix + " </span><div id='target'>cookies alpha beta gamma delta epsilon zeta eta theta iota kappa lambda</div>"
            + "<article id='real'>real article alpha beta gamma delta epsilon zeta eta theta iota kappa</article>";

        foreach (string? preferredSelector in new string?[] { null, "#target" }) {
            var result = HtmlParserToText.ExtractReadableText(html, preferredSelector);
            Assert.Equal("article#real", result.SelectorHint);
            Assert.Equal("real article alpha beta gamma delta epsilon zeta eta theta iota kappa", result.Text);
            Assert.Equal(72, result.Score);
            Assert.Equal(1, result.CandidateCount);
        }
    }

    [Theory]
    [InlineData("alpha<b>beta</b><em>'</em><span>gamma</span> delta", 2)]
    [InlineData("alpha<b>---</b><span>beta</span>", 1)]
    [InlineData("<b>---</b><span>beta</span>", 1)]
    [InlineData("alpha<b>---</b><span> </span>beta", 2)]
    [InlineData("ą<b>ę</b><span>²</span> ١٢", 2)]
    [InlineData("alpha<b>\u0301</b>beta", 2)]
    public void ExtractReadableText_PreservesWordScoringAcrossInlineElements(string content, double expectedScore) {
        var result = HtmlParserToText.ExtractReadableText("<section id='target'>" + content + "</section>", "#target");
        Assert.Equal(expectedScore + 15, result.Score);
        Assert.Equal("section#target", result.SelectorHint);
        Assert.Equal(1, result.CandidateCount);
    }

    [Theory]
    [InlineData("prefix", "suffix")]
    [InlineData("prefix", " ")]
    [InlineData(" ", "suffix")]
    [InlineData(" ", " ")]
    public void ExtractReadableText_PreservesKeywordBoundariesAtPreferredSubtreeEdges(string before, string after) {
        var result = HtmlParserToText.ExtractReadableText("<main>" + before + "<span id='target'>down<b>load</b></span>" + after + "</main>", "#target");
        Assert.Equal(46, result.Score);
        Assert.Equal("span#target", result.SelectorHint);
    }

    [Fact]
    public void ExtractReadableText_PreservesDescendantAndMetadataScoringForPreferredElements() {
        var anchor = HtmlParserToText.ExtractReadableText("<a id='target' href='/file'>download</a>", "#target");
        var paragraph = HtmlParserToText.ExtractReadableText("<p id='target'>alpha beta</p>", "#target");
        var metadataSeparated = HtmlParserToText.ExtractReadableText("<div id='strona'>główna</div>", "#strona");
        var metadataPhrase = HtmlParserToText.ExtractReadableText("<div id='target' aria-label='strona'>główna</div>", "#target");
        Assert.Equal(46, anchor.Score);
        Assert.Equal(2, paragraph.Score);
        Assert.Equal(1, metadataSeparated.Score);
        Assert.Equal(-34, metadataPhrase.Score);
    }

    [Fact]
    public void ExtractReadableText_PrefersArticleContentOverNavigation() {
        const string html = """
<html>
  <head><title>Site shell</title></head>
  <body>
    <header>Home Search Menu Contact</header>
    <nav><a href="/">Home</a><a href="/a">Alpha</a><a href="/b">Beta</a><a href="/c">Gamma</a></nav>
    <main>
      <section class="sidebar"><a href="/one">One</a><a href="/two">Two</a><a href="/three">Three</a></section>
      <article id="notice">
        <h1>Road works notice</h1>
        <p>The public road will be rebuilt during summer.</p>
        <p>Attachments include a map PDF and schedule XLSX.</p>
      </article>
    </main>
    <footer>Privacy Cookies Footer</footer>
  </body>
</html>
""";

        HtmlReadableTextResult result = HtmlParserToText.ExtractReadableText(html);

        Assert.Equal("Road works notice", result.Title);
        Assert.Contains("public road", result.Text);
        Assert.Contains("map PDF", result.Text);
        Assert.DoesNotContain("Privacy Cookies Footer", result.Text);
        Assert.DoesNotContain("Alpha Beta Gamma", result.Text);
        Assert.Equal("article#notice", result.SelectorHint);
    }

    [Fact]
    public void ExtractReadableText_UsesPreferredSelectorWhenProvided() {
        const string html = """
<html>
  <head><title>Site shell</title></head>
  <body>
    <main>
      <div class="site-title">BIP City</div>
      <div id="article-content-print">
        <h3>Budget resolution</h3>
        <p>Resolution details and attachment list.</p>
      </div>
    </main>
  </body>
</html>
""";

        HtmlReadableTextResult result = HtmlParserToText.ExtractReadableText(html, "#article-content-print");

        Assert.Equal("Budget resolution", result.Title);
        Assert.Contains("Resolution details", result.Text);
        Assert.Equal("div#article-content-print", result.SelectorHint);
    }

    [Fact]
    public void ExtractReadableText_UsesMetadataWhenReadableDomIsEmpty() {
        const string html = """
<html>
  <head>
    <title>BIP City</title>
    <meta property="og:title" content="Budget opinion resolution" />
    <meta name="description" content="Regional chamber opinion about planned city debt." />
  </head>
  <body>
    <script>self.__next_f.push([1, "article text rendered by framework"])</script>
  </body>
</html>
""";

        HtmlReadableTextResult result = HtmlParserToText.ExtractReadableText(html, "#article-content-print");

        Assert.Equal("Budget opinion resolution", result.Title);
        Assert.Equal("Regional chamber opinion about planned city debt.", result.Text);
        Assert.Equal(0, result.CandidateCount);
    }
}