using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using OfficeIMO.Markdown;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlCrawlerMarkdownTests {
    private static readonly Regex LocalhostPortRegex = new(@"http://localhost:\d+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static int GetFreePort() {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static HttpListener StartServer(Dictionary<string, string> responses, out string rootUrl) {
        int port = GetFreePort();
        rootUrl = $"http://localhost:{port}/";
        HttpListener listener = new();
        listener.Prefixes.Add(rootUrl);
        listener.Start();

        _ = Task.Run(async () => {
            try {
                while (listener.IsListening) {
                    HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
                    string key = context.Request.RawUrl ?? "/";
                    if (responses.TryGetValue(key, out string? html)) {
                        byte[] data = Encoding.UTF8.GetBytes(html);
                        context.Response.ContentType = "text/html; charset=utf-8";
                        context.Response.ContentLength64 = data.Length;
                        await context.Response.OutputStream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                    } else {
                        context.Response.StatusCode = 404;
                    }

                    context.Response.OutputStream.Close();
                }
            } catch (HttpListenerException) {
            } catch (ObjectDisposedException) {
            }
        });

        return listener;
    }

    [Fact]
    public async Task CrawlAsync_IncludeMarkdown_PopulatesMarkdownAndPersistsFile() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><head><title>Home</title></head><body><main><h1>Hello</h1><p>World with <strong>bold</strong> and <a href='/docs/start'>link</a>.</p><ul><li>One</li><li>Two</li></ul></main></body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        string outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                IncludeMarkdown = true,
                OutputPath = outputPath
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Contains("# Hello", page.Markdown);
            Assert.Contains("World", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("**bold**", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("[link](", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("http://localhost", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("- One", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("- Two", page.Markdown, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(page.MarkdownPath));
            Assert.True(File.Exists(page.MarkdownPath!));

            string manifest = File.ReadAllText(page.ManifestPath!);
            Assert.Contains("MarkdownPath", manifest, StringComparison.Ordinal);
        } finally {
            server.Stop();
            server.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public void ConvertToMarkdownDocument_UsesTypedOfficeImoAstForResponsiveImages() {
        const string html = """
<main>
  <figure>
    <picture>
      <source srcset="/img/hero.webp 1x, /img/hero@2x.webp 2x" type="image/webp" />
      <img alt="Hero" />
    </picture>
    <figcaption>Hero image</figcaption>
  </figure>
</main>
""";

        MarkdownDoc document = HtmlMarkdownConverterAdapter.ConvertToMarkdownDocument(html, "https://example.com/docs/start");

        var image = Assert.IsType<ImageBlock>(Assert.Single(document.Blocks));
        Assert.Equal("https://example.com/img/hero.webp", image.Path);
        Assert.Equal("Hero", image.Alt);
        Assert.Equal("Hero image", image.Caption);
    }

    [Fact]
    public void ConvertToMarkdownDocument_PreservesLinkedFigureHtmlMetadataInTypedAst() {
        const string html = """
<main>
  <figure>
    <a href="/docs/hero" title="Hero page" target="_blank" rel="nofollow sponsored">
      <img src="/img/hero.png" alt="Hero" title="View hero" />
    </a>
    <figcaption>Hero image</figcaption>
  </figure>
</main>
""";

        MarkdownDoc document = HtmlMarkdownConverterAdapter.ConvertToMarkdownDocument(html, "https://example.com/docs/start");

        var image = Assert.IsType<ImageBlock>(Assert.Single(document.Blocks));
        Assert.Equal("https://example.com/img/hero.png", image.Path);
        Assert.Equal("Hero", image.Alt);
        Assert.Equal("View hero", image.Title);
        Assert.Equal("https://example.com/docs/hero", image.LinkUrl);
        Assert.Equal("Hero page", image.LinkTitle);
        Assert.Equal("_blank", image.LinkTarget);
        Assert.Equal("nofollow sponsored", image.LinkRel);
        Assert.Equal("Hero image", image.Caption);

        string renderedHtml = document.ToHtmlFragment(new HtmlOptions { Style = HtmlStyle.Plain, CssDelivery = CssDelivery.None, BodyClass = null });
        Assert.Contains("<a href=\"https://example.com/docs/hero\" title=\"Hero page\" target=\"_blank\" rel=\"", renderedHtml, StringComparison.Ordinal);
        Assert.Contains("nofollow", renderedHtml, StringComparison.Ordinal);
        Assert.Contains("sponsored", renderedHtml, StringComparison.Ordinal);
        Assert.Contains("noopener", renderedHtml, StringComparison.Ordinal);
        Assert.Contains("noreferrer", renderedHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void ConvertToMarkdownDocument_PreservesInlineLinkHtmlMetadataInTypedAst() {
        const string html = """
<main>
  <p><a href="/docs/hero" title="Hero docs" target="_blank" rel="nofollow sponsored">Read more</a></p>
</main>
""";

        MarkdownDoc document = HtmlMarkdownConverterAdapter.ConvertToMarkdownDocument(html, "https://example.com/docs/start");

        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(document.Blocks));
        var link = Assert.IsType<LinkInline>(Assert.Single(paragraph.Inlines.Nodes));
        Assert.Equal("Read more", link.Text);
        Assert.Equal("https://example.com/docs/hero", link.Url);
        Assert.Equal("Hero docs", link.Title);
        Assert.Equal("_blank", link.LinkTarget);
        Assert.Equal("nofollow sponsored", link.LinkRel);

        string renderedHtml = document.ToHtmlFragment(new HtmlOptions { Style = HtmlStyle.Plain, CssDelivery = CssDelivery.None, BodyClass = null });
        Assert.Contains("<a href=\"https://example.com/docs/hero\" title=\"Hero docs\" target=\"_blank\" rel=\"", renderedHtml, StringComparison.Ordinal);
        Assert.Contains("nofollow", renderedHtml, StringComparison.Ordinal);
        Assert.Contains("sponsored", renderedHtml, StringComparison.Ordinal);
        Assert.Contains("noopener", renderedHtml, StringComparison.Ordinal);
        Assert.Contains("noreferrer", renderedHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void ConvertToMarkdownDocument_PreservesArtDirectedPictureSourcesInTypedAst() {
        string html = ReadFixture("publisher-art-direction-picture-article.html");

        MarkdownDoc document = HtmlMarkdownConverterAdapter.ConvertToMarkdownDocument(html, "https://example.com/world/live/storm-update.html");

        Assert.Collection(document.Blocks,
            block => Assert.Equal("Storm Update", Assert.IsType<HeadingBlock>(block).Text),
            block => {
                var image = Assert.IsType<ImageBlock>(block);
                Assert.Equal("https://example.com/news/2026/media/storm-wide.webp", image.Path);
                Assert.Equal("https://example.com/news/2026/media/storm-fallback.jpg", image.PictureFallbackPath);
                Assert.Collection(image.PictureSources,
                    source => {
                        Assert.Equal("https://example.com/news/2026/media/storm-wide.webp", source.Path);
                        Assert.Equal("https://example.com/news/2026/media/storm-wide.webp 1x, https://example.com/news/2026/media/storm-wide@2x.webp 2x", source.SrcSet);
                        Assert.Equal("(min-width: 960px)", source.Media);
                        Assert.Equal("image/webp", source.Type);
                        Assert.Equal("100vw", source.Sizes);
                    },
                    source => {
                        Assert.Equal("https://example.com/news/2026/media/storm-mobile.webp", source.Path);
                        Assert.Equal("https://example.com/news/2026/media/storm-mobile.webp 1x, https://example.com/news/2026/media/storm-mobile@2x.webp 2x", source.SrcSet);
                        Assert.Equal("(max-width: 959px)", source.Media);
                        Assert.Equal("image/webp", source.Type);
                        Assert.Equal("100vw", source.Sizes);
                    });
            });

        string renderedHtml = document.ToHtmlFragment(new HtmlOptions { Style = HtmlStyle.Plain, CssDelivery = CssDelivery.None, BodyClass = null });
        Assert.Contains("<picture>", renderedHtml, StringComparison.Ordinal);
        Assert.Contains("storm-fallback.jpg", renderedHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void ConvertToMarkdownDocument_PreservesCdnLazyPictureSourcesInTypedAst() {
        string html = ReadFixture("publisher-cdn-lazy-picture-article.html");

        MarkdownDoc document = HtmlMarkdownConverterAdapter.ConvertToMarkdownDocument(html, "https://example.com/world/live/storm-update.html");

        Assert.Collection(document.Blocks,
            block => Assert.Equal("Storm Update", Assert.IsType<HeadingBlock>(block).Text),
            block => {
                var image = Assert.IsType<ImageBlock>(block);
                Assert.Equal("https://cdn.example.net/images/storm-wide.avif", image.Path);
                Assert.Equal("https://example.com/news/2026/media/storm-fallback.jpg", image.PictureFallbackPath);
                Assert.Collection(image.PictureSources,
                    source => {
                        Assert.Equal("https://cdn.example.net/images/storm-wide.avif", source.Path);
                        Assert.Equal("https://cdn.example.net/images/storm-wide.avif 1x, https://cdn.example.net/images/storm-wide@2x.avif 2x", source.SrcSet);
                        Assert.Equal("(min-width: 960px)", source.Media);
                        Assert.Equal("image/avif", source.Type);
                        Assert.Equal("100vw", source.Sizes);
                    },
                    source => {
                        Assert.Equal("https://example.com/news/2026/media/storm-mobile.webp", source.Path);
                        Assert.Equal("https://example.com/news/2026/media/storm-mobile.webp 1x, https://example.com/news/2026/media/storm-mobile@2x.webp 2x", source.SrcSet);
                        Assert.Equal("(max-width: 959px)", source.Media);
                        Assert.Equal("image/webp", source.Type);
                        Assert.Equal("100vw", source.Sizes);
                    });
            });

        string renderedHtml = document.ToHtmlFragment(new HtmlOptions { Style = HtmlStyle.Plain, CssDelivery = CssDelivery.None, BodyClass = null });
        Assert.Contains("https://cdn.example.net/images/storm-wide.avif", renderedHtml, StringComparison.Ordinal);
        Assert.Contains("storm-fallback.jpg", renderedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("data:image/gif", renderedHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void ConvertToMarkdownDocument_PreservesWidthDescriptorPictureSourcesInTypedAst() {
        string html = ReadFixture("publisher-width-descriptor-picture-article.html");

        MarkdownDoc document = HtmlMarkdownConverterAdapter.ConvertToMarkdownDocument(html, "https://example.com/world/live/storm-update.html");

        Assert.Collection(document.Blocks,
            block => Assert.Equal("Storm Update", Assert.IsType<HeadingBlock>(block).Text),
            block => {
                var image = Assert.IsType<ImageBlock>(block);
                Assert.Equal("https://example.com/news/2026/media/storm-wide.webp?fit=cover&crop=10,20,300,400", image.Path);
                Assert.Equal("https://example.com/news/2026/media/storm-fallback.jpg?download=1", image.PictureFallbackPath);
                Assert.Collection(image.PictureSources,
                    source => {
                        Assert.Equal("https://example.com/news/2026/media/storm-wide.webp?fit=cover&crop=10,20,300,400", source.Path);
                        Assert.Equal("https://example.com/news/2026/media/storm-wide.webp?fit=cover&crop=10,20,300,400 640w, https://example.com/news/2026/media/storm-wide.webp?fit=cover&crop=20,40,600,800 1280w", source.SrcSet);
                        Assert.Equal("(min-width: 960px)", source.Media);
                        Assert.Equal("image/webp", source.Type);
                        Assert.Equal("(min-width: 960px) 90vw, 100vw", source.Sizes);
                    },
                    source => {
                        Assert.Equal("https://example.com/news/2026/media/storm-mobile.webp?fit=cover&crop=5,10,200,250", source.Path);
                        Assert.Equal("https://example.com/news/2026/media/storm-mobile.webp?fit=cover&crop=5,10,200,250 320w, https://example.com/news/2026/media/storm-mobile.webp?fit=cover&crop=10,20,400,500 640w", source.SrcSet);
                        Assert.Equal("(max-width: 959px)", source.Media);
                        Assert.Equal("image/webp", source.Type);
                        Assert.Equal("100vw", source.Sizes);
                    });
            });

        string renderedHtml = document.ToHtmlFragment(new HtmlOptions { Style = HtmlStyle.Plain, CssDelivery = CssDelivery.None, BodyClass = null });
        Assert.Contains("<source srcset=\"https://example.com/news/2026/media/storm-wide.webp?fit=cover&amp;crop=10,20,300,400 640w, https://example.com/news/2026/media/storm-wide.webp?fit=cover&amp;crop=20,40,600,800 1280w\" media=\"(min-width: 960px)\" type=\"image/webp\" sizes=\"(min-width: 960px) 90vw, 100vw\" />", renderedHtml, StringComparison.Ordinal);
        Assert.Contains("<source srcset=\"https://example.com/news/2026/media/storm-mobile.webp?fit=cover&amp;crop=5,10,200,250 320w, https://example.com/news/2026/media/storm-mobile.webp?fit=cover&amp;crop=10,20,400,500 640w\" media=\"(max-width: 959px)\" type=\"image/webp\" sizes=\"100vw\" />", renderedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("%20", renderedHtml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("publisher-linked-picture-article.html", "publisher-linked-picture-article.html.snapshot.txt")]
    [InlineData("publisher-noscript-linked-picture-article.html", "publisher-noscript-linked-picture-article.html.snapshot.txt")]
    [InlineData("publisher-art-direction-picture-article.html", "publisher-art-direction-picture-article.html.snapshot.txt")]
    [InlineData("publisher-width-descriptor-picture-article.html", "publisher-width-descriptor-picture-article.html.snapshot.txt")]
    public void ConvertToMarkdownDocument_RendersPublisherFigureFixtureToStableHtmlSnapshot(string fixtureFileName, string snapshotFileName) {
        AssertRenderedHtmlSnapshot(fixtureFileName, snapshotFileName);
    }

    [Fact]
    public void ConvertToMarkdownDocument_UsesDocumentBaseHrefForRelativeResources() {
        const string html = """
<html>
  <head><base href="/static/docs/" /></head>
  <body>
    <main>
      <p><a href="guide/start">Docs</a></p>
      <figure><img src="img/hero.png" alt="Hero" /></figure>
    </main>
  </body>
</html>
""";

        MarkdownDoc document = HtmlMarkdownConverterAdapter.ConvertToMarkdownDocument(html, "https://example.com/app/page.html");

        var paragraph = Assert.IsType<ParagraphBlock>(document.Blocks[0]);
        string markdown = document.ToMarkdown();
        Assert.Contains("[Docs](https://example.com/static/docs/guide/start)", markdown, StringComparison.Ordinal);

        var image = Assert.IsType<ImageBlock>(document.Blocks[1]);
        Assert.Equal("https://example.com/static/docs/img/hero.png", image.Path);
        Assert.Equal("Hero", image.Alt);
    }

    [Fact]
    public void ConvertToMarkdownDocument_HandlesMalformedFigureMarkupWithoutThrowing() {
        const string html = """
<main>
  <figure>
    <picture>
      <source srcset="/img/hero.webp 1x, /img/hero@2x.webp 2x"
      <img src="/img/hero-fallback.png" alt="Hero"
    </picture>
    <figcaption>Hero image
  </figure>
</main>
""";

        MarkdownDoc document = HtmlMarkdownConverterAdapter.ConvertToMarkdownDocument(html, "https://example.com/docs/start");

        var image = Assert.IsType<ImageBlock>(Assert.Single(document.Blocks));
        Assert.Equal("https://example.com/img/hero.webp", image.Path);
        Assert.Contains("https://example.com/img/hero.webp", document.ToMarkdown(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CrawlAsync_IncludeMarkdown_UsesResponsivePictureSourceForCrawlerMarkdown() {
        Dictionary<string, string> responses = new() {
            ["/"] = """
<html>
  <head><title>Docs</title></head>
  <body>
    <main>
      <h1>Responsive</h1>
      <figure>
        <picture>
          <source srcset="/img/hero.webp 1x, /img/hero@2x.webp 2x" type="image/webp" />
          <img alt="Hero" />
        </picture>
        <figcaption>Hero image</figcaption>
      </figure>
    </main>
  </body>
</html>
"""
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        string outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                IncludeMarkdown = true,
                OutputPath = outputPath
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Contains("![Hero](http://localhost", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("hero.webp", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("_Hero image_", page.Markdown, StringComparison.Ordinal);
        } finally {
            server.Stop();
            server.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CrawlAsync_IncludeMarkdown_HonorsDocumentBaseHrefForRelativeLinksAndImages() {
        Dictionary<string, string> responses = new() {
            ["/"] = """
<html>
  <head>
    <title>Docs</title>
    <base href="/assets/" />
  </head>
  <body>
    <main>
      <h1>Base href</h1>
      <p><a href="guide/start">Docs</a></p>
      <figure><img src="img/hero.png" alt="Hero" /></figure>
    </main>
  </body>
</html>
"""
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        string outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                IncludeMarkdown = true,
                OutputPath = outputPath
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Contains("[Docs](http://localhost", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("/assets/guide/start", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("![Hero](http://localhost", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("/assets/img/hero.png", page.Markdown, StringComparison.Ordinal);
        } finally {
            server.Stop();
            server.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CrawlAsync_IncludeMarkdown_PreservesSupplementalFigureBlocks() {
        Dictionary<string, string> responses = new() {
            ["/"] = """
<html>
  <head><title>Figure notes</title></head>
  <body>
    <main>
      <h1>Figure notes</h1>
      <figure>
        <img src="/img/hero.png" alt="Hero" />
        <figcaption>Hero image</figcaption>
        <p>Photo credit: Team</p>
      </figure>
    </main>
  </body>
</html>
"""
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        string outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                IncludeMarkdown = true,
                OutputPath = outputPath
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Contains("![Hero](http://localhost", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("_Hero image_", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("Photo credit: Team", page.Markdown, StringComparison.Ordinal);
        } finally {
            server.Stop();
            server.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CrawlAsync_IncludeMarkdown_PreservesLinkedFigureMedia() {
        Dictionary<string, string> responses = new() {
            ["/"] = """
<html>
  <head><title>Linked figure</title></head>
  <body>
    <main>
      <h1>Linked figure</h1>
      <figure>
        <a href="/docs/hero" title="Hero page">
          <img src="/img/hero.png" alt="Hero" title="View hero" />
        </a>
        <figcaption>Hero image</figcaption>
      </figure>
    </main>
  </body>
</html>
"""
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        string outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                IncludeMarkdown = true,
                OutputPath = outputPath
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Contains("[![Hero](http://localhost", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("](http://localhost", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("/docs/hero", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("\"Hero page\")", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("_Hero image_", page.Markdown, StringComparison.Ordinal);
        } finally {
            server.Stop();
            server.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CrawlAsync_IncludeMarkdown_PreservesWrappedLinkedPictureFigureMedia() {
        Dictionary<string, string> responses = new() {
            ["/"] = """
<html>
  <head><title>Wrapped linked figure</title></head>
  <body>
    <main>
      <h1>Wrapped linked figure</h1>
      <figure>
        <div class="figure-media">
          <a href="/docs/hero">
            <picture>
              <source srcset="/img/hero.webp 1x, /img/hero@2x.webp 2x" />
              <img alt="Hero" />
            </picture>
          </a>
        </div>
        <figcaption>Hero image</figcaption>
        <p>Photo credit: Team</p>
      </figure>
    </main>
  </body>
</html>
"""
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        string outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                IncludeMarkdown = true,
                OutputPath = outputPath
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Contains("[![Hero](http://localhost", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("hero.webp", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("](http://localhost", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("/docs/hero", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("_Hero image_", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("Photo credit: Team", page.Markdown, StringComparison.Ordinal);
        } finally {
            server.Stop();
            server.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CrawlAsync_IncludeMarkdown_PreservesAnchorWrappedImageFigureMedia() {
        Dictionary<string, string> responses = new() {
            ["/"] = """
<html>
  <head><title>Anchor wrapped figure</title></head>
  <body>
    <main>
      <h1>Anchor wrapped figure</h1>
      <figure>
        <a href="/docs/hero">
          <span class="media-frame">
            <img src="/img/hero.png" alt="Hero" title="View hero" />
          </span>
        </a>
        <figcaption>Hero image</figcaption>
        <p>Photo credit: Team</p>
      </figure>
    </main>
  </body>
</html>
"""
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        string outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                IncludeMarkdown = true,
                OutputPath = outputPath
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Contains("[![Hero](http://localhost", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("hero.png", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("](http://localhost", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("/docs/hero", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("_Hero image_", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("Photo credit: Team", page.Markdown, StringComparison.Ordinal);
        } finally {
            server.Stop();
            server.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CrawlAsync_IncludeMarkdown_ProcessesPublisherFixtureThroughArticleSelector() {
        Dictionary<string, string> responses = new() {
            ["/"] = ReadFixture("publisher-linked-picture-article.html")
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        string outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "article.story",
                IncludeMarkdown = true,
                OutputPath = outputPath
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Contains("# Storm Update", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("[briefing](http://localhost", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("/news/2026/briefing.html", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("[![Flooded street at dawn](http://localhost", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("/news/2026/media/storm-center.webp", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("](http://localhost", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("/news/2026/gallery/storm-center", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("\"Open photo\")", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("_Residents navigate floodwater after the overnight storm._", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("Photo credit: City Desk", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("[flood map](http://localhost", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("/news/maps/flood-zones.html", page.Markdown, StringComparison.Ordinal);
        } finally {
            server.Stop();
            server.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("publisher-linked-picture-article.html", "publisher-linked-picture-article.markdown.snapshot.txt")]
    [InlineData("publisher-noscript-linked-picture-article.html", "publisher-noscript-linked-picture-article.markdown.snapshot.txt")]
    [InlineData("publisher-art-direction-picture-article.html", "publisher-art-direction-picture-article.markdown.snapshot.txt")]
    [InlineData("publisher-width-descriptor-picture-article.html", "publisher-width-descriptor-picture-article.markdown.snapshot.txt")]
    public async Task CrawlAsync_IncludeMarkdown_ProcessesPublisherFixtureThroughArticleSelectorSnapshot(string fixtureFileName, string snapshotFileName) {
        Dictionary<string, string> responses = new() {
            ["/"] = ReadFixture(fixtureFileName)
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        string outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "article.story",
                IncludeMarkdown = true,
                OutputPath = outputPath
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            AssertMarkdownSnapshot(snapshotFileName, page.Markdown);
        } finally {
            server.Stop();
            server.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CrawlAsync_IncludeMarkdown_ProcessesPublisherNoscriptFixtureThroughArticleSelector() {
        Dictionary<string, string> responses = new() {
            ["/"] = ReadFixture("publisher-noscript-linked-picture-article.html")
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        string outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "article.story",
                IncludeMarkdown = true,
                OutputPath = outputPath
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Contains("# Storm Update", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("[![Flooded street at dawn](http://localhost", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("/news/2026/media/storm-center.webp", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("](http://localhost", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("/news/2026/gallery/storm-center", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("\"Open photo\")", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("_Residents navigate floodwater after the overnight storm._", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("Photo credit: City Desk", page.Markdown, StringComparison.Ordinal);
            Assert.DoesNotContain("data:image/gif", page.Markdown, StringComparison.Ordinal);
        } finally {
            server.Stop();
            server.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }


    [Fact]
    public async Task CrawlAsync_IncludeMarkdown_ProcessesPublisherCdnLazyPictureFixtureThroughArticleSelector() {
        Dictionary<string, string> responses = new() {
            ["/"] = ReadFixture("publisher-cdn-lazy-picture-article.html")
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        string outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "article.story",
                IncludeMarkdown = true,
                OutputPath = outputPath
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Contains("# Storm Update", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("https://cdn.example.net/images/storm-wide.avif", page.Markdown, StringComparison.Ordinal);
            Assert.Contains("_Residents navigate floodwater after the overnight storm._", page.Markdown, StringComparison.Ordinal);
            Assert.DoesNotContain("data:image/gif", page.Markdown, StringComparison.Ordinal);
        } finally {
            server.Stop();
            server.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    private static string ReadFixture(string fileName) {
        string path = Path.Combine(GetTestsProjectRoot(), "Fixtures", fileName);
        return File.ReadAllText(path);
    }

    // Set HTMLTINKERX_UPDATE_SNAPSHOTS=1 to rewrite checked-in baselines after an intentional renderer change.
    private static void AssertMarkdownSnapshot(string fileName, string actualMarkdown) {
        string path = Path.Combine(GetTestsProjectRoot(), "Fixtures", "Expected", fileName);
        string normalized = NormalizeMarkdown(actualMarkdown);

        if (string.Equals(Environment.GetEnvironmentVariable("HTMLTINKERX_UPDATE_SNAPSHOTS"), "1", StringComparison.Ordinal)) {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, normalized + Environment.NewLine);
            return;
        }

        string expected = File.ReadAllText(path);
        Assert.Equal(NormalizeMarkdown(expected), normalized);
    }

    // HTML snapshots use the same update switch so markdown and rendered-output baselines stay in sync.
    private static void AssertRenderedHtmlSnapshot(string fixtureFileName, string snapshotFileName) {
        string html = ReadFixture(fixtureFileName);
        MarkdownDoc document = HtmlMarkdownConverterAdapter.ConvertToMarkdownDocument(html, "https://example.com/world/live/storm-update.html");
        string renderedHtml = document.ToHtmlFragment(new HtmlOptions { Style = HtmlStyle.Plain, CssDelivery = CssDelivery.None, BodyClass = null });
        AssertHtmlSnapshot(snapshotFileName, renderedHtml);
    }

    private static void AssertHtmlSnapshot(string fileName, string actualHtml) {
        string path = Path.Combine(GetTestsProjectRoot(), "Fixtures", "Expected", fileName);
        string normalized = NormalizeHtml(actualHtml);

        if (string.Equals(Environment.GetEnvironmentVariable("HTMLTINKERX_UPDATE_SNAPSHOTS"), "1", StringComparison.Ordinal)) {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, normalized + Environment.NewLine);
            return;
        }

        string expected = File.ReadAllText(path);
        Assert.Equal(NormalizeHtml(expected), normalized);
    }

    private static string NormalizeMarkdown(string markdown) {
        return LocalhostPortRegex.Replace(markdown.Replace("\r\n", "\n").TrimEnd('\n'), "http://localhost");
    }

    private static string NormalizeHtml(string html) {
        if (string.IsNullOrWhiteSpace(html)) {
            return string.Empty;
        }

        html = LocalhostPortRegex.Replace(html, "http://localhost");

        var sb = new StringBuilder(html.Length);
        bool inTag = false;
        bool lastWasWhitespace = false;

        for (int i = 0; i < html.Length; i++) {
            char ch = html[i];
            if (ch == '<') {
                if (!inTag && lastWasWhitespace && sb.Length > 0 && sb[sb.Length - 1] != '>') {
                    sb.Append(' ');
                }

                inTag = true;
                lastWasWhitespace = false;
                sb.Append(ch);
                continue;
            }

            if (ch == '>') {
                inTag = false;
                lastWasWhitespace = false;
                sb.Append(ch);
                continue;
            }

            if (inTag) {
                sb.Append(ch);
                continue;
            }

            if (char.IsWhiteSpace(ch)) {
                lastWasWhitespace = true;
                continue;
            }

            if (lastWasWhitespace && sb.Length > 0) {
                sb.Append(' ');
            }

            lastWasWhitespace = false;
            sb.Append(ch);
        }

        return sb.ToString()
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Trim();
    }

    private static string GetTestsProjectRoot() {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null) {
            string candidate = Path.Combine(dir.FullName, "HtmlTinkerX.Tests.csproj");
            if (File.Exists(candidate)) {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate HtmlTinkerX.Tests project root from test runtime base directory.");
    }
}
