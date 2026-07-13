using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Fact]
    public async Task CrawlAsync_SmartCleanupOptOut_PreservesBoilerplateButStillFiltersHiddenContent() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><body><main><header>Preserved header</header><nav>Preserved navigation</nav><article><h1>Visible</h1><p>Visible article text.</p></article><div hidden>Hidden secret</div></main></body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                IncludeHtml = true,
                IncludeText = true,
                IncludeMarkdown = true,
                SmartContentCleanup = false,
                HiddenContentMode = HtmlCrawlHiddenContentMode.RespectHidden
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Contains("Preserved header", page.Html, StringComparison.Ordinal);
            Assert.Contains("Preserved navigation", page.Text, StringComparison.Ordinal);
            Assert.Contains("Preserved navigation", page.Markdown, StringComparison.Ordinal);
            Assert.DoesNotContain("Hidden secret", page.Html, StringComparison.Ordinal);
            Assert.DoesNotContain("Hidden secret", page.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("Hidden secret", page.Markdown, StringComparison.Ordinal);
        } finally {
            DisposeListenerSafely(server);
        }
    }
}
