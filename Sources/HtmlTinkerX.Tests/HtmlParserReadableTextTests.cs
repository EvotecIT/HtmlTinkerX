using HtmlTinkerX;

namespace HtmlTinkerX.Tests;

public class HtmlParserReadableTextTests {
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
