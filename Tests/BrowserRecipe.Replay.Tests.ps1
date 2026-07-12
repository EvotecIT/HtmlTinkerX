Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'Browser recipe replay and evidence' {

    It 'supports CI-style strict recipe validation and terminating failure' {
        $recipePath = Join-Path $TestDrive 'browser-ci-preflight.recipe.json'
        $recipe = [ordered]@{
            SchemaVersion = 1
            Name          = 'CiPreflight'
            StartUrl      = 'https://example.org/app'
            Timeout       = 3000
            Steps         = @(
                [ordered]@{
                    Name            = 'Optional cleanup'
                    Action          = 'WaitMilliseconds'
                    Milliseconds    = 1
                    ContinueOnError = $true
                }
            )
        }
        $recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8

        $normal = Test-HtmlBrowserRecipe -Path $recipePath
        $strict = Test-HtmlBrowserRecipe -Path $recipePath -StrictPreflight

        $normal.IsValid | Should -BeTrue
        $normal.Passed | Should -BeTrue
        $normal.BlockingIssueCount | Should -Be 0
        $normal.RecommendedExitCode | Should -Be 0
        $normal.SuggestedCommand | Should -Be "Invoke-HtmlBrowserRecipe -Path '<recipe.json>'"

        $strict.IsValid | Should -BeTrue
        $strict.Passed | Should -BeFalse
        $strict.StrictPreflight | Should -BeTrue
        $strict.WarningCount | Should -Be 1
        $strict.BlockingIssueCount | Should -Be 1
        $strict.BlockingIssues[0].Severity | Should -Be 'Warning'
        $strict.BlockingIssues[0].Message | Should -Be 'Step will continue on error, which may hide broken evidence or extraction flows.'
        $strict.BlockingIssues[0].SuggestedCommand | Should -Be '$validation.BlockingIssues | Where-Object Property -eq ''ContinueOnError'' | Format-Table StepIndex,Action,Message,SuggestedFix -AutoSize'
        $strict.BlockingIssues[0].DocumentationHint | Should -Match 'ContinueOnError'
        $strict.RecommendedExitCode | Should -Be 1
        $strict.Summary | Should -Match 'Recipe strict preflight failed'
        $strict.SuggestedCommand | Should -Be '$validation.BlockingIssues | Format-Table Severity,StepIndex,Action,Property,Message,SuggestedFix,SuggestedCommand -AutoSize'

        { Test-HtmlBrowserRecipe -Path $recipePath -StrictPreflight -ThrowOnFailure } |
            Should -Throw -ExpectedMessage '*Recipe strict preflight failed*'
    }

    It 'uses selector alternates when recorded recipe selectors change' {
        $pagePath = Join-Path $TestDrive 'browser-recipe-selector-alternates.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head><title>Selector Alternates Recipe</title></head>
<body>
  <main>
    <label for="searchBox">Search mailbox</label>
    <input id="searchBox" name="q" placeholder="Search mailbox" />
    <button data-testid="load-results" onclick="document.getElementById('results').textContent = 'Found ' + document.getElementById('searchBox').value;">Load results</button>
    <section id="results">Waiting</section>
  </main>
</body>
</html>
'@
        $recipePath = Join-Path $TestDrive 'browser-selector-alternates.recipe.json'
        $recipe = [ordered]@{
            SchemaVersion = 1
            Name          = 'SelectorAlternatesRecipe'
            StartUrl      = [System.Uri]::new($pagePath).AbsoluteUri
            LoadState     = 'DomContentLoaded'
            Timeout       = 3000
            Steps         = @(
                [ordered]@{
                    Name               = 'Type search through fallback'
                    Action             = 'Input'
                    Selector           = '#old-search'
                    SelectorAlternates = @('#searchBox')
                    Value              = 'mailbox'
                },
                [ordered]@{
                    Name               = 'Click through fallback'
                    Action             = 'Click'
                    Selector           = '#old-load'
                    SelectorAlternates = @('[data-testid="load-results"]')
                },
                [ordered]@{
                    Name               = 'Wait through fallback'
                    Action             = 'WaitText'
                    Selector           = '#old-results'
                    SelectorAlternates = @('#results')
                    Text               = 'Found mailbox'
                    Exact              = $true
                }
            )
        }
        $recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8

        $result = Invoke-HtmlBrowserRecipe -Path $recipePath

        $result.Succeeded | Should -BeTrue
        $result.Steps[0].SelectedSelector | Should -Be '#searchBox'
        $result.Steps[0].AttemptedSelectors | Should -Be @('#old-search', '#searchBox')
        $result.Steps[1].SelectedSelector | Should -Be '[data-testid="load-results"]'
        $result.Steps[2].SelectedSelector | Should -Be '#results'
    }

    It 'tries selector alternates after a matching primary selector fails' {
        $pagePath = Join-Path $TestDrive 'browser-recipe-selector-action-fallback.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<body>
  <main>
    <button class="choice" onclick="document.getElementById('result').textContent = 'first'">Choose</button>
    <button id="second" class="choice" onclick="document.getElementById('result').textContent = 'second'">Choose</button>
    <section id="result">waiting</section>
  </main>
</body>
</html>
'@
        $recipePath = Join-Path $TestDrive 'browser-selector-action-fallback.recipe.json'
        $recipe = [ordered]@{
            SchemaVersion = 1
            Name          = 'SelectorActionFallback'
            StartUrl      = [System.Uri]::new($pagePath).AbsoluteUri
            LoadState     = 'DomContentLoaded'
            Timeout       = 3000
            Steps         = @(
                [ordered]@{
                    Name               = 'Click unique fallback'
                    Action             = 'Click'
                    Selector           = '.choice'
                    SelectorAlternates = @('#second')
                },
                [ordered]@{
                    Name     = 'Wait for fallback click'
                    Action   = 'WaitText'
                    Selector = '#result'
                    Text     = 'second'
                    Exact    = $true
                }
            )
        }
        $recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8

        $result = Invoke-HtmlBrowserRecipe -Path $recipePath

        $result.Succeeded | Should -BeTrue
        $result.Steps[0].AttemptedSelectors | Should -Be @('.choice', '#second')
        $result.Steps[0].SelectedSelector | Should -Be '#second'
    }

    It 'records artifact IO failures as recipe step failures' {
        $pagePath = Join-Path $TestDrive 'browser-recipe-artifact-io.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value '<!doctype html><main>artifact io ready</main>'
        $blockedOutFolder = Join-Path $TestDrive 'blocked-evidence-target'
        Set-Content -LiteralPath $blockedOutFolder -Encoding UTF8 -Value 'not a directory'
        $recipePath = Join-Path $TestDrive 'browser-artifact-io.recipe.json'
        $recipe = [ordered]@{
            SchemaVersion = 1
            Name          = 'ArtifactIoFailure'
            StartUrl      = [System.Uri]::new($pagePath).AbsoluteUri
            LoadState     = 'DomContentLoaded'
            Timeout       = 3000
            Steps         = @(
                [ordered]@{
                    Name            = 'Capture evidence into blocked path'
                    Action          = 'Evidence'
                    OutFolder       = $blockedOutFolder
                    ContinueOnError = $true
                },
                [ordered]@{
                    Name   = 'Continue after artifact failure'
                    Action = 'Script'
                    Script = "() => document.body.innerText.trim()"
                }
            )
        }
        $recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8

        $result = Invoke-HtmlBrowserRecipe -Path $recipePath

        $result.Succeeded | Should -BeFalse
        $result.Steps.Count | Should -Be 2
        $result.Steps[0].Succeeded | Should -BeFalse
        $result.Steps[0].ErrorType | Should -Match 'IOException|UnauthorizedAccessException'
        $result.Steps[1].Succeeded | Should -BeTrue
        $result.Steps[1].Output | Should -Be 'artifact io ready'
    }

    It 'preflights selector alternates and warns about sensitive fallback selectors' {
        $recipePath = Join-Path $TestDrive 'browser-selector-alternates-preflight.recipe.json'
        $recipe = [ordered]@{
            SchemaVersion = 1
            Name          = 'SelectorAlternatesPreflight'
            StartUrl      = 'https://example.org/app'
            LoadState     = 'DomContentLoaded'
            Timeout       = 3000
            Steps         = @(
                [ordered]@{
                    Name               = 'Click fallback only'
                    Action             = 'Click'
                    SelectorAlternates = @('[data-testid="continue"]')
                },
                [ordered]@{
                    Name               = 'Sensitive fallback'
                    Action             = 'Click'
                    Selector           = '#download'
                    SelectorAlternates = @("a[href='/download?token=super-secret-token']")
                }
            )
        }
        $recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8

        $validation = Test-HtmlBrowserRecipe -Path $recipePath

        $validation.IsValid | Should -BeTrue
        $validation.WarningCount | Should -BeGreaterOrEqual 1
        $validation.Issues.Message | Should -Contain 'Selector or selector alternate appears to contain sensitive values.'
        ($validation.Issues | Where-Object Property -eq 'Selector').SuggestedFix | Should -Not -Match 'super-secret-token'
        ($validation.Issues | Where-Object Property -eq 'Selector').SuggestedCommand | Should -Not -Match 'super-secret-token'
    }

    It 'creates recipe sessions from browser profiles and explicit launch overrides' {
        $pagePath = Join-Path $TestDrive 'browser-recipe-profile-session.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head><title>Recipe Profile Session</title></head>
<body><main id="status">profile recipe ready</main></body>
</html>
'@
        $profilePath = Join-Path $TestDrive 'recipe-profile.json'
        New-HtmlBrowserProfile -Name RecipeProfile -Path $profilePath -Scenario AuditProof -ViewportWidth 1111 -ViewportHeight 777 -LoadState DomContentLoaded | Out-Null

        $recipePath = Join-Path $TestDrive 'browser-profile.recipe.json'
        $recipe = [ordered]@{
            SchemaVersion = 1
            Name          = 'RecipeProfileLaunch'
            StartUrl      = [System.Uri]::new($pagePath).AbsoluteUri
            Timeout       = 3000
            Steps         = @(
                [ordered]@{
                    Name   = 'Read viewport'
                    Action = 'Script'
                    Script = "() => window.innerWidth + 'x' + window.innerHeight"
                }
            )
        }
        $recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8

        $result = Invoke-HtmlBrowserRecipe -Path $recipePath -ProfilePath $profilePath -ViewportWidth 1200

        $result.Succeeded | Should -BeTrue
        $result.CreatedSession | Should -BeTrue
        $result.Steps[0].Output | Should -Be '1200x777'
    }

    It 'rejects document resource blocking for recipe-created sessions' {
        $pagePath = Join-Path $TestDrive 'browser-recipe-document-block.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value '<!doctype html><main>blocked</main>'

        $recipePath = Join-Path $TestDrive 'browser-document-block.recipe.json'
        $recipe = [ordered]@{
            SchemaVersion = 1
            Name          = 'RecipeDocumentBlock'
            StartUrl      = [System.Uri]::new($pagePath).AbsoluteUri
            Steps         = @(
                [ordered]@{
                    Action = 'WaitText'
                    Text   = 'blocked'
                }
            )
        }
        $recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8

        { Invoke-HtmlBrowserRecipe -Path $recipePath -BlockResourceType Document } |
            Should -Throw -ExpectedMessage '*BlockResourceType Document would abort page navigation*'
    }

    It 'honors evidence artifact options in recipe steps' {
        $pagePath = Join-Path $TestDrive 'browser-recipe-evidence-options.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head><title>Recipe Evidence Options</title></head>
<body><main>Evidence option proof</main></body>
</html>
'@
        $evidencePath = Join-Path $TestDrive 'custom-evidence'
        $recipePath = Join-Path $TestDrive 'browser-evidence-options.recipe.json'
        $recipe = [ordered]@{
            SchemaVersion = 1
            Name          = 'EvidenceOptionsRecipe'
            StartUrl      = [System.Uri]::new($pagePath).AbsoluteUri
            LoadState     = 'DomContentLoaded'
            Timeout       = 3000
            Steps         = @(
                [ordered]@{
                    Action                 = 'Evidence'
                    OutFolder              = $evidencePath
                    BaseFileName           = 'custom-proof'
                    Screenshot             = $false
                    FullPageScreenshot     = $false
                    Pdf                    = $false
                    Html                   = $true
                    VisibleText            = $true
                    Markdown               = $false
                    NetworkSummary         = $false
                    Manifest               = $false
                    RedactSensitiveValues  = $true
                }
            )
        }
        $recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8

        $result = Invoke-HtmlBrowserRecipe -Path $recipePath

        $result.Succeeded | Should -BeTrue
        $result.Steps[0].Evidence.ManifestPath | Should -BeNullOrEmpty
        Test-Path -LiteralPath (Join-Path $evidencePath 'custom-proof.html') | Should -BeTrue
        Test-Path -LiteralPath (Join-Path $evidencePath 'custom-proof.txt') | Should -BeTrue
        Test-Path -LiteralPath (Join-Path $evidencePath 'custom-proof.png') | Should -BeFalse
        Test-Path -LiteralPath (Join-Path $evidencePath 'custom-proof.md') | Should -BeFalse
        Test-Path -LiteralPath (Join-Path $evidencePath 'network-summary.json') | Should -BeFalse
        Test-Path -LiteralPath (Join-Path $evidencePath 'evidence-manifest.json') | Should -BeFalse
    }

    It 'honors load state for click navigation recipe steps' {
        $startPath = Join-Path $TestDrive 'browser-recipe-click-start.html'
        $finishPath = Join-Path $TestDrive 'browser-recipe-click-finish.html'
        Set-Content -LiteralPath $startPath -Encoding UTF8 -Value @"
<!doctype html>
<html>
<body>
  <main>Start</main>
  <button id="continue" onclick="location.href='$([System.Uri]::new($finishPath).AbsoluteUri)'">Continue with SSO</button>
</body>
</html>
"@
        Set-Content -LiteralPath $finishPath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<body>
  <main id="status">recipe-click-ready</main>
  <script>fetch('https://example.invalid/recipe-never-ending').catch(() => {});</script>
</body>
</html>
'@
        $recipePath = Join-Path $TestDrive 'browser-click-navigation.recipe.json'
        $recipe = [ordered]@{
            SchemaVersion = 1
            Name          = 'ClickNavigationLoadStateRecipe'
            StartUrl      = [System.Uri]::new($startPath).AbsoluteUri
            LoadState     = 'DomContentLoaded'
            Timeout       = 3000
            Steps         = @(
                [ordered]@{
                    Name              = 'Continue'
                    Action            = 'Click'
                    Selector          = '#continue'
                    WaitForNavigation = $true
                    LoadState         = 'DomContentLoaded'
                    NavigationUrl     = "**/$([System.IO.Path]::GetFileName($finishPath))"
                },
                [ordered]@{
                    Name     = 'Wait for result'
                    Action   = 'WaitText'
                    Selector = '#status'
                    Text     = 'recipe-click-ready'
                    Exact    = $true
                }
            )
        }
        $recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8

        $result = Invoke-HtmlBrowserRecipe -Path $recipePath

        $result.Succeeded | Should -BeTrue
        $result.Steps.Count | Should -Be 2
        $result.Steps[0].Succeeded | Should -BeTrue
        $result.Steps[1].Succeeded | Should -BeTrue
    }

    It 'preflights ignored and sensitive click navigation URL patterns' {
        $recipePath = Join-Path $TestDrive 'browser-click-navigation-preflight.recipe.json'
        $recipe = [ordered]@{
            SchemaVersion = 1
            Name          = 'ClickNavigationPreflight'
            StartUrl      = 'https://example.org/start'
            LoadState     = 'DomContentLoaded'
            Timeout       = 3000
            Steps         = @(
                [ordered]@{
                    Action        = 'Click'
                    Selector      = '#continue'
                    NavigationUrl = '**/dashboard'
                },
                [ordered]@{
                    Action            = 'ClickText'
                    Text              = 'Continue'
                    WaitForNavigation = $true
                    NavigationUrl     = 'https://example.org/callback?access_token=super-secret-token'
                }
            )
        }
        $recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8

        $validation = Test-HtmlBrowserRecipe -Path $recipePath

        $validation.IsValid | Should -BeTrue
        $validation.WarningCount | Should -BeGreaterOrEqual 2
        $validation.Issues.Message | Should -Contain 'NavigationUrl is ignored unless WaitForNavigation is enabled.'
        $validation.Issues.Message | Should -Contain 'NavigationUrl appears to contain sensitive query parameter names.'
        ($validation.Issues | Where-Object Property -eq 'NavigationUrl').SuggestedFix | Should -Not -Match 'super-secret-token'
        ($validation.Issues | Where-Object Property -eq 'NavigationUrl').SuggestedCommand | Should -Not -Match 'super-secret-token'
    }

    It 'reports actionable diagnostics when a recipe step fails' {
        $pagePath = Join-Path $TestDrive 'browser-recipe-failure.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head><title>Recipe Failure Diagnostics</title></head>
<body><main><button id="actual">Actual button</button><a href="/download?token=recipe-secret-token">Download proof</a></main></body>
</html>
'@
        $failureRoot = Join-Path $TestDrive 'recipe-failure-evidence'
        $recipePath = Join-Path $TestDrive 'browser-failure.recipe.json'
        $recipe = [ordered]@{
            SchemaVersion = 1
            Name          = 'FailureDiagnosticsRecipe'
            StartUrl      = [System.Uri]::new($pagePath).AbsoluteUri
            LoadState     = 'DomContentLoaded'
            Timeout       = 500
            Steps         = @(
                [ordered]@{
                    Name     = 'Click missing button'
                    Action   = 'Click'
                    Selector = '#missing'
                    Timeout  = 200
                }
            )
        }
        $recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8

        $result = Invoke-HtmlBrowserRecipe -Path $recipePath -OnFailureEvidence -FailureEvidenceFolder $failureRoot
        $failed = $result.Steps[0]

        $result.Succeeded | Should -BeFalse
        $result.FailedStepIndex | Should -Be 0
        $result.FailedStepName | Should -Be 'Click missing button'
        $result.FailureSummary | Should -Match "Recipe failed at step 0"
        $result.FailureSummary | Should -Match "#missing"
        $result.SuggestedCommand | Should -Be "Test-HtmlBrowserElement -Session `$session -Selector '#missing' -Visible; Invoke-HtmlBrowserClick -Session `$session -Selector '#missing'"
        $failed.PageUrl | Should -Match 'browser-recipe-failure\.html'
        $failed.PageTitle | Should -Be 'Recipe Failure Diagnostics'
        $failed.SuggestedFix | Should -Match 'Find-HtmlBrowserLocator'
        $failed.SuggestedCommand | Should -Be $result.SuggestedCommand
        $failed.ErrorMessage | Should -Not -BeNullOrEmpty
        $failed.Evidence | Should -Not -BeNullOrEmpty
        $failed.Evidence.Purpose | Should -Be 'FailureEvidence'
        $failed.Evidence.Operation | Should -Be 'Click missing button'
        $failed.Evidence.LocatorSuggestionCount | Should -BeGreaterThan 0
        $failed.Evidence.Artifacts.Kind | Should -Contain 'LocatorSuggestions'
        Test-Path -LiteralPath $failed.Evidence.ManifestPath | Should -BeTrue
        $failureFolders = @(Get-ChildItem -LiteralPath $failureRoot -Directory)
        $failureFolders.Count | Should -Be 1
        $locatorJsonPath = Join-Path $failureFolders[0].FullName 'locator-suggestions.json'
        Test-Path -LiteralPath $locatorJsonPath | Should -BeTrue
        $locators = Get-Content -LiteralPath $locatorJsonPath -Raw | ConvertFrom-Json
        $locatorJson = Get-Content -LiteralPath $locatorJsonPath -Raw
        $locatorJson | Should -Not -Match 'recipe-secret-token'
        ($locators.Candidates.Selector -join "`n") | Should -Match 'token=<redacted>'
    }

    It 'keeps recipe failures best-effort when failure evidence cannot be written' {
        $pagePath = Join-Path $TestDrive 'browser-recipe-unwritable-evidence.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value '<!doctype html><main>Ready</main>'
        $blockedEvidencePath = Join-Path $TestDrive 'blocked-evidence-path'
        Set-Content -LiteralPath $blockedEvidencePath -Encoding UTF8 -Value 'not a directory'
        $recipePath = Join-Path $TestDrive 'browser-unwritable-evidence.recipe.json'
        $recipe = [ordered]@{
            SchemaVersion = 1
            Name          = 'UnwritableEvidenceRecipe'
            StartUrl      = [System.Uri]::new($pagePath).AbsoluteUri
            LoadState     = 'DomContentLoaded'
            Timeout       = 500
            Steps         = @(
                [ordered]@{
                    Name     = 'Click missing button'
                    Action   = 'Click'
                    Selector = '#missing'
                    Timeout  = 200
                }
            )
        }
        $recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8

        $result = Invoke-HtmlBrowserRecipe -Path $recipePath -OnFailureEvidence -FailureEvidenceFolder $blockedEvidencePath

        $result.Succeeded | Should -BeFalse
        $result.FailedStepIndex | Should -Be 0
        $result.Steps[0].ErrorMessage | Should -Not -BeNullOrEmpty
        $result.Steps[0].Evidence.Purpose | Should -Be 'FailureEvidence'
        $result.Steps[0].Evidence.Operation | Should -Be 'Click missing button'
        $result.Steps[0].Evidence.ErrorMessage | Should -Not -BeNullOrEmpty
    }

    It 'does not leak sensitive selector values in recipe failure commands' {
        $pagePath = Join-Path $TestDrive 'browser-recipe-sensitive-failure.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head><title>Recipe Sensitive Failure</title></head>
<body><main>Ready</main></body>
</html>
'@
        $recipePath = Join-Path $TestDrive 'browser-sensitive-failure.recipe.json'
        $recipe = [ordered]@{
            SchemaVersion = 1
            Name          = 'SensitiveFailureRecipe'
            StartUrl      = [System.Uri]::new($pagePath).AbsoluteUri
            LoadState     = 'DomContentLoaded'
            Timeout       = 500
            Steps         = @(
                [ordered]@{
                    Name     = 'Click sensitive download'
                    Action   = 'Click'
                    Selector = "a[href='/download?token=super-secret-token']"
                    Timeout  = 200
                }
            )
        }
        $recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8

        $result = Invoke-HtmlBrowserRecipe -Path $recipePath

        $result.Succeeded | Should -BeFalse
        $result.SuggestedCommand | Should -Not -Match 'super-secret-token'
        $result.Steps[0].SuggestedCommand | Should -Not -Match 'super-secret-token'
        $result.Steps[0].SuggestedCommand | Should -Match 'Format-List'
    }

    It 'redacts sensitive final URLs from recipe results' {
        $pagePath = Join-Path $TestDrive 'browser-recipe-final-url.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value '<!doctype html><main>callback ready</main>'
        $recipePath = Join-Path $TestDrive 'browser-final-url.recipe.json'
        $pageUri = [System.Uri]::new($pagePath).AbsoluteUri
        $recipe = [ordered]@{
            SchemaVersion = 1
            Name          = 'FinalUrlRedaction'
            StartUrl      = "${pageUri}#code=start-secret&state=start-state"
            LoadState     = 'DomContentLoaded'
            Timeout       = 3000
            Steps         = @(
                [ordered]@{
                    Name      = 'Move to tokenized target'
                    Action    = 'Navigate'
                    Url       = "${pageUri}#access_token=target-secret-token"
                    LoadState = 'DomContentLoaded'
                },
                [ordered]@{
                    Name   = 'Move to callback'
                    Action = 'Script'
                    Script = "() => { location.hash = '/callback?code=secret-code&state=secret-state'; return location.href; }"
                }
            )
        }
        $recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8

        $result = Invoke-HtmlBrowserRecipe -Path $recipePath

        $result.Succeeded | Should -BeTrue
        $result.StartUrl | Should -Match 'code=<redacted>'
        $result.StartUrl | Should -Match 'state=<redacted>'
        $result.StartUrl | Should -Not -Match 'start-secret|start-state'
        $result.Steps[0].Target | Should -Match 'access_token=<redacted>'
        $result.Steps[0].Target | Should -Not -Match 'target-secret-token'
        $result.FinalUrl | Should -Match 'code=<redacted>'
        $result.FinalUrl | Should -Match 'state=<redacted>'
        $result.FinalUrl | Should -Not -Match 'secret-code|secret-state|target-secret-token'
    }

    It 'honors timeouts for script recipe steps' {
        $pagePath = Join-Path $TestDrive 'browser-recipe-script-timeout.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value '<!doctype html><main>script timeout ready</main>'
        $recipePath = Join-Path $TestDrive 'browser-script-timeout.recipe.json'
        $recipe = [ordered]@{
            SchemaVersion = 1
            Name          = 'ScriptTimeout'
            StartUrl      = [System.Uri]::new($pagePath).AbsoluteUri
            LoadState     = 'DomContentLoaded'
            Timeout       = 100
            Steps         = @(
                [ordered]@{
                    Name   = 'Never resolves'
                    Action = 'Script'
                    Script = "() => new Promise(() => {})"
                }
            )
        }
        $recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8

        $result = Invoke-HtmlBrowserRecipe -Path $recipePath -SkipPreflight

        $result.Succeeded | Should -BeFalse
        $result.Steps[0].Succeeded | Should -BeFalse
        $result.Steps[0].ErrorType | Should -Match 'TimeoutException'
        $result.Steps[0].ErrorMessage | Should -Match 'Timed out after 100 ms executing recipe script step'
    }
}
