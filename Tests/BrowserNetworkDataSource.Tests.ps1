Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'Browser network data sources' {
    It 'exports the network data-source bridge command' {
        Get-Command Find-HtmlBrowserDataSource | Should -Not -BeNullOrEmpty
        (Get-Command Find-HtmlBrowserDataSource).Parameters.Keys | Should -Contain 'ResponseBodyMaxBytes'
        (Get-Command Find-HtmlBrowserDataSource).Parameters.Keys | Should -Contain 'ResponseBodyResourceType'
        (Get-Command Find-HtmlBrowserDataSource).Parameters.Keys | Should -Contain 'RedactResponseBody'
    }

    It 'turns observed fetch responses into browserless extraction sources and recipes' {
        $recipePath = Join-Path $TestDrive 'observed-api.recipe.json'
        $session = Start-HtmlBrowserSession -Url 'about:blank' -LoadState DomContentLoaded
        try {
            Register-HtmlRoute -Session $session -Pattern '**/observed.html' -ScriptBlock {
                param($route)
                $route.FulfillAsync([Microsoft.Playwright.RouteFulfillOptions] @{
                    Status      = 200
                    ContentType = 'text/html'
                    Body        = @'
<!doctype html>
<html>
<body>
<main id="status">loading</main>
<script>
fetch('/api/items')
  .then(response => response.json())
  .then(data => {
    document.getElementById('status').textContent = data[0].name;
  });
</script>
</body>
</html>
'@
                }) | Out-Null
            } | Out-Null

            Register-HtmlRoute -Session $session -Pattern '**/api/items' -ScriptBlock {
                param($route)
                $route.FulfillAsync([Microsoft.Playwright.RouteFulfillOptions] @{
                    Status      = 200
                    ContentType = 'application/json'
                    Body        = '[{"name":"Alpha","id":1},{"name":"Beta","id":2}]'
                }) | Out-Null
            } | Out-Null

            Invoke-HtmlBrowserNavigation -Session $session -Url 'https://example.com/observed.html' -LoadState DomContentLoaded
            Wait-HtmlBrowserContent -Session $session -Text 'Alpha' -Selector '#status' -Exact

            $sources = Find-HtmlBrowserDataSource -Session $session -IncludeResponseBody -RedactResponseBody
            $source = $sources | Where-Object Url -like '*/api/items' | Select-Object -First 1
            $sourcesWithDocument = Find-HtmlBrowserDataSource -Session $session -IncludeDocument

            $source | Should -Not -BeNullOrEmpty
            $source.Kind | Should -Be 'ObservedApiEndpoint'
            $source.CanExtractDirectly | Should -BeTrue
            $source.RequiresHttpFetch | Should -BeFalse
            $source.RawContent | Should -Match 'Alpha'
            $source.RedactedUrl | Should -Be $source.ResolvedUrl
            $source.SuggestedCommand | Should -Be '$source | Invoke-HtmlDataExtraction'
            $source.SuggestedRecipeCommand | Should -Be "`$source | Export-HtmlExtractionRecipe -Path '.\observed-api.recipe.json' -IncludeRawContent"

            $result = $source | Invoke-HtmlDataExtraction
            $result.Success | Should -BeTrue
            $result.Items.Name | Should -Contain 'Alpha'
            $result.Items.Name | Should -Contain 'Beta'

            $source | Export-HtmlExtractionRecipe -Path $recipePath -IncludeRawContent
            $recipeResult = Import-HtmlExtractionRecipe -Path $recipePath | Invoke-HtmlExtractionRecipe
            $recipeResult.Success | Should -BeTrue
            $recipeResult.Items.Name | Should -Contain 'Alpha'

            ($sourcesWithDocument | Where-Object Url -like '*/api/items' | Select-Object -First 1) | Should -Not -BeNullOrEmpty
            ($sourcesWithDocument | Where-Object { $_.Type -eq 'Document' -and $_.Url -like '*/observed.html' } | Select-Object -First 1) | Should -Not -BeNullOrEmpty
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }

    It 'redacts sensitive observed API URLs in display and suggested command surfaces' {
        $session = Start-HtmlBrowserSession -Url 'about:blank' -LoadState DomContentLoaded
        try {
            Register-HtmlRoute -Session $session -Pattern '**/secure.html' -ScriptBlock {
                param($route)
                $route.FulfillAsync([Microsoft.Playwright.RouteFulfillOptions] @{
                    Status      = 200
                    ContentType = 'text/html'
                    Body        = @'
<!doctype html>
<html>
<body>
<main id="status">loading</main>
<script>
fetch('/api/secure?access_token=super-secret-token&tenant=contoso')
  .then(response => response.json())
  .then(data => {
    document.getElementById('status').textContent = data.name;
  });
</script>
</body>
</html>
'@
                }) | Out-Null
            } | Out-Null

            Register-HtmlRoute -Session $session -Pattern '**/api/secure?*' -ScriptBlock {
                param($route)
                $route.FulfillAsync([Microsoft.Playwright.RouteFulfillOptions] @{
                    Status      = 200
                    ContentType = 'application/json'
                    Body        = '{"name":"Secure Alpha"}'
                }) | Out-Null
            } | Out-Null

            Invoke-HtmlBrowserNavigation -Session $session -Url 'https://example.com/secure.html' -LoadState DomContentLoaded
            Wait-HtmlBrowserContent -Session $session -Text 'Secure Alpha' -Selector '#status' -Exact

            $source = Find-HtmlBrowserDataSource -Session $session -IncludeResponseBody -RedactResponseBody -ResponseBodyResourceType Fetch |
                Where-Object Url -like '*/api/secure*' |
                Select-Object -First 1

            $source | Should -Not -BeNullOrEmpty
            $source.Url | Should -Match 'access_token=<redacted>'
            $source.Url | Should -Not -Match 'super-secret-token'
            $source.ResolvedUrl | Should -Match 'access_token=<redacted>'
            $source.ResolvedUrl | Should -Not -Match 'super-secret-token'
            $source.RedactedUrl | Should -Match 'access_token=<redacted>'
            $source.RedactedUrl | Should -Match 'tenant=contoso'
            $source.RedactedUrl | Should -Not -Match 'super-secret-token'
            $source.SuggestedCommand | Should -Not -Match 'super-secret-token'
            $source.SuggestedRecipeCommand | Should -Not -Match 'super-secret-token'
            $recipe = [HtmlTinkerX.HtmlBrowserlessExtraction]::CreateRecipe($source)
            ($recipe | ConvertTo-Json -Depth 8) | Should -Not -Match 'super-secret-token'
            $source.RawContent | Should -Match 'Secure Alpha'
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }

    It 'detects nested tokenized callback URLs before suggesting browserless replay' {
        $entry = [HtmlTinkerX.HtmlNetworkEntry]::new()
        $entry.Url = 'https://example.com/api/items?returnUrl=%2Fcallback%3Fcode%3Dnested-secret-code%26state%3Dnested-secret-state&tenant=contoso'
        $entry.Method = [HtmlTinkerX.HtmlHttpMethod]::Get
        $entry.ResourceType = [HtmlTinkerX.HtmlNetworkResourceType]::Fetch
        $entry.Status = [System.Net.HttpStatusCode]::OK

        $sources = [HtmlTinkerX.HtmlBrowser]::FindNetworkDataSources(
            [HtmlTinkerX.HtmlNetworkEntry[]] @($entry),
            $null,
            'https://example.com/proof')

        $source = $sources | Select-Object -First 1

        $source | Should -Not -BeNullOrEmpty
        $source.RequiresAuthenticationHint | Should -BeTrue
        $source.CanExtractDirectly | Should -BeFalse
        $source.SuggestedCommand | Should -Be '$source | Format-List RedactedUrl,Method,RiskLevel,Warnings'
        $source.RedactedUrl | Should -Match 'redacted'
        $source.RedactedUrl | Should -Not -Match 'nested-secret-code|nested-secret-state'
        $source.Warnings -join "`n" | Should -Match 'sensitive query or fragment parameter names'
    }

    It 'surfaces safe replay headers while redacting sensitive request headers' {
        $entry = [HtmlTinkerX.HtmlNetworkEntry]::new()
        $entry.Url = 'https://example.com/api/audit/mailbox'
        $entry.Method = [HtmlTinkerX.HtmlHttpMethod]::Get
        $entry.ResourceType = [HtmlTinkerX.HtmlNetworkResourceType]::Fetch
        $entry.Status = [System.Net.HttpStatusCode]::OK
        $entry.RequestHeaders = [System.Collections.Generic.Dictionary[string,string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        $entry.RequestHeaders['accept'] = 'application/json'
        $entry.RequestHeaders['authorization'] = 'Bearer very-secret-token'
        $entry.RequestHeaders['cookie'] = 'sessionid=super-secret-cookie'
        $entry.RequestHeaders['x-requested-with'] = 'XMLHttpRequest'
        $entry.RequestHeaders['sec-fetch-mode'] = 'cors'
        $entry.RequestHeaders['user-agent'] = 'Browser noise'

        $sources = [HtmlTinkerX.HtmlBrowser]::FindNetworkDataSources(
            [HtmlTinkerX.HtmlNetworkEntry[]] @($entry),
            $null,
            'https://example.com/proof')

        $source = $sources | Select-Object -First 1

        $source | Should -Not -BeNullOrEmpty
        $source.RequiresAuthenticationHint | Should -BeTrue
        $source.ObservedRequestHeaders.Keys | Should -Contain 'authorization'
        $source.ObservedRequestHeaders.Keys | Should -Contain 'cookie'
        $source.ObservedRequestHeaders.Keys | Should -Contain 'x-requested-with'
        $source.ObservedRequestHeaders.Keys | Should -Not -Contain 'sec-fetch-mode'
        $source.ObservedRequestHeaders.Keys | Should -Not -Contain 'user-agent'
        $source.ObservedRequestHeaders['authorization'] | Should -Be '<redacted>'
        $source.ObservedRequestHeaders['cookie'] | Should -Be '<redacted>'
        $source.ObservedRequestHeaders.Values | Should -Not -Contain 'Bearer very-secret-token'
        $source.ObservedRequestHeaders.Values | Should -Not -Contain 'sessionid=super-secret-cookie'
        $source.ReplayRequestHeaders.Keys | Should -Contain 'accept'
        $source.ReplayRequestHeaders.Keys | Should -Contain 'x-requested-with'
        $source.ReplayRequestHeaders.Keys | Should -Not -Contain 'authorization'
        $source.ReplayRequestHeaders.Keys | Should -Not -Contain 'cookie'
        $recipe = [HtmlTinkerX.HtmlBrowserlessExtraction]::CreateRecipe($source)
        $recipe.ReplayRequestHeaders.Keys | Should -Contain 'accept'
        $recipe.ReplayRequestHeaders.Keys | Should -Contain 'x-requested-with'
        $recipe.ReplayRequestHeaders.Keys | Should -Not -Contain 'authorization'
        $recipe.ReplayRequestHeaders.Keys | Should -Not -Contain 'cookie'
        $source.SensitiveRequestHeaderNames | Should -Contain 'authorization'
        $source.SensitiveRequestHeaderNames | Should -Contain 'cookie'
        $source.SuggestedReplayCommand | Should -Match 'ConvertTo-HtmlWebRequestSession'
        $source.SuggestedReplayCommand | Should -Match 'ReplayRequestHeaders'
        $source.Warnings -join "`n" | Should -Match 'do not copy Authorization, Cookie, CSRF, or token values'
    }

    It 'treats sensitive replay headers as authentication hints' {
        $entry = [HtmlTinkerX.HtmlNetworkEntry]::new()
        $entry.Url = 'https://example.com/api/tenant'
        $entry.Method = [HtmlTinkerX.HtmlHttpMethod]::Get
        $entry.ResourceType = [HtmlTinkerX.HtmlNetworkResourceType]::Fetch
        $entry.Status = [System.Net.HttpStatusCode]::OK
        $entry.RequestHeaders = [System.Collections.Generic.Dictionary[string,string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        $entry.RequestHeaders['accept'] = 'application/json'
        $entry.RequestHeaders['x-api-key'] = 'secret-api-key'

        $sources = [HtmlTinkerX.HtmlBrowser]::FindNetworkDataSources(
            [HtmlTinkerX.HtmlNetworkEntry[]] @($entry),
            $null,
            'https://example.com/proof')

        $source = $sources | Select-Object -First 1

        $source | Should -Not -BeNullOrEmpty
        $source.RequiresAuthenticationHint | Should -BeTrue
        $source.RiskLevel | Should -Be ([HtmlTinkerX.HtmlApiEndpointRiskLevel]::Medium)
        $source.CanExtractDirectly | Should -BeFalse
        $source.SensitiveRequestHeaderNames | Should -Contain 'x-api-key'
        $source.ObservedRequestHeaders['x-api-key'] | Should -Be '<redacted>'
        $source.ReplayRequestHeaders.Keys | Should -Not -Contain 'x-api-key'
        $source.SuggestedCommand | Should -Be '$source | Format-List RedactedUrl,Method,RiskLevel,Warnings'
        $source.SuggestedCommand | Should -Not -Match 'AllowHttpFetch'
    }

    It 'treats URL user-info credentials as authentication hints' {
        $entry = [HtmlTinkerX.HtmlNetworkEntry]::new()
        $entry.Url = 'https://audit-user:audit-password@example.com/api/proof'
        $entry.Method = [HtmlTinkerX.HtmlHttpMethod]::Get
        $entry.ResourceType = [HtmlTinkerX.HtmlNetworkResourceType]::Fetch
        $entry.Status = [System.Net.HttpStatusCode]::OK

        $sources = [HtmlTinkerX.HtmlBrowser]::FindNetworkDataSources(
            [HtmlTinkerX.HtmlNetworkEntry[]] @($entry),
            $null,
            'https://example.com/proof')

        $source = $sources | Select-Object -First 1

        $source | Should -Not -BeNullOrEmpty
        $source.RequiresAuthenticationHint | Should -BeTrue
        $source.CanExtractDirectly | Should -BeFalse
        $source.Url | Should -Match 'https://<redacted>@example.com/api/proof'
        $source.Url | Should -Not -Match 'audit-user|audit-password'
        $source.Warnings -join "`n" | Should -Match 'URL user-info credentials'
    }

    It 'redacts OAuth state and fragment tokens from observed endpoint and page URLs' {
        $entry = [HtmlTinkerX.HtmlNetworkEntry]::new()
        $entry.Url = 'https://example.com/api/items?state=api-state-secret&accessToken=camel-secret&tenant=contoso#idToken=fragment-secret'
        $entry.Method = [HtmlTinkerX.HtmlHttpMethod]::Get
        $entry.ResourceType = [HtmlTinkerX.HtmlNetworkResourceType]::Fetch
        $entry.Status = [System.Net.HttpStatusCode]::OK

        $sources = [HtmlTinkerX.HtmlBrowser]::FindNetworkDataSources(
            [HtmlTinkerX.HtmlNetworkEntry[]] @($entry),
            $null,
            'https://example.com/callback?code=page-code-secret&state=page-state-secret')

        $source = $sources | Select-Object -First 1

        $source | Should -Not -BeNullOrEmpty
        $source.Url | Should -Match 'state=<redacted>'
        $source.Url | Should -Match 'accessToken=<redacted>'
        $source.Url | Should -Match 'idToken=<redacted>'
        $source.Url | Should -Not -Match 'api-state-secret'
        $source.Url | Should -Not -Match 'fragment-secret'
        $source.Url | Should -Not -Match 'camel-secret'
        $source.ResolvedUrl | Should -Be $source.Url
        $source.PageUrl | Should -Not -Match 'page-code-secret'
        $source.PageUrl | Should -Not -Match 'page-state-secret'
        $source.RedactedUrl | Should -Match 'state=<redacted>'
        $source.RedactedUrl | Should -Match 'accessToken=<redacted>'
        $source.RedactedUrl | Should -Match 'idToken=<redacted>'
        $source.RedactedUrl | Should -Not -Match 'api-state-secret'
        $source.RedactedUrl | Should -Not -Match 'camel-secret'
        $source.RedactedUrl | Should -Not -Match 'fragment-secret'
        $source.RequiresAuthenticationHint | Should -BeTrue
        $source.CanExtractDirectly | Should -BeFalse
        $source.RiskLevel | Should -Be ([HtmlTinkerX.HtmlApiEndpointRiskLevel]::Medium)
        $source.Warnings | Should -Contain 'Observed endpoint contains sensitive query or fragment parameter names or URL user-info credentials.'
        $recipe = [HtmlTinkerX.HtmlBrowserlessExtraction]::CreateRecipe($source)
        ($recipe | ConvertTo-Json -Depth 8) | Should -Not -Match 'api-state-secret|camel-secret|fragment-secret'
    }

    It 'requires IncludeResponseBody before response-body redaction is enabled' {
        $session = Start-HtmlBrowserSession -Url 'about:blank' -LoadState DomContentLoaded
        try {
            { Find-HtmlBrowserDataSource -Session $session -RedactResponseBody } |
                Should -Throw -ExpectedMessage '*RedactResponseBody requires -IncludeResponseBody*'
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }
}
