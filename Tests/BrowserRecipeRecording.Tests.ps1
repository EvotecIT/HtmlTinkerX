Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'Browser recipe recording' {
    It 'exports recorder commands' {
        Get-Command Start-HtmlBrowserRecipeRecording | Should -Not -BeNullOrEmpty
        Get-Command Stop-HtmlBrowserRecipeRecording | Should -Not -BeNullOrEmpty
        Get-Command Export-HtmlBrowserRecipe | Should -Not -BeNullOrEmpty
        Get-Command Optimize-HtmlBrowserRecipe | Should -Not -BeNullOrEmpty
        (Get-Command Invoke-HtmlBrowserRecipe).Parameters.Keys | Should -Contain 'Variable'
        (Get-Command Start-HtmlBrowserRecipeRecording).Parameters.Keys | Should -Contain 'NoSelectorAlternates'
        (Get-Command Start-HtmlBrowserRecipeRecording).Parameters.Keys | Should -Contain 'SelectorAlternateLimit'
        (Get-Command Stop-HtmlBrowserRecipeRecording).Parameters.Keys | Should -Contain 'VariableTemplatePath'
        (Get-Command Stop-HtmlBrowserRecipeRecording).Parameters.Keys | Should -Contain 'IncludeOptionalVariables'
        (Get-Command Stop-HtmlBrowserRecipeRecording).Parameters.Keys | Should -Contain 'HardenSelectors'
        (Get-Command Stop-HtmlBrowserRecipeRecording).Parameters.Keys | Should -Contain 'HardeningReportPath'
        (Get-Command Export-HtmlBrowserRecipe).Parameters.Keys | Should -Contain 'HardenSelectors'
        (Get-Command Export-HtmlBrowserRecipe).Parameters.Keys | Should -Contain 'HardeningReportPath'
        (Get-Command Optimize-HtmlBrowserRecipe).Parameters.Keys | Should -Contain 'ReplaceSelectorAlternates'
        (Get-Command Optimize-HtmlBrowserRecipe).Parameters.Keys | Should -Contain 'ReportPath'
    }

    It 'records successful session actions and replays them as a recipe' {
        $pagePath = Join-Path $TestDrive 'recordable-page.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head><title>Recordable Recipe</title></head>
<body>
  <main>
    <input id="search" name="q" />
    <input id="password" name="password" type="password" />
    <label><input id="include" type="checkbox" /> Include archive</label>
    <select id="scope"><option value="inbox">Inbox</option><option value="archive">Archive</option></select>
    <button id="load" onclick="document.getElementById('results').textContent = document.getElementById('search').value + ':' + document.getElementById('scope').value + ':' + document.getElementById('include').checked;">Load</button>
    <section id="results">Waiting</section>
  </main>
</body>
</html>
'@
        $recipePath = Join-Path $TestDrive 'recorded.browser.recipe.json'
        $variableTemplatePath = Join-Path $TestDrive 'recorded.browser.variables.json'
        $uri = [System.Uri]::new($pagePath).AbsoluteUri
        $session = Start-HtmlBrowserSession -Url $uri -LoadState DomContentLoaded

        try {
            Start-HtmlBrowserRecipeRecording -Session $session -Name 'RecordedProof' -IncludeCurrentUrl | Out-Null
            Set-HtmlBrowserInput -Session $session -Selector '#search' -Value 'mailbox'
            Set-HtmlBrowserInput -Session $session -Selector '#password' -Value 'VerySecret123!'
            Set-HtmlBrowserChecked -Session $session -Selector '#include'
            Set-HtmlBrowserSelectOption -Session $session -Selector '#scope' -Value archive
            Invoke-HtmlBrowserClick -Session $session -Selector '#load'
            Wait-HtmlBrowserContent -Session $session -Selector '#results' -Text 'mailbox:archive:true' -Exact
            Find-HtmlBrowserLocator -Session $session -Query 'Load' -Limit 3 | Out-Null

            $recordedPath = Stop-HtmlBrowserRecipeRecording -Session $session -Path $recipePath -VariableTemplatePath $variableTemplatePath
            $recordedPath | Should -Be $recipePath
        } finally {
            Close-HtmlBrowserSession -Session $session
        }

        $recipe = Get-Content -LiteralPath $recipePath -Raw | ConvertFrom-Json
        $recipe.Name | Should -Be 'RecordedProof'
        $recipe.StartUrl | Should -Be $uri
        $recipe.Steps.Action | Should -Contain 'Input'
        $recipe.Steps.Action | Should -Contain 'SetChecked'
        $recipe.Steps.Action | Should -Contain 'SelectOption'
        $recipe.Steps.Action | Should -Contain 'Click'
        $recipe.Steps.Action | Should -Contain 'WaitText'
        $recipe.Steps.Action | Should -Contain 'Locator'
        $recipeJson = Get-Content -LiteralPath $recipePath -Raw
        $recipeJson | Should -Not -Match 'VerySecret123!'
        $searchStep = $recipe.Steps | Where-Object { $_.Action -eq 'Input' -and $_.Selector -eq '#search' } | Select-Object -First 1
        $passwordStep = $recipe.Steps | Where-Object { $_.Action -eq 'Input' -and $_.Selector -eq '#password' } | Select-Object -First 1
        $searchStep.Value | Should -Be 'mailbox'
        $searchStep.SelectorAlternates | Should -Contain 'input#search'
        $searchStep.SelectorAlternates | Should -Contain "input[name='q']"
        $passwordStep.Value | Should -Be '<redacted>'
        $passwordStep.ValueRedacted | Should -BeTrue
        $passwordStep.ValueRedactionReason | Should -Match 'sensitive field'
        @($passwordStep.SelectorAlternates).Count | Should -Be 0
        $variableTemplate = Get-Content -LiteralPath $variableTemplatePath -Raw | ConvertFrom-Json
        $variableTemplate.password | Should -Be '<secret>'
        $variableTemplate.PSObject.Properties.Name | Should -Not -Contain 'search'

        $missingVariableResult = Invoke-HtmlBrowserRecipe -Path $recipePath
        $missingVariableResult.Succeeded | Should -BeFalse
        $missingVariableResult.SkippedBeforeExecution | Should -BeTrue
        $missingVariableResult.CreatedSession | Should -BeFalse
        $missingVariableResult.Validation.MissingVariables | Should -Be @('password')
        $missingVariableResult.Steps.Count | Should -Be 0

        $result = Invoke-HtmlBrowserRecipe -Path $recipePath -Variable @{ password = 'runtime-secret' }

        $result.Succeeded | Should -BeTrue
        $result.CreatedSession | Should -BeTrue
        $result.Steps.Count | Should -Be $recipe.Steps.Count
        $result.Steps.Action | Should -Contain 'SetChecked'
        $result.Steps.Action | Should -Contain 'SelectOption'
    }

    It 'can disable selector alternate capture for compact recordings' {
        $pagePath = Join-Path $TestDrive 'recording-no-alternates.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<body>
  <main>
    <input id="q" name="query" placeholder="Search" />
  </main>
</body>
</html>
'@
        $uri = [System.Uri]::new($pagePath).AbsoluteUri
        $recipePath = Join-Path $TestDrive 'no-alternates.browser.recipe.json'
        $session = Start-HtmlBrowserSession -Url $uri -LoadState DomContentLoaded

        try {
            Start-HtmlBrowserRecipeRecording -Session $session -Name 'CompactRecording' -IncludeCurrentUrl -NoSelectorAlternates | Out-Null
            Set-HtmlBrowserInput -Session $session -Selector '#q' -Value 'mailbox'
            Stop-HtmlBrowserRecipeRecording -Session $session -Path $recipePath | Out-Null
        } finally {
            Close-HtmlBrowserSession -Session $session
        }

        $recipe = Get-Content -LiteralPath $recipePath -Raw | ConvertFrom-Json
        $inputStep = $recipe.Steps | Where-Object Action -eq 'Input' | Select-Object -First 1

        $inputStep.Selector | Should -Be '#q'
        @($inputStep.SelectorAlternates).Count | Should -Be 0
    }

    It 'records Nth for disambiguated selector clicks and replays the same occurrence' {
        $pagePath = Join-Path $TestDrive 'recording-nth-click.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<body>
  <main>
    <button class="choice" onclick="document.getElementById('result').textContent = 'first'">Choose</button>
    <button class="choice" onclick="document.getElementById('result').textContent = 'second'">Choose</button>
    <section id="result">waiting</section>
  </main>
</body>
</html>
'@
        $recipePath = Join-Path $TestDrive 'recorded-nth.browser.recipe.json'
        $uri = [System.Uri]::new($pagePath).AbsoluteUri
        $session = Start-HtmlBrowserSession -Url $uri -LoadState DomContentLoaded

        try {
            Start-HtmlBrowserRecipeRecording -Session $session -Name 'NthRecording' -IncludeCurrentUrl | Out-Null
            Invoke-HtmlBrowserClick -Session $session -Selector '.choice' -Nth 1
            Wait-HtmlBrowserContent -Session $session -Selector '#result' -Text 'second' -Exact
            Stop-HtmlBrowserRecipeRecording -Session $session -Path $recipePath | Out-Null
        } finally {
            Close-HtmlBrowserSession -Session $session
        }

        $recipe = Get-Content -LiteralPath $recipePath -Raw | ConvertFrom-Json
        $clickStep = $recipe.Steps | Where-Object Action -eq 'Click' | Select-Object -First 1

        $clickStep.Nth | Should -Be 1

        $result = Invoke-HtmlBrowserRecipe -Path $recipePath

        $result.Succeeded | Should -BeTrue
        $result.Steps[0].Succeeded | Should -BeTrue
    }

    It 'exports a recording snapshot without stopping recording' {
        $pagePath = Join-Path $TestDrive 'recording-snapshot.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value '<!doctype html><main><input id="q" /></main>'
        $uri = [System.Uri]::new($pagePath).AbsoluteUri
        $recipePath = Join-Path $TestDrive 'snapshot.browser.recipe.json'
        $session = Start-HtmlBrowserSession -Url $uri -LoadState DomContentLoaded

        try {
            Start-HtmlBrowserRecipeRecording -Session $session -Name 'Snapshot' -IncludeCurrentUrl | Out-Null
            Set-HtmlBrowserInput -Session $session -Selector '#q' -Value 'first'
            $snapshotPath = Export-HtmlBrowserRecipe -Session $session -Path $recipePath
            Set-HtmlBrowserInput -Session $session -Selector '#q' -Value 'second'
            $stopped = Stop-HtmlBrowserRecipeRecording -Session $session

            $snapshotPath | Should -Be $recipePath
            $snapshot = Get-Content -LiteralPath $recipePath -Raw | ConvertFrom-Json
            $snapshot.Steps.Count | Should -Be 1
            $snapshot.Steps[0].Value | Should -Be 'first'
            $stopped.Steps.Count | Should -Be 2
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }

    It 'repairs recipe selectors by adding safe alternates from the current page' {
        $pagePath = Join-Path $TestDrive 'repair-recipe-selectors.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<body>
  <main>
    <label for="q">Search mailbox</label>
    <input id="q" name="query" placeholder="Search mailbox" />
    <a id="download" href="/download?token=repair-secret-token">Download</a>
  </main>
</body>
</html>
'@
        $uri = [System.Uri]::new($pagePath).AbsoluteUri
        $recipePath = Join-Path $TestDrive 'repair-source.browser.recipe.json'
        $outPath = Join-Path $TestDrive 'repair-hardened.browser.recipe.json'
        $reportPath = Join-Path $TestDrive 'repair-hardening-report.json'
        $recipe = [ordered]@{
            SchemaVersion = 1
            Name          = 'RepairSelectors'
            StartUrl      = $uri
            Steps         = @(
                [ordered]@{
                    Name     = 'Type search'
                    Action   = 'Input'
                    Selector = '#q'
                    Value    = 'mailbox'
                },
                [ordered]@{
                    Name     = 'Sensitive download'
                    Action   = 'Click'
                    Selector = "a[href='/download?token=repair-secret-token']"
                }
            )
        }
        $recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8
        $session = Start-HtmlBrowserSession -Url $uri -LoadState DomContentLoaded

        try {
            $result = Optimize-HtmlBrowserRecipe -Session $session -Path $recipePath -OutPath $outPath -ReportPath $reportPath
        } finally {
            Close-HtmlBrowserSession -Session $session
        }

        $result.Changed | Should -BeTrue
        $result.ReportPath | Should -Be $reportPath
        $result.ChangedStepCount | Should -Be 1
        $result.AddedAlternateCount | Should -BeGreaterThan 0
        $result.Steps[0].AddedAlternates | Should -Contain 'input#q'
        $result.Steps[0].AddedAlternates | Should -Contain "input[name='query']"
        $result.Steps[1].Changed | Should -BeFalse
        $result.Steps[1].Reason | Should -Match 'sensitive values'
        $result.Steps[1].SuggestedCommand | Should -Be 'Get-HtmlBrowserInteractable -Session $session | Select-Object -First 20'
        $hardened = Get-Content -LiteralPath $outPath -Raw | ConvertFrom-Json
        ($hardened.Steps | Where-Object Name -eq 'Type search').SelectorAlternates | Should -Contain 'input#q'
        ($hardened.Steps | Where-Object Name -eq 'Sensitive download').SelectorAlternates.Count | Should -Be 0
        $reportJson = Get-Content -LiteralPath $reportPath -Raw
        $reportJson | Should -Not -Match 'repair-secret-token'
        $report = $reportJson | ConvertFrom-Json
        $report.Changed | Should -BeTrue
        $report.ChangedStepCount | Should -Be 1
        $report.AddedAlternateCount | Should -BeGreaterThan 0
        ($report.Steps | Where-Object StepName -eq 'Type search').AddedAlternates | Should -Contain 'input#q'
        ($report.Steps | Where-Object StepName -eq 'Sensitive download').Selector | Should -Match '<redacted>'
        ($report.Steps | Where-Object StepName -eq 'Sensitive download').SuggestedCommand | Should -Not -Match 'repair-secret-token'
    }

    It 'can harden selector alternates while stopping a compact recording' {
        $pagePath = Join-Path $TestDrive 'recording-stop-harden.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<body>
  <main>
    <input id="q" name="query" placeholder="Search" />
  </main>
</body>
</html>
'@
        $uri = [System.Uri]::new($pagePath).AbsoluteUri
        $recipePath = Join-Path $TestDrive 'stop-harden.browser.recipe.json'
        $reportPath = Join-Path $TestDrive 'stop-harden.report.json'
        $session = Start-HtmlBrowserSession -Url $uri -LoadState DomContentLoaded

        try {
            Start-HtmlBrowserRecipeRecording -Session $session -Name 'StopHarden' -IncludeCurrentUrl -NoSelectorAlternates | Out-Null
            Set-HtmlBrowserInput -Session $session -Selector '#q' -Value 'mailbox'
            Stop-HtmlBrowserRecipeRecording -Session $session -Path $recipePath -HardenSelectors -HardeningReportPath $reportPath | Out-Null
        } finally {
            Close-HtmlBrowserSession -Session $session
        }

        $recipe = Get-Content -LiteralPath $recipePath -Raw | ConvertFrom-Json
        $inputStep = $recipe.Steps | Where-Object Action -eq 'Input' | Select-Object -First 1

        $inputStep.Selector | Should -Be '#q'
        $inputStep.SelectorAlternates | Should -Contain 'input#q'
        $inputStep.SelectorAlternates | Should -Contain "input[name='query']"
        $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
        $report.Summary | Should -Match 'Hardened'
        $report.AddedAlternateCount | Should -BeGreaterThan 0
    }

    It 'can harden selector alternates while exporting an active recording snapshot' {
        $pagePath = Join-Path $TestDrive 'recording-export-harden.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<body>
  <main>
    <input id="q" name="query" placeholder="Search" />
  </main>
</body>
</html>
'@
        $uri = [System.Uri]::new($pagePath).AbsoluteUri
        $recipePath = Join-Path $TestDrive 'export-harden.browser.recipe.json'
        $reportPath = Join-Path $TestDrive 'export-harden.report.json'
        $session = Start-HtmlBrowserSession -Url $uri -LoadState DomContentLoaded

        try {
            Start-HtmlBrowserRecipeRecording -Session $session -Name 'ExportHarden' -IncludeCurrentUrl -NoSelectorAlternates | Out-Null
            Set-HtmlBrowserInput -Session $session -Selector '#q' -Value 'mailbox'
            Export-HtmlBrowserRecipe -Session $session -Path $recipePath -HardenSelectors -HardeningReportPath $reportPath | Out-Null
            $stopped = Stop-HtmlBrowserRecipeRecording -Session $session
        } finally {
            Close-HtmlBrowserSession -Session $session
        }

        $snapshot = Get-Content -LiteralPath $recipePath -Raw | ConvertFrom-Json
        $snapshotStep = $snapshot.Steps | Where-Object Action -eq 'Input' | Select-Object -First 1

        $snapshotStep.SelectorAlternates | Should -Contain 'input#q'
        $snapshotStep.SelectorAlternates | Should -Contain "input[name='query']"
        @($stopped.Steps[0].SelectorAlternates).Count | Should -Be 0
        $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
        $report.ChangedStepCount | Should -Be 1
    }

    It 'records evidence export options for replay fidelity' {
        $pagePath = Join-Path $TestDrive 'recording-evidence.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head><title>Evidence Recording</title></head>
<body>
  <main>Authorization: Bearer abc.def.ghi</main>
  <form id="handoff" method="post" action="https://service-provider.example/saml/consume">
    <input type="hidden" name="SAMLResponse" value="recorded-sso-secret" />
  </form>
</body>
</html>
'@
        $uri = [System.Uri]::new($pagePath).AbsoluteUri
        $recipePath = Join-Path $TestDrive 'evidence-recording.browser.recipe.json'
        $recordedEvidence = Join-Path $TestDrive 'recorded-evidence'
        $replayEvidence = Join-Path $TestDrive 'replay-evidence'
        $session = Start-HtmlBrowserSession -Url $uri -LoadState DomContentLoaded

        try {
            Start-HtmlBrowserRecipeRecording -Session $session -Name 'EvidenceRecording' -IncludeCurrentUrl | Out-Null
            Export-HtmlBrowserEvidence -Session $session -OutFolder $recordedEvidence -BaseFileName recorded -Artifact Html,Text,SsoHandoffSummary -NoManifest | Out-Null
            Stop-HtmlBrowserRecipeRecording -Session $session -Path $recipePath | Out-Null
        } finally {
            Close-HtmlBrowserSession -Session $session
        }

        $recipeJson = Get-Content -LiteralPath $recipePath -Raw
        $recipe = $recipeJson | ConvertFrom-Json
        $evidenceStep = $recipe.Steps | Where-Object Action -eq 'Evidence' | Select-Object -First 1

        $evidenceStep | Should -Not -BeNullOrEmpty
        $evidenceStep.Screenshot | Should -BeFalse
        $evidenceStep.Html | Should -BeTrue
        $evidenceStep.VisibleText | Should -BeTrue
        $evidenceStep.Markdown | Should -BeFalse
        $evidenceStep.SsoHandoffSummary | Should -BeTrue
        $evidenceStep.Manifest | Should -BeFalse
        $evidenceStep.RedactSensitiveValues | Should -BeTrue

        $recipe.Steps[0].OutFolder = $replayEvidence
        $recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8
        $result = Invoke-HtmlBrowserRecipe -Path $recipePath

        $result.Succeeded | Should -BeTrue
        Test-Path -LiteralPath (Join-Path $replayEvidence 'recorded.html') | Should -BeTrue
        Test-Path -LiteralPath (Join-Path $replayEvidence 'recorded.txt') | Should -BeTrue
        Test-Path -LiteralPath (Join-Path $replayEvidence 'sso-handoff-summary.json') | Should -BeTrue
        Test-Path -LiteralPath (Join-Path $replayEvidence 'recorded.png') | Should -BeFalse
        Test-Path -LiteralPath (Join-Path $replayEvidence 'recorded.md') | Should -BeFalse
        Test-Path -LiteralPath (Join-Path $replayEvidence 'evidence-manifest.json') | Should -BeFalse
        Get-Content -LiteralPath (Join-Path $replayEvidence 'recorded.txt') -Raw | Should -Not -Match 'abc\.def\.ghi'
        Get-Content -LiteralPath (Join-Path $replayEvidence 'sso-handoff-summary.json') -Raw | Should -Not -Match 'recorded-sso-secret'
    }
}
