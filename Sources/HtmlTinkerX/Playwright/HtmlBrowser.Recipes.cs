using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Browser recipe serialization and execution helpers.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Serializes a browser recipe to JSON.
    /// </summary>
    public static string SerializeRecipe(HtmlBrowserRecipe recipe) {
        if (recipe == null) {
            throw new ArgumentNullException(nameof(recipe));
        }

        return JsonSerializer.Serialize(recipe, CreateRecipeJsonOptions());
    }

    /// <summary>
    /// Deserializes a browser recipe from JSON.
    /// </summary>
    public static HtmlBrowserRecipe DeserializeRecipe(string json) {
        if (string.IsNullOrWhiteSpace(json)) {
            throw new ArgumentException("Recipe JSON cannot be empty.", nameof(json));
        }

        return JsonSerializer.Deserialize<HtmlBrowserRecipe>(json, CreateRecipeJsonOptions())
            ?? throw new InvalidDataException("Recipe JSON did not contain a browser recipe.");
    }

    /// <summary>
    /// Executes a browser automation recipe against an existing session or a recipe-created session.
    /// </summary>
    /// <param name="recipe">Recipe to execute.</param>
    /// <param name="session">Optional existing browser session.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recipe run result.</returns>
    public static async Task<HtmlBrowserRecipeRunResult> ExecuteRecipeAsync(
        HtmlBrowserRecipe recipe,
        HtmlBrowserSession? session = null,
        CancellationToken cancellationToken = default) {
        return await ExecuteRecipeAsync(recipe, session, options: null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a browser automation recipe against an existing session or a recipe-created session.
    /// </summary>
    /// <param name="recipe">Recipe to execute.</param>
    /// <param name="session">Optional existing browser session.</param>
    /// <param name="options">Recipe execution options, including runtime variables for redacted values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recipe run result.</returns>
    public static async Task<HtmlBrowserRecipeRunResult> ExecuteRecipeAsync(
        HtmlBrowserRecipe recipe,
        HtmlBrowserSession? session,
        HtmlBrowserRecipeRunOptions? options,
        CancellationToken cancellationToken = default) {
        if (recipe == null) {
            throw new ArgumentNullException(nameof(recipe));
        }

        if (recipe.Timeout < 0) {
            throw new ArgumentOutOfRangeException(nameof(recipe.Timeout), "Timeout must be zero or greater.");
        }

        bool createdSession = session == null;
        bool previousRecordingSuppression = false;
        if (session == null) {
            if (string.IsNullOrWhiteSpace(recipe.StartUrl)) {
                throw new ArgumentException("Recipe StartUrl is required when no browser session is supplied.", nameof(recipe));
            }

            HtmlBrowserLaunchOptions launchOptions = options?.LaunchOptions ?? CreateRecipeLaunchOptions(recipe);
            session = await OpenSessionAsync(recipe.StartUrl!, launchOptions, cancellationToken).ConfigureAwait(false);
        }

        previousRecordingSuppression = session.SuppressRecipeRecording;
        session.SuppressRecipeRecording = true;

        HtmlBrowserRecipeRunResult result = new() {
            Name = recipe.Name,
            StartedAtUtc = DateTimeOffset.UtcNow,
            StartUrl = recipe.StartUrl ?? session.Page.Url,
            CreatedSession = createdSession
        };

        try {
            for (int i = 0; i < recipe.Steps.Count; i++) {
                HtmlBrowserRecipeStep step = recipe.Steps[i];
                HtmlBrowserRecipeStepResult stepResult = await ExecuteRecipeStepAsync(session, recipe, step, i, options, cancellationToken).ConfigureAwait(false);
                result.Steps.Add(stepResult);
                if (!stepResult.Succeeded && !step.ContinueOnError) {
                    break;
                }
            }

            result.FinalUrl = session.Page.Url;
            result.Title = await session.Page.TitleAsync().ConfigureAwait(false);
            result.Succeeded = result.Steps.TrueForAll(static step => step.Succeeded);
            PopulateRecipeRunFailureSummary(result);
            result.CompletedAtUtc = DateTimeOffset.UtcNow;
            return result;
        } finally {
            session.SuppressRecipeRecording = previousRecordingSuppression;
            if (createdSession) {
                await session.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task<HtmlBrowserRecipeStepResult> ExecuteRecipeStepAsync(
        HtmlBrowserSession session,
        HtmlBrowserRecipe recipe,
        HtmlBrowserRecipeStep step,
        int index,
        HtmlBrowserRecipeRunOptions? options,
        CancellationToken cancellationToken) {
        HtmlBrowserRecipeStepResult result = new() {
            Index = index,
            Name = step.Name,
            Action = step.Action,
            Target = GetRecipeStepTarget(step),
            StartedAtUtc = DateTimeOffset.UtcNow
        };

        try {
            int timeout = step.Timeout ?? recipe.Timeout;
            switch (step.Action) {
                case HtmlBrowserRecipeAction.Navigate:
                    Require(step.Url, nameof(step.Url), step.Action);
                    await NavigateAsync(session, step.Url!, step.LoadState, timeout, cancellationToken).ConfigureAwait(false);
                    break;
                case HtmlBrowserRecipeAction.Click:
                    await ExecuteRecipeSelectorActionAsync(session, step, result, selector =>
                        ClickSelectorAsync(session, selector, step.WaitForNavigation, step.LoadState, step.NavigationUrl, timeout, cancellationToken), cancellationToken).ConfigureAwait(false);
                    break;
                case HtmlBrowserRecipeAction.ClickText:
                    Require(step.Text, nameof(step.Text), step.Action);
                    await ClickTextAsync(session, step.Text!, step.Exact, regex: null, step.WaitForNavigation, step.LoadState, step.NavigationUrl, timeout, cancellationToken).ConfigureAwait(false);
                    break;
                case HtmlBrowserRecipeAction.Input:
                    await ExecuteRecipeSelectorActionAsync(session, step, result, selector =>
                        FillInputAsync(session, selector, ResolveRecipeValue(step, options), timeout, cancellationToken), cancellationToken).ConfigureAwait(false);
                    break;
                case HtmlBrowserRecipeAction.TypeInput:
                    await ExecuteRecipeSelectorActionAsync(session, step, result, selector =>
                        TypeInputAsync(session, selector, ResolveRecipeValue(step, options), step.DelayMilliseconds, timeout, cancellationToken), cancellationToken).ConfigureAwait(false);
                    break;
                case HtmlBrowserRecipeAction.SetChecked:
                    await ExecuteRecipeSelectorActionAsync(session, step, result, selector =>
                        SetCheckedAsync(session, selector, step.Checked ?? true, timeout, cancellationToken), cancellationToken).ConfigureAwait(false);
                    break;
                case HtmlBrowserRecipeAction.SelectOption:
                    await ExecuteRecipeSelectorActionAsync(session, step, result, selector =>
                        SelectOptionAsync(session, selector, ResolveRecipeValues(step, options), timeout, cancellationToken), cancellationToken).ConfigureAwait(false);
                    break;
                case HtmlBrowserRecipeAction.Key:
                    Require(step.Keys, nameof(step.Keys), step.Action);
                    await ExecuteRecipeSelectorActionAsync(session, step, result, selector =>
                        PressKeysAsync(session, selector, step.Keys!, timeout, cancellationToken), cancellationToken).ConfigureAwait(false);
                    break;
                case HtmlBrowserRecipeAction.WaitReady:
                    await ExecuteRecipeReadyWaitAsync(session, step, result, timeout, cancellationToken).ConfigureAwait(false);
                    break;
                case HtmlBrowserRecipeAction.WaitText:
                    Require(step.Text, nameof(step.Text), step.Action);
                    await ExecuteRecipeTextWaitAsync(session, step, result, timeout, cancellationToken).ConfigureAwait(false);
                    break;
                case HtmlBrowserRecipeAction.WaitMilliseconds:
                    if (step.Milliseconds < 0) {
                        throw new ArgumentOutOfRangeException(nameof(step.Milliseconds), "Milliseconds must be zero or greater.");
                    }
                    await Task.Delay(step.Milliseconds, cancellationToken).ConfigureAwait(false);
                    break;
                case HtmlBrowserRecipeAction.Script:
                    Require(step.Script, nameof(step.Script), step.Action);
                    cancellationToken.ThrowIfCancellationRequested();
                    object? output = await session.Page.EvaluateAsync<object?>(step.Script!).ConfigureAwait(false);
                    result.Output = output?.ToString();
                    break;
                case HtmlBrowserRecipeAction.Screenshot:
                    Require(step.OutFile, nameof(step.OutFile), step.Action);
                    await CaptureScreenshotAsync(session.Page, step.OutFile!.ToFullPath(), new ScreenshotOptions { FullPage = step.FullPage }, cancellationToken).ConfigureAwait(false);
                    result.Output = step.OutFile!.ToFullPath();
                    break;
                case HtmlBrowserRecipeAction.Evidence:
                    Require(step.OutFolder, nameof(step.OutFolder), step.Action);
                    result.Evidence = await ExportEvidenceAsync(session, step.OutFolder!, CreateEvidenceOptions(step), cancellationToken).ConfigureAwait(false);
                    result.Output = result.Evidence.ManifestPath;
                    break;
                case HtmlBrowserRecipeAction.Locator:
                    result.LocatorCandidates = await FindLocatorCandidatesAsync(session, step.Text, !step.IncludeHidden, step.Limit, cancellationToken).ConfigureAwait(false);
                    result.Output = result.LocatorCandidates.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(step.Action), step.Action, "Unsupported browser recipe action.");
            }

            result.Succeeded = true;
        } catch (Exception ex) when (ex is PlaywrightException || ex is TimeoutException || ex is InvalidOperationException || ex is ArgumentException || ex is ArgumentOutOfRangeException) {
            result.Succeeded = false;
            result.ErrorType = ex.GetType().FullName;
            result.ErrorMessage = HtmlSensitiveValueRedactor.RedactSensitiveEvidenceText(ex.Message);
            result.SuggestedFix = BuildRecipeStepSuggestedFix(step, result);
            result.SuggestedCommand = BuildRecipeStepSuggestedCommand(step);
            if (ShouldExportRecipeFailureEvidence(recipe, options)) {
                result.Evidence = await ExportFailureEvidenceAsync(
                    session,
                    ex,
                    new HtmlBrowserFailureEvidenceOptions {
                        Operation = string.IsNullOrWhiteSpace(step.Name) ? step.Action.ToString() : step.Name,
                        BaseFileName = $"recipe-step-{index}",
                        OutFolder = GetRecipeFailureEvidenceFolder(recipe, options)
                    },
                    cancellationToken).ConfigureAwait(false);
            }
        } finally {
            await PopulateRecipeStepPageContextAsync(session, result, cancellationToken).ConfigureAwait(false);
            result.CompletedAtUtc = DateTimeOffset.UtcNow;
        }

        return result;
    }

    private static async Task ExecuteRecipeSelectorActionAsync(
        HtmlBrowserSession session,
        HtmlBrowserRecipeStep step,
        HtmlBrowserRecipeStepResult result,
        Func<string, Task> action,
        CancellationToken cancellationToken) {
        IReadOnlyList<string> selectors = GetRecipeSelectors(step);
        if (selectors.Count == 0) {
            throw new ArgumentException($"Selector or SelectorAlternates is required for {step.Action} recipe steps.", nameof(step.Selector));
        }

        result.AttemptedSelectors = selectors;
        string selectedSelector = await ChooseRecipeSelectorAsync(session, selectors, cancellationToken).ConfigureAwait(false);
        result.SelectedSelector = selectedSelector;
        await action(selectedSelector).ConfigureAwait(false);
    }

    private static async Task ExecuteRecipeReadyWaitAsync(
        HtmlBrowserSession session,
        HtmlBrowserRecipeStep step,
        HtmlBrowserRecipeStepResult result,
        int timeout,
        CancellationToken cancellationToken) {
        IReadOnlyList<string> selectors = GetRecipeSelectors(step);
        if (selectors.Count == 0) {
            await WaitUntilReadyAsync(session, CreateReadinessOptions(step, null, timeout), cancellationToken).ConfigureAwait(false);
            return;
        }

        result.AttemptedSelectors = selectors;
        Exception? lastException = null;
        foreach (string selector in selectors) {
            try {
                await WaitUntilReadyAsync(session, CreateReadinessOptions(step, selector, timeout), cancellationToken).ConfigureAwait(false);
                result.SelectedSelector = selector;
                return;
            } catch (Exception ex) when (IsRecipeSelectorFallbackException(ex)) {
                lastException = ex;
            }
        }

        throw lastException ?? new InvalidOperationException("No recipe selector fallback matched the current page.");
    }

    private static async Task ExecuteRecipeTextWaitAsync(
        HtmlBrowserSession session,
        HtmlBrowserRecipeStep step,
        HtmlBrowserRecipeStepResult result,
        int timeout,
        CancellationToken cancellationToken) {
        IReadOnlyList<string> selectors = GetRecipeSelectors(step);
        if (selectors.Count == 0) {
            await WaitForTextAsync(session, step.Text!, "body", step.Exact, timeout, cancellationToken).ConfigureAwait(false);
            return;
        }

        result.AttemptedSelectors = selectors;
        Exception? lastException = null;
        foreach (string selector in selectors) {
            try {
                await WaitForTextAsync(session, step.Text!, selector, step.Exact, timeout, cancellationToken).ConfigureAwait(false);
                result.SelectedSelector = selector;
                return;
            } catch (Exception ex) when (IsRecipeSelectorFallbackException(ex)) {
                lastException = ex;
            }
        }

        throw lastException ?? new InvalidOperationException("No recipe selector fallback matched the current page.");
    }

    private static HtmlBrowserReadinessOptions CreateReadinessOptions(HtmlBrowserRecipeStep step, string? selector, int timeout) =>
        new() {
            LoadState = step.LoadState,
            SkipLoadState = step.NoLoadState,
            Selector = selector,
            Function = step.Script,
            Stable = step.Stable,
            StableMilliseconds = step.StableMilliseconds,
            PollMilliseconds = step.PollMilliseconds,
            Timeout = timeout
        };

    private static async Task<string> ChooseRecipeSelectorAsync(HtmlBrowserSession session, IReadOnlyList<string> selectors, CancellationToken cancellationToken) {
        string? firstExisting = null;
        foreach (string selector in selectors) {
            cancellationToken.ThrowIfCancellationRequested();
            ILocator locator = session.Page.Locator(selector).First;
            int count = await session.Page.Locator(selector).CountAsync().ConfigureAwait(false);
            if (count <= 0) {
                continue;
            }

            firstExisting ??= selector;
            try {
                if (await locator.IsVisibleAsync().ConfigureAwait(false)) {
                    return selector;
                }
            } catch (PlaywrightException) {
                // Keep scanning; the action itself will surface a richer error if no fallback is usable.
            }
        }

        return firstExisting ?? selectors[0];
    }

    private static bool IsRecipeSelectorFallbackException(Exception ex) =>
        ex is PlaywrightException || ex is TimeoutException || ex is InvalidOperationException || ex is ArgumentException || ex is ArgumentOutOfRangeException;

    /// <summary>
    /// Creates browser launch options from the session-related defaults stored in a browser recipe.
    /// </summary>
    /// <param name="recipe">Recipe whose launch defaults should be converted.</param>
    /// <returns>Launch options suitable for opening the recipe start URL.</returns>
    public static HtmlBrowserLaunchOptions CreateRecipeLaunchOptions(HtmlBrowserRecipe recipe) {
        if (recipe == null) {
            throw new ArgumentNullException(nameof(recipe));
        }

        if (recipe.Timeout < 0) {
            throw new ArgumentOutOfRangeException(nameof(recipe.Timeout), "Timeout must be zero or greater.");
        }

        return new HtmlBrowserLaunchOptions {
            Browser = recipe.Browser,
            Headless = recipe.Headless,
            LoadState = recipe.LoadState,
            Timeout = recipe.Timeout
        };
    }

    private static async Task PopulateRecipeStepPageContextAsync(HtmlBrowserSession session, HtmlBrowserRecipeStepResult result, CancellationToken cancellationToken) {
        if (cancellationToken.IsCancellationRequested) {
            return;
        }

        result.PageUrl = HtmlSensitiveValueRedactor.RedactSensitiveQueryValues(session.Page.Url ?? string.Empty);
        try {
            result.PageTitle = await session.Page.TitleAsync().ConfigureAwait(false);
        } catch (Exception ex) when (ex is PlaywrightException || ex is InvalidOperationException) {
            result.PageTitle = string.Empty;
        }
    }

    private static void PopulateRecipeRunFailureSummary(HtmlBrowserRecipeRunResult result) {
        HtmlBrowserRecipeStepResult? failed = result.Steps.FirstOrDefault(static step => !step.Succeeded);
        if (failed == null) {
            result.FailedStepIndex = null;
            result.FailedStepName = string.Empty;
            result.FailureSummary = string.Empty;
            result.SuggestedCommand = string.Empty;
            return;
        }

        result.FailedStepIndex = failed.Index;
        result.FailedStepName = failed.Name;
        string stepName = string.IsNullOrWhiteSpace(failed.Name) ? failed.Action.ToString() : failed.Name;
        string target = string.IsNullOrWhiteSpace(failed.Target) ? failed.Action.ToString() : failed.Target;
        result.FailureSummary = $"Recipe failed at step {failed.Index} ('{stepName}') targeting '{target}'. {failed.ErrorMessage}";
        result.SuggestedCommand = failed.SuggestedCommand;
    }

    private static bool ShouldExportRecipeFailureEvidence(HtmlBrowserRecipe recipe, HtmlBrowserRecipeRunOptions? options) =>
        recipe.OnFailureEvidence || options?.OnFailureEvidence == true;

    private static string GetRecipeFailureEvidenceFolder(HtmlBrowserRecipe recipe, HtmlBrowserRecipeRunOptions? options) {
        string? optionsFolder = options?.FailureEvidenceFolder;
        if (!string.IsNullOrWhiteSpace(optionsFolder)) {
            return optionsFolder!;
        }

        return string.IsNullOrWhiteSpace(recipe.FailureEvidenceFolder)
            ? "HtmlBrowserFailureEvidence"
            : recipe.FailureEvidenceFolder!;
    }

    private static string BuildRecipeStepSuggestedFix(HtmlBrowserRecipeStep step, HtmlBrowserRecipeStepResult result) {
        string target = string.IsNullOrWhiteSpace(result.Target) ? step.Action.ToString() : result.Target;
        return step.Action switch {
            HtmlBrowserRecipeAction.Click or HtmlBrowserRecipeAction.Input or HtmlBrowserRecipeAction.TypeInput or HtmlBrowserRecipeAction.SetChecked or HtmlBrowserRecipeAction.SelectOption or HtmlBrowserRecipeAction.Key
                => $"Verify selector '{target}' on the current page. If it changed, run Find-HtmlBrowserLocator and update the recipe step selector.",
            HtmlBrowserRecipeAction.WaitReady
                => $"Verify that selector or readiness condition '{target}' can appear on the current page, then increase the step timeout or update the wait condition.",
            HtmlBrowserRecipeAction.WaitText
                => $"Verify that text '{target}' appears in the expected container, then update the text, selector, or timeout.",
            HtmlBrowserRecipeAction.Navigate
                => $"Verify that URL '{target}' is reachable from this machine and that proxy/authentication settings are correct.",
            HtmlBrowserRecipeAction.Screenshot or HtmlBrowserRecipeAction.Evidence
                => $"Verify that output path '{target}' is writable and not locked by another process.",
            _ => $"Review recipe step '{target}' and the captured page context before retrying."
        };
    }

    private static string BuildRecipeStepSuggestedCommand(HtmlBrowserRecipeStep step) {
        IReadOnlyList<string> selectors = GetRecipeSelectors(step);
        string selector = selectors.FirstOrDefault() ?? string.Empty;
        if (selectors.Any(SelectorContainsSensitiveRecipeValue)) {
            return "$result.Steps | Where-Object Succeeded -eq $false | Format-List Index,Name,Action,Target,ErrorMessage,SuggestedFix,PageUrl,PageTitle";
        }

        return step.Action switch {
            HtmlBrowserRecipeAction.Click => BuildRecipeSelectorCommand("Test-HtmlBrowserElement -Session $session -Selector", selector, " -Visible") + "; " + BuildRecipeSelectorCommand("Invoke-HtmlBrowserClick -Session $session -Selector", selector, string.Empty),
            HtmlBrowserRecipeAction.Input or HtmlBrowserRecipeAction.TypeInput => BuildRecipeSelectorCommand("Test-HtmlBrowserElement -Session $session -Selector", selector, " -Visible") + "; " + BuildRecipeSelectorCommand("Set-HtmlBrowserInput -Session $session -Selector", selector, " -Value '<value>'"),
            HtmlBrowserRecipeAction.SetChecked => BuildRecipeSelectorCommand("Test-HtmlBrowserElement -Session $session -Selector", selector, " -Visible") + "; " + BuildRecipeSelectorCommand("Set-HtmlBrowserChecked -Session $session -Selector", selector, string.Empty),
            HtmlBrowserRecipeAction.SelectOption => BuildRecipeSelectorCommand("Test-HtmlBrowserElement -Session $session -Selector", selector, " -Visible") + "; " + BuildRecipeSelectorCommand("Set-HtmlBrowserSelectOption -Session $session -Selector", selector, " -Value '<value>'"),
            HtmlBrowserRecipeAction.Key => BuildRecipeSelectorCommand("Test-HtmlBrowserElement -Session $session -Selector", selector, " -Visible") + "; " + BuildRecipeSelectorCommand("Invoke-HtmlBrowserKey -Session $session -Selector", selector, " -Key '<key>'"),
            HtmlBrowserRecipeAction.WaitReady when !string.IsNullOrWhiteSpace(selector) => BuildRecipeSelectorCommand("Wait-HtmlBrowserReady -Session $session -NoLoadState -Selector", selector, " -Timeout 30000"),
            HtmlBrowserRecipeAction.WaitText => BuildWaitTextCommand(step),
            HtmlBrowserRecipeAction.Locator => $"Find-HtmlBrowserLocator -Session $session -Query '{EscapePowerShellSingleQuotedString(step.Text ?? string.Empty)}' -Limit {Math.Max(step.Limit, 1)}",
            _ => "$result.Steps | Where-Object Succeeded -eq $false | Format-List Index,Name,Action,Target,ErrorMessage,SuggestedFix,PageUrl,PageTitle"
        };
    }

    private static string BuildWaitTextCommand(HtmlBrowserRecipeStep step) {
        string selector = string.IsNullOrWhiteSpace(step.Selector) ? "body" : step.Selector!;
        return BuildRecipeSelectorCommand("Wait-HtmlBrowserContent -Session $session -Selector", selector, $" -Text '{EscapePowerShellSingleQuotedString(step.Text ?? string.Empty)}' -Timeout 30000");
    }

    private static string BuildRecipeSelectorCommand(string prefix, string selector, string suffix) =>
        string.IsNullOrWhiteSpace(selector)
            ? "$result.Steps | Where-Object Succeeded -eq $false | Format-List Index,Name,Action,Target,ErrorMessage,SuggestedFix,PageUrl,PageTitle"
            : $"{prefix} '{EscapePowerShellSingleQuotedString(selector)}'{suffix}";

    private static bool SelectorContainsSensitiveRecipeValue(string selector) =>
        !string.Equals(selector, HtmlSensitiveValueRedactor.RedactSensitiveQueryValues(selector), StringComparison.Ordinal)
        || !string.Equals(selector, HtmlSensitiveValueRedactor.RedactSensitiveEvidenceText(selector), StringComparison.Ordinal);

    private static string GetRecipeStepTarget(HtmlBrowserRecipeStep step)
        => step.Url
            ?? step.Selector
            ?? step.SelectorAlternates.FirstOrDefault()
            ?? step.Text
            ?? step.Keys
            ?? step.OutFile
            ?? step.OutFolder
            ?? step.Action.ToString();

    private static IReadOnlyList<string> GetRecipeSelectors(HtmlBrowserRecipeStep step) {
        List<string> selectors = new();
        AddRecipeSelector(selectors, step.Selector);
        foreach (string selector in step.SelectorAlternates) {
            AddRecipeSelector(selectors, selector);
        }

        return selectors;
    }

    private static void AddRecipeSelector(ICollection<string> selectors, string? selector) {
        if (string.IsNullOrWhiteSpace(selector)) {
            return;
        }

        string trimmed = selector!.Trim();
        if (!selectors.Contains(trimmed)) {
            selectors.Add(trimmed);
        }
    }

    private static void Require(string? value, string propertyName, HtmlBrowserRecipeAction action) {
        if (string.IsNullOrWhiteSpace(value)) {
            throw new ArgumentException($"{propertyName} is required for {action} recipe steps.", propertyName);
        }
    }

    private static string ResolveRecipeValue(HtmlBrowserRecipeStep step, HtmlBrowserRecipeRunOptions? options) {
        if (!string.IsNullOrWhiteSpace(step.ValueVariable) && options?.Variables.TryGetValue(step.ValueVariable!, out string? variableValue) == true) {
            return variableValue ?? string.Empty;
        }

        if (step.ValueRedacted == true) {
            RequireRecipeVariable(step);
        }

        return step.Value ?? string.Empty;
    }

    private static string[] ResolveRecipeValues(HtmlBrowserRecipeStep step, HtmlBrowserRecipeRunOptions? options) {
        if (!string.IsNullOrWhiteSpace(step.ValueVariable) && options?.Variables.TryGetValue(step.ValueVariable!, out string? variableValue) == true) {
            return new[] { variableValue ?? string.Empty };
        }

        if (step.ValueRedacted == true) {
            RequireRecipeVariable(step);
        }

        return step.Values.ToArray();
    }

    private static void RequireRecipeVariable(HtmlBrowserRecipeStep step) {
        string variableName = string.IsNullOrWhiteSpace(step.ValueVariable) ? "<missing>" : step.ValueVariable!;
        throw new InvalidOperationException($"Recipe step '{step.Action}' requires runtime variable '{variableName}' because the recorded value was redacted.");
    }

    private static HtmlBrowserEvidenceOptions CreateEvidenceOptions(HtmlBrowserRecipeStep step) {
        HtmlBrowserEvidenceOptions options = new() {
            BaseFileName = string.IsNullOrWhiteSpace(step.BaseFileName) ? "recipe" : step.BaseFileName!
        };

        if (HasExplicitEvidenceArtifactSelection(step)) {
            options.Screenshot = step.Screenshot == true;
            options.FullPageScreenshot = step.FullPageScreenshot == true;
            options.Pdf = step.Pdf == true;
            options.Html = step.Html == true;
            options.VisibleText = step.VisibleText == true;
            options.Markdown = step.Markdown == true;
            options.NetworkSummary = step.NetworkSummary == true;
            options.SsoHandoffSummary = step.SsoHandoffSummary == true;
        } else {
            options.NetworkSummary = true;
        }

        if (step.RedactSensitiveValues.HasValue) {
            options.RedactSensitiveValues = step.RedactSensitiveValues.Value;
        }

        if (step.MaskSensitiveScreenshotElements.HasValue) {
            options.MaskSensitiveScreenshotElements = step.MaskSensitiveScreenshotElements.Value;
        }

        foreach (string selector in step.ScreenshotMaskSelectors) {
            options.ScreenshotMaskSelectors.Add(selector);
        }

        options.ScreenshotMaskColor = step.ScreenshotMaskColor;

        if (step.Manifest.HasValue) {
            options.Manifest = step.Manifest.Value;
        }

        return options;
    }

    private static bool HasExplicitEvidenceArtifactSelection(HtmlBrowserRecipeStep step) =>
        step.Screenshot.HasValue
        || step.FullPageScreenshot.HasValue
        || step.Pdf.HasValue
        || step.Html.HasValue
        || step.VisibleText.HasValue
        || step.Markdown.HasValue
        || step.NetworkSummary.HasValue
        || step.SsoHandoffSummary.HasValue;

    private static JsonSerializerOptions CreateRecipeJsonOptions() {
        JsonSerializerOptions options = new() {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
