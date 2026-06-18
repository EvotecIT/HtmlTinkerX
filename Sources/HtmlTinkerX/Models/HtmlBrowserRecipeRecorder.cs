using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HtmlTinkerX;

/// <summary>
/// Captures successful HtmlTinkerX browser actions into a replayable <see cref="HtmlBrowserRecipe"/>.
/// </summary>
public sealed class HtmlBrowserRecipeRecorder {
    private readonly object _sync = new();

    /// <summary>Recipe being recorded.</summary>
    public HtmlBrowserRecipe Recipe { get; }

    /// <summary>Whether new steps are currently being recorded.</summary>
    public bool IsRecording { get; private set; } = true;

    /// <summary>Whether selector-based recorded steps should include stable alternate selectors.</summary>
    public bool CaptureSelectorAlternates { get; }

    /// <summary>Maximum selector alternates captured for each recorded selector-based step.</summary>
    public int SelectorAlternateLimit { get; }

    /// <summary>
    /// Creates a recorder around a recipe.
    /// </summary>
    public HtmlBrowserRecipeRecorder(HtmlBrowserRecipe recipe, bool captureSelectorAlternates = true, int selectorAlternateLimit = 5) {
        if (selectorAlternateLimit <= 0) {
            throw new ArgumentOutOfRangeException(nameof(selectorAlternateLimit), "SelectorAlternateLimit must be greater than zero.");
        }

        Recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
        CaptureSelectorAlternates = captureSelectorAlternates;
        SelectorAlternateLimit = selectorAlternateLimit;
    }

    /// <summary>
    /// Records one successful step when the recorder is active.
    /// </summary>
    public void Record(HtmlBrowserRecipeStep step) {
        if (step == null) {
            throw new ArgumentNullException(nameof(step));
        }

        lock (_sync) {
            if (!IsRecording) {
                return;
            }

            Recipe.Steps.Add(CloneStep(step));
        }
    }

    /// <summary>
    /// Stops recording and returns a snapshot of the captured recipe.
    /// </summary>
    public HtmlBrowserRecipe Stop() {
        lock (_sync) {
            IsRecording = false;
            return Snapshot();
        }
    }

    /// <summary>
    /// Returns a copy of the current recipe.
    /// </summary>
    public HtmlBrowserRecipe Snapshot() {
        lock (_sync) {
            HtmlBrowserRecipe copy = new() {
                SchemaVersion = Recipe.SchemaVersion,
                Name = Recipe.Name,
                StartUrl = Recipe.StartUrl,
                Browser = Recipe.Browser,
                Headless = Recipe.Headless,
                LoadState = Recipe.LoadState,
                Timeout = Recipe.Timeout,
                OnFailureEvidence = Recipe.OnFailureEvidence,
                FailureEvidenceFolder = Recipe.FailureEvidenceFolder
            };

            foreach (HtmlBrowserRecipeStep step in Recipe.Steps) {
                copy.Steps.Add(CloneStep(step));
            }

            return copy;
        }
    }

    private static HtmlBrowserRecipeStep CloneStep(HtmlBrowserRecipeStep step) {
        HtmlBrowserRecipeStep clone = new() {
            Name = step.Name,
            Action = step.Action,
            Url = step.Url,
            Selector = step.Selector,
            SelectorAlternates = new List<string>(step.SelectorAlternates),
            Text = step.Text,
            Value = step.Value,
            ValueRedacted = step.ValueRedacted,
            ValueRedactionReason = step.ValueRedactionReason,
            ValueVariable = step.ValueVariable,
            Values = new List<string>(step.Values),
            Keys = step.Keys,
            Script = step.Script,
            OutFile = step.OutFile,
            OutFolder = step.OutFolder,
            BaseFileName = step.BaseFileName,
            Screenshot = step.Screenshot,
            FullPageScreenshot = step.FullPageScreenshot,
            Pdf = step.Pdf,
            Html = step.Html,
            VisibleText = step.VisibleText,
            Markdown = step.Markdown,
            NetworkSummary = step.NetworkSummary,
            SsoHandoffSummary = step.SsoHandoffSummary,
            MaskSensitiveScreenshotElements = step.MaskSensitiveScreenshotElements,
            ScreenshotMaskSelectors = new List<string>(step.ScreenshotMaskSelectors),
            ScreenshotMaskColor = step.ScreenshotMaskColor,
            RedactSensitiveValues = step.RedactSensitiveValues,
            Manifest = step.Manifest,
            Exact = step.Exact,
            Nth = step.Nth,
            WaitForNavigation = step.WaitForNavigation,
            NavigationUrl = step.NavigationUrl,
            DelayMilliseconds = step.DelayMilliseconds,
            Milliseconds = step.Milliseconds,
            NoLoadState = step.NoLoadState,
            LoadState = step.LoadState,
            Stable = step.Stable,
            StableMilliseconds = step.StableMilliseconds,
            PollMilliseconds = step.PollMilliseconds,
            Timeout = step.Timeout,
            ContinueOnError = step.ContinueOnError,
            FullPage = step.FullPage,
            IncludeHidden = step.IncludeHidden,
            Limit = step.Limit,
            Checked = step.Checked
        };

        RedactSensitiveRecordedValues(clone);
        return clone;
    }

    private static void RedactSensitiveRecordedValues(HtmlBrowserRecipeStep step) {
        if (!IsValueRecordingAction(step.Action) || !IsSensitiveRecipeSelector(step.Selector)) {
            return;
        }

        if (!string.IsNullOrEmpty(step.Value)) {
            step.Value = "<redacted>";
            step.ValueRedacted = true;
            step.ValueVariable = CreateRecipeVariableName(step.Selector);
            step.ValueRedactionReason = "Recorded value redacted because the selector appears to target a sensitive field.";
        }

        if (step.Values.Count > 0 && step.Values.Any(static value => !string.IsNullOrEmpty(value))) {
            step.Values.Clear();
            step.Values.Add("<redacted>");
            step.ValueRedacted = true;
            step.ValueVariable = CreateRecipeVariableName(step.Selector);
            step.ValueRedactionReason = "Recorded values redacted because the selector appears to target a sensitive field.";
        }
    }

    private static bool IsValueRecordingAction(HtmlBrowserRecipeAction action) =>
        action == HtmlBrowserRecipeAction.Input
        || action == HtmlBrowserRecipeAction.TypeInput
        || action == HtmlBrowserRecipeAction.SelectOption;

    private static bool IsSensitiveRecipeSelector(string? selector) {
        if (string.IsNullOrWhiteSpace(selector)) {
            return false;
        }

        if (HtmlSensitiveValueRedactor.IsSensitiveName(selector!)) {
            return true;
        }

        return Regex.IsMatch(
            selector!,
            @"(?i)(?:\b|[#._\-\[\]'""=])(pwd|pin|passcode|otp|mfa|totp|samlresponse|samlrequest|relaystate)(?:\b|[#._\-\]\s'""=])",
            RegexOptions.CultureInvariant);
    }

    private static string CreateRecipeVariableName(string? selector) {
        string value = selector ?? "value";
        Match attributeMatch = Regex.Match(value, @"(?i)\[(?:name|id)\s*=\s*['""]?([^'""\]]+)");
        if (attributeMatch.Success) {
            value = attributeMatch.Groups[1].Value;
        } else if (value.StartsWith("#", StringComparison.Ordinal) || value.StartsWith(".", StringComparison.Ordinal)) {
            value = value.Substring(1);
        }

        value = Regex.Replace(value, @"[^A-Za-z0-9_]+", "_", RegexOptions.CultureInvariant).Trim('_');
        return string.IsNullOrWhiteSpace(value) ? "value" : value.ToLowerInvariant();
    }
}
