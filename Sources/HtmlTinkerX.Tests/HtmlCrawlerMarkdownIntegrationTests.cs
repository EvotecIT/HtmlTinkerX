using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using OfficeIMO.Markdown;
using OfficeIMO.Markdown.Html;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerMarkdownTests {

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
            DisposeListenerSafely(server);
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
            DisposeListenerSafely(server);
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
            DisposeListenerSafely(server);
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
            DisposeListenerSafely(server);
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
            DisposeListenerSafely(server);
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
            DisposeListenerSafely(server);
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
            DisposeListenerSafely(server);
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
            DisposeListenerSafely(server);
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
            DisposeListenerSafely(server);
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
            DisposeListenerSafely(server);
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
