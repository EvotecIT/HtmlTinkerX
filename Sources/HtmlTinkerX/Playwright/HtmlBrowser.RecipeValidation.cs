using System;
using System.Collections.Generic;
using System.Linq;

namespace HtmlTinkerX;

/// <summary>
/// Browser recipe preflight validation helpers.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Validates a browser recipe before replay so structural issues can be fixed without launching a browser.
    /// </summary>
    /// <param name="recipe">Recipe to validate.</param>
    /// <param name="runtimeVariables">Runtime variables that will be supplied during replay.</param>
    /// <param name="assumeExistingSession">Treat a missing StartUrl as valid because the caller will supply an existing browser session.</param>
    /// <param name="treatWarningsAsErrors">Treat warnings as blocking issues for CI and scheduled replay preflight.</param>
    /// <returns>Validation result with errors, warnings, and suggested next command.</returns>
    public static HtmlBrowserRecipeValidationResult ValidateRecipe(
        HtmlBrowserRecipe recipe,
        IEnumerable<string>? runtimeVariables = null,
        bool assumeExistingSession = false,
        bool treatWarningsAsErrors = false) {
        if (recipe == null) {
            throw new ArgumentNullException(nameof(recipe));
        }

        HashSet<string> variables = new(runtimeVariables ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        HtmlBrowserRecipeValidationResult result = new() {
            Name = recipe.Name,
            StrictPreflight = treatWarningsAsErrors,
            StepCount = recipe.Steps.Count
        };
        result.Variables = GetRecipeVariableRequirements(recipe, variables).ToList();

        if (recipe.SchemaVersion <= 0) {
            AddIssue(result, HtmlBrowserRecipeValidationSeverity.Error, null, null, null, nameof(recipe.SchemaVersion), "Recipe SchemaVersion must be greater than zero.", "Set SchemaVersion to 1.");
        }

        if (recipe.Timeout < 0) {
            AddIssue(result, HtmlBrowserRecipeValidationSeverity.Error, null, null, null, nameof(recipe.Timeout), "Recipe Timeout must be zero or greater.", "Set Timeout to 0 for no timeout or a positive millisecond value.");
        } else if (recipe.Timeout > 0 && recipe.Timeout < 250) {
            AddIssue(result, HtmlBrowserRecipeValidationSeverity.Warning, null, null, null, nameof(recipe.Timeout), "Recipe Timeout is very low and may be unreliable on real pages.", "Use a more realistic timeout such as 5000 or 30000 milliseconds.");
        }

        if (string.IsNullOrWhiteSpace(recipe.StartUrl) && !assumeExistingSession) {
            AddIssue(result, HtmlBrowserRecipeValidationSeverity.Error, null, null, null, nameof(recipe.StartUrl), "Recipe StartUrl is required unless an existing browser session will be supplied.", "Set StartUrl or run Test-HtmlBrowserRecipe with -AssumeSession when the recipe is always replayed against an existing session.");
        }

        if (recipe.Steps.Count == 0) {
            AddIssue(result, HtmlBrowserRecipeValidationSeverity.Error, null, null, null, nameof(recipe.Steps), "Recipe has no steps to execute.", "Add at least one step.");
        }

        for (int index = 0; index < recipe.Steps.Count; index++) {
            ValidateStep(result, recipe.Steps[index], index, variables);
        }

        result.SuggestedCommand = result.Passed
            ? "Invoke-HtmlBrowserRecipe -Path '<recipe.json>'"
            : "$validation.BlockingIssues | Format-Table Severity,StepIndex,Action,Property,Message,SuggestedFix,SuggestedCommand -AutoSize";
        return result;
    }

    /// <summary>
    /// Creates a recipe run result that represents failed preflight validation without launching a browser.
    /// </summary>
    /// <param name="recipe">Recipe that was about to be executed.</param>
    /// <param name="validation">Validation result that blocked execution.</param>
    /// <param name="treatWarningsAsErrors">Whether warnings were treated as blocking preflight issues.</param>
    /// <returns>A failed run result carrying validation details for callers.</returns>
    public static HtmlBrowserRecipeRunResult CreateRecipePreflightFailureResult(HtmlBrowserRecipe recipe, HtmlBrowserRecipeValidationResult validation, bool treatWarningsAsErrors = false) {
        if (recipe == null) {
            throw new ArgumentNullException(nameof(recipe));
        }

        if (validation == null) {
            throw new ArgumentNullException(nameof(validation));
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        HtmlBrowserRecipeValidationIssue? firstIssue = validation.Issues.FirstOrDefault(static issue => issue.Severity == HtmlBrowserRecipeValidationSeverity.Error)
            ?? (treatWarningsAsErrors
                ? validation.Issues.FirstOrDefault(static issue => issue.Severity == HtmlBrowserRecipeValidationSeverity.Warning)
                : null);
        string firstMessage = firstIssue == null ? "Review validation warnings before replay." : firstIssue.Message;
        string mode = treatWarningsAsErrors ? "Strict recipe preflight blocked" : "Recipe preflight failed";
        return new HtmlBrowserRecipeRunResult {
            Name = recipe.Name,
            StartedAtUtc = now,
            CompletedAtUtc = now,
            StartUrl = recipe.StartUrl ?? string.Empty,
            Succeeded = false,
            CreatedSession = false,
            SkippedBeforeExecution = true,
            Validation = validation,
            StrictPreflight = treatWarningsAsErrors,
            FailedStepIndex = firstIssue?.StepIndex,
            FailedStepName = firstIssue?.StepName ?? string.Empty,
            FailureSummary = $"{mode} replay with {validation.ErrorCount} error(s) and {validation.WarningCount} warning(s). First issue: {firstMessage}",
            SuggestedCommand = validation.SuggestedCommand
        };
    }

    private static void ValidateStep(HtmlBrowserRecipeValidationResult result, HtmlBrowserRecipeStep step, int index, ISet<string> runtimeVariables) {
        if (step.Timeout.HasValue && step.Timeout.Value < 0) {
            AddStepIssue(result, HtmlBrowserRecipeValidationSeverity.Error, step, index, nameof(step.Timeout), "Step Timeout must be zero or greater.", "Set Timeout to 0 for no timeout or a positive millisecond value.");
        }

        if (step.ContinueOnError) {
            AddStepIssue(result, HtmlBrowserRecipeValidationSeverity.Warning, step, index, nameof(step.ContinueOnError), "Step will continue on error, which may hide broken evidence or extraction flows.", "Use ContinueOnError only for optional cleanup or best-effort evidence steps.");
        }

        switch (step.Action) {
            case HtmlBrowserRecipeAction.Navigate:
                RequireStepValue(result, step, index, step.Url, nameof(step.Url));
                break;
            case HtmlBrowserRecipeAction.Click:
                RequireStepSelector(result, step, index);
                WarnSensitiveSelectors(result, step, index);
                ValidateClickNavigation(result, step, index);
                break;
            case HtmlBrowserRecipeAction.ClickText:
                RequireStepValue(result, step, index, step.Text, nameof(step.Text));
                ValidateClickNavigation(result, step, index);
                break;
            case HtmlBrowserRecipeAction.Input:
            case HtmlBrowserRecipeAction.TypeInput:
                RequireStepSelector(result, step, index);
                ValidateInputValue(result, step, index, runtimeVariables);
                WarnSensitiveSelectors(result, step, index);
                if (step.Action == HtmlBrowserRecipeAction.TypeInput && step.DelayMilliseconds < 0) {
                    AddStepIssue(result, HtmlBrowserRecipeValidationSeverity.Error, step, index, nameof(step.DelayMilliseconds), "TypeInput DelayMilliseconds must be zero or greater.", "Set DelayMilliseconds to 0 or a positive value.");
                }
                break;
            case HtmlBrowserRecipeAction.SetChecked:
                RequireStepSelector(result, step, index);
                WarnSensitiveSelectors(result, step, index);
                break;
            case HtmlBrowserRecipeAction.SelectOption:
                RequireStepSelector(result, step, index);
                ValidateSelectValues(result, step, index, runtimeVariables);
                WarnSensitiveSelectors(result, step, index);
                break;
            case HtmlBrowserRecipeAction.Key:
                RequireStepSelector(result, step, index);
                RequireStepValue(result, step, index, step.Keys, nameof(step.Keys));
                WarnSensitiveSelectors(result, step, index);
                break;
            case HtmlBrowserRecipeAction.WaitReady:
                ValidateWaitReady(result, step, index);
                WarnSensitiveSelectors(result, step, index);
                break;
            case HtmlBrowserRecipeAction.WaitText:
                RequireStepValue(result, step, index, step.Text, nameof(step.Text));
                WarnSensitiveSelectors(result, step, index);
                break;
            case HtmlBrowserRecipeAction.WaitMilliseconds:
                if (step.Milliseconds < 0) {
                    AddStepIssue(result, HtmlBrowserRecipeValidationSeverity.Error, step, index, nameof(step.Milliseconds), "WaitMilliseconds must be zero or greater.", "Set Milliseconds to 0 or a positive wait duration.");
                } else if (step.Milliseconds > 10000) {
                    AddStepIssue(result, HtmlBrowserRecipeValidationSeverity.Warning, step, index, nameof(step.Milliseconds), "Fixed waits over 10 seconds are usually brittle.", "Prefer WaitReady or WaitText with a meaningful condition.");
                }
                break;
            case HtmlBrowserRecipeAction.Script:
                RequireStepValue(result, step, index, step.Script, nameof(step.Script));
                break;
            case HtmlBrowserRecipeAction.Screenshot:
                RequireStepValue(result, step, index, step.OutFile, nameof(step.OutFile));
                break;
            case HtmlBrowserRecipeAction.Evidence:
                RequireStepValue(result, step, index, step.OutFolder, nameof(step.OutFolder));
                break;
            case HtmlBrowserRecipeAction.Locator:
                if (step.Limit <= 0) {
                    AddStepIssue(result, HtmlBrowserRecipeValidationSeverity.Error, step, index, nameof(step.Limit), "Locator Limit must be greater than zero.", "Set Limit to at least 1.");
                }
                if (string.IsNullOrWhiteSpace(step.Text)) {
                    AddStepIssue(result, HtmlBrowserRecipeValidationSeverity.Warning, step, index, nameof(step.Text), "Locator step has no query and may return too many candidates.", "Set Text to the label, button text, placeholder, id, or name you are trying to find.");
                }
                break;
            default:
                AddStepIssue(result, HtmlBrowserRecipeValidationSeverity.Error, step, index, nameof(step.Action), $"Unsupported browser recipe action '{step.Action}'.", "Use one of the HtmlBrowserRecipeAction values supported by this module version.");
                break;
        }
    }

    private static void ValidateInputValue(HtmlBrowserRecipeValidationResult result, HtmlBrowserRecipeStep step, int index, ISet<string> runtimeVariables) {
        if (step.ValueRedacted == true) {
            if (string.IsNullOrWhiteSpace(step.ValueVariable)) {
                AddStepIssue(result, HtmlBrowserRecipeValidationSeverity.Error, step, index, nameof(step.ValueVariable), "Redacted input step is missing ValueVariable.", "Set ValueVariable so replay can supply the secret at runtime.");
            } else if (!runtimeVariables.Contains(step.ValueVariable!)) {
                AddStepIssue(result, HtmlBrowserRecipeValidationSeverity.Error, step, index, nameof(step.ValueVariable), $"Runtime variable '{step.ValueVariable}' was not supplied.", $"Run Invoke-HtmlBrowserRecipe with -Variable @{{ {step.ValueVariable} = '<value>' }}.");
            }
            return;
        }

        if (!string.IsNullOrWhiteSpace(step.ValueVariable)) {
            if (!runtimeVariables.Contains(step.ValueVariable!) && string.IsNullOrEmpty(step.Value)) {
                AddStepIssue(result, HtmlBrowserRecipeValidationSeverity.Error, step, index, nameof(step.ValueVariable), $"Runtime variable '{step.ValueVariable}' was not supplied.", $"Run Invoke-HtmlBrowserRecipe with -Variable @{{ {step.ValueVariable} = '<value>' }}.");
            }
            return;
        }

        if (string.IsNullOrEmpty(step.Value) && string.IsNullOrWhiteSpace(step.ValueVariable)) {
            AddStepIssue(result, HtmlBrowserRecipeValidationSeverity.Warning, step, index, nameof(step.Value), "Input step will submit an empty value.", "Set Value or ValueVariable when an empty value is not intentional.");
        }
    }

    private static void ValidateSelectValues(HtmlBrowserRecipeValidationResult result, HtmlBrowserRecipeStep step, int index, ISet<string> runtimeVariables) {
        if (!string.IsNullOrWhiteSpace(step.ValueVariable)) {
            if (!runtimeVariables.Contains(step.ValueVariable!) && step.Values.Count == 0) {
                AddStepIssue(result, HtmlBrowserRecipeValidationSeverity.Error, step, index, nameof(step.ValueVariable), $"Runtime variable '{step.ValueVariable}' was not supplied.", $"Run Invoke-HtmlBrowserRecipe with -Variable @{{ {step.ValueVariable} = '<value>' }}.");
            }
            return;
        }

        if (step.Values.Count == 0) {
            AddStepIssue(result, HtmlBrowserRecipeValidationSeverity.Error, step, index, nameof(step.Values), "SelectOption step requires at least one value.", "Set Values or ValueVariable.");
        }
    }

    private static IEnumerable<HtmlBrowserRecipeVariableRequirement> GetRecipeVariableRequirements(HtmlBrowserRecipe recipe, ISet<string> suppliedVariables) {
        Dictionary<string, HtmlBrowserRecipeVariableRequirement> requirements = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < recipe.Steps.Count; index++) {
            HtmlBrowserRecipeStep step = recipe.Steps[index];
            if (string.IsNullOrWhiteSpace(step.ValueVariable)) {
                continue;
            }

            string name = step.ValueVariable!.Trim();
            if (!requirements.TryGetValue(name, out HtmlBrowserRecipeVariableRequirement? requirement)) {
                requirement = new HtmlBrowserRecipeVariableRequirement {
                    Name = name,
                    Supplied = suppliedVariables.Contains(name),
                    Sensitive = IsSensitiveRecipeVariable(step, name),
                    Placeholder = IsSensitiveRecipeVariable(step, name) ? "<secret>" : "<value>"
                };
                requirements[name] = requirement;
            } else if (IsSensitiveRecipeVariable(step, name)) {
                requirement.Sensitive = true;
                requirement.Placeholder = "<secret>";
            }

            bool required = IsRecipeVariableRequired(step);
            requirement.Required |= required;
            requirement.Reason = CombineRecipeVariableReason(requirement.Reason, GetRecipeVariableReason(step, required));
            AddUnique(requirement.StepIndexes, index);
            AddUnique(requirement.StepNames, step.Name);
            AddUnique(requirement.Actions, step.Action);
        }

        return requirements.Values
            .OrderByDescending(static requirement => requirement.Required)
            .ThenBy(static requirement => requirement.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsRecipeVariableRequired(HtmlBrowserRecipeStep step) =>
        step.ValueRedacted == true
        || (step.Action == HtmlBrowserRecipeAction.SelectOption
            ? step.Values.Count == 0
            : string.IsNullOrEmpty(step.Value) && step.Values.Count == 0);

    private static bool IsSensitiveRecipeVariable(HtmlBrowserRecipeStep step, string name) =>
        step.ValueRedacted == true
        || HtmlSensitiveValueRedactor.IsSensitiveName(name)
        || (!string.IsNullOrWhiteSpace(step.Selector) && SelectorContainsSensitiveRecipeValue(step.Selector!));

    private static string GetRecipeVariableReason(HtmlBrowserRecipeStep step, bool required) {
        if (step.ValueRedacted == true) {
            return "Recorded sensitive value was redacted and must be supplied at replay time.";
        }

        if (required) {
            return "Recipe step uses this runtime variable during replay.";
        }

        return "Recipe step can use this runtime variable when supplied, otherwise it falls back to the stored value.";
    }

    private static string CombineRecipeVariableReason(string existingReason, string nextReason) {
        if (string.IsNullOrWhiteSpace(existingReason)) {
            return nextReason;
        }

        return existingReason.IndexOf(nextReason, StringComparison.Ordinal) >= 0
            ? existingReason
            : existingReason + " " + nextReason;
    }

    private static void AddUnique<T>(ICollection<T> values, T value) {
        if (!values.Contains(value)) {
            values.Add(value);
        }
    }

    private static void AddUnique(ICollection<string> values, string? value) {
        if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value!)) {
            values.Add(value!);
        }
    }

    private static void ValidateWaitReady(HtmlBrowserRecipeValidationResult result, HtmlBrowserRecipeStep step, int index) {
        if (step.NoLoadState && string.IsNullOrWhiteSpace(step.Selector) && string.IsNullOrWhiteSpace(step.Script) && !step.Stable) {
            AddStepIssue(result, HtmlBrowserRecipeValidationSeverity.Warning, step, index, nameof(step.NoLoadState), "WaitReady skips load-state and has no selector, script, or stability condition.", "Add Selector, Script, or Stable so the wait proves readiness.");
        }

        if (step.PollMilliseconds <= 0) {
            AddStepIssue(result, HtmlBrowserRecipeValidationSeverity.Error, step, index, nameof(step.PollMilliseconds), "PollMilliseconds must be greater than zero.", "Set PollMilliseconds to a positive value.");
        }

        if (step.Stable && step.StableMilliseconds < 0) {
            AddStepIssue(result, HtmlBrowserRecipeValidationSeverity.Error, step, index, nameof(step.StableMilliseconds), "StableMilliseconds must be zero or greater.", "Set StableMilliseconds to 0 or a positive value.");
        }
    }

    private static void ValidateClickNavigation(HtmlBrowserRecipeValidationResult result, HtmlBrowserRecipeStep step, int index) {
        if (!string.IsNullOrWhiteSpace(step.NavigationUrl) && !step.WaitForNavigation) {
            AddStepIssue(result, HtmlBrowserRecipeValidationSeverity.Warning, step, index, nameof(step.NavigationUrl), "NavigationUrl is ignored unless WaitForNavigation is enabled.", "Set WaitForNavigation to true or remove NavigationUrl.");
        }

        if (!string.IsNullOrWhiteSpace(step.NavigationUrl) && HtmlSensitiveValueRedactor.HasSensitiveQueryText(step.NavigationUrl!)) {
            AddStepIssue(result, HtmlBrowserRecipeValidationSeverity.Warning, step, index, nameof(step.NavigationUrl), "NavigationUrl appears to contain sensitive query parameter names.", "Replace token-bearing URL patterns with a stable route glob such as **/dashboard or **/proof/**.");
        }
    }

    private static void RequireStepValue(HtmlBrowserRecipeValidationResult result, HtmlBrowserRecipeStep step, int index, string? value, string propertyName) {
        if (string.IsNullOrWhiteSpace(value)) {
            AddStepIssue(result, HtmlBrowserRecipeValidationSeverity.Error, step, index, propertyName, $"{propertyName} is required for {step.Action} steps.", $"Set {propertyName} on this recipe step.");
        }
    }

    private static void RequireStepSelector(HtmlBrowserRecipeValidationResult result, HtmlBrowserRecipeStep step, int index) {
        if (GetRecipeSelectors(step).Count == 0) {
            AddStepIssue(result, HtmlBrowserRecipeValidationSeverity.Error, step, index, nameof(step.Selector), $"Selector or SelectorAlternates is required for {step.Action} steps.", "Set Selector or at least one SelectorAlternates entry on this recipe step.");
        }
    }

    private static void WarnSensitiveSelectors(HtmlBrowserRecipeValidationResult result, HtmlBrowserRecipeStep step, int index) {
        IReadOnlyList<string> selectors = GetRecipeSelectors(step);
        if (selectors.Count == 0 || !selectors.Any(SelectorContainsSensitiveRecipeValue)) {
            return;
        }

        AddStepIssue(result, HtmlBrowserRecipeValidationSeverity.Warning, step, index, nameof(step.Selector), "Selector or selector alternate appears to contain sensitive values.", "Replace token-bearing selectors with stable id, data-testid, role, text, or locator values discovered by Find-HtmlBrowserLocator.");
    }

    private static void AddStepIssue(HtmlBrowserRecipeValidationResult result, HtmlBrowserRecipeValidationSeverity severity, HtmlBrowserRecipeStep step, int index, string property, string message, string suggestedFix) =>
        AddIssue(
            result,
            severity,
            index,
            step.Name,
            step.Action,
            property,
            message,
            suggestedFix,
            BuildValidationIssueSuggestedCommand(step, property),
            BuildValidationIssueDocumentationHint(step.Action, property));

    private static void AddIssue(
        HtmlBrowserRecipeValidationResult result,
        HtmlBrowserRecipeValidationSeverity severity,
        int? stepIndex,
        string? stepName,
        HtmlBrowserRecipeAction? action,
        string property,
        string message,
        string suggestedFix,
        string? suggestedCommand = null,
        string? documentationHint = null) {
        result.Issues.Add(new HtmlBrowserRecipeValidationIssue {
            Severity = severity,
            StepIndex = stepIndex,
            StepName = stepName ?? string.Empty,
            Action = action,
            Property = property,
            Message = HtmlSensitiveValueRedactor.RedactSensitiveEvidenceText(message),
            SuggestedFix = HtmlSensitiveValueRedactor.RedactSensitiveEvidenceText(suggestedFix),
            SuggestedCommand = HtmlSensitiveValueRedactor.RedactSensitiveQueryValues(HtmlSensitiveValueRedactor.RedactSensitiveEvidenceText(
                suggestedCommand ?? BuildValidationIssueSuggestedCommand(action, property))),
            DocumentationHint = documentationHint ?? BuildValidationIssueDocumentationHint(action, property)
        });
    }

    private static string BuildValidationIssueSuggestedCommand(HtmlBrowserRecipeAction? action, string property) =>
        property switch {
            nameof(HtmlBrowserRecipe.SchemaVersion) or nameof(HtmlBrowserRecipe.Timeout)
                => "Test-HtmlBrowserRecipe -Path '<recipe.json>'",
            nameof(HtmlBrowserRecipe.StartUrl)
                => "Test-HtmlBrowserRecipe -Path '<recipe.json>' -AssumeSession",
            nameof(HtmlBrowserRecipe.Steps)
                => "Start-HtmlBrowserRecipeRecording -Session $session -Name '<recipe-name>'; Stop-HtmlBrowserRecipeRecording -Session $session -Path '<recipe.json>'",
            _ => action switch {
                HtmlBrowserRecipeAction.Locator => "Find-HtmlBrowserLocator -Session $session -Query '<label-or-text>' -Limit 10",
                HtmlBrowserRecipeAction.WaitReady => "Wait-HtmlBrowserReady -Session $session -Selector '<ready-selector>' -Stable -Timeout 30000",
                HtmlBrowserRecipeAction.WaitText => "Wait-HtmlBrowserContent -Session $session -Selector 'body' -Text '<expected text>' -Timeout 30000",
                HtmlBrowserRecipeAction.Screenshot => "Save-HtmlBrowserScreenshot -Session $session -OutFile '.\\proof.png' -FullPage",
                HtmlBrowserRecipeAction.Evidence => "Export-HtmlBrowserEvidence -Session $session -OutFolder '.\\evidence\\proof' -NetworkSummary -SsoHandoffSummary",
                _ => "$validation.BlockingIssues | Format-List Severity,StepIndex,Action,Property,Message,SuggestedFix,SuggestedCommand"
            }
        };

    private static string BuildValidationIssueSuggestedCommand(HtmlBrowserRecipeStep step, string property) {
        IReadOnlyList<string> selectors = GetRecipeSelectors(step);
        string selector = selectors.FirstOrDefault() ?? string.Empty;
        bool hasSensitiveSelector = selectors.Any(SelectorContainsSensitiveRecipeValue);

        if (property == nameof(step.Selector)) {
            string query = string.IsNullOrWhiteSpace(step.Text)
                ? step.Name
                : step.Text!;
            return string.IsNullOrWhiteSpace(query) || hasSensitiveSelector
                ? "Get-HtmlBrowserInteractable -Session $session | Select-Object -First 20"
                : $"Find-HtmlBrowserLocator -Session $session -Query '{EscapePowerShellSingleQuotedString(query)}' -Limit 10";
        }

        if (property == nameof(step.ValueVariable)) {
            string variableName = string.IsNullOrWhiteSpace(step.ValueVariable) ? "<variable>" : step.ValueVariable!;
            return $"Invoke-HtmlBrowserRecipe -Path '<recipe.json>' -Variable @{{ {EscapePowerShellHashtableKey(variableName)} = '<value>' }}";
        }

        if (property == nameof(step.Value) || property == nameof(step.Values)) {
            return "Export-HtmlBrowserRecipe -Recipe $recipe -Path '<recipe.json>' -VariableTemplatePath '<recipe.variables.json>'";
        }

        if (property == nameof(step.ContinueOnError)) {
            return "$validation.BlockingIssues | Where-Object Property -eq 'ContinueOnError' | Format-Table StepIndex,Action,Message,SuggestedFix -AutoSize";
        }

        if (property == nameof(step.Milliseconds) || property == nameof(step.NoLoadState) || property == nameof(step.PollMilliseconds) || property == nameof(step.StableMilliseconds)) {
            return "Wait-HtmlBrowserReady -Session $session -Selector '<ready-selector>' -Stable -Timeout 30000";
        }

        if (property == nameof(step.NavigationUrl)) {
            if (step.Action == HtmlBrowserRecipeAction.ClickText && !string.IsNullOrWhiteSpace(step.Text)) {
                return $"Invoke-HtmlBrowserClick -Session $session -Text '{EscapePowerShellSingleQuotedString(step.Text!)}' -Exact";
            }

            return string.IsNullOrWhiteSpace(selector) || hasSensitiveSelector
                ? "Invoke-HtmlBrowserNavigation -Session $session -Url '<expected-url>' -WaitUntil DomContentLoaded"
                : BuildRecipeSelectorCommand("Invoke-HtmlBrowserClick -Session $session -Selector", selector, string.Empty);
        }

        if (property == nameof(step.Text)) {
            return step.Action == HtmlBrowserRecipeAction.Locator
                ? "Find-HtmlBrowserLocator -Session $session -Query '<label-or-text>' -Limit 10"
                : "Wait-HtmlBrowserContent -Session $session -Selector 'body' -Text '<expected text>' -Timeout 30000";
        }

        if (property == nameof(step.Script)) {
            return "Invoke-HtmlBrowserScript -Session $session -Script '<script>'";
        }

        if (property == nameof(step.Url)) {
            return "Invoke-HtmlBrowserNavigation -Session $session -Url '<url>' -WaitUntil DomContentLoaded";
        }

        if (property == nameof(step.OutFile)) {
            return "Save-HtmlBrowserScreenshot -Session $session -OutFile '.\\proof.png' -FullPage";
        }

        if (property == nameof(step.OutFolder)) {
            return "Export-HtmlBrowserEvidence -Session $session -OutFolder '.\\evidence\\proof' -NetworkSummary -SsoHandoffSummary";
        }

        if (property == nameof(step.Keys)) {
            return string.IsNullOrWhiteSpace(selector) || hasSensitiveSelector
                ? "Get-HtmlBrowserActiveElement -Session $session"
                : BuildRecipeSelectorCommand("Invoke-HtmlBrowserKey -Session $session -Selector", selector, " -Key '<key>'");
        }

        return BuildValidationIssueSuggestedCommand(step.Action, property);
    }

    private static string BuildValidationIssueDocumentationHint(HtmlBrowserRecipeAction? action, string property) =>
        property switch {
            nameof(HtmlBrowserRecipe.StartUrl) => "Existing-session recipes can be validated with -AssumeSession.",
            nameof(HtmlBrowserRecipe.Steps) => "Use recipe recording when you need a replayable browser workflow.",
            nameof(HtmlBrowserRecipeStep.Selector) => "Use Find-HtmlBrowserLocator to replace brittle selectors with stable role, text, id, or data-testid locators.",
            nameof(HtmlBrowserRecipeStep.ValueVariable) => "Use recipe variable templates for secrets and runtime-only values.",
            nameof(HtmlBrowserRecipeStep.ContinueOnError) => "Keep ContinueOnError only for optional cleanup or best-effort evidence steps.",
            nameof(HtmlBrowserRecipeStep.Milliseconds) => "Prefer readiness conditions over fixed sleeps in scheduled browser jobs.",
            nameof(HtmlBrowserRecipeStep.NavigationUrl) => "Use navigation waits only with stable, non-token URL patterns.",
            nameof(HtmlBrowserRecipeStep.OutFile) or nameof(HtmlBrowserRecipeStep.OutFolder) => "Evidence and screenshot steps need writable output paths on the machine running the job.",
            _ => action switch {
                HtmlBrowserRecipeAction.WaitReady or HtmlBrowserRecipeAction.WaitText => "Prefer meaningful readiness checks before proof capture or extraction.",
                HtmlBrowserRecipeAction.Evidence => "Evidence steps can produce HTML, text, screenshots, PDFs, network summaries, and SSO handoff summaries.",
                HtmlBrowserRecipeAction.Locator => "Locator steps are useful for hardening recorded recipes after UI changes.",
                _ => "Run Test-HtmlBrowserRecipe before replay to catch recipe issues without launching a browser."
            }
        };

    private static string EscapePowerShellHashtableKey(string key) =>
        System.Text.RegularExpressions.Regex.IsMatch(key, "^[A-Za-z_][A-Za-z0-9_]*$")
            ? key
            : $"'{EscapePowerShellSingleQuotedString(key)}'";
}
