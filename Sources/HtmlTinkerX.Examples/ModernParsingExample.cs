using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX.Examples;

/// <summary>
/// Demonstrates the static parsers for data commonly embedded in modern HTML pages.
/// </summary>
public static class ModernParsingExample {
    /// <summary>Executes the example logic.</summary>
    public static void Run() {
        const string html = """
            <!doctype html>
            <html>
            <head>
                <link rel="canonical" href="/products/widget" />
                <link rel="alternate" type="application/rss+xml" href="/feed.xml" />
                <link rel="preload" as="image" href="/images/widget-preload.png" />
                <meta name="csrf-token" content="meta-token-123" />
                <script type="application/json" id="settings">
                {"featureFlags":{"checkout":true}}
                </script>
                <script type="application/ld+json">
                {
                    "@context": "https://schema.org",
                    "@type": "Product",
                    "@id": "https://example.org/products/widget",
                    "name": "Widget"
                }
                </script>
                <script id="__NEXT_DATA__" type="application/json">
                {"props":{"pageProps":{"productId":42}}}
                </script>
                <script>
                self.__next_f = self.__next_f || [];
                self.__next_f.push([1, "1:{\"name\":\"Widget\"}\n"]);
                fetch("/api/products/42", { method: "POST" });
                client.post("/graphql", { query: "query ProductDetails { product { id } }" });
                </script>
                <script src="/assets/app.js"></script>
            </head>
            <body>
                <picture>
                    <source type="image/webp" srcset="/images/widget.webp 1x, /images/widget@2x.webp 2x" />
                    <img src="/images/widget.jpg" srcset="/images/widget-small.jpg 480w, /images/widget-large.jpg 960w" sizes="100vw" alt="Widget" />
                </picture>
                <form>
                    <input type="hidden" name="__RequestVerificationToken" value="form-token-456" />
                </form>
            </body>
            </html>
            """;

        const string robots = """
            User-agent: *
            Allow: /
            Disallow: /admin
            Crawl-delay: 5
            Sitemap: /sitemap.xml
            """;
        const string manifestJson = """
            {
              "name": "Example App",
              "short_name": "Example",
              "start_url": "/app",
              "icons": [
                { "src": "/icons/icon-192.png", "sizes": "192x192", "type": "image/png", "purpose": "any maskable" }
              ]
            }
            """;
        const string securityTxt = """
            Contact: /security
            Expires: 2026-12-31T23:59:59Z
            """;

        var jsonLd = HtmlJsonLdParser.Parse(html);
        var scriptData = HtmlScriptDataParser.Parse(html);
        var appState = HtmlAppStateParser.Parse(html);
        var headLinks = HtmlHeadLinkParser.Parse(html, new Uri("https://example.org/"));
        var images = HtmlImageCandidateParser.Parse(html, new Uri("https://example.org/products/widget"));
        var tokens = HtmlTokenParser.Parse(html);
        var reactFlight = HtmlReactFlightParser.Parse(html);
        var endpoints = HtmlJavaScriptEndpointParser.ParseHtml(html);
        var robotsRules = HtmlRobotsParser.Parse(robots, new Uri("https://example.org/robots.txt"));
        var manifest = HtmlWebManifestParser.Parse(manifestJson, new Uri("https://example.org/manifest.webmanifest"));
        var security = HtmlWellKnownParser.Parse(securityTxt, "security.txt", new Uri("https://example.org/.well-known/security.txt"));
        var linkedEndpoints = RunLinkedScriptExampleAsync().GetAwaiter().GetResult();

        Console.WriteLine($"JSON-LD: {jsonLd.FirstOrDefault()?.Type}");
        Console.WriteLine($"Script data: {scriptData.FirstOrDefault()?.Id}");
        Console.WriteLine($"App state: {appState.FirstOrDefault()?.Name}");
        Console.WriteLine($"Canonical: {headLinks.FirstOrDefault(link => link.Rel == "canonical")?.Url}");
        Console.WriteLine($"Images: {images.Count}");
        Console.WriteLine($"Token: {tokens.FirstOrDefault()?.Name}");
        Console.WriteLine($"React Flight rows: {reactFlight.Rows.Count}");
        Console.WriteLine($"Endpoints: {string.Join(", ", endpoints.Select(endpoint => endpoint.Url))}");
        Console.WriteLine($"Linked endpoints: {string.Join(", ", linkedEndpoints.Select(endpoint => endpoint.Url))}");
        Console.WriteLine($"Robots directives: {robotsRules.Count}");
        Console.WriteLine($"Manifest icon: {manifest.Icons.FirstOrDefault()?.Url}");
        Console.WriteLine($"security.txt contact: {security.FirstOrDefault(record => record.Field == "Contact")?.Url}");
    }

    /// <summary>Executes linked JavaScript endpoint discovery with an in-memory HTTP handler.</summary>
    public static async Task<IReadOnlyList<HtmlLinkedJavaScriptEndpoint>> RunLinkedScriptExampleAsync() {
        const string html = """<script src="/assets/app.js"></script>""";
        using HttpClient client = new(new ExampleHttpHandler());
        return await HtmlLinkedJavaScriptEndpointParser
            .ParseAsync(html, new Uri("https://example.org/"), client: client)
            .ConfigureAwait(false);
    }

    private sealed class ExampleHttpHandler : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            string content = request.RequestUri?.AbsolutePath switch {
                "/assets/app.js" => """fetch("/api/linked", { method: "POST" });""",
                _ => string.Empty
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(content)
            });
        }
    }
}
