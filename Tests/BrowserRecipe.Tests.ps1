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

    It 'requires a login completion selector before manual-login recipe replay' {
        $recipe = [HtmlTinkerX.HtmlBrowserRecipe]::new()
        $recipe.Name = 'Manual Login Replay'
        $recipe.StartUrl = 'https://portal.example/login'

        $launchOptions = [HtmlTinkerX.HtmlBrowserLaunchOptions]::new()
        $launchOptions.ManualLogin = $true
        $launchOptions.Headless = $false
        $runOptions = [HtmlTinkerX.HtmlBrowserRecipeRunOptions]::new()
        $runOptions.LaunchOptions = $launchOptions

        {
            [HtmlTinkerX.HtmlBrowser]::ExecuteRecipeAsync(
                $recipe,
                $null,
                $runOptions,
                [System.Threading.CancellationToken]::None).GetAwaiter().GetResult()
        } | Should -Throw -ExpectedMessage '*Manual login recipe replay requires LoginSuccessSelector*'
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
                },
                [ordered]@{
                    Name          = 'Choose stored scope'
                    Action        = 'SelectOption'
                    Selector      = '#storedScope'
                    Values        = @('archive')
                    ValueVariable = 'storedScope'
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
        ($validation.Variables | Where-Object Name -eq 'storedScope').Required | Should -BeFalse
        ($validation.Issues | Where-Object Message -eq "Runtime variable 'query' was not supplied.").Count | Should -Be 0
        ($validation.Issues | Where-Object Message -eq "Runtime variable 'storedScope' was not supplied.").Count | Should -Be 0
    }

    It 'requires runtime variables for redacted select option steps even when redacted placeholders are stored' {
        $recipePath = Join-Path $TestDrive 'redacted-select-preflight.recipe.json'
        $recipe = [ordered]@{
            SchemaVersion = 1
            Name          = 'RedactedSelectPreflight'
            StartUrl      = 'https://example.org/app'
            LoadState     = 'DomContentLoaded'
            Timeout       = 3000
            Steps         = @(
                [ordered]@{
                    Name          = 'Choose confidential scope'
                    Action        = 'SelectOption'
                    Selector      = '#scope'
                    Values        = @('<redacted>')
                    ValueRedacted = $true
                    ValueVariable = 'scope'
                }
            )
        }
        $recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8

        $validation = Test-HtmlBrowserRecipe -Path $recipePath

        $validation.IsValid | Should -BeFalse
        $validation.RequiredVariables | Should -Be @('scope')
        $validation.MissingVariables | Should -Be @('scope')
        ($validation.Issues | Where-Object Property -eq 'ValueVariable').Message | Should -Contain "Runtime variable 'scope' was not supplied."

        $supplied = Test-HtmlBrowserRecipe -Path $recipePath -Variable @{ scope = 'openid' }
        $supplied.IsValid | Should -BeTrue
        $supplied.MissingVariables.Count | Should -Be 0
    }

    It 'requires input runtime variables when no fallback value is stored' {
        $pagePath = Join-Path $TestDrive 'missing-input-variable.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<body>
  <main><input id="search" /></main>
</body>
</html>
'@
        $recipePath = Join-Path $TestDrive 'missing-input-variable.recipe.json'
        $recipe = [ordered]@{
            SchemaVersion = 1
            Name          = 'MissingInputVariable'
            StartUrl      = [System.Uri]::new($pagePath).AbsoluteUri
            LoadState     = 'DomContentLoaded'
            Timeout       = 3000
            Steps         = @(
                [ordered]@{
                    Name          = 'Type query'
                    Action        = 'Input'
                    Selector      = '#search'
                    ValueVariable = 'query'
                }
            )
        }
        $recipe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recipePath -Encoding UTF8

        $validation = Test-HtmlBrowserRecipe -Path $recipePath

        $validation.IsValid | Should -BeFalse
        $validation.RequiredVariables | Should -Be @('query')
        $validation.MissingVariables | Should -Be @('query')
        $validation.Issues.Message | Should -Contain "Runtime variable 'query' was not supplied."

        $preflightResult = Invoke-HtmlBrowserRecipe -Path $recipePath
        $preflightResult.Succeeded | Should -BeFalse
        $preflightResult.SkippedBeforeExecution | Should -BeTrue
        $preflightResult.Steps.Count | Should -Be 0

        $runtimeResult = Invoke-HtmlBrowserRecipe -Path $recipePath -SkipPreflight
        $runtimeResult.Succeeded | Should -BeFalse
        $runtimeResult.Steps.Count | Should -Be 1
        $runtimeResult.Steps[0].Succeeded | Should -BeFalse
        $runtimeResult.Steps[0].ErrorMessage | Should -Match 'no fallback value is stored'
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
        $pageUri = [System.Uri]::new($pagePath).AbsoluteUri
        $recipePath = Join-Path $TestDrive 'browser-preflight-skip.recipe.json'
        $recipe = [ordered]@{
            SchemaVersion = 1
            Name          = 'PreflightSkip'
            StartUrl      = "${pageUri}#code=preflight-code&state=preflight-state"
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
        $result.StartUrl | Should -Match 'code=<redacted>'
        $result.StartUrl | Should -Match 'state=<redacted>'
        $result.StartUrl | Should -Not -Match 'preflight-code|preflight-state'
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
}
