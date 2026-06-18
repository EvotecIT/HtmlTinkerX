Import-Module "$PSScriptRoot\..\PSParseHTML.psd1" -Force

$outputRoot = Join-Path $PSScriptRoot 'Output'
$pagePath = Join-Path $outputRoot 'browser-recipe-demo.html'
$recipePath = Join-Path $outputRoot 'browser.recipe.json'
$evidencePath = Join-Path $outputRoot 'browser-recipe-evidence'

New-Item -Path $outputRoot -ItemType Directory -Force | Out-Null
Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head><title>Browser Recipe Demo</title></head>
<body>
  <main>
    <label for="mailboxSearch">Search mailbox</label>
    <input id="mailboxSearch" name="q" placeholder="Search mailbox" />
    <button data-testid="load-proof" onclick="document.getElementById('proof').textContent = 'Mailbox proof ready';">Load proof</button>
    <section id="proof">Waiting</section>
  </main>
</body>
</html>
'@

$recipe = [ordered]@{
    SchemaVersion = 1
    Name          = 'MailboxProofRecipe'
    StartUrl      = [System.Uri]::new($pagePath).AbsoluteUri
    LoadState     = 'DomContentLoaded'
    Timeout       = 3000
    Steps         = @(
        [ordered]@{
            Name        = 'Wait for page'
            Action      = 'WaitReady'
            NoLoadState = $true
            Selector    = 'main'
            Stable      = $true
        },
        [ordered]@{
            Name     = 'Find proof button'
            Action   = 'Locator'
            Text     = 'Load proof'
            Limit    = 5
        },
        [ordered]@{
            Name     = 'Search mailbox'
            Action   = 'Input'
            Selector = '#mailboxSearch'
            Value    = 'audit request'
        },
        [ordered]@{
            Name     = 'Load proof'
            Action   = 'Click'
            Selector = '[data-testid="load-proof"]'
        },
        [ordered]@{
            Name     = 'Wait for proof'
            Action   = 'WaitText'
            Selector = '#proof'
            Text     = 'Mailbox proof ready'
        },
        [ordered]@{
            Name         = 'Export proof evidence'
            Action       = 'Evidence'
            OutFolder    = $evidencePath
            BaseFileName = 'mailbox-proof'
        }
    )
}

$recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8
$result = Invoke-HtmlBrowserRecipe -Path $recipePath

[PSCustomObject]@{
    RecipePath          = $recipePath
    Succeeded           = $result.Succeeded
    StepCount           = $result.Steps.Count
    LocatorCandidateTop = $result.Steps[1].LocatorCandidates[0].Locator
    EvidenceManifest    = $result.Steps[-1].Evidence.ManifestPath
    FailureSummary      = $result.FailureSummary
    SuggestedCommand    = $result.SuggestedCommand
    FinalUrl            = $result.FinalUrl
}
