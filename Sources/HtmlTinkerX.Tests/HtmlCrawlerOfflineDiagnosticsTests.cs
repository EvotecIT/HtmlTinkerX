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
    public async Task CrawlAsync_Reports_Offline_Runtime_Dependency_Diagnostics_In_Page_And_Manifest() {
        string outputPath = Path.Combine(Path.GetTempPath(), "HtmlCrawlerOfflineDiagnosticTests", Guid.NewGuid().ToString("N"));
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
                    if (!string.Equals(key, "/", StringComparison.Ordinal)) {
                        context.Response.StatusCode = 404;
                        context.Response.OutputStream.Close();
                        continue;
                    }

                    byte[] data = Encoding.UTF8.GetBytes("""
                        <html>
                        <head><title>Offline Diagnostics</title></head>
                        <body>
                          <main>
                            <h1>Offline Diagnostics</h1>
                            <button onclick="fetch('/api/status').then(r => r.json())">Refresh</button>
                            <script>
                              const socket = new WebSocket('wss://example.test/socket');
                              navigator.serviceWorker.register('/sw.js');
                            </script>
                          </main>
                        </body>
                        </html>
                        """);
                    context.Response.ContentType = "text/html; charset=utf-8";
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
                OutputPath = outputPath
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Contains(page.OfflineDependencyDiagnostics, diagnostic => string.Equals(diagnostic.Kind, "fetch-api", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(page.OfflineDependencyDiagnostics, diagnostic => string.Equals(diagnostic.Kind, "websocket", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(page.OfflineDependencyDiagnostics, diagnostic => string.Equals(diagnostic.Kind, "service-worker", StringComparison.OrdinalIgnoreCase));
            Assert.All(page.OfflineDependencyDiagnostics, diagnostic => Assert.False(string.IsNullOrWhiteSpace(diagnostic.Evidence)));
            Assert.Equal("live-dependent", page.OfflineReadinessGrade);
            Assert.Equal("high", page.HighestOfflineRiskSeverity);
            Assert.Equal(3, page.OfflineDependencyDiagnosticCount);
            Assert.Equal("fetch-api, service-worker, websocket", page.OfflineDependencyKindsSummary);
            Assert.True(result.Summary.OfflineRiskPageCount >= 1);
            Assert.True(result.Summary.OfflineRiskDiagnosticCount >= 3);
            Assert.True(result.Summary.HighOfflineRiskPageCount >= 1);
            Assert.Equal("live-dependent", result.Summary.OfflineReadinessGrade);
            Assert.Contains("fetch-api", result.Summary.OfflineDependencyKinds.Keys, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("high", result.Summary.OfflineDependencySeverityCounts.Keys, StringComparer.OrdinalIgnoreCase);

            string report = result.Summary.ToReportText(result.SitemapUrls);
            Assert.Contains("Offline-risk pages:", report, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("High offline-risk pages:", report, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Offline readiness grade: live-dependent", report, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Offline readiness:", report, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Offline severity", report, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Offline dependency fetch-api:", report, StringComparison.OrdinalIgnoreCase);

            string indexHtml = File.ReadAllText(Path.Combine(outputPath, "index.html"));
            Assert.Contains("Offline Readiness", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Offline Grade", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Offline-Risk Pages", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("High-Risk Pages", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Offline readiness grade: <code>live-dependent</code>", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("offline risk:", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("fetch-api", indexHtml, StringComparison.OrdinalIgnoreCase);

            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(page.ManifestPath!));
            Assert.Equal("live-dependent", manifest.RootElement.GetProperty("OfflineReadinessGrade").GetString());
            Assert.Equal("high", manifest.RootElement.GetProperty("HighestOfflineRiskSeverity").GetString());
            JsonElement diagnostics = manifest.RootElement.GetProperty("OfflineDependencyDiagnostics");
            Assert.True(diagnostics.ValueKind == JsonValueKind.Array);
            Assert.Contains(diagnostics.EnumerateArray().Select(item => item.GetProperty("Kind").GetString()), kind => string.Equals(kind, "fetch-api", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(diagnostics.EnumerateArray().Select(item => item.GetProperty("Kind").GetString()), kind => string.Equals(kind, "websocket", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(diagnostics.EnumerateArray().Select(item => item.GetProperty("Kind").GetString()), kind => string.Equals(kind, "service-worker", StringComparison.OrdinalIgnoreCase));

            using JsonDocument pagesJsonl = JsonDocument.Parse(File.ReadAllLines(result.PagesJsonlPath!).Single());
            Assert.Equal("live-dependent", pagesJsonl.RootElement.GetProperty("OfflineReadinessGrade").GetString());
            Assert.Equal("high", pagesJsonl.RootElement.GetProperty("HighestOfflineRiskSeverity").GetString());
            Assert.Equal(3, pagesJsonl.RootElement.GetProperty("OfflineDependencyDiagnosticCount").GetInt32());
            Assert.Equal("fetch-api, service-worker, websocket", pagesJsonl.RootElement.GetProperty("OfflineDependencyKindsSummary").GetString());
            Assert.Contains(pagesJsonl.RootElement.GetProperty("OfflineDependencyKinds").EnumerateArray().Select(item => item.GetString()), kind => string.Equals(kind, "fetch-api", StringComparison.OrdinalIgnoreCase));

            string[] csvLines = File.ReadAllLines(result.PagesCsvPath!);
            Assert.Contains("OfflineReadinessGrade", csvLines[0], StringComparison.OrdinalIgnoreCase);
            Assert.Contains("HighestOfflineRiskSeverity", csvLines[0], StringComparison.OrdinalIgnoreCase);
            Assert.Contains("OfflineDependencyKindsSummary", csvLines[0], StringComparison.OrdinalIgnoreCase);
            Assert.Contains("live-dependent", csvLines[1], StringComparison.OrdinalIgnoreCase);
            Assert.Contains("high", csvLines[1], StringComparison.OrdinalIgnoreCase);
            Assert.Contains("fetch-api, service-worker, websocket", csvLines[1], StringComparison.OrdinalIgnoreCase);
        } finally {
            listener.Stop();
            listener.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public void DetectRenderedNetworkDependencyDiagnostics_Reports_Runtime_And_CrossOrigin_Findings() {
        List<HtmlNetworkEntry> entries = new() {
            new() {
                Url = "https://app.example.test/api/status",
                ResourceType = HtmlNetworkResourceType.Fetch
            },
            new() {
                Url = "https://api.example.test/v1/data",
                ResourceType = HtmlNetworkResourceType.XHR
            },
            new() {
                Url = "wss://stream.example.test/live",
                ResourceType = HtmlNetworkResourceType.WebSocket
            },
            new() {
                Url = "https://app.example.test/assets/app.js",
                ResourceType = HtmlNetworkResourceType.Script
            }
        };

        IList<HtmlCrawlOfflineDependencyDiagnostic> diagnostics = HtmlCrawler.DetectRenderedNetworkDependencyDiagnostics(
            entries,
            new Uri("https://app.example.test/dashboard"));

        Assert.Contains(diagnostics, diagnostic => string.Equals(diagnostic.Kind, "observed-fetch-api", StringComparison.OrdinalIgnoreCase)
            && string.Equals(diagnostic.Evidence, "https://app.example.test/api/status", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(diagnostics, diagnostic => string.Equals(diagnostic.Kind, "observed-xml-http-request", StringComparison.OrdinalIgnoreCase)
            && string.Equals(diagnostic.Evidence, "https://api.example.test/v1/data", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(diagnostics, diagnostic => string.Equals(diagnostic.Kind, "observed-websocket", StringComparison.OrdinalIgnoreCase)
            && string.Equals(diagnostic.Evidence, "wss://stream.example.test/live", StringComparison.OrdinalIgnoreCase)
            && string.Equals(diagnostic.Severity, "high", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(diagnostics, diagnostic => string.Equals(diagnostic.Kind, "observed-cross-origin-runtime", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(diagnostic.Evidence)
            && string.Equals(diagnostic.Severity, "high", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(diagnostics, diagnostic => string.Equals(diagnostic.Kind, "observed-fetch-api", StringComparison.OrdinalIgnoreCase)
            && string.Equals(diagnostic.Severity, "warning", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(diagnostics, diagnostic => string.Equals(diagnostic.Kind, "script", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void HtmlCrawlSummary_OfflineReadinessGrade_IsReadyWithoutDiagnostics() {
        HtmlCrawlResult result = new() {
            StartUrl = "https://example.com/",
            Started = DateTimeOffset.UtcNow.AddSeconds(-1),
            Finished = DateTimeOffset.UtcNow,
            Pages = {
                new HtmlCrawlPage {
                    Url = "https://example.com/",
                    Status = HtmlCrawlPageStatus.Success
                }
            }
        };

        HtmlCrawlSummary summary = result.Summary;

        Assert.Equal("ready", summary.OfflineReadinessGrade);
        Assert.Equal(0, summary.OfflineRiskDiagnosticCount);
        Assert.Equal(0, summary.HighOfflineRiskPageCount);
        Assert.Equal("ready", Assert.Single(result.Pages).OfflineReadinessGrade);
        Assert.Equal("none", Assert.Single(result.Pages).HighestOfflineRiskSeverity);
    }

    [Fact]
    public void HtmlCrawlSummary_OfflineReadinessGrade_IsPartialForWarningsOnly() {
        HtmlCrawlResult result = new() {
            StartUrl = "https://example.com/",
            Started = DateTimeOffset.UtcNow.AddSeconds(-1),
            Finished = DateTimeOffset.UtcNow,
            Pages = {
                new HtmlCrawlPage {
                    Url = "https://example.com/",
                    Status = HtmlCrawlPageStatus.Success,
                    OfflineDependencyDiagnostics = {
                        new HtmlCrawlOfflineDependencyDiagnostic {
                            Kind = "fetch-api",
                            Severity = "warning",
                            Evidence = "fetch('/api/status')"
                        }
                    }
                }
            }
        };

        HtmlCrawlSummary summary = result.Summary;

            Assert.Equal("partial", summary.OfflineReadinessGrade);
            Assert.Equal(1, summary.OfflineRiskDiagnosticCount);
            Assert.Equal(0, summary.HighOfflineRiskPageCount);
            Assert.Equal("partial", Assert.Single(result.Pages).OfflineReadinessGrade);
            Assert.Equal("warning", Assert.Single(result.Pages).HighestOfflineRiskSeverity);
            Assert.Equal(1, summary.OfflineReadinessCounts["partial"]);
            Assert.Equal(1, summary.OfflineReadinessCountsByState["Success:partial"]);
        }

    [Fact]
    public async Task CrawlAsync_Exports_NotAssessed_OfflineReadiness_For_Skipped_And_Pending_Candidates() {
        string outputPath = Path.Combine(Path.GetTempPath(), "HtmlCrawlerPendingSkippedOfflineTests", Guid.NewGuid().ToString("N"));
        Dictionary<string, string> responses = new() {
            ["/"] = "<html><head><title>Home</title></head><body><a href='/allowed'>Allowed</a><a href='/blocked'>Blocked</a></body></html>",
            ["/allowed"] = "<html><head><title>Allowed</title></head><body>Allowed</body></html>",
            ["/blocked"] = "<html><head><title>Blocked</title></head><body>Blocked</body></html>"
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 1,
                MaxPages = 1,
                OutputPath = outputPath,
                ExcludePatterns = { "*blocked" }
            });

            HtmlCrawlPendingItem pending = Assert.Single(result.PendingPages);
            Assert.Equal("not-assessed", pending.OfflineReadinessGrade);
            Assert.Equal("none", pending.HighestOfflineRiskSeverity);

            HtmlCrawlPage skipped = Assert.Single(result.SkippedPages);
            Assert.Equal(HtmlCrawlSkipReason.ExcludedByPattern, skipped.SkipReason);
            Assert.Equal("not-assessed", skipped.OfflineReadinessGrade);
            Assert.Equal("none", skipped.HighestOfflineRiskSeverity);

            HtmlCrawlSummary summary = result.Summary;
            Assert.Equal(1, summary.OfflineReadinessCounts["ready"]);
            Assert.Equal(2, summary.OfflineReadinessCounts["not-assessed"]);
            Assert.Equal(1, summary.OfflineReadinessCountsByState["Success:ready"]);
            Assert.Equal(1, summary.OfflineReadinessCountsByState["Skipped:not-assessed"]);
            Assert.Equal(1, summary.OfflineReadinessCountsByState["Pending:not-assessed"]);

            using JsonDocument skippedJsonl = JsonDocument.Parse(File.ReadAllLines(result.SkippedPagesJsonlPath!).Single());
            Assert.Equal("not-assessed", skippedJsonl.RootElement.GetProperty("OfflineReadinessGrade").GetString());
            Assert.Equal("none", skippedJsonl.RootElement.GetProperty("HighestOfflineRiskSeverity").GetString());
            Assert.Equal(0, skippedJsonl.RootElement.GetProperty("OfflineDependencyDiagnosticCount").GetInt32());
            Assert.Equal(string.Empty, skippedJsonl.RootElement.GetProperty("OfflineDependencyKindsSummary").GetString());

            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(result.ManifestPath!));
            JsonElement pendingJson = Assert.Single(manifest.RootElement.GetProperty("PendingPages").EnumerateArray());
            Assert.Equal("not-assessed", pendingJson.GetProperty("OfflineReadinessGrade").GetString());
            Assert.Equal("none", pendingJson.GetProperty("HighestOfflineRiskSeverity").GetString());
            Assert.Equal(0, pendingJson.GetProperty("OfflineDependencyDiagnosticCount").GetInt32());
            Assert.Equal(string.Empty, pendingJson.GetProperty("OfflineDependencyKindsSummary").GetString());

            string indexHtml = File.ReadAllText(result.IndexHtmlPath!);
            Assert.Contains("Pending Pages", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Skipped Pages", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("not-assessed", indexHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Offline state <code>Pending:not-assessed</code>: 1", indexHtml, StringComparison.OrdinalIgnoreCase);
        } finally {
            DisposeListenerSafely(server);
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    private static string BuildRelativeForTest(string fromFilePath, string toFilePath) {
        string fromDirectory = Path.GetDirectoryName(fromFilePath)!;
        Uri fromUri = new((fromDirectory.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? fromDirectory : fromDirectory + Path.DirectorySeparatorChar));
        Uri toUri = new(toFilePath);
        return Uri.UnescapeDataString(fromUri.MakeRelativeUri(toUri).ToString()).Replace('\\', '/');
    }
}
