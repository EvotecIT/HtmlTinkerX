Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'Browser locator candidates' {
    It 'exports locator command' {
        (Get-Command Find-HtmlBrowserLocator).Name | Should -Be 'Find-HtmlBrowserLocator'
    }

    It 'returns ranked locator candidates for user-facing controls' {
        $pagePath = Join-Path $TestDrive 'locator-page.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head><title>Locator Test</title></head>
<body>
  <main>
    <label for="searchBox">Search mailbox</label>
    <input id="searchBox" name="q" placeholder="Search mailbox" />
    <button data-testid="export-proof" aria-label="Export mailbox proof">Export proof</button>
    <a href="/mailbox/details">Mailbox details</a>
  </main>
</body>
</html>
'@
        $uri = [System.Uri]::new($pagePath).AbsoluteUri

        $session = Start-HtmlBrowserSession -Url $uri -LoadState DomContentLoaded
        try {
            $buttonCandidates = @(Find-HtmlBrowserLocator -Session $session -Query 'Export proof')
            $buttonCandidates.Count | Should -BeGreaterThan 0
            $buttonCandidates[0].Strategy | Should -Be 'TestId'
            $buttonCandidates[0].Selector | Should -Match 'data-testid'
            $buttonCandidates[0].Score | Should -BeGreaterOrEqual 90
            $buttonCandidates[0].SuggestedAction | Should -Be 'Click'
            $buttonCandidates[0].SuggestedCommand | Should -Be "Invoke-HtmlBrowserClick -Session `$session -Selector '$($buttonCandidates[0].Selector.Replace("'", "''"))'"
            $buttonCandidates[0].TestCommand | Should -Be "Test-HtmlBrowserElement -Session `$session -Selector '$($buttonCandidates[0].Selector.Replace("'", "''"))' -Visible"

            $inputCandidates = @(Find-HtmlBrowserLocator -Session $session -Query 'Search mailbox')
            $inputCandidates.Strategy | Should -Contain 'Id'
            $inputCandidates.Strategy | Should -Contain 'Placeholder'
            ($inputCandidates | Where-Object Strategy -eq 'Id' | Select-Object -First 1).SuggestedCommand |
                Should -Be "Set-HtmlBrowserInput -Session `$session -Selector 'input#searchBox' -Value '<value>'"
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }

    It 'returns usable selectors for native role candidates' {
        $pagePath = Join-Path $TestDrive 'locator-native-role-page.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<body>
  <main>
    <button>Approve request</button>
  </main>
</body>
</html>
'@
        $uri = [System.Uri]::new($pagePath).AbsoluteUri

        $session = Start-HtmlBrowserSession -Url $uri -LoadState DomContentLoaded
        try {
            $candidate = @(Find-HtmlBrowserLocator -Session $session -Query 'Approve request' -Limit 10) |
                Where-Object Strategy -eq 'Role' |
                Select-Object -First 1

            $candidate | Should -Not -BeNullOrEmpty
            $candidate.Selector | Should -Be 'button'
            $candidate.Locator | Should -Be "GetByRole('button', Name='Approve request')"
            Test-HtmlBrowserElement -Session $session -Selector $candidate.Selector -Visible | Should -BeTrue
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }

    It 'does not suggest ambiguous CSS selector candidates' {
        $pagePath = Join-Path $TestDrive 'locator-ambiguous-page.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<body>
  <main>
    <button data-testid="save" class="shared">Save draft</button>
    <button data-test="save" class="shared">Save final</button>
  </main>
</body>
</html>
'@
        $uri = [System.Uri]::new($pagePath).AbsoluteUri

        $session = Start-HtmlBrowserSession -Url $uri -LoadState DomContentLoaded
        try {
            $candidates = @(Find-HtmlBrowserLocator -Session $session -Query 'Save draft' -Limit 10)

            $candidates.Selector | Should -Not -Contain "[data-testid='save'],[data-test='save']"
            $candidates.Selector | Should -Not -Contain 'button.shared'
            ($candidates | Where-Object Selector -eq 'text=Save draft').Count | Should -Be 1
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }

    It 'does not suggest ambiguous visible text locator candidates' {
        $pagePath = Join-Path $TestDrive 'locator-ambiguous-text-page.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<body>
  <main>
    <button>Save</button>
    <button>Save</button>
  </main>
</body>
</html>
'@
        $uri = [System.Uri]::new($pagePath).AbsoluteUri

        $session = Start-HtmlBrowserSession -Url $uri -LoadState DomContentLoaded
        try {
            $candidates = @(Find-HtmlBrowserLocator -Session $session -Query 'Save' -Limit 10)

            $candidates.Selector | Should -Not -Contain 'text=Save'
            ($candidates | Where-Object Strategy -eq 'Text').Count | Should -Be 0
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }

    It 'escapes selector quotes and warns before suggesting sensitive selectors' {
        $pagePath = Join-Path $TestDrive 'locator-escaping-page.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head><title>Locator Escaping Test</title></head>
<body>
  <main>
    <button aria-label="Manager's approval">Approve</button>
    <a href="/download?token=super-secret-token">Download proof</a>
  </main>
</body>
</html>
'@
        $uri = [System.Uri]::new($pagePath).AbsoluteUri

        $session = Start-HtmlBrowserSession -Url $uri -LoadState DomContentLoaded
        try {
            $approval = @(Find-HtmlBrowserLocator -Session $session -Query "Manager's approval" -Limit 3) |
                Where-Object Strategy -eq 'AriaLabel' |
                Select-Object -First 1
            $approval.Selector | Should -Be "button[aria-label='Manager\'s approval']"
            $approval.SuggestedCommand | Should -Be "Invoke-HtmlBrowserClick -Session `$session -Selector 'button[aria-label=''Manager\''s approval'']'"
            $approval.TestCommand | Should -Be "Test-HtmlBrowserElement -Session `$session -Selector 'button[aria-label=''Manager\''s approval'']' -Visible"

            $download = @(Find-HtmlBrowserLocator -Session $session -Query 'Download proof' -Limit 10) |
                Where-Object Strategy -eq 'Href' |
                Select-Object -First 1
            $download.Warnings | Should -Contain 'Candidate selector appears to contain sensitive values. Review it before copying into scripts or logs.'
            $download.SuggestedCommand | Should -Be '$candidate | Format-List Strategy,Selector,Reason,Warnings'
            $download.SuggestedCommand | Should -Not -Match 'super-secret-token'
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }
}
