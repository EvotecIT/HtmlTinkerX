Import-Module "$PSScriptRoot\..\PSParseHTML.psd1" -Force

$outputRoot = Join-Path $PSScriptRoot 'Output'
$failureEvidenceRoot = Join-Path $outputRoot 'browser-failure-evidence'
$pagePath = Join-Path $outputRoot 'locator-failure-demo.html'

New-Item -Path $outputRoot -ItemType Directory -Force | Out-Null
Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head><title>Locator and Failure Evidence Demo</title></head>
<body>
  <main>
    <label for="mailboxSearch">Search mailbox</label>
    <input id="mailboxSearch" name="q" placeholder="Search mailbox" />
    <button data-testid="export-proof" aria-label="Export mailbox proof">Export proof</button>
    <section id="results">Ready</section>
  </main>
</body>
</html>
'@

$session = Start-HtmlBrowserSession -Url ([System.Uri]::new($pagePath).AbsoluteUri) -LoadState DomContentLoaded
try {
    $locators = Find-HtmlBrowserLocator -Session $session -Query 'Export proof' -Limit 5

    Set-HtmlBrowserInput -Session $session -Selector '#mailboxSearch' -Value 'audit request'
    Invoke-HtmlBrowserClick -Session $session -Selector $locators[0].Selector

    try {
        Wait-HtmlBrowserReady -Session $session -NoLoadState -Selector '#never-arrives' -Timeout 250 -OnFailureEvidence -FailureEvidenceFolder $failureEvidenceRoot
    } catch {
        $failure = $_
    }

    [PSCustomObject]@{
        BestLocatorStrategy = $locators[0].Strategy
        BestLocator         = $locators[0].Locator
        SuggestedCommand    = $locators[0].SuggestedCommand
        TestCommand         = $locators[0].TestCommand
        FailureMessage      = $failure.Exception.Message
        EvidenceFolder      = (Get-ChildItem -LiteralPath $failureEvidenceRoot -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
    }
} finally {
    Close-HtmlBrowserSession -Session $session
}
