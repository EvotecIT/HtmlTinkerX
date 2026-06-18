Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'SSO handoff analysis' {
    BeforeAll {
        function New-TestSamlResponse {
            $xml = @'
<samlp:Response xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion" ID="_response" Version="2.0" IssueInstant="2026-01-02T03:04:05Z" Destination="https://service.example/saml/consume">
  <saml:Issuer>https://idp.example/adfs/services/trust</saml:Issuer>
  <samlp:Status>
    <samlp:StatusCode Value="urn:oasis:names:tc:SAML:2.0:status:Success" />
  </samlp:Status>
  <saml:Assertion ID="_assertion" IssueInstant="2026-01-02T03:04:06Z" Version="2.0">
    <saml:Issuer>https://idp.example/adfs/services/trust</saml:Issuer>
    <saml:Subject>
      <saml:NameID>alice@example.com</saml:NameID>
    </saml:Subject>
    <saml:Conditions NotOnOrAfter="2035-01-02T03:10:00Z">
      <saml:AudienceRestriction>
        <saml:Audience>https://service.example/saml/metadata</saml:Audience>
      </saml:AudienceRestriction>
    </saml:Conditions>
  </saml:Assertion>
</samlp:Response>
'@
            [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($xml))
        }

        function New-TestJwt {
            $exp = [DateTimeOffset]::UtcNow.AddHours(1).ToUnixTimeSeconds()
            $header = ConvertTo-Base64Url '{"alg":"RS256","typ":"JWT","kid":"kid-1"}'
            $payload = ConvertTo-Base64Url "{`"iss`":`"https://login.example/tenant/v2.0`",`"sub`":`"user-secret`",`"aud`":`"api://mailbox-proof`",`"exp`":$exp,`"scp`":`"Mailbox.Read Audit.Read`"}"
            $signature = ConvertTo-Base64Url 'signature'
            "$header.$payload.$signature"
        }

        function ConvertTo-Base64Url {
            param([string] $Value)
            [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Value)).TrimEnd('=').Replace('+', '-').Replace('/', '_')
        }

        function New-Handoff {
            param(
                [HtmlTinkerX.HtmlBrowserSsoHandoffKind] $Kind = [HtmlTinkerX.HtmlBrowserSsoHandoffKind]::Unknown,
                [hashtable] $Fields,
                [string[]] $Redacted = @(),
                [string[]] $Truncated = @()
            )

            $handoff = [HtmlTinkerX.HtmlBrowserSsoHandoff]::new()
            $handoff.Kind = $Kind
            $handoff.PageUrl = 'https://login.example/handoff'
            $handoff.Title = 'SSO Handoff'
            $handoff.Action = 'https://service.example/consume'
            $handoff.Method = 'POST'
            foreach ($entry in $Fields.GetEnumerator()) {
                $field = [HtmlTinkerX.HtmlBrowserSsoField]::new()
                $field.Name = $entry.Key
                $field.Type = 'hidden'
                $field.Value = [string] $entry.Value
                $field.ValueLength = $field.Value.Length
                $field.IsSensitive = $entry.Key -match 'token|saml|code|state'
                $field.Redacted = $Redacted -contains $entry.Key
                $field.Truncated = $Truncated -contains $entry.Key
                $handoff.Fields.Add($field)
                $handoff.FormData[$entry.Key] = $field.Value
            }

            $handoff
        }
    }

    It 'exports SSO handoff analysis command' {
        (Get-Command ConvertFrom-HtmlSsoHandoff).Name | Should -Be 'ConvertFrom-HtmlSsoHandoff'
    }

    It 'auto-analyzes SAML and JWT artifacts from one handoff' {
        $handoff = New-Handoff -Kind Saml -Fields @{
            SAMLResponse = New-TestSamlResponse
            id_token     = New-TestJwt
            RelayState   = 'relay-state'
        }

        $analysis = $handoff | ConvertFrom-HtmlSsoHandoff

        $analysis.HasProtocolArtifact | Should -BeTrue
        $analysis.Kind | Should -Be ([HtmlTinkerX.HtmlBrowserSsoHandoffKind]::Saml)
        $analysis.FieldNames | Should -Contain 'SAMLResponse'
        $analysis.FieldNames | Should -Contain 'id_token'
        $analysis.StatePresent | Should -BeTrue
        $analysis.SamlResponse.IsValid | Should -BeTrue
        $analysis.SamlResponse.SubjectNameId | Should -Be '<redacted>'
        $analysis.JsonWebTokens.Count | Should -Be 1
        $analysis.JsonWebTokens[0].FieldName | Should -Be 'id_token'
        $analysis.JsonWebTokens[0].Summary.IsValid | Should -BeTrue
        $analysis.JsonWebTokens[0].Summary.Subject | Should -Be '<redacted>'
    }

    It 'reveals nested values only when explicitly requested' {
        $handoff = New-Handoff -Kind OpenIdConnect -Fields @{
            id_token = New-TestJwt
        }

        $analysis = $handoff | ConvertFrom-HtmlSsoHandoff -IncludeSensitiveValues

        $analysis.JsonWebTokens[0].Summary.Subject | Should -Be 'user-secret'
    }

    It 'identifies authorization code handoffs without pretending to decode them' {
        $handoff = New-Handoff -Kind OAuth2 -Fields @{
            code  = 'auth-code-secret'
            state = 'state-value'
        }

        $analysis = $handoff | ConvertFrom-HtmlSsoHandoff

        $analysis.HasProtocolArtifact | Should -BeTrue
        $analysis.AuthorizationCodePresent | Should -BeTrue
        $analysis.StatePresent | Should -BeTrue
        $analysis.JsonWebTokens.Count | Should -Be 0
        $analysis.SamlResponse | Should -BeNullOrEmpty
        $analysis.Warnings | Should -Contain 'OAuth authorization code is present. Codes are short-lived and cannot be decoded locally; exchange only through the intended authorized client flow.'
    }

    It 'summarizes OAuth and OpenID Connect error handoffs as protocol artifacts' {
        $handoff = New-Handoff -Kind OAuth2 -Fields @{
            error             = 'access_denied'
            error_description = 'User canceled the sign-in prompt'
            state             = 'state-value'
        }

        $analysis = $handoff | ConvertFrom-HtmlSsoHandoff

        $analysis.HasProtocolArtifact | Should -BeTrue
        $analysis.Error | Should -Be 'access_denied'
        $analysis.ErrorDescription | Should -Be 'User canceled the sign-in prompt'
        $analysis.StatePresent | Should -BeTrue
        $analysis.Warnings | Should -Contain 'OAuth/OpenID Connect error returned: access_denied (User canceled the sign-in prompt).'
    }

    It 'guides redacted handoffs toward explicit reveal before analysis' {
        $handoff = New-Handoff -Kind OpenIdConnect -Fields @{
            id_token = '<redacted>'
        } -Redacted id_token

        $analysis = $handoff | ConvertFrom-HtmlSsoHandoff

        $analysis.ContainsRedactedValues | Should -BeTrue
        $analysis.JsonWebTokens[0].Summary.IsValid | Should -BeFalse
        $analysis.Warnings | Should -Contain 'One or more handoff values are redacted. Rerun Get-HtmlBrowserSsoHandoff with -IncludeSensitiveValues before deep protocol analysis or replay.'
        $analysis.SuggestedCommand | Should -Be 'Get-HtmlBrowserSsoHandoff -Session $session -IncludeSensitiveValues | ConvertFrom-HtmlSsoHandoff'
    }

    It 'accepts deserialized handoff-shaped objects from the pipeline' {
        $handoff = [pscustomobject]@{
            Kind                    = 'OpenIdConnect'
            PageUrl                 = 'https://login.example/handoff'
            Title                   = 'SSO Handoff'
            Action                  = 'https://service.example/consume'
            Method                  = 'POST'
            AutoSubmitPrevented     = $true
            ContainsSensitiveValues = $true
            Fields                  = @(
                [pscustomobject]@{
                    Name        = 'id_token'
                    Type        = 'hidden'
                    Value       = New-TestJwt
                    ValueLength = 42
                    IsSensitive = $true
                    Redacted    = $false
                    Truncated   = $false
                }
            )
            FormData                = @{
                id_token = New-TestJwt
            }
        }

        $analysis = $handoff | ConvertFrom-HtmlSsoHandoff

        $analysis.Kind | Should -Be ([HtmlTinkerX.HtmlBrowserSsoHandoffKind]::OpenIdConnect)
        $analysis.JsonWebTokens[0].Summary.Issuer | Should -Be 'https://login.example/tenant/v2.0'
    }
}
