Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'SAML response inspection' {
    BeforeAll {
        function New-TestSamlResponse {
            $xml = @'
<samlp:Response xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion" xmlns:ds="http://www.w3.org/2000/09/xmldsig#" ID="_response" Version="2.0" IssueInstant="2026-01-02T03:04:05Z" Destination="https://service.example/saml/consume" InResponseTo="_request">
  <saml:Issuer>https://idp.example/adfs/services/trust</saml:Issuer>
  <ds:Signature>
    <ds:SignatureValue>secret-signature-value</ds:SignatureValue>
  </ds:Signature>
  <samlp:Status>
    <samlp:StatusCode Value="urn:oasis:names:tc:SAML:2.0:status:Success" />
  </samlp:Status>
  <saml:Assertion ID="_assertion" IssueInstant="2026-01-02T03:04:06Z" Version="2.0">
    <saml:Issuer>https://idp.example/adfs/services/trust</saml:Issuer>
    <saml:Subject>
      <saml:NameID>alice@example.com</saml:NameID>
    </saml:Subject>
    <saml:Conditions NotBefore="2026-01-02T03:00:00Z" NotOnOrAfter="2035-01-02T03:10:00Z">
      <saml:AudienceRestriction>
        <saml:Audience>https://service.example/saml/metadata</saml:Audience>
      </saml:AudienceRestriction>
    </saml:Conditions>
    <saml:AttributeStatement>
      <saml:Attribute Name="role">
        <saml:AttributeValue>Admin</saml:AttributeValue>
      </saml:Attribute>
    </saml:AttributeStatement>
  </saml:Assertion>
</samlp:Response>
'@
            [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($xml))
        }
    }

    It 'exports SAML response command' {
        (Get-Command ConvertFrom-HtmlSamlResponse).Name | Should -Be 'ConvertFrom-HtmlSamlResponse'
    }

    It 'summarizes a SAMLResponse without revealing subject or attribute values by default' {
        $summary = ConvertFrom-HtmlSamlResponse -SamlResponse (New-TestSamlResponse)

        $summary.IsValid | Should -BeTrue
        $summary.Issuer | Should -Be 'https://idp.example/adfs/services/trust'
        $summary.Destination | Should -Be 'https://service.example/saml/consume'
        $summary.StatusCode | Should -Be 'urn:oasis:names:tc:SAML:2.0:status:Success'
        $summary.SubjectNameId | Should -Be '<redacted>'
        $summary.Audiences | Should -Contain 'https://service.example/saml/metadata'
        $summary.AttributeNames | Should -Contain 'role'
        $summary.ContainsSignature | Should -BeTrue
        $summary.ContainsSensitiveValues | Should -BeTrue
        $summary.Warnings | Should -Contain 'Subject and attribute values were redacted. Use -IncludeSensitiveValues only for authorized troubleshooting.'
        $summary.Xml | Should -Be ''
    }

    It 'binds a positional raw SAMLResponse string to the SAMLResponse parameter set' {
        $summary = ConvertFrom-HtmlSamlResponse (New-TestSamlResponse)

        $summary.IsValid | Should -BeTrue
        $summary.AssertionId | Should -Be '_assertion'
    }

    It 'reveals sensitive subject only when explicitly requested' {
        $summary = ConvertFrom-HtmlSamlResponse -SamlResponse (New-TestSamlResponse) -IncludeSensitiveValues

        $summary.IsValid | Should -BeTrue
        $summary.SubjectNameId | Should -Be 'alice@example.com'
        $summary.Warnings | Should -Not -Contain 'Subject and attribute values were redacted. Use -IncludeSensitiveValues only for authorized troubleshooting.'
    }

    It 'redacts decoded XML unless sensitive values are explicitly requested' {
        $summary = ConvertFrom-HtmlSamlResponse -SamlResponse (New-TestSamlResponse) -IncludeXml

        $summary.Xml | Should -Not -BeNullOrEmpty
        $summary.Xml | Should -Not -Match 'alice@example.com'
        $summary.Xml | Should -Not -Match 'Admin'
        $summary.Xml | Should -Not -Match 'secret-signature-value'
        $summary.Xml | Should -Match 'redacted'
    }

    It 'accepts captured SSO handoff objects from the pipeline' {
        $handoff = [HtmlTinkerX.HtmlBrowserSsoHandoff]::new()
        $handoff.Kind = [HtmlTinkerX.HtmlBrowserSsoHandoffKind]::Saml
        $handoff.FormData['SAMLResponse'] = New-TestSamlResponse

        $summary = $handoff | ConvertFrom-HtmlSamlResponse

        $summary.IsValid | Should -BeTrue
        $summary.AssertionId | Should -Be '_assertion'
    }

    It 'reports redacted handoff values with a recovery command' {
        $summary = ConvertFrom-HtmlSamlResponse -SamlResponse '<redacted>'

        $summary.IsValid | Should -BeFalse
        $summary.ErrorMessage | Should -Be 'SAMLResponse value is redacted. Rerun Get-HtmlBrowserSsoHandoff with -IncludeSensitiveValues before analyzing it.'
        $summary.SuggestedCommand | Should -Be 'Get-HtmlBrowserSsoHandoff -Session $session -IncludeSensitiveValues | ConvertFrom-HtmlSamlResponse'
    }
}
