Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'Browser SSO handoff inspection' {
    It 'exports SSO handoff commands and launch parameters' {
        (Get-Command Get-HtmlBrowserSsoHandoff).Name | Should -Be 'Get-HtmlBrowserSsoHandoff'
        (Get-Alias Get-HtmlSsoHandoff).Definition | Should -Be 'Get-HtmlBrowserSsoHandoff'
        (Get-Command Start-HtmlBrowserSession).Parameters.Keys | Should -Contain 'PreventSsoAutoSubmit'
        (Get-Command New-HtmlBrowserProfile).Parameters.Keys | Should -Contain 'PreventSsoAutoSubmit'
        (Get-Command Get-HtmlBrowserSsoHandoff).Parameters.Keys | Should -Contain 'Wait'
        (Get-Command Get-HtmlBrowserSsoHandoff).Parameters.Keys | Should -Contain 'Timeout'
        (Get-Command Get-HtmlBrowserSsoHandoff).Parameters.Keys | Should -Contain 'PollMilliseconds'
        (Get-Command Get-HtmlBrowserSsoHandoff).Parameters.Keys | Should -Contain 'Analyze'
        (Get-Command Get-HtmlBrowserSsoHandoff).Parameters.Keys | Should -Contain 'IncludeXml'
        (Get-Command Get-HtmlBrowserSsoHandoff).Parameters.Keys | Should -Contain 'IncludeJson'
        (Get-Command Get-HtmlBrowserSsoHandoff).Parameters.Keys | Should -Contain 'Url'
        (Get-Command Get-HtmlBrowserSsoHandoff).Parameters.Keys | Should -Contain 'Path'
        (Get-Command Get-HtmlBrowserSsoHandoff).Parameters.Keys | Should -Contain 'ProfilePath'
        (Get-Command Get-HtmlBrowserSsoHandoff).Parameters.Keys | Should -Contain 'Scenario'
        (Get-Command Get-HtmlBrowserSsoHandoff).Parameters.Keys | Should -Contain 'StatePath'
        (Get-Command Get-HtmlBrowserSsoHandoff).Parameters.Keys | Should -Contain 'NavigationTimeout'
        (Get-Command Get-HtmlBrowserSsoHandoff).Parameters.Keys | Should -Contain 'BlockResourceType'
    }

    It 'detects SAML handoff forms and redacts values by default' {
        $pagePath = Join-Path $TestDrive 'saml-handoff.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head><title>SAML Handoff</title></head>
<body>
  <form id="handoff" method="post" action="https://service-provider.example/saml/consume">
    <input type="hidden" name="SAMLResponse" value="very-sensitive-saml-response" />
    <input type="hidden" name="RelayState" value="relay-secret" />
    <input type="hidden" name="display" value="summary" />
  </form>
</body>
</html>
'@
        $session = Start-HtmlBrowserSession -Path $pagePath -LoadState DomContentLoaded
        try {
            $handoff = @(Get-HtmlBrowserSsoHandoff -Session $session)

            $handoff.Count | Should -Be 1
            $handoff[0].Kind | Should -Be 'Saml'
            $handoff[0].Action | Should -Be 'https://service-provider.example/saml/consume'
            $handoff[0].ContainsSensitiveValues | Should -BeTrue
            ($handoff[0].Fields | Where-Object Name -eq 'SAMLResponse').Value | Should -Be '<redacted>'
            ($handoff[0].Fields | Where-Object Name -eq 'SAMLResponse').ValueLength | Should -Be 'very-sensitive-saml-response'.Length
            ($handoff[0].Fields | Where-Object Name -eq 'display').Value | Should -Be 'summary'
            $handoff[0].FormData['SAMLResponse'] | Should -Be '<redacted>'
            $handoff[0].FormData['RelayState'] | Should -Be '<redacted>'
            $handoff[0].FormData['display'] | Should -Be 'summary'
            $handoff[0].SuggestedCommand | Should -Be '$webSession = ConvertTo-HtmlWebRequestSession -Session $session; Invoke-WebRequest -Uri $handoff.Action -Method $handoff.Method -Body $handoff.FormData -WebSession $webSession'
            $handoff[0].SuggestedCommand | Should -Not -Match 'very-sensitive-saml-response'
            $handoff[0].Warnings | Should -Contain 'FormData contains redacted values. Rerun Get-HtmlBrowserSsoHandoff with -IncludeSensitiveValues only when you intentionally need to replay the handoff.'

            $revealed = @(Get-HtmlBrowserSsoHandoff -Session $session -IncludeSensitiveValues)
            ($revealed[0].Fields | Where-Object Name -eq 'SAMLResponse').Value | Should -Be 'very-sensitive-saml-response'
            ($revealed[0].Fields | Where-Object Name -eq 'RelayState').Value | Should -Be 'relay-secret'
            $revealed[0].FormData['SAMLResponse'] | Should -Be 'very-sensitive-saml-response'
            $revealed[0].FormData['RelayState'] | Should -Be 'relay-secret'
            $revealed[0].Warnings | Should -Not -Contain 'FormData contains redacted values. Rerun Get-HtmlBrowserSsoHandoff with -IncludeSensitiveValues only when you intentionally need to replay the handoff.'
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }

    It 'redacts sensitive form action URLs unless explicitly requested' {
        $pagePath = Join-Path $TestDrive 'sso-action-sensitive-url.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<body>
  <form id="handoff" method="post" action="https://service-provider.example/callback?code=action-code-secret&state=action-state-secret">
    <input type="hidden" name="SAMLResponse" value="sso-proof-secret" />
  </form>
</body>
</html>
'@

        $handoff = @(Get-HtmlBrowserSsoHandoff -Path $pagePath -LoadState DomContentLoaded)

        $handoff.Count | Should -Be 1
        $handoff[0].Action | Should -Match 'code=<redacted>'
        $handoff[0].Action | Should -Match 'state=<redacted>'
        $handoff[0].Action | Should -Not -Match 'action-code-secret|action-state-secret'

        $revealed = @(Get-HtmlBrowserSsoHandoff -Path $pagePath -IncludeSensitiveValues -LoadState DomContentLoaded)
        $revealed[0].Action | Should -Match 'action-code-secret'
        $revealed[0].Action | Should -Match 'action-state-secret'
    }

    It 'can safely analyze a SAML handoff in one step without returning raw subject values' {
        $xml = @'
<samlp:Response xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion" ID="_response" Version="2.0" IssueInstant="2026-01-02T03:04:05Z" Destination="https://service-provider.example/saml/consume">
  <saml:Issuer>https://login.example/tenant</saml:Issuer>
  <samlp:Status>
    <samlp:StatusCode Value="urn:oasis:names:tc:SAML:2.0:status:Success" />
  </samlp:Status>
  <saml:Assertion ID="_assertion" IssueInstant="2026-01-02T03:04:06Z" Version="2.0">
    <saml:Subject>
      <saml:NameID>alice@example.com</saml:NameID>
    </saml:Subject>
    <saml:Conditions NotOnOrAfter="2035-01-02T03:10:00Z">
      <saml:AudienceRestriction>
        <saml:Audience>https://service-provider.example/saml/metadata</saml:Audience>
      </saml:AudienceRestriction>
    </saml:Conditions>
  </saml:Assertion>
</samlp:Response>
'@
        $samlResponse = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($xml))
        $pagePath = Join-Path $TestDrive 'analyze-saml-handoff.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @"
<!doctype html>
<html>
<head><title>Analyze SAML Handoff</title></head>
<body>
  <form id="handoff" method="post" action="https://service-provider.example/saml/consume">
    <input type="hidden" name="SAMLResponse" value="$samlResponse" />
    <input type="hidden" name="RelayState" value="relay-secret" />
  </form>
</body>
</html>
"@

        $analysis = @(Get-HtmlBrowserSsoHandoff -Path $pagePath -Analyze -LoadState DomContentLoaded)

        $analysis.Count | Should -Be 1
        $analysis[0].GetType().Name | Should -Be 'HtmlSsoHandoffAnalysis'
        $analysis[0].HasProtocolArtifact | Should -BeTrue
        $analysis[0].Kind | Should -Be 'Saml'
        $analysis[0].StatePresent | Should -BeTrue
        $analysis[0].SamlResponse.IsValid | Should -BeTrue
        $analysis[0].SamlResponse.SubjectNameId | Should -Be '<redacted>'
        ($analysis[0] | ConvertTo-Json -Depth 10) | Should -Not -Match 'alice@example.com'
    }

    It 'detects OAuth authorization code handoffs from the current URL without leaking code values' {
        $pagePath = Join-Path $TestDrive 'oauth-callback.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value '<!doctype html><html><head><title>OAuth Callback</title></head><body><main>Signed in</main></body></html>'
        $callbackUrl = ([System.Uri]::new($pagePath).AbsoluteUri) + '?code=secret-auth-code&state=secret-state&session_state=session-secret'

        $handoff = @(Get-HtmlBrowserSsoHandoff -Url $callbackUrl -LoadState DomContentLoaded)

        $handoff.Count | Should -Be 1
        $handoff[0].Kind | Should -Be 'OAuth2'
        $handoff[0].FormSelector | Should -Be 'location'
        $handoff[0].Method | Should -Be 'GET'
        $handoff[0].PageUrl | Should -Not -Match 'secret-auth-code'
        $handoff[0].PageUrl | Should -Not -Match 'secret-state'
        $handoff[0].FormData['code'] | Should -Be '<redacted>'
        $handoff[0].FormData['state'] | Should -Be '<redacted>'
        $handoff[0].Warnings | Should -Contain 'SSO values were found in the current URL. Browser history, proxy logs, and transcripts may retain query or fragment values; prefer safe analysis output over logging the raw URL.'

        $analysis = @(Get-HtmlBrowserSsoHandoff -Url $callbackUrl -Analyze -LoadState DomContentLoaded)
        $analysis.Count | Should -Be 1
        $analysis[0].AuthorizationCodePresent | Should -BeTrue
        $analysis[0].StatePresent | Should -BeTrue
        ($analysis[0] | ConvertTo-Json -Depth 10) | Should -Not -Match 'secret-auth-code'
        ($analysis[0] | ConvertTo-Json -Depth 10) | Should -Not -Match 'secret-state'
    }

    It 'detects OAuth handoffs from SPA hash-route query fragments' {
        $pagePath = Join-Path $TestDrive 'oauth-hash-callback.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value '<!doctype html><html><head><title>OAuth Hash Callback</title></head><body><main>Signed in</main></body></html>'
        $callbackUrl = ([System.Uri]::new($pagePath).AbsoluteUri) + '#/callback?code=hash-secret-code&state=hash-secret-state'

        $handoff = @(Get-HtmlBrowserSsoHandoff -Url $callbackUrl -LoadState DomContentLoaded)

        $handoff.Count | Should -Be 1
        $handoff[0].Kind | Should -Be 'OAuth2'
        $handoff[0].FormSelector | Should -Be 'location'
        $handoff[0].Method | Should -Be 'GET'
        $handoff[0].PageUrl | Should -Not -Match 'hash-secret-code'
        $handoff[0].PageUrl | Should -Match '#/callback\?code=<redacted>&state=<redacted>'
        ($handoff[0].Fields | Where-Object Name -eq 'code').Type | Should -Be 'url-fragment'
        $handoff[0].FormData['code'] | Should -Be '<redacted>'
        $handoff[0].FormData['state'] | Should -Be '<redacted>'

        $revealed = @(Get-HtmlBrowserSsoHandoff -Url $callbackUrl -IncludeSensitiveValues -LoadState DomContentLoaded)
        $revealed[0].FormData['code'] | Should -Be 'hash-secret-code'
        $revealed[0].FormData['state'] | Should -Be 'hash-secret-state'
    }

    It 'warns when handoff replay form data is truncated or has duplicate fields' {
        $pagePath = Join-Path $TestDrive 'saml-handoff-duplicate.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head><title>SAML Handoff Duplicate</title></head>
<body>
  <form id="handoff" method="post" action="https://service-provider.example/saml/consume">
    <input type="hidden" name="SAMLResponse" value="very-sensitive-saml-response" />
    <input type="hidden" name="display" value="first" />
    <input type="hidden" name="display" value="second" />
  </form>
</body>
</html>
'@
        $handoff = @(Get-HtmlBrowserSsoHandoff -Path $pagePath -IncludeSensitiveValues -MaxValueLength 8 -LoadState DomContentLoaded)

        $handoff.Count | Should -Be 1
        $handoff[0].FormData['SAMLResponse'] | Should -Be 'very-sen'
        $handoff[0].FormData['display'] | Should -Be 'second'
        $handoff[0].Warnings | Should -Contain 'One or more field values were truncated. Increase -MaxValueLength or set it to 0 before replaying the handoff.'
        $handoff[0].Warnings | Should -Contain 'Duplicate form field names were observed (display). FormData keeps the last value for each name; use Fields if duplicate values matter.'
    }

    It 'can prevent SSO auto-submit so a handoff form remains inspectable' {
        $submitPath = Join-Path $TestDrive 'auto-submit-saml.html'
        $targetPath = Join-Path $TestDrive 'after-submit.html'
        Set-Content -LiteralPath $targetPath -Encoding UTF8 -Value '<!doctype html><title>After Submit</title><main>Submitted</main>'
        Set-Content -LiteralPath $submitPath -Encoding UTF8 -Value @"
<!doctype html>
<html>
<head><title>Auto Submit SAML</title></head>
<body>
  <form id="auto" method="get" action="$([System.Uri]::new($targetPath).AbsoluteUri)">
    <input type="hidden" name="SAMLResponse" value="auto-submit-secret" />
  </form>
  <script>document.getElementById('auto').submit();</script>
</body>
</html>
"@

        $session = Start-HtmlBrowserSession -Path $submitPath -PreventSsoAutoSubmit -LoadState DomContentLoaded
        try {
            $handoff = @(Get-HtmlBrowserSsoHandoff -Session $session)

            $session.Page.Url | Should -Match 'auto-submit-saml\.html'
            $handoff.Count | Should -Be 1
            $handoff[0].AutoSubmitPrevented | Should -BeTrue
            ($handoff[0].Fields | Where-Object Name -eq 'SAMLResponse').Value | Should -Be '<redacted>'
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }

    It 'captures an auto-submitted SAML handoff directly from a file and redacts by default' {
        $submitPath = Join-Path $TestDrive 'direct-auto-submit-saml.html'
        $targetPath = Join-Path $TestDrive 'direct-after-submit.html'
        Set-Content -LiteralPath $targetPath -Encoding UTF8 -Value '<!doctype html><title>Direct After Submit</title><main>Submitted</main>'
        Set-Content -LiteralPath $submitPath -Encoding UTF8 -Value @"
<!doctype html>
<html>
<head><title>Direct Auto Submit SAML</title></head>
<body>
  <form id="auto" method="get" action="$([System.Uri]::new($targetPath).AbsoluteUri)">
    <input type="hidden" name="SAMLResponse" value="direct-auto-submit-secret" />
    <input type="hidden" name="RelayState" value="direct-relay-secret" />
    <input type="hidden" name="display" value="proof" />
  </form>
  <script>document.getElementById('auto').submit();</script>
</body>
</html>
"@

        $handoff = @(Get-HtmlBrowserSsoHandoff -Path $submitPath -Wait -Timeout 2000 -PollMilliseconds 50 -LoadState DomContentLoaded)

        $handoff.Count | Should -Be 1
        $handoff[0].Kind | Should -Be 'Saml'
        $handoff[0].PageUrl | Should -Match 'direct-auto-submit-saml\.html'
        $handoff[0].Action | Should -Match 'direct-after-submit\.html'
        $handoff[0].AutoSubmitPrevented | Should -BeTrue
        ($handoff[0].Fields | Where-Object Name -eq 'SAMLResponse').Value | Should -Be '<redacted>'
        ($handoff[0].Fields | Where-Object Name -eq 'RelayState').Value | Should -Be '<redacted>'
        ($handoff[0].Fields | Where-Object Name -eq 'display').Value | Should -Be 'proof'
    }

    It 'reveals one-shot SAML field values only when explicitly requested' {
        $pagePath = Join-Path $TestDrive 'direct-sensitive-saml.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head><title>Direct Sensitive SAML</title></head>
<body>
  <form id="handoff" method="post" action="https://service-provider.example/saml/consume">
    <input type="hidden" name="SAMLResponse" value="direct-sensitive-saml-response" />
    <input type="hidden" name="RelayState" value="direct-relay-secret" />
  </form>
</body>
</html>
'@

        $handoff = @(Get-HtmlBrowserSsoHandoff -Path $pagePath -IncludeSensitiveValues -LoadState DomContentLoaded)

        $handoff.Count | Should -Be 1
        ($handoff[0].Fields | Where-Object Name -eq 'SAMLResponse').Value | Should -Be 'direct-sensitive-saml-response'
        ($handoff[0].Fields | Where-Object Name -eq 'RelayState').Value | Should -Be 'direct-relay-secret'
    }

    It 'rejects document resource blocking for one-shot SSO capture' {
        $pagePath = Join-Path $TestDrive 'document-block-sso.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<body>
  <form><input type="hidden" name="SAMLResponse" value="blocked" /></form>
</body>
</html>
'@

        { Get-HtmlBrowserSsoHandoff -Path $pagePath -BlockResourceType Document } |
            Should -Throw -ExpectedMessage '*BlockResourceType Document would abort page navigation*'
    }

    It 'waits for delayed SSO handoff forms' {
        $pagePath = Join-Path $TestDrive 'delayed-saml-handoff.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head><title>Delayed SAML Handoff</title></head>
<body>
  <main>Signing in</main>
  <script>
    setTimeout(() => {
      const form = document.createElement('form');
      form.id = 'delayed';
      form.method = 'post';
      form.action = 'https://service-provider.example/saml/consume';
      form.innerHTML = '<input type="hidden" name="SAMLResponse" value="delayed-sensitive-saml-response" />';
      document.body.appendChild(form);
    }, 150);
  </script>
</body>
</html>
'@
        $session = Start-HtmlBrowserSession -Path $pagePath -LoadState DomContentLoaded
        try {
            $handoff = @(Get-HtmlBrowserSsoHandoff -Session $session -Wait -Timeout 2000 -PollMilliseconds 50)

            $handoff.Count | Should -Be 1
            $handoff[0].Kind | Should -Be 'Saml'
            $handoff[0].FormSelector | Should -Be 'form#delayed'
            ($handoff[0].Fields | Where-Object Name -eq 'SAMLResponse').Value | Should -Be '<redacted>'
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }

    It 'reports page context when waiting for an SSO handoff times out' {
        $pagePath = Join-Path $TestDrive 'no-saml-handoff.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head><title>No SAML Handoff</title></head>
<body>
  <main>Signed in, but no SSO handoff form appeared.</main>
</body>
</html>
'@
        $session = Start-HtmlBrowserSession -Path $pagePath -LoadState DomContentLoaded
        try {
            {
                Get-HtmlBrowserSsoHandoff -Session $session -Wait -Timeout 150 -PollMilliseconds 50
            } | Should -Throw -ExpectedMessage '*Timed out after 150 ms waiting for an SSO handoff form*No SAML Handoff*no-saml-handoff.html*'
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }
}
