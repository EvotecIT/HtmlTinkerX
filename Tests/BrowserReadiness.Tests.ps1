Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'Browser readiness waits' {
    It 'exports readiness command and alias' {
        (Get-Command Wait-HtmlBrowserReady).Name | Should -Be 'Wait-HtmlBrowserReady'
        (Get-Alias Wait-HtmlReady).Definition | Should -Be 'Wait-HtmlBrowserReady'
        (Get-Command Wait-HtmlBrowserReady).Parameters.Keys | Should -Contain 'OnFailureEvidence'
        (Get-Command Wait-HtmlBrowserReady).Parameters.Keys | Should -Contain 'FailureEvidenceFolder'
    }

    It 'waits for a JavaScript readiness signal and DOM stability' {
        $pagePath = Join-Path $TestDrive 'ready-page.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head><title>Ready Test</title></head>
<body>
  <main id="app">Loading</main>
  <script>
    window.appReady = false;
    setTimeout(() => {
      document.getElementById('app').textContent = 'Ready now';
      window.appReady = true;
    }, 100);
  </script>
</body>
</html>
'@
        $uri = [System.Uri]::new($pagePath).AbsoluteUri

        $session = Start-HtmlBrowserSession -Url $uri -LoadState DomContentLoaded
        try {
            $ready = $session | Wait-HtmlBrowserReady -NoLoadState -Selector '#app' -Function '() => window.appReady === true' -Stable -StableMilliseconds 50 -PollMilliseconds 25 -Timeout 2000 -PassThru
            $ready | Should -Be $session
            Get-HtmlBrowserContent -Session $session -Selector '#app' -AsText | Should -Be 'Ready now'
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }

    It 'exports failure evidence when readiness fails' {
        $pagePath = Join-Path $TestDrive 'not-ready-page.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head><title>Failure Evidence Test</title></head>
<body>
  <main id="app">Still loading</main>
  <button data-testid="retry-proof" aria-label="Retry proof export">Retry proof export</button>
  <a href="/download?token=locator-secret-token">Download proof</a>
</body>
</html>
'@
        $uri = [System.Uri]::new($pagePath).AbsoluteUri
        $failureRoot = Join-Path $TestDrive 'failure-evidence'

        $session = Start-HtmlBrowserSession -Url $uri -LoadState DomContentLoaded
        try {
            {
                Wait-HtmlBrowserReady -Session $session -NoLoadState -Selector '#missing' -Timeout 100 -OnFailureEvidence -FailureEvidenceFolder $failureRoot
            } | Should -Throw

            $failureFolders = @(Get-ChildItem -LiteralPath $failureRoot -Directory)
            $failureFolders.Count | Should -Be 1
            $folder = $failureFolders[0].FullName
            Test-Path -LiteralPath (Join-Path $folder 'evidence-manifest.json') | Should -BeTrue
            Test-Path -LiteralPath (Join-Path $folder 'failure-context.json') | Should -BeTrue
            Test-Path -LiteralPath (Join-Path $folder 'locator-suggestions.json') | Should -BeTrue

            $manifest = Get-Content -LiteralPath (Join-Path $folder 'evidence-manifest.json') -Raw | ConvertFrom-Json
            $locators = Get-Content -LiteralPath (Join-Path $folder 'locator-suggestions.json') -Raw | ConvertFrom-Json
            $locatorJson = Get-Content -LiteralPath (Join-Path $folder 'locator-suggestions.json') -Raw
            $manifest.Purpose | Should -Be 'FailureEvidence'
            $manifest.Operation | Should -Be 'ReadyWait'
            $manifest.LocatorSuggestionCount | Should -BeGreaterThan 0
            $manifest.Artifacts.Kind | Should -Contain 'FailureContext'
            $manifest.Artifacts.Kind | Should -Contain 'LocatorSuggestions'
            $manifest.Artifacts.Kind | Should -Contain 'FullPageScreenshot'
            $manifest.Artifacts.Kind | Should -Contain 'NetworkSummary'
            $locators.Redacted | Should -BeTrue
            $locators.Candidates.Strategy | Should -Contain 'TestId'
            $locatorJson | Should -Match 'Retry proof export'
            $locatorJson | Should -Not -Match 'locator-secret-token'
            ($locators.Candidates.Selector -join "`n") | Should -Match 'token=<redacted>'
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }
}
