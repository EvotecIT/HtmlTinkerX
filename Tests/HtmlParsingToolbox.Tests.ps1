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
            <input type="hidden" name="returnUrl" value="/dashboard" />
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

    It 'applies property paths to framework app-state payloads' {
        $html = @'
<html><head>
<script id="__NEXT_DATA__" type="application/json">
{"props":{"pageProps":{"id":42,"slug":"docs"}}}
</script>
</head></html>
'@
        $state = Select-HtmlJavaScriptConfig -Content $html -Name __NEXT_DATA__ -PropertyPath props.pageProps.id

        $state | Should -HaveCount 1
        $state[0].Source | Should -Be 'AppState'
        $state[0].PropertyPath | Should -Be 'props.pageProps.id'
        $state[0].Value | Should -Be 42
    }

    It 'keeps named JavaScript config searches tolerant' {
        $html = @'
<html><body>
<script>if (</script>
<script>window.__CONFIG__ = { api: { baseUrl: "/api" } };</script>
</body></html>
'@
        $config = Select-HtmlJavaScriptConfig -Content $html -Name window.__CONFIG__ -PropertyPath api.baseUrl

        $config | Should -HaveCount 1
        $config[0].Value | Should -Be '/api'
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
        ($surface | Where-Object { $_.Kind -eq 'Field' -and $_.Name -eq 'returnUrl' }).Value | Should -Be '/dashboard'
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

    It 'detects fields added to an existing rendered form' {
        $static = '<form id="login" method="post" action="/login"><input name="user" /></form>'
        $rendered = '<form id="login" method="post" action="/login"><input name="user" /><input type="hidden" name="csrf" value="abc" /></form>'

        $comparison = Compare-HtmlStaticRendered -StaticContent $static -RenderedContent $rendered
        $formDelta = $comparison.Deltas | Where-Object Kind -EQ 'Form'

        ($formDelta.Added -join '|') | Should -Match 'csrf'
    }

    It 'does not report unchanged anonymous forms as removed when rendering inserts an earlier form' {
        $static = '<form method="post" action="/login"><input name="user" /></form>'
        $rendered = '<form method="get" action="/search"><input name="q" /></form><form method="post" action="/login"><input name="user" /></form>'

        $comparison = Compare-HtmlStaticRendered -StaticContent $static -RenderedContent $rendered
        $formDelta = $comparison.Deltas | Where-Object Kind -EQ 'Form'

        ($formDelta.Removed -join '|') | Should -Not -Match 'user'
        ($formDelta.Added -join '|') | Should -Match 'q'
    }

    It 'returns submitted selected option values when parsing forms' {
        $html = @'
<form id="plan">
  <select name="tier">
    <option value="basic" selected>Basic</option>
    <option value="pro">Pro</option>
  </select>
</form>
'@
        $form = ConvertFrom-HtmlForm -Content $html

        ($form.Fields | Where-Object Name -EQ 'tier').Value | Should -Be 'basic'
    }

    It 'preserves empty selected option values when parsing forms' {
        $html = @'
<form id="plan">
  <select name="tier">
    <option value="basic">Basic</option>
    <option value="" selected>None</option>
  </select>
</form>
'@
        $form = ConvertFrom-HtmlForm -Content $html

        ($form.Fields | Where-Object Name -EQ 'tier').Value | Should -Be ''
    }

    It 'does not default empty multi-selects to the first option' {
        $html = @'
<form id="plan">
  <select name="tier" multiple>
    <option value="basic">Basic</option>
    <option value="pro">Pro</option>
  </select>
</form>
'@
        $form = ConvertFrom-HtmlForm -Content $html

        ($form.Fields | Where-Object Name -EQ 'tier').Value | Should -Be ''
    }

    It 'returns default checkbox and radio values when no value attribute is present' {
        $html = @'
<form id="prefs">
  <input type="checkbox" name="remember" checked>
  <input type="radio" name="mode" checked>
</form>
'@
        $form = ConvertFrom-HtmlForm -Content $html

        ($form.Fields | Where-Object Name -EQ 'remember').Value | Should -Be 'on'
        ($form.Fields | Where-Object Name -EQ 'mode').Value | Should -Be 'on'
    }

    It 'keeps configured HTTP defaults when page credentials are used' {
        try {
            [HtmlTinkerX.HtmlHttpClientFactory]::DefaultTimeout = [TimeSpan]::FromSeconds(7)
            [HtmlTinkerX.HtmlHttpClientFactory]::DefaultHeaders['X-Toolbox-Test'] = 'Yes'
            $credential = [System.Net.NetworkCredential]::new('user', 'pass')

            $client = [HtmlTinkerX.HtmlHttpClientFactory]::Create([string] $null, [System.Net.ICredentials] $null, [System.Net.ICredentials] $credential)

            $client.Timeout.TotalSeconds | Should -Be 7
            $client.DefaultRequestHeaders.GetValues('X-Toolbox-Test') | Should -Contain 'Yes'
        } finally {
            if ($client) {
                $client.Dispose()
            }
            [HtmlTinkerX.HtmlHttpClientFactory]::DefaultHeaders.Remove('X-Toolbox-Test') | Out-Null
            [HtmlTinkerX.HtmlHttpClientFactory]::DefaultTimeout = [TimeSpan]::FromSeconds(100)
            [HtmlTinkerX.HtmlHttpClientFactory]::ResetShared()
        }
    }
}
