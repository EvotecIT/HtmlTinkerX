using System.Net;
using System.Net.Http;
using System.Threading;

namespace HtmlTinkerX.Tests;

public class HtmlPageWorkbenchTests {
    [Fact]
    public async Task AnalyzeAsync_ReturnsUnifiedStaticPageIntelligence() {
        string html = """
<html>
<head>
<title>Workbench Demo</title>
<meta property="og:title" content="Workbench Demo">
<script type="application/ld+json">{"@context":"https://schema.org","@type":"Article","headline":"Workbench Demo"}</script>
<script>window.__CONFIG__ = { api: { baseUrl: "/api" } }; fetch("/api/items");</script>
</head>
<body>
<main>
<h1>Workbench Demo</h1>
<p>This page has enough readable content to prove that the page workbench returns text and markdown from one command.</p>
<a href="/docs">Docs</a>
<img src="/hero.png" alt="Hero">
<form method="post" action="/login">
<input type="hidden" name="token" value="secret">
<input name="user">
</form>
</main>
</body>
</html>
""";

        HtmlPageWorkbenchResult result = await HtmlPageWorkbench.AnalyzeAsync(
            html,
            new HtmlPageWorkbenchOptions {
                BaseUri = new Uri("https://example.org/page")
            });

        Assert.Equal("https://example.org/page", result.SourceUrl);
        Assert.Equal("Workbench Demo", result.Title);
        Assert.Contains("readable content", result.ReadableText!.Text);
        Assert.Contains("Workbench Demo", result.Markdown);
        Assert.NotNull(result.ExtractionPlan);
        Assert.Equal(result.ExtractionPlan!.SuggestedCommand, result.SuggestedNextCommand);
        Assert.Single(result.Forms);
        Assert.Single(result.HiddenFields);
        Assert.Single(result.Links);
        Assert.True(result.Assets.Count >= 1);
        Assert.Single(result.JsonLd);
        Assert.Single(result.OpenGraph);
        Assert.NotEmpty(result.JavaScriptConfig);
        Assert.Contains(result.Endpoints, item => item.Url == "/api/items");
        Assert.Contains(result.Warnings, warning => warning.Contains("Hidden fields", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AnalyzeAsync_CanOmitOriginalHtml() {
        HtmlPageWorkbenchResult result = await HtmlPageWorkbench.AnalyzeAsync(
            "<html><body><main><p>Small page.</p></main></body></html>",
            new HtmlPageWorkbenchOptions {
                IncludeHtml = false
            });

        Assert.Equal(string.Empty, result.Html);
        Assert.NotNull(result.ReadableText);
    }

    [Fact]
    public async Task AnalyzeAsync_UsesRenderedSnapshotAsPrimaryView() {
        string staticHtml = """
<html>
<head><title>Loading</title><script src="/app.js"></script></head>
<body><div id="root">Loading...</div></body>
</html>
""";
        HtmlRenderedPageSnapshot snapshot = new() {
            Url = "https://example.org/app",
            FinalUrl = "https://example.org/app#ready",
            Title = "Rendered App",
            Html = """
<html>
<head><title>Rendered App</title></head>
<body>
<main>
<h1>Rendered App</h1>
<p>The rendered application now exposes real content and navigation links.</p>
<a href="/ready">Ready</a>
<form method="post" action="/submit"><input type="hidden" name="csrf" value="token"></form>
</main>
</body>
</html>
""",
            Markdown = "# Rendered App\n\nThe rendered application now exposes real content and navigation links."
        };

        HtmlPageWorkbenchResult result = await HtmlPageWorkbench.AnalyzeAsync(
            staticHtml,
            new HtmlPageWorkbenchOptions {
                BaseUri = new Uri("https://example.org/app"),
                RenderedSnapshot = snapshot
            });

        Assert.Equal("RenderedSnapshot", result.AnalysisMode);
        Assert.Equal("https://example.org/app#ready", result.FinalUrl);
        Assert.Equal("Rendered App", result.Title);
        Assert.Same(snapshot, result.RenderedSnapshot);
        Assert.NotNull(result.StaticRenderedComparison);
        Assert.DoesNotContain(result.StaticData, static item => item.Kind == "Link");
        Assert.Single(result.Links);
        Assert.Single(result.Forms);
        Assert.Single(result.HiddenFields);
        Assert.NotEmpty(result.RenderedData);
        Assert.NotEmpty(result.RenderedInteractionSurface);
        Assert.Contains(result.Warnings, warning => warning.Contains("Rendered content differs", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AnalyzeAsync_HonorsLinkedScriptInspectionForRenderedSnapshot() {
        string staticHtml = "<html><body><div id=\"root\">Loading...</div></body></html>";
        HtmlRenderedPageSnapshot snapshot = new() {
            Url = "https://example.org/app",
            FinalUrl = "https://example.org/app",
            Title = "Rendered App",
            Html = """
<html>
<head><script src="/app.js"></script></head>
<body><main><h1>Rendered App</h1></main></body>
</html>
"""
        };
        using HttpClient client = new(new LinkedScriptHandler());

        HtmlPageWorkbenchResult result = await HtmlPageWorkbench.AnalyzeAsync(
            staticHtml,
            new HtmlPageWorkbenchOptions {
                BaseUri = new Uri("https://example.org/app"),
                RenderedSnapshot = snapshot,
                IncludeLinkedScripts = true
            },
            client);

        Assert.Contains(result.Endpoints, endpoint => endpoint.Kind == "LinkedEndpoint" && endpoint.Url == "/api/rendered-linked");
    }

    private sealed class LinkedScriptHandler : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            HttpResponseMessage response = new(HttpStatusCode.OK) {
                RequestMessage = request,
                Content = new StringContent("fetch('/api/rendered-linked');")
            };

            return Task.FromResult(response);
        }
    }
}
