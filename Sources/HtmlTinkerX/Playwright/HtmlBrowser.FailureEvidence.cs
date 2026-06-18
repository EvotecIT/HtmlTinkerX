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
/// Helper methods for exporting browser evidence after automation failures.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Exports a diagnostic evidence bundle for the current browser page after an operation fails.
    /// </summary>
    /// <param name="session">Browser session whose current page should be captured.</param>
    /// <param name="exception">Exception that caused the failure.</param>
    /// <param name="options">Failure evidence options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Evidence result describing produced files.</returns>
    public static async Task<HtmlBrowserEvidenceResult> ExportFailureEvidenceAsync(
        HtmlBrowserSession session,
        Exception exception,
        HtmlBrowserFailureEvidenceOptions? options = null,
        CancellationToken cancellationToken = default) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        if (exception == null) {
            throw new ArgumentNullException(nameof(exception));
        }

        options ??= new HtmlBrowserFailureEvidenceOptions();
        string operation = GetSafeBaseFileName(options.Operation);
        string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        string root = string.IsNullOrWhiteSpace(options.OutFolder)
            ? "HtmlBrowserFailureEvidence"
            : options.OutFolder;
        string outFolder = Path.Combine(root.ToFullPath(), $"{timestamp}-{operation}");

        HtmlBrowserEvidenceOptions evidenceOptions = new() {
            BaseFileName = GetSafeBaseFileName(options.BaseFileName),
            Screenshot = options.Screenshot,
            FullPageScreenshot = options.FullPageScreenshot,
            Pdf = false,
            Html = options.Html,
            VisibleText = options.VisibleText,
            Markdown = options.Markdown,
            NetworkSummary = options.NetworkSummary,
            Manifest = false
        };

        bool previousRecordingSuppression = session.SuppressRecipeRecording;
        session.SuppressRecipeRecording = true;
        try {
            HtmlBrowserEvidenceResult result = await ExportEvidenceAsync(session, outFolder, evidenceOptions, cancellationToken).ConfigureAwait(false);

            result.Purpose = "FailureEvidence";
            result.Operation = options.Operation;
            result.ErrorType = exception.GetType().FullName;
            result.ErrorMessage = HtmlSensitiveValueRedactor.RedactSensitiveEvidenceText(exception.Message);

            if (options.LocatorSuggestions) {
                string locatorPath = Path.Combine(result.OutFolder, "locator-suggestions.json");
                (object LocatorPayload, int Count) locatorSuggestions = await CreateFailureLocatorSuggestionsAsync(
                    session,
                    options.LocatorSuggestionLimit,
                    cancellationToken).ConfigureAwait(false);
                result.LocatorSuggestionCount = locatorSuggestions.Count;
                string locatorJson = JsonSerializer.Serialize(locatorSuggestions.LocatorPayload, CreateJsonOptions());
                await WriteTextAsync(locatorPath, locatorJson, cancellationToken).ConfigureAwait(false);
                AddArtifact(result, result.OutFolder, "LocatorSuggestions", locatorPath, "application/json; charset=utf-8");
            }

            string contextPath = Path.Combine(result.OutFolder, "failure-context.json");
            string contextJson = JsonSerializer.Serialize(new {
                result.Purpose,
                result.Operation,
                result.ErrorType,
                result.ErrorMessage,
                result.Url,
                result.FinalUrl,
                result.Title,
                result.CapturedAtUtc
            }, CreateJsonOptions());
            await WriteTextAsync(contextPath, contextJson, cancellationToken).ConfigureAwait(false);
            AddArtifact(result, result.OutFolder, "FailureContext", contextPath, "application/json; charset=utf-8");

            if (options.Manifest) {
                string manifestPath = Path.Combine(result.OutFolder, "evidence-manifest.json");
                result.ManifestPath = manifestPath;
                string manifestJson = JsonSerializer.Serialize(result, CreateJsonOptions());
                await WriteTextAsync(manifestPath, manifestJson, cancellationToken).ConfigureAwait(false);
            }

            return result;
        } finally {
            session.SuppressRecipeRecording = previousRecordingSuppression;
        }
    }

    private static async Task<(object LocatorPayload, int Count)> CreateFailureLocatorSuggestionsAsync(
        HtmlBrowserSession session,
        int limit,
        CancellationToken cancellationToken) {
        int effectiveLimit = limit <= 0 ? 10 : limit;
        try {
            IReadOnlyList<HtmlBrowserLocatorCandidate> candidates = await FindLocatorCandidatesAsync(
                session,
                query: null,
                visibleOnly: true,
                limit: effectiveLimit,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return (new {
                Count = candidates.Count,
                Redacted = true,
                Guidance = "Review TestCommand first, then copy SuggestedCommand only when it targets the intended visible control.",
                Candidates = candidates.Select(SanitizeFailureLocatorCandidate).ToArray()
            }, candidates.Count);
        } catch (Exception ex) when (ex is InvalidOperationException || ex is JsonException || ex is PlaywrightException) {
            return (new {
                Count = 0,
                Redacted = true,
                ErrorMessage = HtmlSensitiveValueRedactor.RedactSensitiveEvidenceText(ex.Message),
                Guidance = "Locator suggestions could not be collected from the failed page."
            }, 0);
        }
    }

    private static object SanitizeFailureLocatorCandidate(HtmlBrowserLocatorCandidate candidate) {
        string selector = HtmlSensitiveValueRedactor.RedactSensitiveEvidenceText(
            HtmlSensitiveValueRedactor.RedactSensitiveQueryValues(candidate.Selector));
        string locator = HtmlSensitiveValueRedactor.RedactSensitiveEvidenceText(
            HtmlSensitiveValueRedactor.RedactSensitiveQueryValues(candidate.Locator));
        string text = HtmlSensitiveValueRedactor.RedactSensitiveEvidenceText(candidate.Text);
        string suggestedCommand = HtmlSensitiveValueRedactor.RedactSensitiveEvidenceText(
            HtmlSensitiveValueRedactor.RedactSensitiveQueryValues(candidate.SuggestedCommand));
        string testCommand = HtmlSensitiveValueRedactor.RedactSensitiveEvidenceText(
            HtmlSensitiveValueRedactor.RedactSensitiveQueryValues(candidate.TestCommand));

        return new {
            candidate.Index,
            candidate.Strategy,
            Selector = selector,
            Locator = locator,
            candidate.Score,
            candidate.Reason,
            Text = text,
            candidate.Tag,
            candidate.Visible,
            candidate.Enabled,
            candidate.Editable,
            candidate.InViewport,
            candidate.SuggestedAction,
            SuggestedCommand = suggestedCommand,
            TestCommand = testCommand,
            Warnings = candidate.Warnings
        };
    }
}
