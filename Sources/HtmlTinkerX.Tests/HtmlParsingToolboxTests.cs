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
}
