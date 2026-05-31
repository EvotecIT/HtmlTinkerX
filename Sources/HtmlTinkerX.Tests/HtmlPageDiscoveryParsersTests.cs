using HtmlTinkerX;
using Microsoft.AspNetCore.Http;

namespace HtmlTinkerX.Tests;

public class HtmlPageDiscoveryParsersTests {
    [Fact]
    public void ScriptDataParserFindsGenericJsonScripts() {
        string html = """
            <script type="application/json" id="settings">{"enabled":true}</script>
            <script type="application/json; charset=utf-8" id="qualified">{"qualified":true}</script>
            <script type="application/activity+json">{"name":"activity"}</script>
            <script>const ignored = true;</script>
            """;

        var items = HtmlScriptDataParser.Parse(html);

        Assert.Equal(3, items.Count);
        Assert.Equal("settings", items[0].Id);
        Assert.True(items[0].IsJson);
        Assert.Contains("\"enabled\":true", items[0].RawJson);
        Assert.Equal("qualified", items[1].Id);
    }

    [Fact]
    public async Task LinkedJavaScriptEndpointParserDownloadsSameOriginScripts() {
        using var server = TestServerCompat.CreateTestServer(async context => {
            if (context.Request.Path == "/page") {
                await context.Response.WriteAsync("""<base href="/assets/"><script src="data:text/javascript,fetch('/api/inline')"></script><script type="text/javascript; charset=utf-8" src="app.js"></script>""");
                return;
            }

            if (context.Request.Path == "/assets/app.js") {
                await context.Response.WriteAsync("""fetch("/api/items", { method: "POST" });""");
                return;
            }

            context.Response.StatusCode = 404;
        }, null, null);
        using var client = server.CreateClient();
        string html = await client.GetStringAsync(server.BaseAddress + "page");

        var endpoints = await HtmlLinkedJavaScriptEndpointParser.ParseAsync(html, server.BaseAddress, includeExternal: true, client: client);

        Assert.Equal(2, endpoints.Count);
        Assert.Equal("data:text/javascript,fetch('/api/inline')", endpoints[0].ScriptUrl);
        Assert.False(endpoints[0].IsDownloaded);
        Assert.Equal(server.BaseAddress + "assets/app.js", endpoints[1].ScriptUrl);
        Assert.Equal("/api/items", endpoints[1].Url);
        Assert.Equal("POST", endpoints[1].Method);
        Assert.True(endpoints[1].IsDownloaded);
    }

    [Fact]
    public void ImageCandidateParserFindsSrcSrcSetSourcesAndPreloads() {
        string html = """
            <picture>
              <source type="image/webp" media="(min-width: 800px)" srcset="/hero.webp 1x, /hero@2x.webp 2x" />
              <img src="hero.jpg" srcset="/hero-small.jpg 480w, data:image/svg+xml,<svg></svg> 1x, /hero-large.jpg 960w" sizes="100vw" alt="Hero" />
            </picture>
            <link rel="preload" as="image" href="/preload.png" />
            <link rel="preload" as="image" imagesrcset="/preload-small.png 1x, /preload-large.png 2x" imagesizes="100vw" />
            """;

        var images = HtmlImageCandidateParser.Parse(html, new Uri("https://example.org/page"));

        Assert.Contains(images, image => image.Source == "hero.jpg" && image.Url == "https://example.org/hero.jpg" && image.Element == "img");
        Assert.Contains(images, image => image.WidthDescriptor == "480w");
        Assert.Contains(images, image => image.Source == "data:image/svg+xml,<svg></svg>" && image.Url.StartsWith("data:image/svg+xml,", StringComparison.OrdinalIgnoreCase) && image.PixelDensityDescriptor == "1x");
        Assert.Contains(images, image => image.PixelDensityDescriptor == "2x" && image.Type == "image/webp");
        Assert.Contains(images, image => image.SourceAttribute == "href" && image.Url == "https://example.org/preload.png");
        Assert.Contains(images, image => image.SourceAttribute == "imagesrcset" && image.Source == "/preload-large.png" && image.PixelDensityDescriptor == "2x" && image.Sizes == "100vw");
    }

    [Fact]
    public void ImageCandidateParserResolvesAgainstDocumentBase() {
        string html = """
            <base href="/assets/" />
            <img src="hero.jpg" srcset="small.jpg 1x, large.jpg 2x" />
            """;

        var images = HtmlImageCandidateParser.Parse(html, new Uri("https://example.org/page"));

        Assert.Contains(images, image => image.Source == "hero.jpg" && image.Url == "https://example.org/assets/hero.jpg");
        Assert.Contains(images, image => image.Source == "large.jpg" && image.Url == "https://example.org/assets/large.jpg");
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
              "related_applications": [{ "platform": "play", "url": "/other.webmanifest", "id": "org.example.app" }]
            }
            """;

        HtmlWebManifestDocument manifest = HtmlWebManifestParser.Parse(json, new Uri("https://example.org/manifest.webmanifest"));

        Assert.Equal("Example App", manifest.Name);
        Assert.Equal("https://example.org/app", manifest.StartUrl);
        Assert.Single(manifest.Icons);
        Assert.Equal("https://example.org/icon-192.png", manifest.Icons[0].Url);
        Assert.Single(manifest.Screenshots);
        Assert.Single(manifest.RelatedApplications);
        Assert.Equal("https://example.org/other.webmanifest", manifest.RelatedApplications[0].Url);
    }

    [Fact]
    public void WellKnownParserParsesSecurityHumansAndAdsTxt() {
        string security = """
            # security contact details
            Contact: /security
            Policy: https://example.org/report#scope
            Canonical: /.well-known/security.txt#v1
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
        Assert.Contains(securityRecords, record => record.Field == "Policy" && record.Value == "https://example.org/report#scope" && record.Url == "https://example.org/report#scope");
        Assert.Contains(securityRecords, record => record.Field == "Canonical" && record.Url == "https://example.org/.well-known/security.txt#v1");
        Assert.Contains(humansRecords, record => record.Section == "TEAM" && record.Field == "Developer" && record.Value == "Ada");
        Assert.Contains(adsRecords, record => record.Domain == "example.com" && record.Relationship == "DIRECT");
        Assert.Contains(adsRecords, record => record.Field == "OWNERDOMAIN" && record.Value == "example.org");
    }
}
