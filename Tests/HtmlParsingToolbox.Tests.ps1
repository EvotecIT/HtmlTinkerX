Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'HTML parsing toolbox cmdlets' {
    BeforeAll {
        $script:Html = @'
<!doctype html>
<html>
<head>
    <base href="https://example.org/app/" />
    <title>Toolbox</title>
    <meta name="description" content="Toolbox page" />
    <meta property="og:title" content="Toolbox OG" />
    <meta name="csrf-token" content="meta-token" />
    <link rel="canonical" href="/toolbox" />
    <style>
        .used { color: green; }
        .unused { color: red; }
        form[data-mode='login'] input[name='csrfToken'] { display: none; }
    </style>
    <script type="application/ld+json">
    {"@context":"https://schema.org","@type":"Article","@id":"https://example.org/toolbox","headline":"Toolbox"}
    </script>
    <script id="settings" type="application/json">{"feature":true}</script>
</head>
<body>
    <main class="used">
        <a href="/docs">Docs</a>
        <form id="login" data-mode="login" method="post" action="/api/login">
            <input type="hidden" name="csrfToken" value="form-token" />
            <input type="text" name="user" />
        </form>
    </main>
    <script>
        window.__CONFIG__ = { api: { baseUrl: "/api" }, feature: true };
        window.__INITIAL_STATE__ = { user: { name: "Ada" } };
        fetch("/api/profile", { method: "POST" });
    </script>
</body>
</html>
'@
        $script:RenderedHtml = $script:Html -replace '</main>', '<a href="/rendered">Rendered</a></main>' -replace '</body>', '<form id="dynamic" method="get" action="/api/dynamic"></form></body>'
    }

    It 'exports the toolbox commands and aliases' {
        Get-Command Select-HtmlData | Should -Not -BeNullOrEmpty
        Get-Command Select-HtmlJavaScriptConfig | Should -Not -BeNullOrEmpty
        Get-Command Select-HtmlJSConfig | Should -Not -BeNullOrEmpty
        Get-Command Select-HtmlStyleUsage | Should -Not -BeNullOrEmpty
        Get-Command Find-HtmlInteractionSurface | Should -Not -BeNullOrEmpty
        Get-Command Compare-HtmlStaticRendered | Should -Not -BeNullOrEmpty
    }

    It 'normalizes structured data families with provenance' {
        $items = Select-HtmlData -Content $script:Html -Kind JsonLd,OpenGraph,Meta,HeadLink,ScriptData,AppState,Token,Form,Link -BaseUrl 'https://example.org/app/page'

        ($items | Where-Object Kind -EQ 'JsonLd').Type | Should -Contain 'Article'
        ($items | Where-Object Kind -EQ 'OpenGraph').Name | Should -Contain 'title'
        ($items | Where-Object Kind -EQ 'HeadLink').Value | Should -Contain 'https://example.org/toolbox'
        ($items | Where-Object Kind -EQ 'ScriptData').Name | Should -Contain 'settings'
        ($items | Where-Object Kind -EQ 'AppState').Name | Should -Contain '__INITIAL_STATE__'
        ($items | Where-Object Kind -EQ 'Token').Value | Should -Contain 'form-token'
        ($items | Where-Object Kind -EQ 'Form').Selector | Should -Contain 'form#login'
        ($items | Where-Object Kind -EQ 'Link').Value | Should -Contain 'https://example.org/docs'
    }

    It 'accepts HtmlNode pipeline input for normalized data' {
        $items = ConvertFrom-HTML -Content $script:Html |
            Select-HtmlNode -XPath '//head' |
            Select-HtmlData -Kind JsonLd,Meta,HeadLink,ScriptData

        $items.Kind | Should -Contain 'JsonLd'
        $items.Kind | Should -Contain 'ScriptData'
        $items.Kind | Should -Contain 'HeadLink'
    }

    It 'extracts JavaScript config values by property path' {
        $config = Select-HtmlJavaScriptConfig -Content $script:Html -Name window.__CONFIG__ -PropertyPath api.baseUrl
        $state = Select-HtmlJavaScriptConfig -Content $script:Html -Name __INITIAL_STATE__ -NoAppState

        $config | Should -HaveCount 1
        $config[0].Value | Should -Be '/api'
        $config[0].Selector | Should -Be 'script:nth-of-type(3)'
        $state.Name | Should -Contain '__INITIAL_STATE__'
    }

    It 'reports CSS selector usage against HTML' {
        $usage = Select-HtmlStyleUsage -Content $script:Html

        ($usage | Where-Object Selector -EQ '.used').IsUsed | Should -BeTrue
        ($usage | Where-Object Selector -EQ '.unused').IsUsed | Should -BeFalse
        ($usage | Where-Object Selector -Match 'csrfToken').MatchCount | Should -Be 1
    }

    It 'finds forms, tokens, and inline JavaScript endpoints as interaction surfaces' {
        $surface = Find-HtmlInteractionSurface -Content $script:Html

        ($surface | Where-Object Kind -EQ 'Form').Url | Should -Contain '/api/login'
        ($surface | Where-Object Kind -EQ 'Field').Name | Should -Contain 'csrfToken'
        ($surface | Where-Object Kind -EQ 'Token').Value | Should -Contain 'form-token'
        ($surface | Where-Object Kind -EQ 'Endpoint').Url | Should -Contain '/api/profile'
        ($surface | Where-Object Kind -EQ 'Endpoint').Method | Should -Contain 'POST'
    }

    It 'compares static and rendered HTML signatures' {
        $comparison = Compare-HtmlStaticRendered -StaticContent $script:Html -RenderedContent $script:RenderedHtml -BaseUrl 'https://example.org/app/page'
        $linkDelta = $comparison.Deltas | Where-Object Kind -EQ 'Link'
        $formDelta = $comparison.Deltas | Where-Object Kind -EQ 'Form'

        $comparison.RenderedLinkCount | Should -BeGreaterThan $comparison.StaticLinkCount
        $comparison.RenderedFormCount | Should -BeGreaterThan $comparison.StaticFormCount
        ($linkDelta.Added -join '|') | Should -Match 'Rendered'
        ($formDelta.Added -join '|') | Should -Match 'dynamic'
    }
}
