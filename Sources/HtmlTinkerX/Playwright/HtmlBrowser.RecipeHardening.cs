using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Browser recipe hardening helpers.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Adds safe selector alternates to selector-based recipe steps by inspecting the current page.
    /// </summary>
    /// <param name="session">Browser session whose current page matches the recipe state to harden.</param>
    /// <param name="recipe">Recipe to update in-place.</param>
    /// <param name="limit">Maximum selector alternates to keep per step.</param>
    /// <param name="replaceExisting">Replace existing alternates instead of only filling missing entries.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Hardening report containing the updated recipe and per-step details.</returns>
    public static async Task<HtmlBrowserRecipeHardeningResult> HardenRecipeSelectorsAsync(
        HtmlBrowserSession session,
        HtmlBrowserRecipe recipe,
        int limit = 5,
        bool replaceExisting = false,
        CancellationToken cancellationToken = default) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        if (recipe == null) {
            throw new ArgumentNullException(nameof(recipe));
        }

        if (limit <= 0) {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than zero.");
        }

        HtmlBrowserRecipeHardeningResult result = new() {
            Name = recipe.Name,
            Recipe = recipe,
            StepCount = recipe.Steps.Count
        };

        for (int index = 0; index < recipe.Steps.Count; index++) {
            cancellationToken.ThrowIfCancellationRequested();
            HtmlBrowserRecipeStep step = recipe.Steps[index];
            HtmlBrowserRecipeHardeningStepResult stepResult = await HardenRecipeStepSelectorsAsync(
                session,
                step,
                index,
                limit,
                replaceExisting,
                cancellationToken).ConfigureAwait(false);
            result.Steps.Add(stepResult);
        }

        return result;
    }

    /// <summary>
    /// Creates a redacted JSON-safe report from a recipe selector hardening result.
    /// </summary>
    /// <param name="result">Hardening result to summarize.</param>
    /// <returns>Redacted report suitable for CI artifacts and review evidence.</returns>
    public static HtmlBrowserRecipeHardeningReport CreateRecipeHardeningReport(HtmlBrowserRecipeHardeningResult result) {
        if (result == null) {
            throw new ArgumentNullException(nameof(result));
        }

        HtmlBrowserRecipeHardeningReport report = new() {
            Name = RedactHardeningReportValue(result.Name),
            StepCount = result.StepCount,
            EligibleStepCount = result.EligibleStepCount,
            ChangedStepCount = result.ChangedStepCount,
            AddedAlternateCount = result.AddedAlternateCount,
            Changed = result.Changed,
            Summary = RedactHardeningReportValue(result.Summary)
        };

        foreach (HtmlBrowserRecipeHardeningStepResult step in result.Steps) {
            report.Steps.Add(new HtmlBrowserRecipeHardeningReportStep {
                StepIndex = step.StepIndex,
                StepName = RedactHardeningReportValue(step.StepName),
                Action = step.Action,
                Selector = RedactHardeningReportValue(step.Selector),
                Eligible = step.Eligible,
                Changed = step.Changed,
                AddedAlternates = step.AddedAlternates.Select(RedactHardeningReportValue).ToList(),
                ExistingAlternates = step.ExistingAlternates.Select(RedactHardeningReportValue).ToList(),
                Reason = RedactHardeningReportValue(step.Reason),
                SuggestedCommand = RedactHardeningReportValue(step.SuggestedCommand)
            });
        }

        return report;
    }

    /// <summary>
    /// Serializes a redacted hardening report to JSON.
    /// </summary>
    /// <param name="result">Hardening result to summarize.</param>
    /// <returns>Indented JSON report text.</returns>
    public static string SerializeRecipeHardeningReport(HtmlBrowserRecipeHardeningResult result) =>
        JsonSerializer.Serialize(CreateRecipeHardeningReport(result), CreateRecipeJsonOptions());

    /// <summary>
    /// Writes a redacted hardening report to JSON.
    /// </summary>
    /// <param name="result">Hardening result to summarize.</param>
    /// <param name="path">Report output path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resolved report path.</returns>
    public static async Task<string> SaveRecipeHardeningReportAsync(
        HtmlBrowserRecipeHardeningResult result,
        string path,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Report path is required.", nameof(path));
        }

        string fullPath = path.ToFullPath();
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }

        string json = SerializeRecipeHardeningReport(result);
#if NETSTANDARD2_0 || NETFRAMEWORK
        File.WriteAllText(fullPath, json);
        await Task.CompletedTask.ConfigureAwait(false);
#else
        await File.WriteAllTextAsync(fullPath, json, cancellationToken).ConfigureAwait(false);
#endif
        result.ReportPath = fullPath;
        return fullPath;
    }

    private static async Task<HtmlBrowserRecipeHardeningStepResult> HardenRecipeStepSelectorsAsync(
        HtmlBrowserSession session,
        HtmlBrowserRecipeStep step,
        int index,
        int limit,
        bool replaceExisting,
        CancellationToken cancellationToken) {
        string selector = step.Selector ?? string.Empty;
        HtmlBrowserRecipeHardeningStepResult result = new() {
            StepIndex = index,
            StepName = step.Name,
            Action = step.Action,
            Selector = selector,
            ExistingAlternates = new List<string>(step.SelectorAlternates),
            SuggestedCommand = BuildHardeningSuggestedCommand(step)
        };

        if (!IsSelectorHardeningAction(step.Action)) {
            result.Reason = "Step action does not use a CSS selector.";
            return result;
        }

        if (string.IsNullOrWhiteSpace(selector)) {
            result.Eligible = true;
            result.Reason = "Step has no primary selector to analyze.";
            return result;
        }

        if (SelectorContainsSensitiveValue(selector)) {
            result.Eligible = true;
            result.Reason = "Primary selector appears to contain sensitive values; automatic alternate capture was skipped.";
            result.SuggestedCommand = "Get-HtmlBrowserInteractable -Session $session | Select-Object -First 20";
            return result;
        }

        if (!replaceExisting && step.SelectorAlternates.Count >= limit) {
            result.Eligible = true;
            result.Reason = "Step already has the requested number of selector alternates.";
            return result;
        }

        result.Eligible = true;
        IReadOnlyList<string> alternates;
        try {
            alternates = await FindSelectorAlternatesAsync(session, selector, limit, cancellationToken).ConfigureAwait(false);
        } catch (Exception ex) when (ex is PlaywrightException || ex is ArgumentException || ex is InvalidOperationException) {
            result.Reason = $"Could not inspect selector on the current page: {ex.Message}";
            return result;
        }

        if (replaceExisting) {
            step.SelectorAlternates.Clear();
        }

        HashSet<string> knownSelectors = new(step.SelectorAlternates, StringComparer.Ordinal);
        foreach (string alternate in alternates) {
            if (knownSelectors.Count >= limit) {
                break;
            }

            if (!knownSelectors.Add(alternate)) {
                continue;
            }

            step.SelectorAlternates.Add(alternate);
            result.AddedAlternates.Add(alternate);
        }

        result.Changed = result.AddedAlternates.Count > 0 || (replaceExisting && !SequenceEqual(result.ExistingAlternates, step.SelectorAlternates));
        result.Reason = result.Changed
            ? "Selector alternates were updated from the current page."
            : "No new safe selector alternates were found on the current page.";
        return result;
    }

    private static bool IsSelectorHardeningAction(HtmlBrowserRecipeAction action) =>
        action == HtmlBrowserRecipeAction.Click
        || action == HtmlBrowserRecipeAction.Input
        || action == HtmlBrowserRecipeAction.TypeInput
        || action == HtmlBrowserRecipeAction.SetChecked
        || action == HtmlBrowserRecipeAction.SelectOption
        || action == HtmlBrowserRecipeAction.Key
        || action == HtmlBrowserRecipeAction.WaitReady
        || action == HtmlBrowserRecipeAction.WaitText;

    private static string BuildHardeningSuggestedCommand(HtmlBrowserRecipeStep step) {
        if (!string.IsNullOrWhiteSpace(step.Text)) {
            return $"Find-HtmlBrowserLocator -Session $session -Query '{EscapePowerShellSingleQuotedString(step.Text!)}' -Limit 10";
        }

        return string.IsNullOrWhiteSpace(step.Selector)
            ? "Get-HtmlBrowserInteractable -Session $session | Select-Object -First 20"
            : BuildSelectorCommand("Find-HtmlBrowserLocator -Session $session -Query", step.Selector!, " -Limit 10");
    }

    private static bool SequenceEqual(IReadOnlyList<string> left, IReadOnlyList<string> right) =>
        left.Count == right.Count && left.SequenceEqual(right, StringComparer.Ordinal);

    private static string RedactHardeningReportValue(string? value) =>
        HtmlSensitiveValueRedactor.RedactSensitiveEvidenceText(
            HtmlSensitiveValueRedactor.RedactSensitiveQueryValues(value ?? string.Empty));
}
