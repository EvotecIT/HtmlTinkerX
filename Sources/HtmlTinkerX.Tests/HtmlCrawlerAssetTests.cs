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
    public async Task CrawlAsync_CanDownloadDiscoveredAssets() {
        int port = GetFreePort();
        string rootUrl = $"http://localhost:{port}/";
        string outputPath = Path.Combine(Path.GetTempPath(), "HtmlCrawlerAssetTests", Guid.NewGuid().ToString("N"));
        HttpListener listener = new();
        listener.Prefixes.Add(rootUrl);
        listener.Start();

        _ = Task.Run(async () => {
            try {
                while (listener.IsListening) {
                    HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
                    string key = context.Request.RawUrl ?? "/";
                    string body;
                    string contentType;
                    switch (key) {
                        case "/":
                            body = "<html><head><link rel='stylesheet' href='/css/site.css' /><style>.hero{background-image:url('/images/bg.png');}</style></head><body><img src='/images/logo.png' alt='Logo' /><div style=\"background-image:url('/images/card.png')\"></div><a href='/files/manual.pdf'>Manual</a></body></html>";
                            contentType = "text/html; charset=utf-8";
                            break;
                        case "/css/site.css":
                            body = "@import '/css/theme.css'; .page{background-image:url('/images/logo.png'); color:#333;}";
                            contentType = "text/css";
                            break;
                        case "/css/theme.css":
                            body = ".theme{background-image:url('/images/bg.png');}";
                            contentType = "text/css";
                            break;
                        case "/images/logo.png":
                            body = "fake-png";
                            contentType = "image/png";
                            break;
                        case "/images/bg.png":
                            body = "fake-bg";
                            contentType = "image/png";
                            break;
                        case "/images/card.png":
                            body = "fake-card";
                            contentType = "image/png";
                            break;
                        case "/files/manual.pdf":
                            body = "%PDF-1.7 fake";
                            contentType = "application/pdf";
                            break;
                        default:
                            context.Response.StatusCode = 404;
                            context.Response.OutputStream.Close();
                            continue;
                    }

                    byte[] data = Encoding.UTF8.GetBytes(body);
                    context.Response.ContentType = contentType;
                    context.Response.ContentLength64 = data.Length;
                    await context.Response.OutputStream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                    context.Response.OutputStream.Close();
                }
            } catch (HttpListenerException) {
            } catch (ObjectDisposedException) {
            }
        });

        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 5,
                DownloadAssets = true,
                ContentMode = HtmlCrawlContentMode.Raw,
                OutputPath = outputPath
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Equal(5, page.AssetUrls.Count);
            Assert.Equal(6, result.AssetCount);
            Assert.All(result.Assets, asset => Assert.True(File.Exists(asset.FilePath)));
            Assert.All(result.Pages, crawledPage => {
                if (!string.IsNullOrWhiteSpace(crawledPage.HtmlPath)) {
                    Assert.StartsWith(Path.GetFullPath(outputPath), Path.GetFullPath(crawledPage.HtmlPath!), StringComparison.OrdinalIgnoreCase);
                }

                if (!string.IsNullOrWhiteSpace(crawledPage.TextPath)) {
                    Assert.StartsWith(Path.GetFullPath(outputPath), Path.GetFullPath(crawledPage.TextPath!), StringComparison.OrdinalIgnoreCase);
                }

                Assert.StartsWith(Path.GetFullPath(outputPath), Path.GetFullPath(crawledPage.ManifestPath!), StringComparison.OrdinalIgnoreCase);
            });
            Assert.All(result.Assets, asset => Assert.StartsWith(Path.GetFullPath(Path.Combine(outputPath, "assets")), Path.GetFullPath(asset.FilePath!), StringComparison.OrdinalIgnoreCase));
            Assert.True(File.Exists(Path.Combine(outputPath, "assets.jsonl")));
            Assert.True(File.Exists(Path.Combine(outputPath, "skipped-assets.jsonl")));
            Assert.True(File.Exists(result.IndexHtmlPath));
            string persistedHtml = File.ReadAllText(page.HtmlPath!);
            Assert.Contains("../assets/", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("site-", persistedHtml, StringComparison.OrdinalIgnoreCase);
            HtmlCrawlAsset stylesheet = Assert.Single(result.Assets, asset => asset.Url == rootUrl + "css/site.css");
            Assert.Contains(result.Assets, asset => asset.Url == rootUrl + "css/theme.css");
            string persistedCss = File.ReadAllText(stylesheet.FilePath!);
            Assert.DoesNotContain("/images/logo.png", persistedCss, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/css/theme.css", persistedCss, StringComparison.OrdinalIgnoreCase);
            string indexHtml = File.ReadAllText(result.IndexHtmlPath!);
            Assert.Contains("Pages CSV", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("assets/site-", indexHtml, StringComparison.OrdinalIgnoreCase);
        } finally {
            listener.Stop();
            listener.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CrawlAsync_RewritesInternalPageLinksToLocalFiles() {
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><body><a href='/about'>About</a><a href='https://example.com/remote'>Remote</a></body></html>",
            ["/about"] = "<html><body>About page</body></html>"
        };

        string outputPath = Path.Combine(Path.GetTempPath(), "HtmlCrawlerPageLinkTests", Guid.NewGuid().ToString("N"));
        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 1,
                MaxPages = 10,
                OutputPath = outputPath
            });

            HtmlCrawlPage home = result.Pages.Single(page => page.Url == rootUrl);
            HtmlCrawlPage about = result.Pages.Single(page => page.Url == rootUrl + "about");
            string persistedHtml = File.ReadAllText(home.HtmlPath!);
            string indexHtml = File.ReadAllText(result.IndexHtmlPath!);
            string expectedRelative = BuildRelativeForTest(home.HtmlPath!, about.HtmlPath!);

            Assert.Contains(expectedRelative, persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("https://example.com/remote", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(Path.GetFileName(home.HtmlPath!), indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(Path.GetFileName(about.HtmlPath!), indexHtml, StringComparison.OrdinalIgnoreCase);
        } finally {
            DisposeListenerSafely(server);
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CrawlAsync_HonorsBaseHrefForLinksAssetsAndOfflineRewrite() {
        string outputPath = Path.Combine(Path.GetTempPath(), "HtmlCrawlerBaseHrefTests", Guid.NewGuid().ToString("N"));
        HttpListener listener = new();
        string rootUrl;
        {
            int port = GetFreePort();
            rootUrl = $"http://localhost:{port}/";
            listener.Prefixes.Add(rootUrl);
        }
        listener.Start();

        _ = Task.Run(async () => {
            try {
                while (listener.IsListening) {
                    HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
                    string key = context.Request.RawUrl ?? "/";
                    string body;
                    string contentType;
                    switch (key) {
                        case "/":
                            body = "<html><head><title>Offline Home</title><base href='/docs/' /><link rel='stylesheet' href='css/site.css' /></head><body><h1>Offline Home</h1><p>Useful docs for offline testing and local search metadata.</p><a href='guide'>Guide</a><a href='manual.pdf'>Manual</a><a href='https://example.com/offsite'>Offsite</a><img src='images/logo.png' alt='Logo' /><script>fetch('/api/status')</script></body></html>";
                            contentType = "text/html; charset=utf-8";
                            break;
                        case "/docs/guide":
                            body = "<html><body><h1>Guide page</h1><p>Guide content for offline browsing.</p></body></html>";
                            contentType = "text/html; charset=utf-8";
                            break;
                        case "/docs/css/site.css":
                            body = ".hero{background-image:url('../images/logo.png');}";
                            contentType = "text/css";
                            break;
                        case "/docs/images/logo.png":
                            body = "fake-png";
                            contentType = "image/png";
                            break;
                        default:
                            context.Response.StatusCode = 404;
                            context.Response.OutputStream.Close();
                            continue;
                    }

                    byte[] data = Encoding.UTF8.GetBytes(body);
                    context.Response.ContentType = contentType;
                    context.Response.ContentLength64 = data.Length;
                    await context.Response.OutputStream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                    context.Response.OutputStream.Close();
                }
            } catch (HttpListenerException) {
            } catch (ObjectDisposedException) {
            }
        });

        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 1,
                MaxPages = 10,
                DownloadAssets = true,
                OutputPath = outputPath
            });

            HtmlCrawlPage home = result.Pages.Single(page => page.Url == rootUrl);
            HtmlCrawlPage guide = result.Pages.Single(page => page.Url == rootUrl + "docs/guide");
            string chunksJson = File.ReadAllText(result.ChunksJsonlPath!);
            string graphJson = File.ReadAllText(result.GraphJsonPath!);
            Assert.Contains(home.Links, link => string.Equals(link, rootUrl + "docs/guide", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(home.Links, link => string.Equals(link, rootUrl + "docs/manual.pdf", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(home.Links, link => string.Equals(link, "https://example.com/offsite", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(home.AssetUrls, asset => string.Equals(asset, rootUrl + "docs/css/site.css", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(home.AssetUrls, asset => string.Equals(asset, rootUrl + "docs/images/logo.png", StringComparison.OrdinalIgnoreCase));

            string persistedHtml = File.ReadAllText(home.HtmlPath!);
            string manifestJson = File.ReadAllText(home.ManifestPath!);
            using JsonDocument manifest = JsonDocument.Parse(manifestJson);
            using JsonDocument graph = JsonDocument.Parse(graphJson);
            string indexHtml = File.ReadAllText(result.IndexHtmlPath!);
            Assert.DoesNotContain("<base", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(BuildRelativeForTest(home.HtmlPath!, guide.HtmlPath!), persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("../assets/", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(result.ChunksJsonlPath));
            Assert.True(File.Exists(result.GraphJsonPath));
            Assert.True(result.Summary.ChunkCount >= 1);
            Assert.Equal(1, result.SkippedContentPageCount);
            Assert.Equal(1, result.SkippedAssetCount);
            Assert.Equal(1, result.Summary.SkippedContentPageCount);
            Assert.Equal(1, result.Summary.SkippedAssetCount);
            Assert.True(File.Exists(result.SkippedAssetsJsonlPath));
            Assert.Equal(4, result.GraphNodeCount);
            Assert.Equal(3, result.GraphEdgeCount);
            Assert.Equal(2, result.GraphFetchedNodeCount);
            Assert.Equal(1, result.GraphSkippedNodeCount);
            Assert.Equal(1, result.GraphExternalNodeCount);
            Assert.Equal(4, result.Summary.GraphNodeCount);
            Assert.Equal(3, result.Summary.GraphEdgeCount);
            Assert.Equal(2, result.Summary.GraphFetchedNodeCount);
            Assert.Equal(1, result.Summary.GraphSkippedNodeCount);
            Assert.Equal(1, result.Summary.GraphExternalNodeCount);
            Assert.Equal(2, result.GraphNodeCategories["Fetched"]);
            Assert.Equal(1, result.GraphNodeCategories["Skipped"]);
            Assert.Equal(1, result.GraphNodeCategories["External"]);
            Assert.Equal(1, result.GraphEdgeRelations["fetched"]);
            Assert.Equal(1, result.GraphEdgeRelations["skipped"]);
            Assert.Equal(1, result.GraphEdgeRelations["external"]);
            Assert.Equal(1, result.GraphSkippedNodeReasons[HtmlCrawlSkipReason.AssetPath.ToString()]);
            Assert.Equal(2, result.Summary.GraphNodeCategories["Fetched"]);
            Assert.Equal(1, result.Summary.GraphEdgeRelations["external"]);
            Assert.Equal(1, result.Summary.GraphSkippedNodeReasons[HtmlCrawlSkipReason.AssetPath.ToString()]);
            Assert.Equal(Path.GetFileName(home.HtmlPath), manifest.RootElement.GetProperty("PageFiles").GetProperty("HtmlPath").GetString());
            Assert.Equal(HtmlCrawlContentMode.Focused.ToString(), manifest.RootElement.GetProperty("Extraction").GetProperty("ContentModeUsed").GetString());
            Assert.Equal(HtmlCrawlContentSelectionReasonCode.FocusedFullDocumentFallback.ToString(), manifest.RootElement.GetProperty("Extraction").GetProperty("ContentSelectionReasonCode").GetString());
            Assert.True(manifest.RootElement.GetProperty("Extraction").GetProperty("ContentElementSelectorHint").ValueKind is JsonValueKind.Null or JsonValueKind.Undefined);
            Assert.Equal("Offline Home", manifest.RootElement.GetProperty("Search").GetProperty("Headings")[0].GetString());
            Assert.True(manifest.RootElement.GetProperty("Search").GetProperty("WordCount").GetInt32() >= 8);
            Assert.True(manifest.RootElement.GetProperty("Search").GetProperty("ChunkCount").GetInt32() >= 1);
            Assert.Contains("Useful docs for offline testing", manifest.RootElement.GetProperty("Search").GetProperty("Summary").GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains(manifest.RootElement.GetProperty("Search").GetProperty("Keywords").EnumerateArray(), item =>
                string.Equals(item.GetString(), "offline", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(manifest.RootElement.GetProperty("Search").GetProperty("Keywords").EnumerateArray(), item =>
                string.Equals(item.GetString(), "testing", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("partial", manifest.RootElement.GetProperty("OfflineReadinessGrade").GetString());
            Assert.Equal("warning", manifest.RootElement.GetProperty("HighestOfflineRiskSeverity").GetString());
            Assert.Equal(1, manifest.RootElement.GetProperty("OfflineDependencyDiagnosticCount").GetInt32());
            Assert.Equal("fetch-api", manifest.RootElement.GetProperty("OfflineDependencyKindsSummary").GetString());
            Assert.Contains(manifest.RootElement.GetProperty("Links").EnumerateArray(), item =>
                string.Equals(item.GetProperty("Url").GetString(), rootUrl + "docs/guide", StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.GetProperty("LocalPagePath").GetString(), Path.GetFileName(guide.HtmlPath), StringComparison.OrdinalIgnoreCase));
            Assert.Contains(manifest.RootElement.GetProperty("ReferencedAssets").EnumerateArray(), item =>
                string.Equals(item.GetProperty("Url").GetString(), rootUrl + "docs/images/logo.png", StringComparison.OrdinalIgnoreCase)
                && item.GetProperty("LocalFilePath").GetString()!.StartsWith("../assets/", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("\"ChunkId\"", chunksJson, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"ManifestPath\":\"pages/", chunksJson.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"Keywords\":[\"offline\"", chunksJson, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"OfflineReadinessGrade\":\"partial\"", chunksJson, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"OfflineDependencyKindsSummary\":\"fetch-api\"", chunksJson, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(4, graph.RootElement.GetProperty("Summary").GetProperty("NodeCount").GetInt32());
            Assert.Equal(3, graph.RootElement.GetProperty("Summary").GetProperty("EdgeCount").GetInt32());
            Assert.Equal(1, graph.RootElement.GetProperty("Summary").GetProperty("OfflineRiskNodeCount").GetInt32());
            Assert.Equal(0, graph.RootElement.GetProperty("Summary").GetProperty("HighOfflineRiskNodeCount").GetInt32());
            Assert.Equal(1, graph.RootElement.GetProperty("Summary").GetProperty("OfflineReadinessCounts").GetProperty("partial").GetInt32());
            Assert.Equal(1, graph.RootElement.GetProperty("Summary").GetProperty("OfflineReadinessCounts").GetProperty("ready").GetInt32());
            Assert.Equal(2, graph.RootElement.GetProperty("Summary").GetProperty("OfflineReadinessCounts").GetProperty("not-assessed").GetInt32());
            Assert.Equal(1, graph.RootElement.GetProperty("Summary").GetProperty("OfflineSeverityCounts").GetProperty("warning").GetInt32());
            Assert.Equal(1, graph.RootElement.GetProperty("Summary").GetProperty("OfflineDependencyKindCounts").GetProperty("fetch-api").GetInt32());
            Assert.Equal(4, graph.RootElement.GetProperty("Nodes").GetArrayLength());
            Assert.Equal(3, graph.RootElement.GetProperty("Edges").GetArrayLength());
            Assert.Contains(graph.RootElement.GetProperty("Nodes").EnumerateArray(), node =>
                string.Equals(node.GetProperty("Url").GetString(), rootUrl, StringComparison.OrdinalIgnoreCase)
                && string.Equals(node.GetProperty("Category").GetString(), "Fetched", StringComparison.OrdinalIgnoreCase)
                && string.Equals(node.GetProperty("OfflineReadinessGrade").GetString(), "partial", StringComparison.OrdinalIgnoreCase)
                && string.Equals(node.GetProperty("OfflineDependencyKindsSummary").GetString(), "fetch-api", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(graph.RootElement.GetProperty("Nodes").EnumerateArray(), node =>
                string.Equals(node.GetProperty("Url").GetString(), rootUrl + "docs/manual.pdf", StringComparison.OrdinalIgnoreCase)
                && string.Equals(node.GetProperty("Category").GetString(), "Skipped", StringComparison.OrdinalIgnoreCase)
                && string.Equals(node.GetProperty("SkipReason").GetString(), HtmlCrawlSkipReason.AssetPath.ToString(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(node.GetProperty("OfflineReadinessGrade").GetString(), "not-assessed", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(graph.RootElement.GetProperty("Nodes").EnumerateArray(), node =>
                string.Equals(node.GetProperty("Url").GetString(), "https://example.com/offsite", StringComparison.OrdinalIgnoreCase)
                && string.Equals(node.GetProperty("Category").GetString(), "External", StringComparison.OrdinalIgnoreCase)
                && string.Equals(node.GetProperty("OfflineReadinessGrade").GetString(), "not-assessed", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(graph.RootElement.GetProperty("Edges").EnumerateArray(), edge =>
                string.Equals(edge.GetProperty("SourceUrl").GetString(), rootUrl, StringComparison.OrdinalIgnoreCase)
                && string.Equals(edge.GetProperty("TargetUrl").GetString(), rootUrl + "docs/guide", StringComparison.OrdinalIgnoreCase)
                && string.Equals(edge.GetProperty("Relation").GetString(), "fetched", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(graph.RootElement.GetProperty("Edges").EnumerateArray(), edge =>
                string.Equals(edge.GetProperty("TargetUrl").GetString(), rootUrl + "docs/manual.pdf", StringComparison.OrdinalIgnoreCase)
                && string.Equals(edge.GetProperty("Relation").GetString(), "skipped", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(graph.RootElement.GetProperty("Edges").EnumerateArray(), edge =>
                string.Equals(edge.GetProperty("TargetUrl").GetString(), "https://example.com/offsite", StringComparison.OrdinalIgnoreCase)
                && string.Equals(edge.GetProperty("Relation").GetString(), "external", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("Offline Home", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Useful docs for offline testing", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("keywords: offline", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Render Summary", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Content Summary", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("FocusedFullDocumentFallback", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Render mode", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("StaticRenderDisabled", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Chunks JSONL", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Graph JSON", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Graph Summary", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Node category", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Edge relation", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Skipped-node reason", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Offline Readiness", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Skipped Pages", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Skipped Assets", indexHtml, StringComparison.OrdinalIgnoreCase);
        } finally {
            listener.Stop();
            listener.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CrawlAsync_Downloads_And_Rewrites_Script_Assets_To_LocalPaths() {
        string outputPath = Path.Combine(Path.GetTempPath(), "HtmlCrawlerScriptAssetTests", Guid.NewGuid().ToString("N"));
        HttpListener listener = new();
        StartListenerWithFreePort(listener, out string rootUrl);

        _ = Task.Run(async () => {
            try {
                while (listener.IsListening) {
                    HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
                    string key = context.Request.RawUrl ?? "/";
                    string body;
                    string contentType;
                    switch (key) {
                        case "/":
                            body = "<html><head><title>Offline App</title></head><body><main><h1>Offline App</h1><p>Scripts should be mirrored locally.</p><script src='/app/runtime.js'></script><script src='/app/main.js'></script></main></body></html>";
                            contentType = "text/html; charset=utf-8";
                            break;
                        case "/app/runtime.js":
                            body = "window.__runtimeLoaded = true;";
                            contentType = "application/javascript";
                            break;
                        case "/app/main.js":
                            body = "window.__mainLoaded = (window.__runtimeLoaded === true);";
                            contentType = "application/javascript";
                            break;
                        default:
                            context.Response.StatusCode = 404;
                            context.Response.OutputStream.Close();
                            continue;
                    }

                    byte[] data = Encoding.UTF8.GetBytes(body);
                    context.Response.ContentType = contentType;
                    context.Response.ContentLength64 = data.Length;
                    await context.Response.OutputStream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                    context.Response.OutputStream.Close();
                }
            } catch (HttpListenerException) {
            } catch (ObjectDisposedException) {
            }
        });

        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 5,
                DownloadAssets = true,
                OutputPath = outputPath
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Contains(page.AssetUrls, asset => string.Equals(asset, rootUrl + "app/runtime.js", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(page.AssetUrls, asset => string.Equals(asset, rootUrl + "app/main.js", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.Assets, asset => string.Equals(asset.Url, rootUrl + "app/runtime.js", StringComparison.OrdinalIgnoreCase) && File.Exists(asset.FilePath));
            Assert.Contains(result.Assets, asset => string.Equals(asset.Url, rootUrl + "app/main.js", StringComparison.OrdinalIgnoreCase) && File.Exists(asset.FilePath));

            string persistedHtml = File.ReadAllText(page.HtmlPath!);
            Assert.Contains("<script src=\"../assets/", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/app/runtime.js", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/app/main.js", persistedHtml, StringComparison.OrdinalIgnoreCase);
        } finally {
            listener.Stop();
            listener.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CrawlAsync_Downloads_And_Rewrites_Lazy_Image_Attributes_To_LocalPaths() {
        string outputPath = Path.Combine(Path.GetTempPath(), "HtmlCrawlerLazyAssetTests", Guid.NewGuid().ToString("N"));
        HttpListener listener = new();
        string rootUrl;
        {
            int port = GetFreePort();
            rootUrl = $"http://localhost:{port}/";
            listener.Prefixes.Add(rootUrl);
        }
        listener.Start();

        _ = Task.Run(async () => {
            try {
                while (listener.IsListening) {
                    HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
                    string key = context.Request.RawUrl ?? "/";
                    byte[] data;
                    string contentType;
                    switch (key) {
                        case "/":
                            data = Encoding.UTF8.GetBytes("""
                                <html>
                                <head><title>Offline Lazy Media</title></head>
                                <body>
                                  <main>
                                    <picture>
                                      <source data-srcset="/media/hero.webp 1x, /media/hero@2x.webp 2x" type="image/webp" />
                                      <img src="data:image/svg+xml,%3Csvg%20xmlns='http://www.w3.org/2000/svg'%20viewBox='0%200%20320%20180'%3E%3C/svg%3E"
                                           data-src="/media/hero.jpg"
                                           data-srcset="/media/hero-small.jpg 1x, /media/hero.jpg 2x"
                                           alt="Lazy hero" />
                                    </picture>
                                  </main>
                                </body>
                                </html>
                                """);
                            contentType = "text/html; charset=utf-8";
                            break;
                        case "/media/hero.webp":
                        case "/media/hero@2x.webp":
                            data = Encoding.UTF8.GetBytes("fake-webp");
                            contentType = "image/webp";
                            break;
                        case "/media/hero-small.jpg":
                        case "/media/hero.jpg":
                            data = Encoding.UTF8.GetBytes("fake-jpg");
                            contentType = "image/jpeg";
                            break;
                        default:
                            context.Response.StatusCode = 404;
                            context.Response.OutputStream.Close();
                            continue;
                    }

                    context.Response.ContentType = contentType;
                    context.Response.ContentLength64 = data.Length;
                    await context.Response.OutputStream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                    context.Response.OutputStream.Close();
                }
            } catch (HttpListenerException) {
            } catch (ObjectDisposedException) {
            }
        });

        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 5,
                DownloadAssets = true,
                OutputPath = outputPath
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Contains(page.AssetUrls, asset => string.Equals(asset, rootUrl + "media/hero.webp", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(page.AssetUrls, asset => string.Equals(asset, rootUrl + "media/hero@2x.webp", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(page.AssetUrls, asset => string.Equals(asset, rootUrl + "media/hero-small.jpg", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(page.AssetUrls, asset => string.Equals(asset, rootUrl + "media/hero.jpg", StringComparison.OrdinalIgnoreCase));

            Assert.Contains(result.Assets, asset => string.Equals(asset.Url, rootUrl + "media/hero.webp", StringComparison.OrdinalIgnoreCase) && File.Exists(asset.FilePath));
            Assert.Contains(result.Assets, asset => string.Equals(asset.Url, rootUrl + "media/hero@2x.webp", StringComparison.OrdinalIgnoreCase) && File.Exists(asset.FilePath));
            Assert.Contains(result.Assets, asset => string.Equals(asset.Url, rootUrl + "media/hero-small.jpg", StringComparison.OrdinalIgnoreCase) && File.Exists(asset.FilePath));
            Assert.Contains(result.Assets, asset => string.Equals(asset.Url, rootUrl + "media/hero.jpg", StringComparison.OrdinalIgnoreCase) && File.Exists(asset.FilePath));

            string persistedHtml = File.ReadAllText(page.HtmlPath!);
            Assert.Contains("data-src=\"../assets/", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("data-srcset=\"../assets/", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("<source data-srcset=\"../assets/", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/media/hero.jpg", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/media/hero-small.jpg", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/media/hero.webp", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/media/hero@2x.webp", persistedHtml, StringComparison.OrdinalIgnoreCase);
        } finally {
            listener.Stop();
            listener.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CrawlAsync_Downloads_And_Rewrites_Noscript_Media_Fallbacks_To_LocalPaths() {
        string outputPath = Path.Combine(Path.GetTempPath(), "HtmlCrawlerNoscriptAssetTests", Guid.NewGuid().ToString("N"));
        HttpListener listener = new();
        string rootUrl;
        {
            int port = GetFreePort();
            rootUrl = $"http://localhost:{port}/";
            listener.Prefixes.Add(rootUrl);
        }
        listener.Start();

        _ = Task.Run(async () => {
            try {
                while (listener.IsListening) {
                    HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
                    string key = context.Request.RawUrl ?? "/";
                    byte[] data;
                    string contentType;
                    switch (key) {
                        case "/":
                            data = Encoding.UTF8.GetBytes("""
                                <html>
                                <head><title>Offline Noscript Media</title></head>
                                <body>
                                  <main>
                                    <a href="/story">
                                      <img src="data:image/svg+xml,%3Csvg%20xmlns='http://www.w3.org/2000/svg'%20viewBox='0%200%20320%20180'%3E%3C/svg%3E" alt="Fallback teaser" />
                                      <noscript><img src="/media/fallback.jpg" alt="Fallback teaser" srcset="/media/fallback.jpg 1x, /media/fallback@2x.jpg 2x" /></noscript>
                                    </a>
                                  </main>
                                </body>
                                </html>
                                """);
                            contentType = "text/html; charset=utf-8";
                            break;
                        case "/media/fallback.jpg":
                        case "/media/fallback@2x.jpg":
                            data = Encoding.UTF8.GetBytes("fake-fallback");
                            contentType = "image/jpeg";
                            break;
                        default:
                            context.Response.StatusCode = 404;
                            context.Response.OutputStream.Close();
                            continue;
                    }

                    context.Response.ContentType = contentType;
                    context.Response.ContentLength64 = data.Length;
                    await context.Response.OutputStream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                    context.Response.OutputStream.Close();
                }
            } catch (HttpListenerException) {
            } catch (ObjectDisposedException) {
            }
        });

        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 5,
                DownloadAssets = true,
                OutputPath = outputPath
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Contains(page.AssetUrls, asset => string.Equals(asset, rootUrl + "media/fallback.jpg", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(page.AssetUrls, asset => string.Equals(asset, rootUrl + "media/fallback@2x.jpg", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.Assets, asset => string.Equals(asset.Url, rootUrl + "media/fallback.jpg", StringComparison.OrdinalIgnoreCase) && File.Exists(asset.FilePath));
            Assert.Contains(result.Assets, asset => string.Equals(asset.Url, rootUrl + "media/fallback@2x.jpg", StringComparison.OrdinalIgnoreCase) && File.Exists(asset.FilePath));

            string persistedHtml = File.ReadAllText(page.HtmlPath!);
            Assert.Contains("<noscript><img src=\"../assets/", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("srcset=\"../assets/", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/media/fallback.jpg", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/media/fallback@2x.jpg", persistedHtml, StringComparison.OrdinalIgnoreCase);
        } finally {
            listener.Stop();
            listener.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CrawlAsync_Downloads_And_Rewrites_Preload_Assets_To_LocalPaths() {
        string outputPath = Path.Combine(Path.GetTempPath(), "HtmlCrawlerPreloadAssetTests", Guid.NewGuid().ToString("N"));
        HttpListener listener = new();
        string rootUrl;
        {
            int port = GetFreePort();
            rootUrl = $"http://localhost:{port}/";
            listener.Prefixes.Add(rootUrl);
        }
        listener.Start();

        _ = Task.Run(async () => {
            try {
                while (listener.IsListening) {
                    HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
                    string key = context.Request.RawUrl ?? "/";
                    string body;
                    string contentType;
                    switch (key) {
                        case "/":
                            body = """
                                <html>
                                <head>
                                  <title>Offline Preload</title>
                                </head>
                                <body><main><link rel="preload" href="/assets/site.css" as="style" /><link rel="modulepreload" href="/assets/app.mjs" /><link rel="preload" as="image" href="/assets/poster.jpg" imagesrcset="/assets/poster-small.jpg 1x, /assets/poster.jpg 2x" imagesizes="100vw" /><h1>Offline Preload</h1></main></body>
                                </html>
                                """;
                            contentType = "text/html; charset=utf-8";
                            break;
                        case "/assets/site.css":
                            body = "body{background:#fff;}";
                            contentType = "text/css";
                            break;
                        case "/assets/app.mjs":
                            body = "export const boot = true;";
                            contentType = "application/javascript";
                            break;
                        case "/assets/poster-small.jpg":
                        case "/assets/poster.jpg":
                            body = "fake-image";
                            contentType = "image/jpeg";
                            break;
                        default:
                            context.Response.StatusCode = 404;
                            context.Response.OutputStream.Close();
                            continue;
                    }

                    byte[] data = Encoding.UTF8.GetBytes(body);
                    context.Response.ContentType = contentType;
                    context.Response.ContentLength64 = data.Length;
                    await context.Response.OutputStream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                    context.Response.OutputStream.Close();
                }
            } catch (HttpListenerException) {
            } catch (ObjectDisposedException) {
            }
        });

        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 5,
                DownloadAssets = true,
                OutputPath = outputPath
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Contains(page.AssetUrls, asset => string.Equals(asset, rootUrl + "assets/site.css", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(page.AssetUrls, asset => string.Equals(asset, rootUrl + "assets/app.mjs", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(page.AssetUrls, asset => string.Equals(asset, rootUrl + "assets/poster-small.jpg", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(page.AssetUrls, asset => string.Equals(asset, rootUrl + "assets/poster.jpg", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.Assets, asset => string.Equals(asset.Url, rootUrl + "assets/site.css", StringComparison.OrdinalIgnoreCase) && File.Exists(asset.FilePath));
            Assert.Contains(result.Assets, asset => string.Equals(asset.Url, rootUrl + "assets/app.mjs", StringComparison.OrdinalIgnoreCase) && File.Exists(asset.FilePath));
            Assert.Contains(result.Assets, asset => string.Equals(asset.Url, rootUrl + "assets/poster-small.jpg", StringComparison.OrdinalIgnoreCase) && File.Exists(asset.FilePath));
            Assert.Contains(result.Assets, asset => string.Equals(asset.Url, rootUrl + "assets/poster.jpg", StringComparison.OrdinalIgnoreCase) && File.Exists(asset.FilePath));

            string persistedHtml = File.ReadAllText(page.HtmlPath!);
            Assert.Contains("<link rel=\"preload\" href=\"../assets/", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("<link rel=\"modulepreload\" href=\"../assets/", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("imagesrcset=\"../assets/", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/assets/site.css", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/assets/app.mjs", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/assets/poster-small.jpg", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/assets/poster.jpg", persistedHtml, StringComparison.OrdinalIgnoreCase);
        } finally {
            listener.Stop();
            listener.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CrawlAsync_Downloads_And_Rewrites_VideoPoster_And_Track_Assets_To_LocalPaths() {
        string outputPath = Path.Combine(Path.GetTempPath(), "HtmlCrawlerVideoAssetTests", Guid.NewGuid().ToString("N"));
        HttpListener listener = new();
        string rootUrl;
        {
            int port = GetFreePort();
            rootUrl = $"http://localhost:{port}/";
            listener.Prefixes.Add(rootUrl);
        }
        listener.Start();

        _ = Task.Run(async () => {
            try {
                while (listener.IsListening) {
                    HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
                    string key = context.Request.RawUrl ?? "/";
                    byte[] data;
                    string contentType;
                    switch (key) {
                        case "/":
                            data = Encoding.UTF8.GetBytes("""
                                <html>
                                <head><title>Offline Video</title></head>
                                <body>
                                  <main>
                                    <video controls poster="/media/poster.jpg">
                                      <source src="/media/story.mp4" type="video/mp4" />
                                      <track kind="captions" srclang="en" src="/media/story.en.vtt" default />
                                    </video>
                                  </main>
                                </body>
                                </html>
                                """);
                            contentType = "text/html; charset=utf-8";
                            break;
                        case "/media/poster.jpg":
                            data = Encoding.UTF8.GetBytes("fake-poster");
                            contentType = "image/jpeg";
                            break;
                        case "/media/story.mp4":
                            data = Encoding.UTF8.GetBytes("fake-video");
                            contentType = "video/mp4";
                            break;
                        case "/media/story.en.vtt":
                            data = Encoding.UTF8.GetBytes("""
                                WEBVTT

                                00:00.000 --> 00:02.000
                                Story intro
                                """);
                            contentType = "text/vtt";
                            break;
                        default:
                            context.Response.StatusCode = 404;
                            context.Response.OutputStream.Close();
                            continue;
                    }

                    context.Response.ContentType = contentType;
                    context.Response.ContentLength64 = data.Length;
                    await context.Response.OutputStream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                    context.Response.OutputStream.Close();
                }
            } catch (HttpListenerException) {
            } catch (ObjectDisposedException) {
            }
        });

        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 5,
                DownloadAssets = true,
                OutputPath = outputPath
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Contains(page.AssetUrls, asset => string.Equals(asset, rootUrl + "media/poster.jpg", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(page.AssetUrls, asset => string.Equals(asset, rootUrl + "media/story.mp4", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(page.AssetUrls, asset => string.Equals(asset, rootUrl + "media/story.en.vtt", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.Assets, asset => string.Equals(asset.Url, rootUrl + "media/poster.jpg", StringComparison.OrdinalIgnoreCase) && File.Exists(asset.FilePath));
            Assert.Contains(result.Assets, asset => string.Equals(asset.Url, rootUrl + "media/story.mp4", StringComparison.OrdinalIgnoreCase) && File.Exists(asset.FilePath));
            Assert.Contains(result.Assets, asset => string.Equals(asset.Url, rootUrl + "media/story.en.vtt", StringComparison.OrdinalIgnoreCase) && File.Exists(asset.FilePath));

            string persistedHtml = File.ReadAllText(page.HtmlPath!);
            Assert.Contains("poster=\"../assets/", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("<source src=\"../assets/", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("<track kind=\"captions\" srclang=\"en\" src=\"../assets/", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/media/poster.jpg", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/media/story.mp4", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/media/story.en.vtt", persistedHtml, StringComparison.OrdinalIgnoreCase);
        } finally {
            listener.Stop();
            listener.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CrawlAsync_Downloads_And_Rewrites_Embedded_Resource_Assets_To_LocalPaths() {
        string outputPath = Path.Combine(Path.GetTempPath(), "HtmlCrawlerEmbeddedAssetTests", Guid.NewGuid().ToString("N"));
        HttpListener listener = new();
        string rootUrl;
        {
            int port = GetFreePort();
            rootUrl = $"http://localhost:{port}/";
            listener.Prefixes.Add(rootUrl);
        }
        listener.Start();

        _ = Task.Run(async () => {
            try {
                while (listener.IsListening) {
                    HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
                    string key = context.Request.RawUrl ?? "/";
                    byte[] data;
                    string contentType;
                    switch (key) {
                        case "/":
                            data = Encoding.UTF8.GetBytes("""
                                <html>
                                <head><title>Offline Embedded Resources</title></head>
                                <body>
                                  <main>
                                    <iframe src="/frames/report.html" title="Report"></iframe>
                                    <embed src="/widgets/chart.svg" type="image/svg+xml" />
                                    <object data="/docs/guide.pdf" type="application/pdf"></object>
                                  </main>
                                </body>
                                </html>
                                """);
                            contentType = "text/html; charset=utf-8";
                            break;
                        case "/frames/report.html":
                            data = Encoding.UTF8.GetBytes("<html><body><h1>Embedded report</h1></body></html>");
                            contentType = "text/html; charset=utf-8";
                            break;
                        case "/widgets/chart.svg":
                            data = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\"><circle cx=\"5\" cy=\"5\" r=\"4\" /></svg>");
                            contentType = "image/svg+xml";
                            break;
                        case "/docs/guide.pdf":
                            data = Encoding.UTF8.GetBytes("%PDF-1.4 fake pdf");
                            contentType = "application/pdf";
                            break;
                        default:
                            context.Response.StatusCode = 404;
                            context.Response.OutputStream.Close();
                            continue;
                    }

                    context.Response.ContentType = contentType;
                    context.Response.ContentLength64 = data.Length;
                    await context.Response.OutputStream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                    context.Response.OutputStream.Close();
                }
            } catch (HttpListenerException) {
            } catch (ObjectDisposedException) {
            }
        });

        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 5,
                DownloadAssets = true,
                OutputPath = outputPath
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Contains(page.AssetUrls, asset => string.Equals(asset, rootUrl + "frames/report.html", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(page.AssetUrls, asset => string.Equals(asset, rootUrl + "widgets/chart.svg", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(page.AssetUrls, asset => string.Equals(asset, rootUrl + "docs/guide.pdf", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.Assets, asset => string.Equals(asset.Url, rootUrl + "frames/report.html", StringComparison.OrdinalIgnoreCase) && File.Exists(asset.FilePath));
            Assert.Contains(result.Assets, asset => string.Equals(asset.Url, rootUrl + "widgets/chart.svg", StringComparison.OrdinalIgnoreCase) && File.Exists(asset.FilePath));
            Assert.Contains(result.Assets, asset => string.Equals(asset.Url, rootUrl + "docs/guide.pdf", StringComparison.OrdinalIgnoreCase) && File.Exists(asset.FilePath));

            string persistedHtml = File.ReadAllText(page.HtmlPath!);
            Assert.Contains("<iframe src=\"../assets/", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("<embed src=\"../assets/", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("<object data=\"../assets/", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/frames/report.html", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/widgets/chart.svg", persistedHtml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/docs/guide.pdf", persistedHtml, StringComparison.OrdinalIgnoreCase);
        } finally {
            listener.Stop();
            listener.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }
}
