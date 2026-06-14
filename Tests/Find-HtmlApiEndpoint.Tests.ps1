Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force
. "$PSScriptRoot/Support/HtmlRedirectTestServer.ps1"

Describe 'Find-HtmlApiEndpoint' {
    It 'exports the API endpoint inventory command' {
        Get-Command Find-HtmlApiEndpoint | Should -Not -BeNullOrEmpty
    }

    It 'classifies form and JavaScript endpoints from content' {
        $html = @'
<html>
<head>
<script>
fetch("/api/items");
fetch("https://api.example.net/public");
fetch("/api/session?token=abc123");
</script>
</head>
<body>
<form method="post" action="/submit"><input name="name" /></form>
</body>
</html>
'@

        $endpoints = Find-HtmlApiEndpoint -Content $html -BaseUrl 'https://example.org/page'

        $endpoints.Count | Should -BeGreaterOrEqual 4

        $readEndpoint = $endpoints | Where-Object ResolvedUrl -EQ 'https://example.org/api/items'
        $readEndpoint.RiskLevel | Should -Be 'Low'
        $readEndpoint.ReasonCodes | Should -Contain 'same-origin-read'

        $externalEndpoint = $endpoints | Where-Object ResolvedUrl -EQ 'https://api.example.net/public'
        $externalEndpoint.RiskLevel | Should -Be 'Medium'
        $externalEndpoint.IsExternal | Should -BeTrue
        $externalEndpoint.ReasonCodes | Should -Contain 'external-origin'

        $formEndpoint = $endpoints | Where-Object Kind -EQ 'Form'
        $formEndpoint.Method | Should -Be 'POST'
        $formEndpoint.RiskLevel | Should -Be 'High'
        $formEndpoint.ReasonCodes | Should -Contain 'state-changing-method'

        $sensitiveEndpoint = $endpoints | Where-Object { $_.ResolvedUrl -like 'https://example.org/api/session*' }
        $sensitiveEndpoint.RiskLevel | Should -Be 'High'
        $sensitiveEndpoint.HasSensitiveQuery | Should -BeTrue
        $sensitiveEndpoint.ReasonCodes | Should -Contain 'sensitive-query-name'
        $sensitiveEndpoint.Url | Should -Match 'token=<redacted>'
        $sensitiveEndpoint.ResolvedUrl | Should -Match 'token=<redacted>'
        $sensitiveEndpoint.Url | Should -Not -Match 'abc123'
        $sensitiveEndpoint.ResolvedUrl | Should -Not -Match 'abc123'
        $sensitiveEndpoint.Name | Should -Not -Match 'abc123'
        $sensitiveEndpoint.Metadata | Should -Not -Match 'abc123'
    }

    It 'accepts page workbench results from the pipeline and can exclude forms' {
        $html = @'
<html>
<head><script>fetch("/api/items");</script></head>
<body><form method="post" action="/submit"><input name="name" /></form></body>
</html>
'@

        $endpoints = Invoke-HtmlPageWorkbench -Content $html -BaseUrl 'https://example.org/page' |
            Find-HtmlApiEndpoint -ExcludeForms

        $endpoints.Kind | Should -Contain 'Endpoint'
        $endpoints.Kind | Should -Not -Contain 'Form'
    }

    It 'uses the final response Url as the base when Url follows redirects' {
        $server = [HtmlRedirectTestServer]::new()
        try {
            $endpoints = Find-HtmlApiEndpoint -Url ($server.Url + 'redirect-api')

            $endpoint = $endpoints | Where-Object { $_.ResolvedUrl -like ($server.Url + 'final/relative-api*') }
            $endpoint | Should -Not -BeNullOrEmpty
            $endpoint.ResolvedUrl | Should -Match 'token=<redacted>'
            $endpoint.Name | Should -Not -Match 'abc123'
        } finally {
            $server.Dispose()
        }
    }
}
