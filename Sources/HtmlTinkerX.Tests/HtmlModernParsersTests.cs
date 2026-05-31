using System.Linq;
using HtmlTinkerX;

namespace HtmlTinkerX.Tests;

public class HtmlModernParsersTests {
    [Fact]
    public void JsonLdParserFlattensGraphNodes() {
        string html = """
            <script type="application/ld+json">
            {"@context":"https://schema.org","@graph":[
              {"@type":"Article","@id":"https://example.org/a","headline":"Hello"},
              {"@type":"BreadcrumbList","@id":"https://example.org/b"}
            ]}
            </script>
            """;

        var items = HtmlJsonLdParser.Parse(html);

        Assert.Equal(2, items.Count);
        Assert.Equal("Article", items[0].Type);
        Assert.Equal("https://example.org/a", items[0].Id);
        Assert.Equal("GraphNode", items[0].SourceKind);
    }

    [Fact]
    public void AppStateParserExtractsNextAndAssignedState() {
        string html = """
            <script id="__NEXT_DATA__" type="application/json">{"props":{"pageProps":{"id":7}}}</script>
            <script>window.__INITIAL_STATE__ = { user: { name: "Ada" }, ok: true };</script>
            """;

        var states = HtmlAppStateParser.Parse(html);

        Assert.Equal(2, states.Count);
        Assert.Equal("__NEXT_DATA__", states[0].Name);
        Assert.Equal("ScriptJson", states[0].SourceKind);
        Assert.Equal("__INITIAL_STATE__", states[1].Name);
        Assert.Contains("\"Ada\"", states[1].RawJson);
    }

    [Fact]
    public void HeadLinkParserResolvesDiscoveryLinks() {
        string html = """
            <html><head>
            <link rel="canonical" href="/article" />
            <link rel="alternate" type="application/rss+xml" href="/feed.xml" />
            <link rel="manifest" href="https://cdn.example.net/app.webmanifest" />
            </head></html>
            """;

        var links = HtmlHeadLinkParser.Parse(html, new System.Uri("https://example.org/base/page"));

        Assert.Equal(3, links.Count);
        Assert.Equal("https://example.org/article", links[0].Url);
        Assert.False(links[0].IsExternal);
        Assert.True(links[2].IsExternal);
    }

    [Fact]
    public void TokenParserFindsInputsMetaNonceAndScriptValues() {
        string html = """
            <html><head><meta name="csrf-token" content="meta-token" /></head>
            <body>
            <input type="hidden" name="__RequestVerificationToken" value="form-token" />
            <script nonce="abc123">const csrfToken = "script-token";</script>
            </body></html>
            """;

        var tokens = HtmlTokenParser.Parse(html);

        Assert.Contains(tokens, token => token.Name == "csrf-token" && token.Value == "meta-token");
        Assert.Contains(tokens, token => token.Name == "__RequestVerificationToken" && token.Value == "form-token");
        Assert.Contains(tokens, token => token.Name == "csrfToken" && token.Value == "script-token");
    }

    [Fact]
    public void JavaScriptEndpointParserFindsLikelyEndpoints() {
        string script = """
            fetch("/api/users", { method: "POST" });
            axios.get("https://api.example.org/v1/items");
            client.post("/graphql", { query: "query GetItems { items { id } }" });
            """;

        var endpoints = HtmlJavaScriptEndpointParser.ParseJavaScript(script);

        Assert.Contains(endpoints, endpoint => endpoint.Url == "/api/users" && endpoint.Method == "POST");
        Assert.Contains(endpoints, endpoint => endpoint.Url == "https://api.example.org/v1/items" && endpoint.Client == "axios");
        Assert.Contains(endpoints, endpoint => endpoint.Url == "/graphql" && endpoint.OperationName == "GetItems");
    }

    [Fact]
    public void JavaScriptEndpointParserSkipsNonJavaScriptScriptsInHtml() {
        string html = """
            <script type="application/ld+json">
            {"@context":"https://schema.org","@id":"https://example.org/products/widget"}
            </script>
            <script type="module">
            fetch("/api/products/42", { method: "POST" });
            </script>
            """;

        var endpoints = HtmlJavaScriptEndpointParser.ParseHtml(html);

        Assert.Single(endpoints);
        Assert.Equal("/api/products/42", endpoints[0].Url);
    }

    [Fact]
    public void RobotsParserReturnsGroupsRulesAndSitemaps() {
        string robots = """
            User-agent: *
            Disallow: /private
            Crawl-delay: 2.5
            Sitemap: /sitemap.xml
            """;

        var rules = HtmlRobotsParser.Parse(robots, new System.Uri("https://example.org/robots.txt"));

        Assert.Contains(rules, rule => rule.Directive == "User-agent" && rule.UserAgent == "*");
        Assert.Contains(rules, rule => rule.Directive == "Disallow" && rule.Path == "/private");
        Assert.Contains(rules, rule => rule.Directive == "Crawl-delay" && rule.CrawlDelay == 2.5m);
        Assert.Contains(rules, rule => rule.Directive == "Sitemap" && rule.Url == "https://example.org/sitemap.xml");
    }
}
