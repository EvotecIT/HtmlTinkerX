using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Browser recipe variable template and variable-file helpers.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Creates a hashtable-style variable template from a recipe.
    /// </summary>
    /// <param name="recipe">Recipe whose variable requirements should be inspected.</param>
    /// <param name="requiredOnly">When true, include only variables required for replay.</param>
    /// <returns>Variable names and placeholder values suitable for JSON template output.</returns>
    public static Dictionary<string, string> CreateRecipeVariableTemplate(HtmlBrowserRecipe recipe, bool requiredOnly = true) {
        if (recipe == null) {
            throw new ArgumentNullException(nameof(recipe));
        }

        HtmlBrowserRecipeValidationResult validation = ValidateRecipe(recipe, runtimeVariables: null, assumeExistingSession: true);
        Dictionary<string, string> template = new(StringComparer.OrdinalIgnoreCase);
        foreach (HtmlBrowserRecipeVariableRequirement variable in validation.Variables) {
            if (requiredOnly && !variable.Required) {
                continue;
            }

            template[variable.Name] = variable.Placeholder;
        }

        return template;
    }

    /// <summary>
    /// Saves a recipe variable template as JSON.
    /// </summary>
    /// <param name="recipe">Recipe whose variable requirements should be inspected.</param>
    /// <param name="path">Output JSON path.</param>
    /// <param name="requiredOnly">When true, include only variables required for replay.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Full output path.</returns>
    public static async Task<string> SaveRecipeVariableTemplateAsync(
        HtmlBrowserRecipe recipe,
        string path,
        bool requiredOnly = true,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Variable template path cannot be empty.", nameof(path));
        }

        string fullPath = path.ToFullPath();
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }

        Dictionary<string, string> template = CreateRecipeVariableTemplate(recipe, requiredOnly);
        string json = JsonSerializer.Serialize(template, CreateRecipeJsonOptions());
        await Task.Run(() => File.WriteAllText(fullPath, json), cancellationToken).ConfigureAwait(false);
        return fullPath;
    }

    /// <summary>
    /// Loads runtime variables from a JSON object file.
    /// </summary>
    /// <param name="path">JSON file containing variable names and scalar values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Variable names and values, excluding empty and placeholder template values.</returns>
    public static async Task<Dictionary<string, string>> LoadRecipeVariablesAsync(string path, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Variable path cannot be empty.", nameof(path));
        }

        string fullPath = path.ToFullPath();
        string json = await Task.Run(() => File.ReadAllText(fullPath), cancellationToken).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object) {
            throw new InvalidDataException("Recipe variable file must contain a JSON object.");
        }

        Dictionary<string, string> variables = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty property in document.RootElement.EnumerateObject()) {
            if (string.IsNullOrWhiteSpace(property.Name)) {
                continue;
            }

            string value = ConvertRecipeVariableJsonValue(property.Value);
            if (IsRecipeVariableTemplatePlaceholder(value)) {
                continue;
            }

            variables[property.Name] = value;
        }

        return variables;
    }

    /// <summary>
    /// Identifies placeholder values emitted by recipe variable templates.
    /// </summary>
    /// <param name="value">Variable value to inspect.</param>
    /// <returns>True when the value should not be treated as a supplied runtime value.</returns>
    public static bool IsRecipeVariableTemplatePlaceholder(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return true;
        }

        string trimmed = (value ?? string.Empty).Trim();
        return string.Equals(trimmed, "<value>", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "<secret>", StringComparison.OrdinalIgnoreCase);
    }

    private static string ConvertRecipeVariableJsonValue(JsonElement value) =>
        value.ValueKind switch {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            JsonValueKind.Null => string.Empty,
            _ => throw new InvalidDataException("Recipe variable file values must be strings, numbers, booleans, or null.")
        };
}
