using HtmlTinkerX;
using Microsoft.AspNetCore.Http;

namespace HtmlTinkerX.Tests;

public class HtmlPageDiscoveryParsersTests {
    [Fact]
    public void ScriptDataParserFindsGenericJsonScripts() {
        string html = """
            <script type="application/json" id="settings">{"enabled":true}</script>
            <script type="application/activity+json">{"name":"activity"}</script>
            <script>const ignored = true;</script>
            """;

        var items = HtmlScriptDataParser.Parse(html);

        Assert.Equal(2, items.Count);
        Assert.Equal("settings", items[0].Id);
        Assert.True(items[0].IsJson);
        Assert.Contains("\"enabled\":true", items[0].RawJson);
    }

    [Fact]
    public async Task LinkedJavaScriptEndpointParserDownloadsSameOriginScripts() {
        using var server = TestServerCompat.CreateTestServer(async context => {
            if (context.Request.Path == "/page") {
                await context.Response.WriteAsync("""<script src="/app.js"></script><script src="https://cdn.example.org/external.js"></script>""");
                return;
            }

            if (context.Request.Path == "/app.js") {
                await context.Response.WriteAsync("""fetch("/api/items", { method: "POST" });""");
                return;
            }

            context.Response.StatusCode = 404;
        }, null, null);
        using var client = server.CreateClient();
        string html = await client.GetStringAsync(server.BaseAddress + "page");

        var endpoints = await HtmlLinkedJavaScriptEndpointParser.ParseAsync(html, server.BaseAddress, client: client);

        Assert.Single(endpoints);
        Assert.Equal("/api/items", endpoints[0].Url);
        Assert.Equal("POST", endpoints[0].Method);
        Assert.True(endpoints[0].IsDownloaded);
    }

    [Fact]
    public void ImageCandidateParserFindsSrcSrcSetSourcesAndPreloads() {
        string html = """
            <picture>
              <source type="image/webp" media="(min-width: 800px)" srcset="/hero.webp 1x, /hero@2x.webp 2x" />
              <img src="/hero.jpg" srcset="/hero-small.jpg 480w, /hero-large.jpg 960w" sizes="100vw" alt="Hero" />
            </picture>
            <link rel="preload" as="image" href="/preload.png" />
            """;

        var images = HtmlImageCandidateParser.Parse(html, new Uri("https://example.org/page"));

        Assert.Contains(images, image => image.Source == "/hero.jpg" && image.Element == "img");
        Assert.Contains(images, image => image.WidthDescriptor == "480w");
        Assert.Contains(images, image => image.PixelDensityDescriptor == "2x" && image.Type == "image/webp");
        Assert.Contains(images, image => image.SourceAttribute == "href" && image.Url == "https://example.org/preload.png");
    }

    [Fact]
    public void WebManifestParserResolvesImagesAndStartUrl() {
        string json = """
            {
              "name": "Example App",
              "short_name": "Example",
              "start_url": "/app",
              "scope": "/",
              "display": "standalone",
              "icons": [{ "src": "/icon-192.png", "sizes": "192x192", "type": "image/png", "purpose": "any maskable" }],
              "screenshots": [{ "src": "/screen.png", "sizes": "1280x720", "label": "Home" }],
              "related_applications": [{ "platform": "play", "id": "org.example.app" }]
            }
            """;

        HtmlWebManifestDocument manifest = HtmlWebManifestParser.Parse(json, new Uri("https://example.org/manifest.webmanifest"));

        Assert.Equal("Example App", manifest.Name);
        Assert.Equal("https://example.org/app", manifest.StartUrl);
        Assert.Single(manifest.Icons);
        Assert.Equal("https://example.org/icon-192.png", manifest.Icons[0].Url);
        Assert.Single(manifest.Screenshots);
        Assert.Single(manifest.RelatedApplications);
    }

    [Fact]
    public void WellKnownParserParsesSecurityHumansAndAdsTxt() {
        string security = """
            Contact: /security
            Expires: 2026-12-31T23:59:59Z
            """;
        string humans = """
            /* TEAM */
            Developer: Ada
            """;
        string ads = """
            example.com, pub-123, DIRECT, f08c47fec0942fa0
            OWNERDOMAIN=example.org
            """;

        var securityRecords = HtmlWellKnownParser.Parse(security, "security.txt", new Uri("https://example.org/.well-known/security.txt"));
        var humansRecords = HtmlWellKnownParser.Parse(humans, "humans.txt");
        var adsRecords = HtmlWellKnownParser.Parse(ads, "ads.txt");

        Assert.Contains(securityRecords, record => record.Field == "Contact" && record.Url == "https://example.org/security");
        Assert.Contains(humansRecords, record => record.Section == "TEAM" && record.Field == "Developer" && record.Value == "Ada");
        Assert.Contains(adsRecords, record => record.Domain == "example.com" && record.Relationship == "DIRECT");
        Assert.Contains(adsRecords, record => record.Field == "OWNERDOMAIN" && record.Value == "example.org");
    }
}
