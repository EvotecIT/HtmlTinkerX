using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Browser recipe recording helpers.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Starts recording successful HtmlTinkerX browser actions on a session.
    /// </summary>
    public static HtmlBrowserRecipe StartRecipeRecording(
        HtmlBrowserSession session,
        string? name = null,
        string? startUrl = null,
        bool includeCurrentUrl = true,
        bool overwrite = false,
        int timeout = 10000,
        bool captureSelectorAlternates = true,
        int selectorAlternateLimit = 5) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        if (selectorAlternateLimit <= 0) {
            throw new ArgumentOutOfRangeException(nameof(selectorAlternateLimit), "SelectorAlternateLimit must be greater than zero.");
        }

        if (session.RecipeRecorder?.IsRecording == true && !overwrite) {
            throw new InvalidOperationException("A browser recipe recording is already active for this session.");
        }

        HtmlBrowserRecipe recipe = new() {
            Name = name ?? string.Empty,
            StartUrl = includeCurrentUrl ? FirstNonEmptyRecording(startUrl, session.Page.Url) : startUrl,
            Timeout = timeout
        };
        session.RecipeRecorder = new HtmlBrowserRecipeRecorder(recipe, captureSelectorAlternates, selectorAlternateLimit);
        return session.RecipeRecorder.Snapshot();
    }

    /// <summary>
    /// Stops recording browser actions and returns the captured recipe.
    /// </summary>
    public static HtmlBrowserRecipe StopRecipeRecording(HtmlBrowserSession session) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        HtmlBrowserRecipeRecorder recorder = session.RecipeRecorder
            ?? throw new InvalidOperationException("No browser recipe recording is attached to this session.");
        return recorder.Stop();
    }

    /// <summary>
    /// Returns the currently captured recipe without stopping recording.
    /// </summary>
    public static HtmlBrowserRecipe GetRecipeRecording(HtmlBrowserSession session) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        HtmlBrowserRecipeRecorder recorder = session.RecipeRecorder
            ?? throw new InvalidOperationException("No browser recipe recording is attached to this session.");
        return recorder.Snapshot();
    }

    /// <summary>
    /// Saves a browser recipe to JSON.
    /// </summary>
    public static async Task SaveRecipeAsync(HtmlBrowserRecipe recipe, string path, CancellationToken cancellationToken = default) {
        if (recipe == null) {
            throw new ArgumentNullException(nameof(recipe));
        }

        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Recipe path is required.", nameof(path));
        }

        string fullPath = path.ToFullPath();
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }

        string json = SerializeRecipe(recipe);
#if NETSTANDARD2_0 || NETFRAMEWORK
        File.WriteAllText(fullPath, json);
        await Task.CompletedTask.ConfigureAwait(false);
#else
        await File.WriteAllTextAsync(fullPath, json, cancellationToken).ConfigureAwait(false);
#endif
    }

    internal static void RecordRecipeStep(HtmlBrowserSession session, HtmlBrowserRecipeStep step) {
        if (session.SuppressRecipeRecording) {
            return;
        }

        session.RecipeRecorder?.Record(step);
    }

    internal static async Task RecordRecipeStepAsync(HtmlBrowserSession session, HtmlBrowserRecipeStep step, CancellationToken cancellationToken = default) {
        if (session.SuppressRecipeRecording) {
            return;
        }

        HtmlBrowserRecipeRecorder? recorder = session.RecipeRecorder;
        if (recorder == null) {
            return;
        }

        if (recorder.CaptureSelectorAlternates
            && step.SelectorAlternates.Count == 0
            && !string.IsNullOrWhiteSpace(step.Selector)) {
            try {
                IReadOnlyList<string> alternates = await FindSelectorAlternatesAsync(
                    session,
                    step.Selector!,
                    recorder.SelectorAlternateLimit,
                    cancellationToken).ConfigureAwait(false);
                step.SelectorAlternates.AddRange(alternates);
            } catch (Exception ex) when (ex is PlaywrightException || ex is ArgumentException || ex is InvalidOperationException) {
                // Selector alternate discovery is best-effort; recording the successful user action is more important.
            }
        }

        recorder.Record(step);
    }

    private static string FirstNonEmptyRecording(params string?[] values) {
        foreach (string? value in values) {
            if (!string.IsNullOrWhiteSpace(value)) {
                return value!.Trim();
            }
        }

        return string.Empty;
    }
}
