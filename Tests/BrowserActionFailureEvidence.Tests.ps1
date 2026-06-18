Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'Browser action failure evidence' {
    It 'exports failure evidence parameters on interactive action cmdlets' {
        foreach ($commandName in @(
            'Invoke-HtmlBrowserClick'
            'Invoke-HtmlBrowserHover'
            'Invoke-HtmlBrowserKey'
            'Invoke-HtmlBrowserNavigation'
            'Invoke-HtmlBrowserScroll'
            'Set-HtmlBrowserChecked'
            'Set-HtmlBrowserInput'
            'Set-HtmlBrowserSelectOption'
            'Submit-HtmlBrowserForm'
            'Wait-HtmlBrowserReady'
        )) {
            $parameters = (Get-Command $commandName).Parameters.Keys
            $parameters | Should -Contain 'OnFailureEvidence'
            $parameters | Should -Contain 'FailureEvidenceFolder'
        }
    }

    It 'writes redacted locator suggestions for failed interactive actions' {
        $pagePath = Join-Path $TestDrive 'action-failure-page.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head><title>Action Failure Evidence Test</title></head>
<body>
  <main id="app">Action surface ready</main>
  <button data-testid="retry-action" aria-label="Retry action evidence">Retry action evidence</button>
  <input id="includeProof" type="checkbox" />
  <input id="searchBox" placeholder="Search mailbox" />
  <select id="scope"><option value="archive">Archive</option></select>
  <a href="/download?token=action-secret-token">Download proof</a>
</body>
</html>
'@
        $uri = [System.Uri]::new($pagePath).AbsoluteUri

        $session = Start-HtmlBrowserSession -Url $uri -LoadState DomContentLoaded
        try {
            $operations = @(
                @{
                    Name   = 'Hover'
                    Invoke = {
                        param($Session, $Folder)
                        Invoke-HtmlBrowserHover -Session $Session -Selector '#missing-hover' -Timeout 100 -OnFailureEvidence -FailureEvidenceFolder $Folder
                    }
                }
                @{
                    Name   = 'Key'
                    Invoke = {
                        param($Session, $Folder)
                        Invoke-HtmlBrowserKey -Session $Session -Selector '#missing-key' -Key Enter -Timeout 100 -OnFailureEvidence -FailureEvidenceFolder $Folder
                    }
                }
                @{
                    Name   = 'Scroll'
                    Invoke = {
                        param($Session, $Folder)
                        Invoke-HtmlBrowserScroll -Session $Session -Selector '#missing-scroll' -Timeout 100 -OnFailureEvidence -FailureEvidenceFolder $Folder
                    }
                }
                @{
                    Name   = 'Checked'
                    Invoke = {
                        param($Session, $Folder)
                        Set-HtmlBrowserChecked -Session $Session -Selector '#missing-checkbox' -Timeout 100 -OnFailureEvidence -FailureEvidenceFolder $Folder
                    }
                }
                @{
                    Name   = 'SelectOption'
                    Invoke = {
                        param($Session, $Folder)
                        Set-HtmlBrowserSelectOption -Session $Session -Selector '#missing-select' -Value archive -Timeout 100 -OnFailureEvidence -FailureEvidenceFolder $Folder
                    }
                }
                @{
                    Name   = 'SubmitForm'
                    Invoke = {
                        param($Session, $Folder)
                        $form = ConvertFrom-HtmlForm -Content '<form id="missing-form"><input name="q" /></form>' -IncludeMetadata
                        Submit-HtmlBrowserForm -Session $Session -Form $form -FieldValue @{ q = 'mailbox' } -Timeout 100 -OnFailureEvidence -FailureEvidenceFolder $Folder
                    }
                }
            )

            foreach ($operation in $operations) {
                $failureRoot = Join-Path $TestDrive "failure-$($operation.Name)"
                { & $operation.Invoke $session $failureRoot } | Should -Throw

                $failureFolders = @(Get-ChildItem -LiteralPath $failureRoot -Directory)
                $failureFolders.Count | Should -Be 1
                $folder = $failureFolders[0].FullName
                $manifestPath = Join-Path $folder 'evidence-manifest.json'
                $locatorPath = Join-Path $folder 'locator-suggestions.json'
                Test-Path -LiteralPath $manifestPath | Should -BeTrue
                Test-Path -LiteralPath $locatorPath | Should -BeTrue

                $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
                $locators = Get-Content -LiteralPath $locatorPath -Raw | ConvertFrom-Json
                $locatorJson = Get-Content -LiteralPath $locatorPath -Raw
                $manifest.Purpose | Should -Be 'FailureEvidence'
                $manifest.Operation | Should -Be $operation.Name
                $manifest.LocatorSuggestionCount | Should -BeGreaterThan 0
                $manifest.Artifacts.Kind | Should -Contain 'FailureContext'
                $manifest.Artifacts.Kind | Should -Contain 'LocatorSuggestions'
                $locators.Redacted | Should -BeTrue
                $locators.Candidates.Strategy | Should -Contain 'TestId'
                $locatorJson | Should -Not -Match 'action-secret-token'
                ($locators.Candidates.Selector -join "`n") | Should -Match 'token=<redacted>'
            }
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }
}
