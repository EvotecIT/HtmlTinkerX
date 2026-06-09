Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'Modern parsing cmdlets' {
    BeforeAll {
        $script:Html = @'
<html>
<head>
<link rel="canonical" href="/article" />
<link rel="alternate" type="application/rss+xml" href="/feed.xml" />
<meta name="description" content="A short summary" />
<meta name="csrf-token" content="meta-token" />
<script>console.log("first script");</script>
<script type="application/ld+json">
{"@context":"https://schema.org","@type":"Article","@id":"https://example.org/article","headline":"Hello"}
</script>
<script id="__NEXT_DATA__" type="application/json">{"props":{"pageProps":{"id":7}}}</script>
</head>
<body>
<input type="hidden" name="__RequestVerificationToken" value="form-token" />
<script>
window.__INITIAL_STATE__ = { user: { name: "Ada" }, ok: true, enabled: !0, disabled: !1 };
const __APOLLO_STATE__ = { cache: { id: 42 } };
fetch("/api/users", { method: "POST" });
fetch("api/reports", { method: "DELETE" });
fetch(`/api/template/${id}`);
fetch("/api/no-method");
fetch("/api/with-method", { method: "POST" });
apiClient.post("/api/wrapped");
window.App = {"csrfToken":"quoted-token"};
const csrfToken = "script-token";
</script>
</body>
</html>
'@
    }

    It 'extracts JSON-LD' {
        $items = ConvertFrom-HtmlJsonLd -Content $script:Html

        $items | Should -HaveCount 1
        $items[0].Type | Should -Be 'Article'
        $items[0].ScriptIndex | Should -Be 1
    }

    It 'extracts JSON-LD from selected HtmlNode pipeline input' {
        $items = ConvertFrom-HTML -Content $script:Html |
            Select-HtmlNode -XPath '//script[@type="application/ld+json"]' |
            ConvertFrom-HtmlJsonLd

        $items | Should -HaveCount 1
        $items[0].Type | Should -Be 'Article'
        $items[0].Id | Should -Be 'https://example.org/article'
        $items[0].ScriptIndex | Should -Be 1
    }

    It 'preserves JSON-LD source script indexes from multiple selected HtmlNode inputs' {
        $html = @'
<html><head>
<script>console.log("one")</script>
<script type="application/ld+json">{"@context":"https://schema.org","@type":"Article","headline":"One"}</script>
<script>console.log("two")</script>
<script type="application/ld+json">{"@context":"https://schema.org","@type":"Article","headline":"Two"}</script>
</head></html>
'@
        $items = ConvertFrom-HTML -Content $html |
            Select-HtmlNode -XPath '//script[@type="application/ld+json"]' |
            ConvertFrom-HtmlJsonLd

        $items | Should -HaveCount 2
        $items.ScriptIndex | Should -Be @(1, 3)
    }

    It 'preserves JSON-LD script indexes from HtmlNode document input' {
        $items = ConvertFrom-HTML -Content $script:Html |
            ConvertFrom-HtmlJsonLd

        $items | Should -HaveCount 1
        $items[0].ScriptIndex | Should -Be 1
        $items[0].Id | Should -Be 'https://example.org/article'
    }

    It 'filters JSON-LD by type and emits parsed objects' {
        $html = @'
<html><head>
<script type="application/ld+json">
[
  {"@context":"https://schema.org","@type":"Article","headline":"Hello"},
  {"@context":"https://schema.org","@type":"Product","name":"Widget"}
]
</script>
</head></html>
'@
        $items = ConvertFrom-HtmlJsonLd -Content $html -Type Product
        $objects = ConvertFrom-HtmlJsonLd -Content $html -Type Product -AsObject

        $items | Should -HaveCount 1
        $items[0].Type | Should -Be 'Product'
        $objects | Should -HaveCount 1
        $objects[0].name | Should -Be 'Widget'
    }

    It 'extracts app state' {
        $state = ConvertFrom-HtmlAppState -Content $script:Html

        $state.Name | Should -Contain '__NEXT_DATA__'
        $state.Name | Should -Contain '__INITIAL_STATE__'
        $state.Name | Should -Contain '__APOLLO_STATE__'
        ($state | Where-Object Name -EQ '__INITIAL_STATE__').RawJson | Should -Match '"enabled":true'
        ($state | Where-Object Name -EQ '__INITIAL_STATE__').RawJson | Should -Match '"disabled":false'
    }

    It 'extracts app state from selected HtmlNode pipeline input' {
        $state = ConvertFrom-HTML -Content $script:Html |
            Select-HtmlNode -XPath '//script[@id="__NEXT_DATA__"]' |
            ConvertFrom-HtmlAppState

        $state | Should -HaveCount 1
        $state[0].Name | Should -Be '__NEXT_DATA__'
    }

    It 'extracts head links' {
        $links = ConvertFrom-HtmlHeadLink -Content $script:Html -BaseUrl 'https://example.org/base/page'

        $links.Url | Should -Contain 'https://example.org/article'
        $links.Url | Should -Contain 'https://example.org/feed.xml'
        ($links | Where-Object Name -EQ 'description').Url | Should -Be ''
    }

    It 'selects tokens' {
        $tokens = Select-HtmlToken -Content $script:Html

        $tokens.Value | Should -Contain 'meta-token'
        $tokens.Value | Should -Contain 'form-token'
        $tokens.Value | Should -Contain 'script-token'
        $tokens.Value | Should -Contain 'quoted-token'
    }

    It 'discovers JavaScript endpoints from HTML scripts' {
        $endpoints = ConvertFrom-JavaScriptEndpoint -Content $script:Html -Html

        $endpoints.Url | Should -Contain '/api/users'
        $endpoints.Url | Should -Contain 'api/reports'
        $endpoints.Url | Should -Contain '/api/template/'
        $endpoints.Url | Should -Contain '/api/wrapped'
        ($endpoints | Where-Object Url -EQ '/api/users').Method | Should -Be 'POST'
        ($endpoints | Where-Object Url -EQ 'api/reports').Method | Should -Be 'DELETE'
        ($endpoints | Where-Object Url -EQ '/api/wrapped').Method | Should -Be 'POST'
        ($endpoints | Where-Object Url -EQ '/api/no-method').Method | Should -Be ''
        ($endpoints | Where-Object Url -EQ '/api/with-method').Method | Should -Be 'POST'
    }

    It 'parses robots.txt' {
        $rules = ConvertFrom-RobotsTxt -Content "User-agent: *`nUser-agent: ExampleBot`nDisallow: /private`nSitemap: /sitemap.xml" -BaseUrl 'https://example.org/robots.txt'

        $rules.Directive | Should -Contain 'User-agent'
        $rules.Path | Should -Contain '/private'
        $rules.Url | Should -Contain 'https://example.org/sitemap.xml'
        @($rules | Where-Object Directive -EQ 'Sitemap') | Should -HaveCount 1
    }

    It 'exports aliases' {
        Get-Command ConvertFrom-JSEndpoint | Should -Not -BeNullOrEmpty
    }
}
