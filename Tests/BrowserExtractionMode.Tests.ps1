Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'Browser extraction mode helpers' {
    It 'Exports higher-level browser commands and aliases' {
        (Get-Command Get-HtmlBrowserDiagnostics).Name | Should -Be 'Get-HtmlBrowserDiagnostics'
        (Get-Alias Get-HtmlDiagnostics).Definition | Should -Be 'Get-HtmlBrowserDiagnostics'
        (Get-Command Get-HtmlBrowserElement).Name | Should -Be 'Get-HtmlBrowserElement'
        (Get-Alias Get-HtmlElement).Definition | Should -Be 'Get-HtmlBrowserElement'
        (Get-Command Test-HtmlBrowserElement).Name | Should -Be 'Test-HtmlBrowserElement'
        (Get-Alias Test-HtmlElement).Definition | Should -Be 'Test-HtmlBrowserElement'
        (Get-Command Get-HtmlBrowserActiveElement).Name | Should -Be 'Get-HtmlBrowserActiveElement'
        (Get-Alias Get-HtmlActiveElement).Definition | Should -Be 'Get-HtmlBrowserActiveElement'
        (Get-Command Get-HtmlBrowserStorage).Name | Should -Be 'Get-HtmlBrowserStorage'
        (Get-Alias Get-HtmlStorage).Definition | Should -Be 'Get-HtmlBrowserStorage'
        (Get-Command Set-HtmlBrowserStorage).Name | Should -Be 'Set-HtmlBrowserStorage'
        (Get-Alias Set-HtmlStorage).Definition | Should -Be 'Set-HtmlBrowserStorage'
        (Get-Command Save-HtmlBrowserContent).Name | Should -Be 'Save-HtmlBrowserContent'
        (Get-Alias Save-HtmlContent).Definition | Should -Be 'Save-HtmlBrowserContent'
        (Get-Command Invoke-HtmlBrowserHover).Name | Should -Be 'Invoke-HtmlBrowserHover'
        (Get-Alias Invoke-HtmlHover).Definition | Should -Be 'Invoke-HtmlBrowserHover'
        (Get-Command Invoke-HtmlBrowserKey).Name | Should -Be 'Invoke-HtmlBrowserKey'
        (Get-Alias Invoke-HtmlKey).Definition | Should -Be 'Invoke-HtmlBrowserKey'
        (Get-Command Invoke-HtmlBrowserScroll).Name | Should -Be 'Invoke-HtmlBrowserScroll'
        (Get-Alias Invoke-HtmlScroll).Definition | Should -Be 'Invoke-HtmlBrowserScroll'
        (Get-Command Wait-HtmlBrowserContent).Name | Should -Be 'Wait-HtmlBrowserContent'
        (Get-Alias Wait-HtmlContent).Definition | Should -Be 'Wait-HtmlBrowserContent'
    }

    It 'Provides discoverable help examples for the browser extraction commands' {
        $commands = @(
            'Get-HtmlBrowserDiagnostics',
            'Invoke-HtmlBrowserHover',
            'Invoke-HtmlBrowserKey',
            'Close-HtmlBrowserOverlay',
            'Invoke-HtmlBrowserScroll',
            'Wait-HtmlBrowserContent',
            'Set-HtmlBrowserInput',
            'Invoke-HtmlBrowserClick',
            'Get-HtmlBrowserElement',
            'Test-HtmlBrowserElement',
            'Get-HtmlBrowserActiveElement',
            'Get-HtmlBrowserStorage',
            'Set-HtmlBrowserStorage',
            'Save-HtmlBrowserContent'
        )

        foreach ($command in $commands) {
            $help = Get-Help $command -Examples | Out-String
            $help | Should -Match $command
            $help | Should -Match 'Session'
        }
    }

    It 'exposes reusable launch profile parameters for one-shot interactable discovery' {
        $parameters = (Get-Command Get-HtmlBrowserInteractable).Parameters.Keys

        $parameters | Should -Contain 'ProfilePath'
        $parameters | Should -Contain 'Scenario'
        $parameters | Should -Contain 'UserDataDirectory'
        $parameters | Should -Contain 'StatePath'
        $parameters | Should -Contain 'BrowserChannel'
        $parameters | Should -Contain 'LoadState'
        $parameters | Should -Contain 'BlockResourceType'
        $parameters | Should -Contain 'BlockResourcePattern'
    }

    It 'rejects document resource blocking for one-shot interactable discovery' {
        $htmlPath = Join-Path $TestDrive 'document-block-interactable.html'
        Set-Content -LiteralPath $htmlPath -Encoding UTF8 -Value '<!doctype html><button>Blocked</button>'

        { Get-HtmlBrowserInteractable -Path $htmlPath -BlockResourceType Document } |
            Should -Throw -ExpectedMessage '*BlockResourceType Document would abort page navigation*'
    }

    It 'discovers interactable elements directly from a file with scenario launch defaults' {
        $htmlPath = Join-Path $TestDrive 'one-shot-interactable.html'
        Set-Content -LiteralPath $htmlPath -Encoding UTF8 -Value '<!doctype html><main><button id="go">Start proof</button></main>'

        $elements = Get-HtmlBrowserInteractable -Path $htmlPath -Scenario SinglePageApp -LoadState DomContentLoaded

        $elements.Text | Should -Contain 'Start proof'
    }

    It 'exposes reusable launch profile parameters for one-shot content saves' {
        $command = Get-Command Save-HtmlBrowserContent

        $command.Parameters.Keys | Should -Contain 'ProfilePath'
        $command.Parameters.Keys | Should -Contain 'Scenario'
        $command.Parameters.Keys | Should -Contain 'UserDataDirectory'
        $command.Parameters.Keys | Should -Contain 'StatePath'
        $command.Parameters.Keys | Should -Contain 'Proxy'
        $command.Parameters.Keys | Should -Contain 'ProxyCredential'
        $command.Parameters.Keys | Should -Contain 'LoadState'
        $command.Parameters.Keys | Should -Contain 'NavigationTimeout'
        $command.Parameters.Keys | Should -Contain 'BlockResourceType'
        $command.Parameters.Keys | Should -Contain 'BlockResourcePattern'
    }

    It 'rejects document resource blocking for one-shot content navigation' {
        $htmlPath = Join-Path $TestDrive 'document-block-content.html'
        $outPath = Join-Path $TestDrive 'document-block-content.html'
        Set-Content -LiteralPath $htmlPath -Encoding UTF8 -Value '<!doctype html><main>blocked</main>'

        { Save-HtmlBrowserContent -Path $htmlPath -OutFile $outPath -BlockResourceType Document } |
            Should -Throw -ExpectedMessage '*BlockResourceType Document would abort page navigation*'
    }

    It 'can save rendered content directly from a file with scenario launch defaults' {
        $htmlPath = Join-Path $TestDrive 'one-shot-content.html'
        $outPath = Join-Path $TestDrive 'one-shot-content.txt'
        @'
<!doctype html>
<html>
<body>
<main id="target">direct content ready</main>
</body>
</html>
'@ | Set-Content -LiteralPath $htmlPath -Encoding UTF8

        Save-HtmlBrowserContent -Path $htmlPath -Selector '#target' -AsText -Scenario AuditProof -OutFile $outPath -PassThru | Should -Be $outPath
        Get-Content -LiteralPath $outPath -Raw | Should -Match 'direct content ready'
    }

    It 'Can inspect elements, storage, active focus, and save rendered content' {
        $htmlPath = Join-Path $TestDrive 'inspection-actions.html'
        $outPath = Join-Path $TestDrive 'rendered-target.html'
        @'
<!doctype html>
<html>
<body>
<main>
<input id="search" name="q" value="">
<button class="item" data-kind="primary">First</button>
<button class="item" data-kind="secondary" onclick="document.getElementById('target').textContent='Second clicked'">Second</button>
<input id="flag" type="checkbox" checked>
<section id="target">Ready</section>
<script>
localStorage.setItem('existingLocal', '1');
sessionStorage.setItem('existingSession', '2');
</script>
</main>
</body>
</html>
'@ | Set-Content -LiteralPath $htmlPath -Encoding UTF8
        $session = Invoke-HtmlRendering -Url ([System.Uri]::new($htmlPath).AbsoluteUri) -Session

        try {
            $elements = Get-HtmlElement -Session $session -Selector '.item' -IncludeAttributes -IncludeHtml
            $elements.Count | Should -Be 2
            $elements[0].Tag | Should -Be 'button'
            $elements[0].Attributes['data-kind'] | Should -Be 'primary'
            $elements[0].Width | Should -BeGreaterThan 0

            (Get-HtmlContent -Session $session -Selector '.item' -All -AsText) | Should -Be @('First', 'Second')
            Test-HtmlElement -Session $session -Selector '#flag' -Checked | Should -BeTrue
            Wait-HtmlContent -Session $session -Element -Selector '#target' -Visible -InViewport -Timeout 1000

            Invoke-HtmlClick -Session $session -Text 'Second' -Exact -Nth 0
            Wait-HtmlContent -Session $session -Text 'Second clicked' -Selector '#target' -Exact

            Invoke-HtmlClick -Session $session -Selector '#search'
            $active = Get-HtmlActiveElement -Session $session -IncludeAttributes
            $active.Id | Should -Be 'search'
            $active.Attributes['name'] | Should -Be 'q'

            Set-HtmlStorage -Session $session -Scope Local -Key story -Value enabled
            $storage = Get-HtmlStorage -Session $session -Scope All
            ($storage | Where-Object { $_.Scope -eq 'Local' -and $_.Key -eq 'story' }).Value | Should -Be 'enabled'
            ($storage | Where-Object { $_.Scope -eq 'Session' -and $_.Key -eq 'existingSession' }).Value | Should -Be '2'

            Save-HtmlContent -Session $session -Selector '#target' -OutFile $outPath -PassThru | Should -Be $outPath
            Get-Content -LiteralPath $outPath -Raw | Should -Match 'Second clicked'
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }

    It 'Runs the local browser extraction story end to end' {
        $statePath = Join-Path $TestDrive 'browser-state.json'
        $summary = & "$PSScriptRoot/../Examples/Example-BrowserExtractionModeLocal.ps1" -StatePath $statePath

        $summary.ResultText | Should -Match 'Found HtmlTinkerX guide'
        $summary.ResultText | Should -Match 'Workbench profile sample'
        $summary.ProfileName | Should -Not -BeNullOrEmpty
        $summary.RenderProfile | Should -Not -BeNullOrEmpty
        $summary.WorkbenchMode | Should -Be 'RenderedSnapshot'
        $summary.WorkbenchTitle | Should -Be 'Browser extraction local story'
        $summary.ObservedApiCallCount | Should -BeGreaterThan 0
        $summary.LocalStorageKeys | Should -Contain 'storyLocal'
        $summary.StaticRenderedDeltaCount | Should -BeGreaterThan 0
        Test-Path -LiteralPath $statePath | Should -BeTrue
    }

    It 'Can use interactive command helpers together' {
        $htmlPath = Join-Path $TestDrive 'interactive-actions.html'
        @'
<!doctype html>
<html>
<body style="min-height:3000px">
<button id="hover" onmouseover="document.getElementById('result').textContent = 'hovered';">Hover</button>
<input id="entry" onkeydown="if (event.key === 'Enter') document.getElementById('result').textContent = 'submitted';">
<button id="overlay" onclick="this.remove()">Accept</button>
<div id="result"></div>
<div id="target" style="margin-top:2500px">Target</div>
</body>
</html>
'@ | Set-Content -LiteralPath $htmlPath -Encoding UTF8
        $uri = [System.Uri]::new($htmlPath).AbsoluteUri
        $session = Invoke-HtmlRendering -Url $uri -Session

        try {
            Invoke-HtmlHover -Session $session -Selector '#hover'
            Wait-HtmlContent -Session $session -Text 'hovered' -Selector '#result' -Exact

            Set-HtmlInput -Session $session -Selector '#entry' -Value 'query' -Type -DelayMs 0
            Invoke-HtmlKey -Session $session -Selector '#entry' -Key 'Enter'
            Wait-HtmlContent -Session $session -Text 'submitted' -Selector '#result' -Exact

            $dismissed = Invoke-HtmlOverlayDismissal -Session $session -Timeout 500
            $dismissed | Should -Contain 'Dismissed text: Accept'

            Invoke-HtmlScroll -Session $session -Selector '#target'
            $inViewport = Invoke-HtmlScript -Session $session -Script '(() => { const rect = document.getElementById("target").getBoundingClientRect(); return rect.top >= 0 && rect.top <= window.innerHeight; })()'
            $inViewport | Should -BeTrue

            { Wait-HtmlContent -Session $session -Stable -StableMilliseconds 50 -PollMilliseconds 25 -Timeout 1000 } | Should -Not -Throw
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }

    It 'Can collect browser diagnostics with storage, console, and API hints' {
        $session = Invoke-HtmlRendering -Url 'about:blank' -Session

        try {
            Register-HtmlRoute -Session $session -Pattern '**/diagnostics.html' -ScriptBlock {
                param($route)
                Complete-HtmlRoute -Route $route -Options @{
                    Status = 200
                    ContentType = 'text/html'
                    Body = @'
<!doctype html>
<html>
<body>
<main id="status">loading</main>
<img src="/blocked.png" alt="">
<script>
localStorage.setItem('diagLocal', '1');
sessionStorage.setItem('diagSession', '1');
console.error('diagnostic console error');
fetch('/api/diagnostics').then(() => {
  document.getElementById('status').textContent = 'diag-ready';
});
</script>
</body>
</html>
'@
                }
            }
            Register-HtmlRoute -Session $session -Pattern '**/api/diagnostics' -ScriptBlock {
                param($route)
                Complete-HtmlRoute -Route $route -Options @{
                    Status = 200
                    ContentType = 'application/json'
                    Body = '{"ok":true}'
                }
            }
            Register-HtmlRoute -Session $session -Pattern '**/blocked.png' -ScriptBlock {
                param($route)
                $route.AbortAsync() | Out-Null
            }

            Invoke-HtmlNavigation -Session $session -Url 'https://example.com/diagnostics.html'
            Wait-HtmlContent -Session $session -Text 'diag-ready' -Selector '#status' -Exact
            $diagnostics = Get-HtmlDiagnostics -Session $session

            $diagnostics.UserAgent | Should -Not -BeNullOrEmpty
            $diagnostics.LocalStorageKeys | Should -Contain 'diagLocal'
            $diagnostics.SessionStorageKeys | Should -Contain 'diagSession'
            $diagnostics.ObservedApiCalls.Url | Should -Contain 'https://example.com/api/diagnostics'
            $diagnostics.ConsoleErrors.Text | Should -Contain 'diagnostic console error'
            $diagnostics.FailedRequests.Url | Should -Contain 'https://example.com/blocked.png'
            ($diagnostics.ConsistencyWarnings | Where-Object { $_ -like 'Console errors observed:*' }).Count | Should -BeGreaterThan 0
            $diagnostics.FingerprintRiskScore | Should -BeGreaterThan 0
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }

    It 'Accepts named render profiles for common extraction modes' {
        $htmlPath = Join-Path $TestDrive 'profile-page.html'
        @'
<!doctype html>
<html>
<body>
<main id="content">profile ready</main>
</body>
</html>
'@ | Set-Content -LiteralPath $htmlPath -Encoding UTF8
        $uri = [System.Uri]::new($htmlPath).AbsoluteUri

        (Invoke-HtmlRendering -Url $uri -RenderProfile FastStaticFallback -Selector '#content' -AsText) | Should -Be 'profile ready'
        (Invoke-HtmlRendering -Url $uri -RenderProfile LoginProtected -Selector '#content' -AsText) | Should -Be 'profile ready'
        (Invoke-HtmlRendering -Url $uri -RenderProfile NetworkCapture -Selector '#content' -AsText) | Should -Be 'profile ready'
        (Invoke-HtmlRendering -Url $uri -RenderProfile LowBandwidth -Selector '#content' -AsText) | Should -Be 'profile ready'
    }
}
