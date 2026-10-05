using System;
using System.Collections.Generic;
using System.Linq;

namespace HtmlTinkerX;

public static partial class HtmlBrowserlessExtraction {
    /// <summary>Creates a saved DOM extraction recipe using the shared selector and quality rules.</summary>
    /// <param name="itemSelector">CSS selector matching repeated items.</param>
    /// <param name="properties">Field definitions evaluated relative to each item.</param>
    /// <param name="options">Optional acceptable item-count bounds.</param>
    /// <param name="baseUri">Optional page URL used to resolve relative field URLs.</param>
    /// <returns>A recipe that can be serialized and evaluated against fresh HTML.</returns>
    public static HtmlBrowserlessExtractionRecipe CreateDomRecipe(
        string itemSelector, IReadOnlyDictionary<string, HtmlDomFieldDefinition> properties,
        HtmlDomExtractionReportOptions? options = null, Uri? baseUri = null) {
        if (properties == null) {
            throw new ArgumentNullException(nameof(properties));
        }
        if (baseUri != null && !baseUri.IsAbsoluteUri) {
            throw new ArgumentException("DOM recipe base URI must be absolute.", nameof(baseUri));
        }
        HtmlBrowserlessExtractionRecipe recipe = new() {
            SourceKind = "Dom", Selector = itemSelector, PageUrl = baseUri?.AbsoluteUri ?? string.Empty,
            DomProperties = properties.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            MinimumItemCount = options?.MinimumItemCount, MaximumItemCount = options?.MaximumItemCount
        };
        // The serialization boundary snapshots caller-owned fields and preserves scalar default types.
        return DeserializeRecipe(SerializeRecipe(recipe));
    }

    /// <summary>
    /// Evaluates a DOM recipe against current HTML. No HTTP request is performed. Success requires
    /// at least one item and all declared quality checks; inspect DomReport for partial records and failures.
    /// </summary>
    /// <param name="recipe">Saved DOM recipe.</param>
    /// <param name="html">Current HTML markup to evaluate.</param>
    /// <param name="includeRawContent">Include the supplied HTML in the result when explicitly requested.</param>
    /// <returns>Normalized items and the shared field quality report.</returns>
    public static HtmlBrowserlessExtractionResult ExtractDomRecipe(
        HtmlBrowserlessExtractionRecipe recipe, string html, bool includeRawContent = false) {
        if (recipe == null) {
            throw new ArgumentNullException(nameof(recipe));
        }
        if (!IsDomRecipe(recipe)) {
            throw new ArgumentException("Current HTML can only be supplied for a DOM recipe.", nameof(recipe));
        }
        ValidateDomRecipeConfiguration(recipe);
        HtmlDomExtractionReport report = HtmlDomExtraction.ExtractReport(html, recipe.Selector,
            recipe.DomProperties!, new HtmlDomExtractionReportOptions {
                MinimumItemCount = recipe.MinimumItemCount, MaximumItemCount = recipe.MaximumItemCount
            }, GetDomRecipeBaseUri(recipe));
        return new HtmlBrowserlessExtractionResult {
            Source = CreateSourceFromRecipe(recipe), DomReport = report,
            Success = report.ItemCount > 0 && report.IsValid, ContentType = "text/html",
            RawContent = includeRawContent ? html : string.Empty,
            Items = report.Records.Select(record => new HtmlBrowserlessExtractionItem {
                Index = record.Index, Kind = "Dom", Path = $"$[{record.Index}]", Value = record.Values
            }).ToArray(),
            Evidence = new[] { $"Evaluated {report.ItemCount} DOM item(s); {report.InvalidFieldCount} field check(s) failed." },
            Warnings = report.IsValid ? Array.Empty<string>()
                : new[] { "DOM extraction did not satisfy the saved field or item-count rules. Inspect DomReport before accepting the dataset." }
        };
    }

    private static bool IsDomRecipe(HtmlBrowserlessExtractionRecipe recipe) =>
        string.Equals(recipe.SourceKind, "Dom", StringComparison.OrdinalIgnoreCase);

    private static void ValidateDomRecipe(HtmlBrowserlessExtractionRecipe recipe) {
        if (IsDomRecipe(recipe)) {
            ValidateDomRecipeConfiguration(recipe);
            HtmlDomExtraction.ExtractReport(string.Empty, recipe.Selector, recipe.DomProperties!,
                new HtmlDomExtractionReportOptions {
                    MinimumItemCount = recipe.MinimumItemCount, MaximumItemCount = recipe.MaximumItemCount
                }, GetDomRecipeBaseUri(recipe));
        } else if (recipe.DomProperties != null || recipe.MinimumItemCount.HasValue || recipe.MaximumItemCount.HasValue) {
            throw new ArgumentException("DOM fields and count bounds require SourceKind Dom.", nameof(recipe));
        }
    }

    private static void ValidateDomRecipeConfiguration(HtmlBrowserlessExtractionRecipe recipe) {
        if (recipe.Version != 1) {
            throw new ArgumentException("Unsupported DOM recipe version. Supported version is 1.", nameof(recipe));
        }
        if (recipe.DomProperties != null
            && recipe.DomProperties.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != recipe.DomProperties.Count) {
            throw new ArgumentException("DOM recipe property names must be unique, ignoring case.", nameof(recipe));
        }
    }

    private static Uri? GetDomRecipeBaseUri(HtmlBrowserlessExtractionRecipe recipe) {
        if (string.IsNullOrWhiteSpace(recipe.PageUrl)) {
            return null;
        }
        if (Uri.TryCreate(recipe.PageUrl, UriKind.Absolute, out Uri? baseUri)) {
            return baseUri;
        }
        throw new ArgumentException("DOM recipe PageUrl must be an absolute URI when supplied.", nameof(recipe));
    }
}