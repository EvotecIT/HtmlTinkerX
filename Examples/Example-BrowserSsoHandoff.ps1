[CmdletBinding()]
param(
    [string] $OutputPath = (Join-Path $PSScriptRoot 'Output\sso-handoff-demo.html')
)

Import-Module "$PSScriptRoot\..\PSParseHTML.psd1" -Force

$outputDirectory = Split-Path -Parent $OutputPath
if ($outputDirectory -and -not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

Set-Content -LiteralPath $OutputPath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head><title>SSO Handoff Demo</title></head>
<body>
  <main>Signing in</main>
  <script>
    setTimeout(() => {
      const base64Url = text => btoa(text)
        .replace(/=/g, '')
        .replace(/\+/g, '-')
        .replace(/\//g, '_');
      const jsonWebToken = payload => `${base64Url(JSON.stringify({ alg: 'RS256', typ: 'JWT', kid: 'demo-key' }))}.${base64Url(JSON.stringify(payload))}.${base64Url('signature')}`;
      const samlResponse = btoa(`
<samlp:Response xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion" ID="_demo-response" Version="2.0" IssueInstant="2026-01-02T03:04:05Z" Destination="https://service-provider.example/saml/consume">
  <saml:Issuer>https://login.example/tenant</saml:Issuer>
  <samlp:Status>
    <samlp:StatusCode Value="urn:oasis:names:tc:SAML:2.0:status:Success" />
  </samlp:Status>
  <saml:Assertion ID="_demo-assertion" IssueInstant="2026-01-02T03:04:06Z" Version="2.0">
    <saml:Issuer>https://login.example/tenant</saml:Issuer>
    <saml:Subject>
      <saml:NameID>demo.user@example.com</saml:NameID>
    </saml:Subject>
    <saml:Conditions NotBefore="2026-01-02T03:00:00Z" NotOnOrAfter="2035-01-02T03:10:00Z">
      <saml:AudienceRestriction>
        <saml:Audience>https://service-provider.example/saml/metadata</saml:Audience>
      </saml:AudienceRestriction>
    </saml:Conditions>
  </saml:Assertion>
</samlp:Response>`);
      const idToken = jsonWebToken({
        iss: 'https://login.example/tenant/v2.0',
        sub: 'demo-user-subject',
        aud: 'api://service-provider-demo',
        exp: Math.floor(Date.now() / 1000) + 3600,
        nbf: Math.floor(Date.now() / 1000) - 60,
        iat: Math.floor(Date.now() / 1000) - 120,
        tid: 'tenant-demo',
        azp: 'client-demo',
        preferred_username: 'demo.user@example.com',
        scp: 'Mailbox.Read Audit.Read'
      });
      const form = document.createElement('form');
      form.id = 'saml-handoff';
      form.method = 'post';
      form.action = 'https://service-provider.example/saml/consume';
      form.innerHTML = `
        <input type="hidden" name="SAMLResponse" value="${samlResponse}" />
        <input type="hidden" name="RelayState" value="demo-relay-state" />
        <input type="hidden" name="id_token" value="${idToken}" />
        <input type="hidden" name="display" value="summary" />
      `;
      document.body.appendChild(form);
      form.submit();
    }, 250);
  </script>
</body>
</html>
'@

$session = Start-HtmlBrowserSession `
    -Path $OutputPath `
    -Scenario LoginProtected `
    -PreventSsoAutoSubmit `
    -LoadState DomContentLoaded

try {
    $redacted = Get-HtmlBrowserSsoHandoff -Session $session -Wait -Timeout 5000 -PollMilliseconds 100
    $handoffAnalysis = Get-HtmlBrowserSsoHandoff -Session $session -Analyze
    $revealed = Get-HtmlBrowserSsoHandoff -Session $session -IncludeSensitiveValues
    $samlSummary = $revealed | ConvertFrom-HtmlSamlResponse
    $jwtSummary = $revealed | ConvertFrom-HtmlJsonWebToken
    $webSession = ConvertTo-HtmlWebRequestSession -Session $session
    $evidencePath = Join-Path $outputDirectory 'sso-handoff-evidence'
    $evidence = Export-HtmlBrowserEvidence -Session $session -OutFolder $evidencePath -BaseFileName sso-handoff -Artifact SsoHandoffSummary

    [pscustomobject] @{
        PagePath                = $OutputPath
        EvidencePath            = $evidence.OutFolder
        Kind                    = $redacted.Kind
        Action                  = $redacted.Action
        AutoSubmitPrevented     = $redacted.AutoSubmitPrevented
        RedactedSamlResponse    = $redacted.FormData['SAMLResponse']
        AnalysisHasArtifact     = $handoffAnalysis.HasProtocolArtifact
        AnalysisFieldCount      = $handoffAnalysis.FieldNames.Count
        AnalysisTokenCount      = $handoffAnalysis.JsonWebTokens.Count
        SamlSummaryValid        = $samlSummary.IsValid
        SamlSummaryIssuer       = $samlSummary.Issuer
        SamlSummaryAudience     = $samlSummary.Audiences[0]
        JwtSummaryValid         = $jwtSummary.IsValid
        JwtSummaryIssuer        = $jwtSummary.Issuer
        JwtSummaryAudience      = $jwtSummary.Audiences[0]
        JwtSummarySubject       = $jwtSummary.Subject
        RevealedValueLength     = ($revealed.Fields | Where-Object Name -eq 'SAMLResponse').ValueLength
        ReplayBodyHasSaml       = $revealed.FormData.ContainsKey('SAMLResponse')
        WebSessionType          = $webSession.GetType().FullName
        NonSensitiveField       = $redacted.FormData['display']
        SuggestedReplayCommand  = $redacted.SuggestedCommand
        FirstWarning            = $redacted.Warnings[0]
    }
} finally {
    Close-HtmlBrowserSession -Session $session
}
