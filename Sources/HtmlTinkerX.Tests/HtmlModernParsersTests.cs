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
            <script>window.__INITIAL_STATE__ = { user: { name: "Ada" }, ok: true, enabled: !0, disabled: !1 };</script>
            <script>const __APOLLO_STATE__ = { cache: { id: 42 } };</script>
            """;

        var states = HtmlAppStateParser.Parse(html);

        Assert.Equal(3, states.Count);
        Assert.Equal("__NEXT_DATA__", states[0].Name);
        Assert.Equal("ScriptJson", states[0].SourceKind);
        Assert.Equal("__INITIAL_STATE__", states[1].Name);
        Assert.Contains("\"Ada\"", states[1].RawJson);
        Assert.Contains("\"enabled\":true", states[1].RawJson);
        Assert.Contains("\"disabled\":false", states[1].RawJson);
        Assert.Equal("__APOLLO_STATE__", states[2].Name);
        Assert.Equal("VariableDeclaration", states[2].SourceKind);
    }

    [Fact]
    public void HeadLinkParserResolvesDiscoveryLinks() {
        string html = """
            <html><head>
            <link rel="canonical" href="/article" />
            <link rel="alternate" type="application/rss+xml" href="/feed.xml" />
            <meta name="description" content="A short summary" />
            <meta property="og:image" content="/social.png" />
            <link rel="manifest" href="https://cdn.example.net/app.webmanifest" />
            </head></html>
            """;

        var links = HtmlHeadLinkParser.Parse(html, new System.Uri("https://example.org/base/page"));

        Assert.Equal(5, links.Count);
        Assert.Equal("https://example.org/article", links[0].Url);
        Assert.False(links[0].IsExternal);
        Assert.Equal(string.Empty, links[2].Url);
        Assert.Equal("https://example.org/social.png", links[3].Url);
        Assert.True(links[4].IsExternal);
    }

    [Fact]
    public void HeadLinkParserResolvesAgainstDocumentBase() {
        string html = """
            <html><head>
            <base href="/assets/" />
            <link rel="manifest" href="app.webmanifest" />
            <meta property="og:image" content="social.png" />
            </head></html>
            """;

        var links = HtmlHeadLinkParser.Parse(html, new System.Uri("https://example.org/page"));

        Assert.Equal("https://example.org/assets/app.webmanifest", links[0].Url);
        Assert.Equal("https://example.org/assets/social.png", links[1].Url);
        Assert.False(links[0].IsExternal);
    }

    [Fact]
    public void HeadLinkParserResolvesAgainstAbsoluteDocumentBaseWithoutBaseUri() {
        string html = """
            <html><head>
            <base href="https://example.org/assets/" />
            <link rel="canonical" href="docs" />
            </head></html>
            """;

        var links = HtmlHeadLinkParser.Parse(html);

        Assert.Single(links);
        Assert.Equal("https://example.org/assets/docs", links[0].Url);
    }

    [Fact]
    public void HeadLinkParserReportsSelectorsByElementPosition() {
        string html = """
            <html><head>
            <meta name="description" content="Summary" />
            <meta property="og:title" content="Title" />
            <link rel="canonical" href="/article" />
            </head></html>
            """;

        var links = HtmlHeadLinkParser.Parse(html, new System.Uri("https://example.org/page"));

        Assert.Equal("meta:nth-of-type(1)", links[0].Selector);
        Assert.Equal("meta:nth-of-type(2)", links[1].Selector);
        Assert.Equal("link:nth-of-type(1)", links[2].Selector);
    }

    [Fact]
    public void TokenParserFindsInputsMetaNonceAndScriptValues() {
        string html = """
            <html><head><meta name="csrf-token" content="meta-token" /></head>
            <body>
            <input type="hidden" name="__RequestVerificationToken" value="form-token" />
            <script nonce="abc123">const csrfToken = "script-token"; window.App = {"csrfToken":"quoted-token"};</script>
            </body></html>
            """;

        var tokens = HtmlTokenParser.Parse(html);

        Assert.Contains(tokens, token => token.Name == "csrf-token" && token.Value == "meta-token");
        Assert.Contains(tokens, token => token.Name == "__RequestVerificationToken" && token.Value == "form-token");
        Assert.Contains(tokens, token => token.Name == "nonce" && token.Value == "abc123");
        Assert.Contains(tokens, token => token.Name == "csrfToken" && token.Value == "script-token");
        Assert.Contains(tokens, token => token.Name == "csrfToken" && token.Value == "quoted-token");
    }

    [Fact]
    public void ExternalDetectionComparesSchemeHostAndPort() {
        var baseUri = new System.Uri("https://example.org/");

        Assert.False(HtmlModernParserUtilities.IsExternal("https://example.org/app.js", baseUri));
        Assert.True(HtmlModernParserUtilities.IsExternal("http://example.org/app.js", baseUri));
        Assert.True(HtmlModernParserUtilities.IsExternal("https://example.org:8443/app.js", baseUri));
        Assert.True(HtmlModernParserUtilities.IsExternal("https://cdn.example.org/app.js", baseUri));
    }

    [Fact]
    public void JavaScriptEndpointParserFindsLikelyEndpoints() {
        string script = """
            fetch("/api/users", { method: "POST" });
            fetch("api/reports", { method: "DELETE" });
            axios.get("https://api.example.org/v1/items");
            axios.get("graphql");
            client.post("/graphql", { query: "query GetItems { items { id } }" });
            apiClient.post("/api/wrapped");
            http.delete("/api/old-items");
            fetch(`/api/template/${id}`);
            fetch("/api/no-method");
            fetch("/api/with-method", { method: "POST" });
            const xhr = new XMLHttpRequest();
            xhr.open("PATCH", "/api/profile");
            xhr.send();
            $.ajax({ url: "/api/jquery", type: "PUT" });
            """;

        var endpoints = HtmlJavaScriptEndpointParser.ParseJavaScript(script);

        Assert.Contains(endpoints, endpoint => endpoint.Url == "/api/users" && endpoint.Method == "POST");
        Assert.Contains(endpoints, endpoint => endpoint.Url == "api/reports" && endpoint.Method == "DELETE");
        Assert.Contains(endpoints, endpoint => endpoint.Url == "https://api.example.org/v1/items" && endpoint.Client == "axios");
        Assert.Contains(endpoints, endpoint => endpoint.Url == "graphql" && endpoint.Client == "axios");
        Assert.Contains(endpoints, endpoint => endpoint.Url == "/graphql" && endpoint.OperationName == "GetItems");
        Assert.Contains(endpoints, endpoint => endpoint.Url == "/api/wrapped" && endpoint.Method == "POST" && endpoint.Client == "client");
        Assert.Contains(endpoints, endpoint => endpoint.Url == "/api/old-items" && endpoint.Method == "DELETE" && endpoint.Client == "client");
        Assert.Contains(endpoints, endpoint => endpoint.Url == "/api/template/" && endpoint.Client == "fetch");
        Assert.Contains(endpoints, endpoint => endpoint.Url == "/api/no-method" && endpoint.Method == string.Empty);
        Assert.Contains(endpoints, endpoint => endpoint.Url == "/api/with-method" && endpoint.Method == "POST");
        Assert.Contains(endpoints, endpoint => endpoint.Url == "/api/profile" && endpoint.Method == "PATCH" && endpoint.Client == "XMLHttpRequest");
        Assert.Contains(endpoints, endpoint => endpoint.Url == "/api/jquery" && endpoint.Method == "PUT" && endpoint.Client == "jQuery.ajax");
    }

    [Fact]
    public void JavaScriptEndpointParserSkipsNonJavaScriptScriptsInHtml() {
        string html = """
            <script type="application/ld+json">
            {"@context":"https://schema.org","@id":"https://example.org/products/widget"}
            </script>
            <script type="module; charset=utf-8">
            fetch("/api/products/42", { method: "POST" });
            </script>
            """;

        var endpoints = HtmlJavaScriptEndpointParser.ParseHtml(html);

        Assert.Single(endpoints);
        Assert.Equal("/api/products/42", endpoints[0].Url);
        Assert.Equal(1, endpoints[0].ScriptIndex);
        Assert.Equal("script:nth-of-type(2)", endpoints[0].Selector);
    }

    [Fact]
    public void JavaScriptEndpointParserReportsInlineScriptProvenance() {
        string html = """
            <script>console.log("first")</script>
            <script id="client-api">fetch("/api/products/42", { method: "POST" });</script>
            """;

        var endpoints = HtmlJavaScriptEndpointParser.ParseHtml(html);

        Assert.Single(endpoints);
        Assert.Equal("/api/products/42", endpoints[0].Url);
        Assert.Equal(1, endpoints[0].ScriptIndex);
        Assert.Equal("script#client-api", endpoints[0].Selector);
    }

    [Fact]
    public void RobotsParserReturnsGroupsRulesAndSitemaps() {
        string robots = """
            User-agent: *
            User-agent: ExampleBot
            Disallow: /private
            Crawl-delay: 2.5
            Sitemap: /sitemap.xml
            """;

        var rules = HtmlRobotsParser.Parse(robots, new System.Uri("https://example.org/robots.txt"));

        Assert.Contains(rules, rule => rule.Directive == "User-agent" && rule.UserAgent == "*");
        Assert.Contains(rules, rule => rule.Directive == "Disallow" && rule.Path == "/private");
        Assert.Contains(rules, rule => rule.Directive == "Crawl-delay" && rule.CrawlDelay == 2.5m);
        Assert.Contains(rules, rule => rule.Directive == "Sitemap" && rule.Url == "https://example.org/sitemap.xml");
        Assert.Single(rules, rule => rule.Directive == "Sitemap");
    }
}
