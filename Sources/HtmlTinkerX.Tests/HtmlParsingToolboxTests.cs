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
    public async Task FindInteractionSurfaceAsync_DefaultsActionlessFormsToPageUrl() {
        IReadOnlyList<HtmlInteractionSurfaceItem> surfaces = await HtmlParsingToolbox.FindInteractionSurfaceAsync(
            """<form id="login" method="post"><input name="user"></form>""",
            new Uri("https://example.org/app/page"));

        HtmlInteractionSurfaceItem form = Assert.Single(surfaces, item => item.Kind == "Form");
        Assert.Equal("https://example.org/app/page", form.Url);
    }

    [Fact]
    public async Task FindInteractionSurfaceAsync_DefaultsActionlessFormsToPageUrlWhenDocumentBaseIsPresent() {
        IReadOnlyList<HtmlInteractionSurfaceItem> surfaces = await HtmlParsingToolbox.FindInteractionSurfaceAsync(
            """<base href="/assets/"><form id="login" method="post"><input name="user"></form>""",
            new Uri("https://example.org/app/page"));

        HtmlInteractionSurfaceItem form = Assert.Single(surfaces, item => item.Kind == "Form");
        Assert.Equal("https://example.org/app/page", form.Url);
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
    public async Task FindInteractionSurfaceAsync_AppliesLinkedScriptFetchOptions() {
        using var server = TestServerCompat.CreateTestServer(async context => {
            await context.Response.WriteAsync("fetch('/api/items');");
        }, null, null);
        using var client = server.CreateClient();
        string html = """<script src="/app.js"></script>""";

        IReadOnlyList<HtmlInteractionSurfaceItem> limited = await HtmlParsingToolbox.FindInteractionSurfaceAsync(
            html,
            server.BaseAddress,
            includeLinkedScripts: true,
            includeExternalLinkedScripts: false,
            client,
            new HtmlHttpFetchOptions { MaximumResponseBytes = 4 });
        IReadOnlyList<HtmlInteractionSurfaceItem> allowed = await HtmlParsingToolbox.FindInteractionSurfaceAsync(
            html,
            server.BaseAddress,
            includeLinkedScripts: true,
            includeExternalLinkedScripts: false,
            client,
            new HtmlHttpFetchOptions { MaximumResponseBytes = 1024 });

        HtmlInteractionSurfaceItem failedDownload = Assert.Single(limited, item => item.Kind == "LinkedEndpoint");
        Assert.Contains("4-byte limit", failedDownload.Metadata, StringComparison.OrdinalIgnoreCase);
        HtmlInteractionSurfaceItem endpoint = Assert.Single(allowed, item => item.Kind == "LinkedEndpoint");
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
    public async Task FindInteractionSurfaceAsync_UsesPageBaseOnceForRelativeDocumentBase() {
        List<string> requestedPaths = new();
        using var server = TestServerCompat.CreateTestServer(async context => {
            requestedPaths.Add(context.Request.Path.Value ?? string.Empty);
            if (context.Request.Path == "/app/assets/app.js") {
                await context.Response.WriteAsync("""fetch("/api/from-relative-base");""");
                return;
            }

            context.Response.StatusCode = 404;
        }, null, null);
        using var client = server.CreateClient();
        string html = """<base href="assets/"><script src="app.js"></script>""";

        IReadOnlyList<HtmlInteractionSurfaceItem> surfaces = await HtmlParsingToolbox.FindInteractionSurfaceAsync(
            html,
            new Uri(server.BaseAddress, "app/page"),
            includeLinkedScripts: true,
            includeExternalLinkedScripts: false,
            client);

        HtmlInteractionSurfaceItem endpoint = Assert.Single(surfaces, item => item.Kind == "LinkedEndpoint");
        Assert.Equal("/api/from-relative-base", endpoint.Url);
        Assert.Contains("/app/assets/app.js", requestedPaths);
        Assert.DoesNotContain("/app/assets/assets/app.js", requestedPaths);
    }

    [Fact]
    public async Task FindInteractionSurfaceAsync_KeepsExternalChecksAnchoredToPageBase() {
        string html = """<base href="https://cdn.example.net/assets/"><script src="app.js"></script>""";

        IReadOnlyList<HtmlInteractionSurfaceItem> surfaces = await HtmlParsingToolbox.FindInteractionSurfaceAsync(
            html,
            new Uri("https://example.org/app/page"),
            includeLinkedScripts: true,
            includeExternalLinkedScripts: false);

        Assert.DoesNotContain(surfaces, item => item.Kind == "LinkedEndpoint");
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
    public void SelectData_ResolvesOpenGraphUrlValuesAgainstBaseUri() {
        string html = """<html><head><meta property="og:image" content="/img.png"><meta property="og:title" content="Docs"></head></html>""";

        IReadOnlyList<HtmlDataItem> items = HtmlParsingToolbox.SelectData(html, new[] { "OpenGraph" }, new Uri("https://example.org/page"));

        HtmlDataItem image = Assert.Single(items, item => item.Name == "image");
        HtmlDataItem title = Assert.Single(items, item => item.Name == "title");
        Assert.Equal("https://example.org/img.png", image.Value);
        Assert.Equal("/img.png", image.RawValue);
        Assert.Equal("Docs", title.Value);
    }

    [Fact]
    public void SelectJavaScriptConfig_PreservesCaseDistinctAppStateKeys() {
        string html = """<script id="__NEXT_DATA__" type="application/json">{"props":{"pageProps":{"id":1,"ID":2}}}</script>""";

        IReadOnlyList<HtmlJavaScriptConfigItem> items = HtmlParsingToolbox.SelectJavaScriptConfig(
            html,
            new[] { "__NEXT_DATA__" },
            propertyPaths: new[] { "props.pageProps.id", "props.pageProps.ID" });

        HtmlJavaScriptConfigItem lower = Assert.Single(items, item => item.PropertyPath == "props.pageProps.id");
        HtmlJavaScriptConfigItem upper = Assert.Single(items, item => item.PropertyPath == "props.pageProps.ID");
        Assert.Equal(1L, lower.Value);
        Assert.Equal(2L, upper.Value);
    }

    [Fact]
    public void SelectJavaScriptConfig_DoesNotDuplicateAssignmentAppStateMatches() {
        string html = """<script>window.__INITIAL_STATE__ = { user: { name: "Ada" } };</script>""";

        IReadOnlyList<HtmlJavaScriptConfigItem> items = HtmlParsingToolbox.SelectJavaScriptConfig(
            html,
            new[] { "__INITIAL_STATE__" },
            propertyPaths: new[] { "user.name" });

        HtmlJavaScriptConfigItem item = Assert.Single(items);
        Assert.Equal("JavaScript", item.Source);
        Assert.Equal("Ada", item.Value);
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

    [Fact]
    public void CompareStaticRendered_DetectsHiddenFieldValueChanges() {
        HtmlStaticRenderedComparison comparison = HtmlParsingToolbox.CompareStaticRendered(
            """<form id="wizard" method="post"><input type="hidden" name="returnUrl" value="/a"><input name="user"></form>""",
            """<form id="wizard" method="post"><input type="hidden" name="returnUrl" value="/b"><input name="user"></form>""",
            new Uri("https://example.org/app/page"));

        HtmlStaticRenderedDelta formDelta = Assert.Single(comparison.Deltas, delta => delta.Kind == "Form");
        Assert.Contains(formDelta.Added, item => item.Contains("returnUrl=/b", StringComparison.Ordinal));
        Assert.Contains(formDelta.Removed, item => item.Contains("returnUrl=/a", StringComparison.Ordinal));
    }

    [Fact]
    public void CompareStaticRendered_DetectsFieldTypeChanges() {
        HtmlStaticRenderedComparison comparison = HtmlParsingToolbox.CompareStaticRendered(
            """<form id="wizard" method="post"><input type="hidden" name="csrf" value="abc"></form>""",
            """<form id="wizard" method="post"><input type="text" name="csrf" value="abc"></form>""",
            new Uri("https://example.org/app/page"));

        HtmlStaticRenderedDelta formDelta = Assert.Single(comparison.Deltas, delta => delta.Kind == "Form");
        Assert.Contains(formDelta.Added, item => item.Contains("Text:csrf=abc", StringComparison.Ordinal));
        Assert.Contains(formDelta.Removed, item => item.Contains("Hidden:csrf=abc", StringComparison.Ordinal));
    }

    [Fact]
    public void CompareStaticRendered_DetectsCaseOnlyValueChanges() {
        HtmlStaticRenderedComparison comparison = HtmlParsingToolbox.CompareStaticRendered(
            """<form id="wizard" method="post"><input type="hidden" name="csrf" value="AbC123"></form>""",
            """<form id="wizard" method="post"><input type="hidden" name="csrf" value="abc123"></form>""",
            new Uri("https://example.org/app/page"));

        HtmlStaticRenderedDelta formDelta = Assert.Single(comparison.Deltas, delta => delta.Kind == "Form");
        Assert.Contains(formDelta.Added, item => item.Contains("csrf=abc123", StringComparison.Ordinal));
        Assert.Contains(formDelta.Removed, item => item.Contains("csrf=AbC123", StringComparison.Ordinal));
    }

    [Fact]
    public void CompareStaticRendered_DetectsDuplicatedRenderedLinks() {
        HtmlStaticRenderedComparison comparison = HtmlParsingToolbox.CompareStaticRendered(
            """<a href="/docs">Docs</a>""",
            """<a href="/docs">Docs</a><a href="/docs">Docs</a>""",
            new Uri("https://example.org/app/page"));

        HtmlStaticRenderedDelta linkDelta = Assert.Single(comparison.Deltas, delta => delta.Kind == "Link");
        Assert.Equal(1, linkDelta.StaticCount);
        Assert.Equal(2, linkDelta.RenderedCount);
        Assert.Single(linkDelta.Added);
        Assert.Empty(linkDelta.Removed);
        Assert.Contains("https://example.org/docs", linkDelta.Added[0], StringComparison.Ordinal);
    }
}
