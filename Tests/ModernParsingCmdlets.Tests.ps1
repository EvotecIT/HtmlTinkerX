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
<script type="application/ld+json">
{"@context":"https://schema.org","@type":"Article","@id":"https://example.org/article","headline":"Hello"}
</script>
<script id="__NEXT_DATA__" type="application/json">{"props":{"pageProps":{"id":7}}}</script>
</head>
<body>
<input type="hidden" name="__RequestVerificationToken" value="form-token" />
<script>
window.__INITIAL_STATE__ = { user: { name: "Ada" }, ok: true };
const __APOLLO_STATE__ = { cache: { id: 42 } };
fetch("/api/users", { method: "POST" });
fetch("api/reports", { method: "DELETE" });
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
    }

    It 'extracts app state' {
        $state = ConvertFrom-HtmlAppState -Content $script:Html

        $state.Name | Should -Contain '__NEXT_DATA__'
        $state.Name | Should -Contain '__INITIAL_STATE__'
        $state.Name | Should -Contain '__APOLLO_STATE__'
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
    }

    It 'discovers JavaScript endpoints from HTML scripts' {
        $endpoints = ConvertFrom-JavaScriptEndpoint -Content $script:Html -Html

        $endpoints.Url | Should -Contain '/api/users'
        $endpoints.Url | Should -Contain 'api/reports'
        ($endpoints | Where-Object Url -EQ '/api/users').Method | Should -Be 'POST'
        ($endpoints | Where-Object Url -EQ 'api/reports').Method | Should -Be 'DELETE'
    }

    It 'parses robots.txt' {
        $rules = ConvertFrom-RobotsTxt -Content "User-agent: *`nDisallow: /private`nSitemap: /sitemap.xml" -BaseUrl 'https://example.org/robots.txt'

        $rules.Directive | Should -Contain 'User-agent'
        $rules.Path | Should -Contain '/private'
        $rules.Url | Should -Contain 'https://example.org/sitemap.xml'
    }

    It 'exports aliases' {
        Get-Command ConvertFrom-JSEndpoint | Should -Not -BeNullOrEmpty
    }
}
