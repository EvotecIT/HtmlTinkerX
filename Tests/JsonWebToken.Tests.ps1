Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'JSON Web Token inspection' {
    BeforeAll {
        function New-TestJwt {
            param(
                [switch] $Expired,
                [string] $Algorithm = 'RS256'
            )

            $now = [DateTimeOffset]::UtcNow
            $exp = if ($Expired) {
                $now.AddMinutes(-5).ToUnixTimeSeconds()
            } else {
                $now.AddHours(1).ToUnixTimeSeconds()
            }
            $nbf = $now.AddMinutes(-5).ToUnixTimeSeconds()
            $iat = $now.AddMinutes(-10).ToUnixTimeSeconds()
            $header = [ordered]@{
                alg = $Algorithm
                typ = 'JWT'
                kid = 'kid-1'
            }
            $payload = [ordered]@{
                iss                = 'https://login.example/tenant/v2.0'
                sub                = 'user-subject-secret'
                aud                = @('api://mailbox-proof', 'api://audit-proof')
                exp                = $exp
                nbf                = $nbf
                iat                = $iat
                tid                = 'tenant-123'
                azp                = 'client-456'
                preferred_username = 'alice@example.com'
                name               = 'Alice Example'
                scp                = 'Mailbox.Read Audit.Read'
                nonce              = 'nonce-secret'
            }

            $headerJson = $header | ConvertTo-Json -Compress
            $payloadJson = $payload | ConvertTo-Json -Compress
            $headerPart = ConvertTo-Base64Url $headerJson
            $payloadPart = ConvertTo-Base64Url $payloadJson
            $signaturePart = ConvertTo-Base64Url 'signature'
            "$headerPart.$payloadPart.$signaturePart"
        }

        function ConvertTo-Base64Url {
            param([string] $Value)
            [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Value)).TrimEnd('=').Replace('+', '-').Replace('/', '_')
        }
    }

    It 'exports JWT inspection command' {
        (Get-Command ConvertFrom-HtmlJsonWebToken).Name | Should -Be 'ConvertFrom-HtmlJsonWebToken'
    }

    It 'summarizes a token without revealing user-identifying claims by default' {
        $summary = ConvertFrom-HtmlJsonWebToken -Token (New-TestJwt)

        $summary.IsValid | Should -BeTrue
        $summary.Algorithm | Should -Be 'RS256'
        $summary.Type | Should -Be 'JWT'
        $summary.KeyId | Should -Be 'kid-1'
        $summary.Issuer | Should -Be 'https://login.example/tenant/v2.0'
        $summary.Subject | Should -Be '<redacted>'
        $summary.Audiences | Should -Contain 'api://mailbox-proof'
        $summary.Audiences | Should -Contain 'api://audit-proof'
        $summary.TenantId | Should -Be 'tenant-123'
        $summary.ClientId | Should -Be 'client-456'
        $summary.Scopes | Should -Contain 'Mailbox.Read'
        $summary.Scopes | Should -Contain 'Audit.Read'
        ($summary.Claims | Where-Object Name -eq 'preferred_username').Value | Should -Be '<redacted>'
        ($summary.Claims | Where-Object Name -eq 'scp').Value | Should -Be 'Mailbox.Read Audit.Read'
        $summary.Warnings | Should -Contain 'Subject and user-identifying claim values were redacted. Use -IncludeSensitiveValues only for authorized troubleshooting.'
    }

    It 'reveals sensitive claims only when explicitly requested' {
        $summary = ConvertFrom-HtmlJsonWebToken -Token (New-TestJwt) -IncludeSensitiveValues

        $summary.Subject | Should -Be 'user-subject-secret'
        ($summary.Claims | Where-Object Name -eq 'preferred_username').Value | Should -Be 'alice@example.com'
        $summary.Warnings | Should -Not -Contain 'Subject and user-identifying claim values were redacted. Use -IncludeSensitiveValues only for authorized troubleshooting.'
    }

    It 'redacts decoded payload JSON unless sensitive values are explicitly requested' {
        $summary = ConvertFrom-HtmlJsonWebToken -Token (New-TestJwt) -IncludeJson

        $summary.HeaderJson | Should -Match '"alg"'
        $summary.PayloadJson | Should -Not -Match 'alice@example.com'
        $summary.PayloadJson | Should -Not -Match 'user-subject-secret'
        $summary.PayloadJson | Should -Match '<redacted>'
    }

    It 'accepts OIDC handoff objects from the pipeline' {
        $handoff = [HtmlTinkerX.HtmlBrowserSsoHandoff]::new()
        $handoff.Kind = [HtmlTinkerX.HtmlBrowserSsoHandoffKind]::OpenIdConnect
        $handoff.FormData['id_token'] = New-TestJwt

        $summary = $handoff | ConvertFrom-HtmlJsonWebToken

        $summary.IsValid | Should -BeTrue
        $summary.Issuer | Should -Be 'https://login.example/tenant/v2.0'
    }

    It 'reports redacted handoff values with a recovery command' {
        $summary = ConvertFrom-HtmlJsonWebToken -Token '<redacted>'

        $summary.IsValid | Should -BeFalse
        $summary.ErrorMessage | Should -Be 'JSON Web Token value is redacted. Rerun Get-HtmlBrowserSsoHandoff with -IncludeSensitiveValues before analyzing it.'
        $summary.SuggestedCommand | Should -Be 'Get-HtmlBrowserSsoHandoff -Session $session -IncludeSensitiveValues | ConvertFrom-HtmlJsonWebToken'
    }

    It 'warns on expired or unsigned tokens' {
        $expired = ConvertFrom-HtmlJsonWebToken -Token (New-TestJwt -Expired)
        $unsigned = ConvertFrom-HtmlJsonWebToken -Token (New-TestJwt -Algorithm none)

        $expired.Warnings | Should -Contain 'JSON Web Token is expired.'
        $unsigned.Warnings | Should -Contain 'JWT header uses alg=none.'
        $unsigned.Warnings | Should -Contain 'JWT signature and issuer keys were not verified. Treat this as decoding and triage only.'
    }
}
