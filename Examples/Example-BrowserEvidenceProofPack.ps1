[CmdletBinding()]
param(
    [string] $BrowserProfilePath = (Join-Path $PSScriptRoot 'Output\proof-browser-profile.json'),
    [string] $UserDataPath = (Join-Path $PSScriptRoot 'Output\proof-browser-user-data'),
    [string] $EvidencePath = (Join-Path $PSScriptRoot 'Output\proof-evidence')
)

Import-Module "$PSScriptRoot\..\PSParseHTML.psd1" -Force

$profileDirectory = Split-Path -Parent $BrowserProfilePath
if ($profileDirectory -and -not (Test-Path -LiteralPath $profileDirectory)) {
    New-Item -ItemType Directory -Path $profileDirectory -Force | Out-Null
}

$browserProfile = New-HtmlBrowserProfile `
    -Name 'AuditorProof' `
    -Scenario AuditProof `
    -Path $BrowserProfilePath `
    -UserDataDirectory $UserDataPath `
    -BrowserChannel chromium `
    -Locale en-US `
    -Timezone UTC

$proofUrl = 'https://proof.local/mailbox.html'
$proofHtml = @'
<!doctype html>
<html>
<head>
<title>Mailbox Proof</title>
<script>
window.proofReady = false;
document.addEventListener("DOMContentLoaded", async () => {
  const response = await fetch("/api/mailbox-proof");
  const data = await response.json();
  document.querySelector("#subject").textContent = data.subject;
  document.querySelector("#mailbox").textContent = data.mailbox;
  document.querySelector("#timestamp").textContent = data.timestamp;
  window.proofReady = true;
});
</script>
</head>
<body>
<main>
  <h1>Mailbox export proof</h1>
  <dl>
    <dt>Mailbox</dt><dd id="mailbox">Loading</dd>
    <dt>Subject</dt><dd id="subject">Loading</dd>
    <dt>Timestamp</dt><dd id="timestamp">Loading</dd>
  </dl>
  <label for="temporarySecret">Temporary proof token</label>
  <input id="temporarySecret" name="proof_token" value="demo-token-that-will-be-masked" />
</main>
</body>
</html>
'@

$session = Start-HtmlBrowserSession -Url 'about:blank' -ProfilePath $BrowserProfilePath
try {
    Register-HtmlRoute -Session $session -Pattern '**/mailbox.html' -ScriptBlock {
        param($route)
        $route.FulfillAsync([Microsoft.Playwright.RouteFulfillOptions] @{
            Status      = 200
            ContentType = 'text/html'
            Body        = $proofHtml
        }) | Out-Null
    } | Out-Null

    Register-HtmlRoute -Session $session -Pattern '**/api/mailbox-proof' -ScriptBlock {
        param($route)
        $route.FulfillAsync([Microsoft.Playwright.RouteFulfillOptions] @{
            Status      = 200
            ContentType = 'application/json'
            Body        = '{"mailbox":"audit@example.com","subject":"Quarterly export confirmation","timestamp":"2026-06-17T08:00:00Z"}'
        }) | Out-Null
    } | Out-Null

    Invoke-HtmlBrowserNavigation -Session $session -Url $proofUrl
    Wait-HtmlBrowserReady -Session $session -NoLoadState -Selector 'main' -Function '() => window.proofReady === true' -Stable
    $evidence = Export-HtmlBrowserEvidence -Session $session -OutFolder $EvidencePath -BaseFileName mailbox-proof -Pdf -NetworkSummary -VisualMaskColor '#00ff00'

    [pscustomobject] @{
        ProfileName           = $browserProfile.Name
        BrowserProfilePath    = $BrowserProfilePath
        UserDataPath          = $UserDataPath
        EvidencePath          = $evidence.OutFolder
        ManifestPath          = $evidence.ManifestPath
        EvidenceArtifactCount = @($evidence.Artifacts).Count
        FinalUrl              = $evidence.FinalUrl
        Title                 = $evidence.Title
    }
} finally {
    Close-HtmlBrowserSession -Session $session
}
