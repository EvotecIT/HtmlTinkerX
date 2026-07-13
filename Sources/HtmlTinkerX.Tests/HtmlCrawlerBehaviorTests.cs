using HtmlTinkerX;
using Microsoft.Playwright;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Linq;
using System.Threading;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {

    [Fact]
    public async Task CrawlAsync_RejectsInvalidResponseLimitsBeforeFetching() {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => HtmlCrawler.CrawlAsync(
            "https://example.com/",
            new HtmlCrawlOptions { MaximumPageResponseBytes = 0 }));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => HtmlCrawler.CrawlAsync(
            "https://example.com/",
            new HtmlCrawlOptions { MaximumAssetResponseBytes = 0 }));
    }

    [Fact]
    public async Task CrawlAsync_RecordsOversizedStaticPageAsFailed() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><body>this page is intentionally larger than the configured response limit</body></html>"
        };

        using HttpListener server = StartServer(responses, out string rootUrl);
        HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
            MaxDepth = 0,
            MaxPages = 1,
            RespectRobotsTxt = false,
            UseSitemaps = false,
            MaximumPageResponseBytes = 32
        });

        HtmlCrawlPage page = Assert.Single(result.Pages);
        Assert.Equal(HtmlCrawlPageStatus.Failed, page.Status);
        Assert.Contains("32-byte limit", page.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CrawlAsync_AppliesPageResponseLimitToSitemaps() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><body>home</body></html>",
            ["/sitemap.xml"] = "<urlset><url><loc>/from-sitemap</loc></url>" + new string(' ', 128) + "</urlset>",
            ["/from-sitemap"] = "<html><body>mapped</body></html>"
        };

        using HttpListener server = StartServer(responses, out string rootUrl);
        HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
            MaxDepth = 0,
            MaxPages = 5,
            RespectRobotsTxt = false,
            UseSitemaps = true,
            MaximumPageResponseBytes = 64
        });

        Assert.DoesNotContain(result.Pages, page => page.Url == rootUrl + "from-sitemap");
        Assert.Contains(rootUrl + "sitemap.xml", result.SitemapUrls);
    }

    [Fact]
    public void ShouldRetryWithRendering_DetectsThinJavascriptShells() {
        HtmlCrawlPage page = new() {
            Status = HtmlCrawlPageStatus.Success,
            Html = "<html><body><div id='app'></div><script src='/runtime.js'></script><script src='/main.js'></script><script src='/vendor.js'></script><script src='/chunk-a.js'></script><script src='/chunk-b.js'></script><script src='/chunk-c.js'></script></body></html>",
            Text = string.Empty
        };

        HtmlCrawlOptions options = new() {
            AutoRenderTextWordThreshold = 20
        };

        HtmlCrawler.AutoRenderDecision decision = HtmlCrawler.EvaluateAutoRender(page, options);
        Assert.True(decision.ShouldRender);
        Assert.Contains("JavaScript shell", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HtmlCrawlRenderReasonCode.AutoRenderJavaScriptShell, decision.ReasonCode);
        Assert.True(HtmlCrawler.ShouldRetryWithRendering(page, options));
    }

    [Fact]
    public void ShouldRetryWithRendering_KeepsRichStaticPagesStatic() {
        HtmlCrawlPage page = new() {
            Status = HtmlCrawlPageStatus.Success,
            Html = "<html><body><main><h1>Hello</h1><p>This page already contains enough static content to avoid a browser fallback.</p></main></body></html>",
            Text = "Hello This page already contains enough static content to avoid a browser fallback."
        };

        HtmlCrawlOptions options = new() {
            AutoRenderTextWordThreshold = 8
        };

        HtmlCrawler.AutoRenderDecision decision = HtmlCrawler.EvaluateAutoRender(page, options);
        Assert.False(decision.ShouldRender);
        Assert.Contains("meeting", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HtmlCrawlRenderReasonCode.StaticThresholdMet, decision.ReasonCode);
        Assert.False(HtmlCrawler.ShouldRetryWithRendering(page, options));
    }

    [Fact]
    public void HtmlCrawlSummary_AggregatesRenderModesAndReasonCodes() {
        HtmlCrawlResult result = new() {
            StartUrl = "https://example.com/",
            Started = DateTimeOffset.UtcNow.AddSeconds(-2),
            Finished = DateTimeOffset.UtcNow,
            Pages = {
                new HtmlCrawlPage {
                    Url = "https://example.com/",
                    Status = HtmlCrawlPageStatus.Success,
                    Rendered = false,
                    RenderMode = HtmlCrawlRenderMode.Static,
                    RenderReasonCode = HtmlCrawlRenderReasonCode.StaticRenderDisabled,
                    RenderReason = "Kept static because browser rendering was not enabled.",
                    AppliedInteractions = { "Dismissed text: Accept" },
                    OfflineDependencyDiagnostics = {
                        new HtmlCrawlOfflineDependencyDiagnostic {
                            Kind = "fetch-api",
                            Evidence = "fetch('/api/status')"
                        }
                    },
                    Text = "Hello world",
                    Started = DateTimeOffset.UtcNow.AddSeconds(-2),
                    Finished = DateTimeOffset.UtcNow.AddSeconds(-1)
                },
                new HtmlCrawlPage {
                    Url = "https://example.com/app",
                    Status = HtmlCrawlPageStatus.Success,
                    Rendered = true,
                    RenderMode = HtmlCrawlRenderMode.AutoRendered,
                    RenderReasonCode = HtmlCrawlRenderReasonCode.AutoRenderJavaScriptShell,
                    RenderReason = "Auto-render triggered because the static HTML looked like a JavaScript shell container.",
                    AppliedInteractions = { "Clicked text [1]: Load more", "Clicked text [2]: Load more" },
                    OfflineDependencyDiagnostics = {
                        new HtmlCrawlOfflineDependencyDiagnostic {
                            Kind = "observed-fetch-api",
                            Evidence = "https://example.com/api/items"
                        },
                        new HtmlCrawlOfflineDependencyDiagnostic {
                            Kind = "observed-cross-origin-runtime",
                            Evidence = "https://api.example.net/v1/items"
                        }
                    },
                    Text = "Rendered content",
                    Started = DateTimeOffset.UtcNow.AddSeconds(-1),
                    Finished = DateTimeOffset.UtcNow
                }
            }
        };

        HtmlCrawlSummary summary = result.Summary;

        Assert.Equal(1, summary.RenderModeCounts["Static"]);
        Assert.Equal(1, summary.RenderModeCounts["AutoRendered"]);
        Assert.Equal(1, summary.RenderReasonCounts["StaticRenderDisabled"]);
        Assert.Equal(1, summary.RenderReasonCounts["AutoRenderJavaScriptShell"]);
        Assert.Equal(1, summary.AutoRenderedPageCount);
        Assert.Equal(2, summary.InteractedPageCount);
        Assert.Equal(3, summary.InteractionCount);
        Assert.Equal(2, summary.OfflineRiskPageCount);
        Assert.Equal(3, summary.OfflineRiskDiagnosticCount);
        Assert.Equal(1, summary.HighOfflineRiskPageCount);
        Assert.Equal("live-dependent", summary.OfflineReadinessGrade);
        Assert.Equal(1, summary.OfflineReadinessCounts["partial"]);
        Assert.Equal(1, summary.OfflineReadinessCounts["live-dependent"]);
        Assert.Equal(1, summary.OfflineReadinessCountsByState["Success:partial"]);
        Assert.Equal(1, summary.OfflineReadinessCountsByState["Success:live-dependent"]);
        Assert.Equal(1, summary.OfflineDependencyKinds["fetch-api"]);
        Assert.Equal(1, summary.OfflineDependencyKinds["observed-fetch-api"]);
        Assert.Equal(1, summary.OfflineDependencyKinds["observed-cross-origin-runtime"]);
        Assert.Equal(2, summary.OfflineDependencySeverityCounts["warning"]);
        Assert.Equal(1, summary.OfflineDependencySeverityCounts["high"]);
        Assert.Equal(1, summary.InteractionCounts["Dismissed text: Accept"]);
        Assert.Equal(1, summary.InteractionCounts["Clicked text [1]: Load more"]);
        string report = summary.ToReportText(result.SitemapUrls);
        Assert.Contains("Render summary:", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Render mode AutoRendered: 1", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Render reason AutoRenderJavaScriptShell: 1", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Interaction summary:", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Applied interactions: 3", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Offline-risk pages: 2", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("High offline-risk pages: 1", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Offline readiness grade: live-dependent", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Offline readiness:", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Offline grade partial: 1", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Offline state Success:live-dependent: 1", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Offline severity high: 1", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Offline dependency observed-fetch-api: 1", report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CrawlAsync_SummaryAndIndex_ExposeHiddenContentAndMarkdownGuidance() {
        string outputPath = Path.Combine(Path.GetTempPath(), "HtmlCrawlerGuidanceTests", Guid.NewGuid().ToString("N"));
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><head><title>Home</title></head><body><main><h1>Hello</h1><p>World</p></main></body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                IncludeMarkdown = true,
                OutputPath = outputPath,
                HiddenContentMode = HtmlCrawlHiddenContentMode.RespectHidden,
                MarkdownImageMode = OfficeIMO.Markdown.MarkdownImageRenderingMode.PortableMarkdown,
                ListingCardMetadataMode = OfficeIMO.Markdown.Html.HtmlListingCardMetadataMode.SuppressInRepeatedCards
            });

            HtmlCrawlSummary summary = result.Summary;
            Assert.Equal(HtmlCrawlHiddenContentMode.RespectHidden, summary.HiddenContentMode);
            Assert.Equal(OfficeIMO.Markdown.MarkdownImageRenderingMode.PortableMarkdown, summary.MarkdownImageMode);
            Assert.Equal(OfficeIMO.Markdown.Html.HtmlListingCardMetadataMode.SuppressInRepeatedCards, summary.ListingCardMetadataMode);
            Assert.Contains(summary.GuidanceNotes, note => note.Contains("static mode", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(summary.GuidanceNotes, note => note.Contains("portable output", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(summary.GuidanceNotes, note => note.Contains("listing-card", StringComparison.OrdinalIgnoreCase));

            string summaryText = File.ReadAllText(result.SummaryTextPath!);
            Assert.Contains("Hidden-content mode: RespectHidden", summaryText, StringComparison.Ordinal);
            Assert.Contains("Markdown image mode: PortableMarkdown", summaryText, StringComparison.Ordinal);
            Assert.Contains("Listing-card metadata mode: SuppressInRepeatedCards", summaryText, StringComparison.Ordinal);
            Assert.Contains("static mode", summaryText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("portable output", summaryText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("listing-card", summaryText, StringComparison.OrdinalIgnoreCase);

            string indexHtml = File.ReadAllText(result.IndexHtmlPath!);
            Assert.Contains("Extraction Settings", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Hidden-content mode: <code>RespectHidden</code>", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Markdown image mode: <code>PortableMarkdown</code>", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Listing-card metadata mode: <code>SuppressInRepeatedCards</code>", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Guidance", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("external CSS", indexHtml, StringComparison.OrdinalIgnoreCase);
        } finally {
            DisposeListenerSafely(server);
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CrawlAsync_UsesSitemapsAndRespectsRobotsTxt() {
        Dictionary<string, string> responses = new();
        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            responses["/"] = "<html><head><title>Home</title></head><body>Home</body></html>";
            responses["/robots.txt"] = $"User-agent: *\nDisallow: /blocked\nSitemap: {rootUrl}sitemap.xml\n";
            responses["/sitemap.xml"] =
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">" +
                $"<url><loc>{rootUrl}from-sitemap</loc></url>" +
                $"<url><loc>{rootUrl}blocked</loc></url>" +
                "</urlset>";
            responses["/from-sitemap"] = "<html><head><title>Mapped</title></head><body>Mapped page</body></html>";
            responses["/blocked"] = "<html><head><title>Blocked</title></head><body>Blocked page</body></html>";

            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 10,
                UseSitemaps = true,
                RespectRobotsTxt = true
            });

            Assert.Equal(2, result.PageCount);
            Assert.Contains(result.Pages, page => page.Url == rootUrl);
            Assert.Contains(result.Pages, page => page.Url == rootUrl + "from-sitemap");
            Assert.Contains(result.SitemapUrls, url => url == rootUrl + "sitemap.xml");
            Assert.Contains(result.SkippedPages, page => page.Url == rootUrl + "blocked" && page.SkipReason == HtmlCrawlSkipReason.DisallowedByRobots);
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_IgnoreRobotsTxt_AllowsDisallowedSitemapPage() {
        Dictionary<string, string> responses = new();
        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            responses["/"] = "<html><head><title>Home</title></head><body>Home</body></html>";
            responses["/robots.txt"] = $"User-agent: *\nDisallow: /blocked\nSitemap: {rootUrl}sitemap.xml\n";
            responses["/sitemap.xml"] =
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">" +
                $"<url><loc>{rootUrl}blocked</loc></url>" +
                "</urlset>";
            responses["/blocked"] = "<html><head><title>Blocked</title></head><body>Blocked page</body></html>";

            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 10,
                UseSitemaps = true,
                RespectRobotsTxt = false
            });

            Assert.Equal(2, result.PageCount);
            Assert.Contains(result.Pages, page => page.Url == rootUrl + "blocked");
            Assert.DoesNotContain(result.SkippedPages, page => page.Url == rootUrl + "blocked");
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_CanPersistAndResumePendingPages() {
        Dictionary<string, string> responses = new();
        HttpListener server = StartServer(responses, out string rootUrl);
        string outputPath = Path.Combine(Path.GetTempPath(), "HtmlCrawlerTests", Guid.NewGuid().ToString("N"));
        try {
            responses["/"] = "<html><head><title>Home</title></head><body>Home</body></html>";
            responses["/robots.txt"] = $"User-agent: *\nSitemap: {rootUrl}sitemap.xml\n";
            responses["/sitemap.xml"] =
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">" +
                $"<url><loc>{rootUrl}from-sitemap</loc></url>" +
                "</urlset>";
            responses["/from-sitemap"] = "<html><head><title>Mapped</title></head><body>Mapped page</body></html>";

            HtmlCrawlResult partial = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                UseSitemaps = true,
                OutputPath = outputPath
            });

            Assert.Single(partial.Pages);
            Assert.NotEmpty(partial.PendingPages);
            Assert.True(File.Exists(Path.Combine(outputPath, "crawl-result.json")));

            HtmlCrawlResult resumed = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 10,
                ResumePath = outputPath,
                OutputPath = outputPath
            });

            Assert.Equal(2, resumed.PageCount);
            Assert.Contains(resumed.Pages, page => page.Url == rootUrl + "from-sitemap");
            Assert.Empty(resumed.PendingPages);
            Assert.Contains(resumed.Pages, page => !string.IsNullOrEmpty(page.HtmlPath) && File.Exists(page.HtmlPath));

            HtmlCrawlResult loaded = await HtmlCrawler.LoadResultAsync(outputPath);
            Assert.Equal(resumed.PageCount, loaded.PageCount);
            Assert.True(File.Exists(Path.Combine(outputPath, "pages.jsonl")));
            Assert.True(File.Exists(Path.Combine(outputPath, "pages.csv")));
            Assert.True(File.Exists(Path.Combine(outputPath, "skipped-pages.jsonl")));
            Assert.True(File.Exists(Path.Combine(outputPath, "links.jsonl")));
            Assert.True(File.Exists(Path.Combine(outputPath, "summary.json")));
            Assert.True(File.Exists(Path.Combine(outputPath, "summary.txt")));

            string summaryText = File.ReadAllText(Path.Combine(outputPath, "summary.txt"));
            Assert.Contains("Sitemap sources:", summaryText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(rootUrl + "sitemap.xml", summaryText, StringComparison.OrdinalIgnoreCase);
        } finally {
            DisposeListenerSafely(server);
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CrawlAsync_CanRestrictToPathPrefix() {
        Dictionary<string, string> responses = new() {
            ["/docs/index"] = "<html><body><a href='/docs/page-1'>Page 1</a><a href='/blog/post-1'>Blog</a></body></html>",
            ["/docs/page-1"] = "<html><body>Docs page</body></html>",
            ["/blog/post-1"] = "<html><body>Blog page</body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        string startUrl = rootUrl + "docs/index";
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(startUrl, new HtmlCrawlOptions {
                MaxDepth = 1,
                MaxPages = 10,
                PathPrefix = "/docs"
            });

            Assert.Equal(2, result.PageCount);
            Assert.Contains(result.Pages, page => page.Url == startUrl);
            Assert.Contains(result.Pages, page => page.Url == rootUrl + "docs/page-1");
            Assert.Contains(result.SkippedPages, page => page.Url == rootUrl + "blog/post-1" && page.SkipReason == HtmlCrawlSkipReason.OutsidePathScope);
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_CanPreferCanonicalUrls() {
        Dictionary<string, string> responses = new() {
            ["/landing"] = $"<html><head><title>Landing</title><link rel='canonical' href='{"/docs/home"}' /></head><body>Landing</body></html>",
            ["/docs/home"] = "<html><head><title>Home</title></head><body>Home</body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl + "landing", new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                UseCanonicalUrls = true
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Equal(rootUrl + "docs/home", page.Url);
            Assert.Equal(rootUrl + "landing", page.RequestedUrl);
            Assert.Equal(rootUrl + "docs/home", page.CanonicalUrl);
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_CanSkipDuplicateContentPages() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><body><a href='/copy-a'>Copy A</a><a href='/copy-b'>Copy B</a><a href='/unique'>Unique</a></body></html>",
            ["/copy-a"] = "<html><head><title>Copy A</title></head><body><main><h1>Same</h1><p>Duplicate body</p></main></body></html>",
            ["/copy-b"] = "<html><head><title>Copy B</title></head><body><main><h1>Same</h1><p>Duplicate body</p></main></body></html>",
            ["/unique"] = "<html><head><title>Unique</title></head><body><main><h1>Different</h1></main></body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 1,
                MaxPages = 10,
                Selector = "main",
                DeduplicatePages = true
            });

            Assert.Equal(3, result.PageCount);
            Assert.Contains(result.Pages, page => page.Url == rootUrl + "copy-a");
            Assert.DoesNotContain(result.Pages, page => page.Url == rootUrl + "copy-b");
            Assert.Contains(result.Pages, page => page.Url == rootUrl + "unique");
            Assert.Contains(result.SkippedPages, page => page.Url == rootUrl + "copy-b" && page.SkipReason == HtmlCrawlSkipReason.DuplicateContent);
            Assert.Equal(1, result.Summary.DuplicatePageCount);
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_IgnoresTrackingQueryParametersDuringNormalization() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><body><a href='/page?utm_source=newsletter'>Tracked A</a><a href='/page?fbclid=12345'>Tracked B</a></body></html>",
            ["/page?utm_source=newsletter"] = "<html><head><title>Tracked</title></head><body>Tracked page</body></html>",
            ["/page?fbclid=12345"] = "<html><head><title>Tracked</title></head><body>Tracked page</body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 1,
                MaxPages = 10
            });

            Assert.Equal(2, result.PageCount);
            Assert.Contains(result.Pages, page => page.Url == rootUrl);
            Assert.Contains(result.Pages, page => page.Url == rootUrl + "page");
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_SkipsKnownAssetUrlsByDefault() {
        int port = GetFreePort();
        string rootUrl = $"http://localhost:{port}/";
        HttpListener listener = new();
        listener.Prefixes.Add(rootUrl);
        listener.Start();

        _ = Task.Run(async () => {
            try {
                while (listener.IsListening) {
                    HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
                    string key = context.Request.RawUrl ?? "/";
                    if (key == "/") {
                        string body = "<html><body><a href='/file.pdf'>PDF</a></body></html>";
                        byte[] data = Encoding.UTF8.GetBytes(body);
                        context.Response.ContentType = "text/html; charset=utf-8";
                        context.Response.ContentLength64 = data.Length;
                        await context.Response.OutputStream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                        context.Response.OutputStream.Close();
                    } else {
                        context.Response.StatusCode = 404;
                        context.Response.OutputStream.Close();
                        continue;
                    }
                }
            } catch (HttpListenerException) {
            } catch (ObjectDisposedException) {
            }
        });

        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 1,
                MaxPages = 10
            });

            Assert.Single(result.Pages);
            Assert.Contains(result.SkippedPages, page => page.Url == rootUrl + "file.pdf" && page.SkipReason == HtmlCrawlSkipReason.AssetPath);
        } finally {
            listener.Stop();
            listener.Close();
        }
    }
}
