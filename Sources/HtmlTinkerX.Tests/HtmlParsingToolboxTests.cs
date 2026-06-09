using Microsoft.AspNetCore.Http;

namespace HtmlTinkerX.Tests;

public class HtmlParsingToolboxTests {
    [Fact]
    public async Task FindInteractionSurfaceAsync_ResolvesRelativeFormActions() {
        IReadOnlyList<HtmlInteractionSurfaceItem> surfaces = await HtmlParsingToolbox.FindInteractionSurfaceAsync(
            """<form id="login" method="post" action="/login"><input name="user"></form>""",
            new Uri("https://example.org/app/page"));

        HtmlInteractionSurfaceItem form = Assert.Single(surfaces, item => item.Kind == "Form");
        Assert.Equal("https://example.org/login", form.Url);
    }

    [Fact]
    public async Task FindInteractionSurfaceAsync_UsesActualLinkedScriptSelector() {
        using var server = TestServerCompat.CreateTestServer(async context => {
            if (context.Request.Path == "/app.js") {
                await context.Response.WriteAsync("""fetch("/api/items", { method: "POST" });""");
                return;
            }

            context.Response.StatusCode = 404;
        }, null, null);
        using var client = server.CreateClient();
        string html = """<script>console.log("inline")</script><script src="/app.js"></script>""";

        IReadOnlyList<HtmlInteractionSurfaceItem> surfaces = await HtmlParsingToolbox.FindInteractionSurfaceAsync(
            html,
            server.BaseAddress,
            includeLinkedScripts: true,
            includeExternalLinkedScripts: false,
            client);

        HtmlInteractionSurfaceItem endpoint = Assert.Single(surfaces, item => item.Kind == "LinkedEndpoint");
        Assert.Equal("script:nth-of-type(2)", endpoint.Selector);
        Assert.Equal(1, endpoint.SourceIndex);
        Assert.Equal("/api/items", endpoint.Url);
    }

    [Fact]
    public async Task FindInteractionSurfaceAsync_UsesActualInlineScriptSelector() {
        string html = """<script>console.log("inline")</script><script>fetch("/api/items", { method: "POST" });</script>""";

        IReadOnlyList<HtmlInteractionSurfaceItem> surfaces = await HtmlParsingToolbox.FindInteractionSurfaceAsync(html);

        HtmlInteractionSurfaceItem endpoint = Assert.Single(surfaces, item => item.Kind == "Endpoint");
        Assert.Equal("script:nth-of-type(2)", endpoint.Selector);
        Assert.Equal(1, endpoint.SourceIndex);
        Assert.Equal("/api/items", endpoint.Url);
    }

    [Fact]
    public async Task FindInteractionSurfaceAsync_UsesAbsoluteDocumentBaseForLinkedScripts() {
        using var server = TestServerCompat.CreateTestServer(async context => {
            if (context.Request.Path == "/assets/app.js") {
                await context.Response.WriteAsync("""fetch("/api/from-base");""");
                return;
            }

            context.Response.StatusCode = 404;
        }, null, null);
        using var client = server.CreateClient();
        string html = $"""<base href="{server.BaseAddress}assets/"><script src="app.js"></script>""";

        IReadOnlyList<HtmlInteractionSurfaceItem> surfaces = await HtmlParsingToolbox.FindInteractionSurfaceAsync(
            html,
            includeLinkedScripts: true,
            includeExternalLinkedScripts: false,
            client: client);

        HtmlInteractionSurfaceItem endpoint = Assert.Single(surfaces, item => item.Kind == "LinkedEndpoint");
        Assert.Equal("/api/from-base", endpoint.Url);
    }

    [Fact]
    public void SelectData_ResolvesHeadLinksAgainstAbsoluteDocumentBase() {
        string html = """<html><head><base href="https://example.org/app/"><link rel="canonical" href="docs"></head></html>""";

        IReadOnlyList<HtmlDataItem> items = HtmlParsingToolbox.SelectData(html, new[] { "HeadLink" });

        HtmlDataItem item = Assert.Single(items);
        Assert.Equal("https://example.org/app/docs", item.Value);
    }

    [Fact]
    public void SelectData_UsesMetaSourceAttributeInSelectors() {
        string html = """<html><head><meta property="twitter:title" content="Docs"></head></html>""";

        IReadOnlyList<HtmlDataItem> items = HtmlParsingToolbox.SelectData(html, new[] { "Meta" });

        HtmlDataItem item = Assert.Single(items);
        Assert.Equal("twitter:title", item.Name);
        Assert.Equal("meta[property='twitter:title']", item.Selector);
    }

    [Fact]
    public void CompareStaticRendered_ResolvesFormActionsInSignatures() {
        HtmlStaticRenderedComparison comparison = HtmlParsingToolbox.CompareStaticRendered(
            """<form id="login" method="post" action="/login"><input name="user"></form>""",
            """<form id="login" method="post" action="https://example.org/login"><input name="user"></form>""",
            new Uri("https://example.org/app/page"));

        HtmlStaticRenderedDelta formDelta = Assert.Single(comparison.Deltas, delta => delta.Kind == "Form");
        Assert.Empty(formDelta.Added);
        Assert.Empty(formDelta.Removed);
    }
}
