Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'Browser recipes' {
    It 'exports browser recipe command' {
        (Get-Command Invoke-HtmlBrowserRecipe).Name | Should -Be 'Invoke-HtmlBrowserRecipe'
        Get-Command Export-HtmlBrowserRecipe | Should -Not -BeNullOrEmpty
        Get-Command Test-HtmlBrowserRecipe | Should -Not -BeNullOrEmpty
        Get-Command Start-HtmlBrowserRecipeRecording | Should -Not -BeNullOrEmpty
        Get-Command Stop-HtmlBrowserRecipeRecording | Should -Not -BeNullOrEmpty
        (Get-Command Invoke-HtmlBrowserRecipe).Parameters.Keys | Should -Contain 'OnFailureEvidence'
        (Get-Command Invoke-HtmlBrowserRecipe).Parameters.Keys | Should -Contain 'FailureEvidenceFolder'
        (Get-Command Invoke-HtmlBrowserRecipe).Parameters.Keys | Should -Contain 'ProfilePath'
        (Get-Command Invoke-HtmlBrowserRecipe).Parameters.Keys | Should -Contain 'UserDataDirectory'
        (Get-Command Invoke-HtmlBrowserRecipe).Parameters.Keys | Should -Contain 'StatePath'
        (Get-Command Invoke-HtmlBrowserRecipe).Parameters.Keys | Should -Contain 'Scenario'
        (Get-Command Invoke-HtmlBrowserRecipe).Parameters.Keys | Should -Contain 'Visible'
        (Get-Command Invoke-HtmlBrowserRecipe).Parameters.Keys | Should -Contain 'ManualLogin'
        (Get-Command Invoke-HtmlBrowserRecipe).Parameters.Keys | Should -Contain 'PreventSsoAutoSubmit'
        (Get-Command Invoke-HtmlBrowserRecipe).Parameters.Keys | Should -Contain 'BlockResourceType'
        (Get-Command Invoke-HtmlBrowserRecipe).Parameters.Keys | Should -Contain 'BlockResourcePattern'
        (Get-Command Invoke-HtmlBrowserRecipe).Parameters.Keys | Should -Contain 'VariablePath'
        (Get-Command Invoke-HtmlBrowserRecipe).Parameters.Keys | Should -Contain 'SkipPreflight'
        (Get-Command Invoke-HtmlBrowserRecipe).Parameters.Keys | Should -Contain 'StrictPreflight'
        (Get-Command Test-HtmlBrowserRecipe).Parameters.Keys | Should -Contain 'VariablePath'
        (Get-Command Test-HtmlBrowserRecipe).Parameters.Keys | Should -Contain 'StrictPreflight'
        (Get-Command Test-HtmlBrowserRecipe).Parameters.Keys | Should -Contain 'ThrowOnFailure'
        (Get-Command Export-HtmlBrowserRecipe).Parameters.Keys | Should -Contain 'VariableTemplatePath'
        (Get-Command Export-HtmlBrowserRecipe).Parameters.Keys | Should -Contain 'IncludeOptionalVariables'
    }

    It 'preflights valid browser recipes without launching a browser' {
        $recipePath = Join-Path $TestDrive 'valid-preflight.recipe.json'
        $recipe = [ordered]@{
            SchemaVersion = 1
            Name          = 'ValidPreflight'
            StartUrl      = 'https://example.org/app'
            LoadState     = 'DomContentLoaded'
            Timeout       = 3000
            Steps         = @(
                [ordered]@{
                    Name     = 'Type secret'
                    Action   = 'Input'
                    Selector = '#password'
                    ValueRedacted = $true
                    ValueVariable = 'password'
                },
                [ordered]@{
                    Name     = 'Find submit'
                    Action   = 'Locator'
                    Text     = 'Submit'
                    Limit    = 3
                }
            )
        }
        $recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8

        $validation = Test-HtmlBrowserRecipe -Path $recipePath -Variable @{ password = 'runtime-secret' }

        $validation.IsValid | Should -BeTrue
        $validation.Passed | Should -BeTrue
        $validation.ErrorCount | Should -Be 0
        $validation.BlockingIssueCount | Should -Be 0
        $validation.RecommendedExitCode | Should -Be 0
        $validation.Summary | Should -Match 'Recipe preflight passed'
        $validation.StepCount | Should -Be 2
        $validation.RequiredVariables | Should -Be @('password')
        $validation.MissingVariables.Count | Should -Be 0
        $validation.RequiredVariableCount | Should -Be 1
        $validation.MissingVariableCount | Should -Be 0
        $validation.VariableTemplate['password'] | Should -Be '<secret>'
        $validation.Variables[0].Sensitive | Should -BeTrue
        $validation.Variables[0].Supplied | Should -BeTrue
        $validation.Variables[0].StepIndexes | Should -Be @(0)
        $validation.Variables[0].Actions | Should -Contain 'Input'
        $validation.SuggestedCommand | Should -Be "Invoke-HtmlBrowserRecipe -Path '<recipe.json>'"
    }

    It 'preflights recipe errors, warnings, and missing runtime variables' {
        $recipePath = Join-Path $TestDrive 'invalid-preflight.recipe.json'
        $recipe = [ordered]@{
            SchemaVersion = 1
            Name          = 'InvalidPreflight'
            Timeout       = 100
            Steps         = @(
                [ordered]@{
                    Name     = 'Missing selector'
                    Action   = 'Click'
                    Timeout  = 200
                },
                [ordered]@{
                    Name          = 'Redacted input'
                    Action        = 'Input'
                    Selector      = '#password'
                    ValueRedacted = $true
                    ValueVariable = 'password'
                },
                [ordered]@{
                    Name         = 'Sensitive selector'
                    Action       = 'Click'
                    Selector     = "a[href='/download?token=super-secret-token']"
                    ContinueOnError = $true
                },
                [ordered]@{
                    Name         = 'Long fixed wait'
                    Action       = 'WaitMilliseconds'
                    Milliseconds = 15000
                }
            )
        }
        $recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8

        $validation = Test-HtmlBrowserRecipe -Path $recipePath

        $validation.IsValid | Should -BeFalse
        $validation.Passed | Should -BeFalse
        $validation.ErrorCount | Should -BeGreaterOrEqual 3
        $validation.WarningCount | Should -BeGreaterOrEqual 3
        $validation.BlockingIssueCount | Should -Be $validation.ErrorCount
        $validation.BlockingIssues.Severity | Should -Not -Contain 'Warning'
        $validation.RecommendedExitCode | Should -Be 1
        $validation.Summary | Should -Match 'blocking issue'
        $validation.RequiredVariables | Should -Be @('password')
        $validation.MissingVariables | Should -Be @('password')
        $validation.VariableTemplate['password'] | Should -Be '<secret>'
        $validation.SuggestedCommand | Should -Be '$validation.BlockingIssues | Format-Table Severity,StepIndex,Action,Property,Message,SuggestedFix,SuggestedCommand -AutoSize'
        $validation.Issues.Message | Should -Contain 'Recipe StartUrl is required unless an existing browser session will be supplied.'
        $validation.Issues.Message | Should -Contain 'Selector or SelectorAlternates is required for Click steps.'
        $validation.Issues.Message | Should -Contain "Runtime variable 'password' was not supplied."
        $missingSelector = $validation.Issues | Where-Object Message -eq 'Selector or SelectorAlternates is required for Click steps.' | Select-Object -First 1
        $missingSelector.SuggestedCommand | Should -Be "Find-HtmlBrowserLocator -Session `$session -Query 'Missing selector' -Limit 10"
        $missingSelector.DocumentationHint | Should -Match 'Find-HtmlBrowserLocator'
        $missingVariable = $validation.Issues | Where-Object Message -eq "Runtime variable 'password' was not supplied." | Select-Object -First 1
        $missingVariable.SuggestedCommand | Should -Be "Invoke-HtmlBrowserRecipe -Path '<recipe.json>' -Variable @{ password = '<value>' }"
        $missingVariable.DocumentationHint | Should -Match 'variable templates'
        $sensitiveSelector = $validation.Issues | Where-Object { $_.Property -eq 'Selector' -and $_.Message -eq 'Selector or selector alternate appears to contain sensitive values.' } | Select-Object -First 1
        $sensitiveSelector.SuggestedFix | Should -Not -Match 'super-secret-token'
        $sensitiveSelector.SuggestedCommand | Should -Not -Match 'super-secret-token'
        $sensitiveSelector.SuggestedCommand | Should -Be 'Get-HtmlBrowserInteractable -Session $session | Select-Object -First 20'

        $sessionValidation = Test-HtmlBrowserRecipe -Path $recipePath -AssumeSession -Variable @{ password = 'runtime-secret' }
        $sessionValidation.Issues.Message | Should -Not -Contain 'Recipe StartUrl is required unless an existing browser session will be supplied.'
        $sessionValidation.Issues.Message | Should -Not -Contain "Runtime variable 'password' was not supplied."
        $sessionValidation.MissingVariables.Count | Should -Be 0
        $sessionValidation.Variables[0].Supplied | Should -BeTrue
    }

    It 'preflights optional recipe variables without treating stored fallback values as missing secrets' {
        $recipePath = Join-Path $TestDrive 'optional-variable-preflight.recipe.json'
        $recipe = [ordered]@{
            SchemaVersion = 1
            Name          = 'OptionalVariablePreflight'
            StartUrl      = 'https://example.org/app'
            LoadState     = 'DomContentLoaded'
            Timeout       = 3000
            Steps         = @(
                [ordered]@{
                    Name          = 'Type query'
                    Action        = 'Input'
                    Selector      = '#search'
                    Value         = 'mailbox'
                    ValueVariable = 'query'
                },
                [ordered]@{
                    Name          = 'Choose scope'
                    Action        = 'SelectOption'
                    Selector      = '#scope'
                    ValueVariable = 'scope'
                }
            )
        }
        $recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8

        $validation = Test-HtmlBrowserRecipe -Path $recipePath

        $validation.IsValid | Should -BeFalse
        $validation.RequiredVariables | Should -Be @('scope')
        $validation.MissingVariables | Should -Be @('scope')
        $validation.VariableTemplate['scope'] | Should -Be '<value>'
        ($validation.Variables | Where-Object Name -eq 'query').Required | Should -BeFalse
        ($validation.Variables | Where-Object Name -eq 'query').Supplied | Should -BeFalse
        ($validation.Variables | Where-Object Name -eq 'scope').Required | Should -BeTrue
        ($validation.Issues | Where-Object Message -eq "Runtime variable 'query' was not supplied.").Count | Should -Be 0
    }

    It 'exports variable templates and validates from filled variable files' {
        $recipePath = Join-Path $TestDrive 'variable-template-source.recipe.json'
        $exportedRecipePath = Join-Path $TestDrive 'variable-template-exported.recipe.json'
        $templatePath = Join-Path $TestDrive 'browser.recipe.variables.json'
        $optionalTemplatePath = Join-Path $TestDrive 'browser.recipe.all-variables.json'
        $recipe = [ordered]@{
            SchemaVersion = 1
            Name          = 'VariableTemplate'
            StartUrl      = 'https://example.org/app'
            LoadState     = 'DomContentLoaded'
            Timeout       = 3000
            Steps         = @(
                [ordered]@{
                    Name          = 'Type secret'
                    Action        = 'Input'
                    Selector      = '#password'
                    ValueRedacted = $true
                    ValueVariable = 'password'
                },
                [ordered]@{
                    Name          = 'Type query'
                    Action        = 'Input'
                    Selector      = '#search'
                    Value         = 'mailbox'
                    ValueVariable = 'query'
                }
            )
        }
        $recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8
        $typedRecipe = [HtmlTinkerX.HtmlBrowser]::DeserializeRecipe((Get-Content -LiteralPath $recipePath -Raw))

        Export-HtmlBrowserRecipe -Recipe $typedRecipe -Path $exportedRecipePath -VariableTemplatePath $templatePath | Out-Null

        $template = Get-Content -LiteralPath $templatePath -Raw | ConvertFrom-Json
        $template.password | Should -Be '<secret>'
        $template.PSObject.Properties.Name | Should -Not -Contain 'query'
        $placeholderValidation = Test-HtmlBrowserRecipe -Path $exportedRecipePath -VariablePath $templatePath
        $placeholderValidation.MissingVariables | Should -Be @('password')

        @{ password = 'runtime-secret' } | ConvertTo-Json | Set-Content -LiteralPath $templatePath -Encoding UTF8
        $filledValidation = Test-HtmlBrowserRecipe -Path $exportedRecipePath -VariablePath $templatePath
        $filledValidation.IsValid | Should -BeTrue
        $filledValidation.MissingVariableCount | Should -Be 0
        $filledValidation.Variables[0].Supplied | Should -BeTrue

        Export-HtmlBrowserRecipe -Recipe $typedRecipe -Path $exportedRecipePath -VariableTemplatePath $optionalTemplatePath -IncludeOptionalVariables | Out-Null
        $optionalTemplate = Get-Content -LiteralPath $optionalTemplatePath -Raw | ConvertFrom-Json
        $optionalTemplate.password | Should -Be '<secret>'
        $optionalTemplate.query | Should -Be '<value>'
    }

    It 'runs a replayable browser recipe from JSON' {
        $pagePath = Join-Path $TestDrive 'browser-recipe-page.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head><title>Browser Recipe Test</title></head>
<body>
  <main>
    <label for="search">Search mailbox</label>
    <input id="search" name="q" placeholder="Search mailbox" />
    <button data-testid="load-results" onclick="document.getElementById('results').textContent = 'Found mailbox proof';">Load results</button>
    <section id="results">Waiting</section>
  </main>
</body>
</html>
'@
        $evidencePath = Join-Path $TestDrive 'recipe-evidence'
        $recipePath = Join-Path $TestDrive 'browser.recipe.json'
        $recipe = [ordered]@{
            SchemaVersion = 1
            Name          = 'MailboxProofRecipe'
            StartUrl      = [System.Uri]::new($pagePath).AbsoluteUri
            LoadState     = 'DomContentLoaded'
            Timeout       = 3000
            Steps         = @(
                [ordered]@{
                    Name        = 'Wait for search'
                    Action      = 'WaitReady'
                    NoLoadState = $true
                    Selector    = '#search'
                },
                [ordered]@{
                    Name     = 'Type search'
                    Action   = 'Input'
                    Selector = '#search'
                    Value    = 'mailbox'
                },
                [ordered]@{
                    Name     = 'Load results'
                    Action   = 'Click'
                    Selector = '[data-testid="load-results"]'
                },
                [ordered]@{
                    Name     = 'Wait for proof'
                    Action   = 'WaitText'
                    Selector = '#results'
                    Text     = 'Found mailbox proof'
                },
                [ordered]@{
                    Name  = 'Find search locator'
                    Action = 'Locator'
                    Text  = 'Search mailbox'
                    Limit = 5
                },
                [ordered]@{
                    Name         = 'Export evidence'
                    Action       = 'Evidence'
                    OutFolder    = $evidencePath
                    BaseFileName = 'recipe-proof'
                }
            )
        }
        $recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8

        $result = Invoke-HtmlBrowserRecipe -Path $recipePath

        $result.Succeeded | Should -BeTrue
        $result.CreatedSession | Should -BeTrue
        $result.Steps.Count | Should -Be 6
        $result.Steps[4].LocatorCandidates.Count | Should -BeGreaterThan 0
        $result.Steps[5].Evidence.ManifestPath | Should -Not -BeNullOrEmpty
        Test-Path -LiteralPath (Join-Path $evidencePath 'evidence-manifest.json') | Should -BeTrue
        Test-Path -LiteralPath (Join-Path $evidencePath 'recipe-proof.html') | Should -BeTrue
    }

    It 'runs a recipe with runtime variables from a variable file and ignores template placeholders' {
        $pagePath = Join-Path $TestDrive 'browser-recipe-variable-file.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head><title>Variable File Recipe</title></head>
<body>
  <main>
    <input id="password" type="password" />
  </main>
</body>
</html>
'@
        $recipePath = Join-Path $TestDrive 'browser-variable-file.recipe.json'
        $variablePath = Join-Path $TestDrive 'browser-variable-file.variables.json'
        $recipe = [ordered]@{
            SchemaVersion = 1
            Name          = 'VariableFileRecipe'
            StartUrl      = [System.Uri]::new($pagePath).AbsoluteUri
            LoadState     = 'DomContentLoaded'
            Timeout       = 3000
            Steps         = @(
                [ordered]@{
                    Name          = 'Type secret'
                    Action        = 'Input'
                    Selector      = '#password'
                    ValueRedacted = $true
                    ValueVariable = 'password'
                },
                [ordered]@{
                    Name   = 'Read secret'
                    Action = 'Script'
                    Script = "() => document.querySelector('#password').value"
                }
            )
        }
        $recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8

        @{ password = '<secret>' } | ConvertTo-Json | Set-Content -LiteralPath $variablePath -Encoding UTF8
        $placeholderResult = Invoke-HtmlBrowserRecipe -Path $recipePath -VariablePath $variablePath
        $placeholderResult.Succeeded | Should -BeFalse
        $placeholderResult.SkippedBeforeExecution | Should -BeTrue
        $placeholderResult.CreatedSession | Should -BeFalse
        $placeholderResult.PreflightFailed | Should -BeTrue
        $placeholderResult.Validation.MissingVariables | Should -Be @('password')
        $placeholderResult.Steps.Count | Should -Be 0

        @{ password = 'runtime-secret' } | ConvertTo-Json | Set-Content -LiteralPath $variablePath -Encoding UTF8
        $result = Invoke-HtmlBrowserRecipe -Path $recipePath -VariablePath $variablePath

        $result.Succeeded | Should -BeTrue
        $result.SkippedBeforeExecution | Should -BeFalse
        $result.PreflightFailed | Should -BeFalse
        $result.Validation.IsValid | Should -BeTrue
        $result.Steps[1].Output | Should -Be 'runtime-secret'
    }

    It 'skips browser launch when preflight validation fails unless explicitly bypassed' {
        $pagePath = Join-Path $TestDrive 'browser-preflight-bypass.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value '<!doctype html><main><input id="password" type="password" /></main>'
        $recipePath = Join-Path $TestDrive 'browser-preflight-skip.recipe.json'
        $recipe = [ordered]@{
            SchemaVersion = 1
            Name          = 'PreflightSkip'
            StartUrl      = [System.Uri]::new($pagePath).AbsoluteUri
            LoadState     = 'DomContentLoaded'
            Timeout       = 3000
            Steps         = @(
                [ordered]@{
                    Name          = 'Type secret'
                    Action        = 'Input'
                    Selector      = '#password'
                    ValueRedacted = $true
                    ValueVariable = 'password'
                }
            )
        }
        $recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8

        $result = Invoke-HtmlBrowserRecipe -Path $recipePath

        $result.Succeeded | Should -BeFalse
        $result.CreatedSession | Should -BeFalse
        $result.SkippedBeforeExecution | Should -BeTrue
        $result.PreflightFailed | Should -BeTrue
        $result.Validation.ErrorCount | Should -Be 1
        $result.Validation.MissingVariables | Should -Be @('password')
        $result.FailureSummary | Should -Match 'Recipe preflight failed'
        $result.SuggestedCommand | Should -Be '$validation.BlockingIssues | Format-Table Severity,StepIndex,Action,Property,Message,SuggestedFix,SuggestedCommand -AutoSize'
        $result.Steps.Count | Should -Be 0

        $bypassed = Invoke-HtmlBrowserRecipe -Path $recipePath -SkipPreflight
        $bypassed.Succeeded | Should -BeFalse
        $bypassed.SkippedBeforeExecution | Should -BeFalse
        $bypassed.Validation | Should -BeNullOrEmpty
        $bypassed.Steps[0].ErrorMessage | Should -Match "runtime variable 'password'"
    }

    It 'can treat warning-only preflight issues as blocking in strict mode' {
        $pagePath = Join-Path $TestDrive 'browser-strict-preflight.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value '<!doctype html><main>strict preflight ready</main>'
        $recipePath = Join-Path $TestDrive 'browser-strict-preflight.recipe.json'
        $recipe = [ordered]@{
            SchemaVersion = 1
            Name          = 'StrictPreflight'
            StartUrl      = [System.Uri]::new($pagePath).AbsoluteUri
            LoadState     = 'DomContentLoaded'
            Timeout       = 3000
            Steps         = @(
                [ordered]@{
                    Name            = 'Optional cleanup'
                    Action          = 'WaitMilliseconds'
                    Milliseconds    = 1
                    ContinueOnError = $true
                },
                [ordered]@{
                    Name   = 'Read body'
                    Action = 'Script'
                    Script = "() => document.body.innerText.trim()"
                }
            )
        }
        $recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8

        $normal = Invoke-HtmlBrowserRecipe -Path $recipePath

        $normal.Succeeded | Should -BeTrue
        $normal.SkippedBeforeExecution | Should -BeFalse
        $normal.Validation.IsValid | Should -BeTrue
        $normal.Validation.WarningCount | Should -Be 1
        $normal.StrictPreflight | Should -BeFalse

        $strict = Invoke-HtmlBrowserRecipe -Path $recipePath -StrictPreflight

        $strict.Succeeded | Should -BeFalse
        $strict.CreatedSession | Should -BeFalse
        $strict.SkippedBeforeExecution | Should -BeTrue
        $strict.PreflightFailed | Should -BeTrue
        $strict.StrictPreflight | Should -BeTrue
        $strict.Validation.IsValid | Should -BeTrue
        $strict.Validation.WarningCount | Should -Be 1
        $strict.FailureSummary | Should -Match 'Strict recipe preflight blocked replay'
        $strict.FailedStepIndex | Should -Be 0
        $strict.Steps.Count | Should -Be 0
    }

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
}
