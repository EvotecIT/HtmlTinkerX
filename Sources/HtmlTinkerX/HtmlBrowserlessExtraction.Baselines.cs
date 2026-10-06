using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlBrowserlessExtraction {
    private const int MaximumBaselineShapes = 256;
    private const int MaximumShapeNodes = 4096;
    private const int MaximumShapeDepth = 32;
    // Object descriptors add containers around each source level; keep serialization
    // above that bounded logical depth without changing payload parser limits.
    private static readonly JsonSerializerOptions ShapeJsonOptions = new() { MaxDepth = MaximumShapeDepth * 4 };

    /// <summary>Returns a recipe copy with an accepted structured-data extraction baseline. No HTTP request is made.</summary>
    /// <param name="recipe">Recipe that produced the accepted result.</param>
    /// <param name="acceptedResult">Successful extraction whose structure has been inspected and accepted.</param>
    /// <returns>A detached recipe containing structure metadata. Existing raw-content settings are preserved.</returns>
    /// <remarks>Baselines accept up to 256 distinct item shapes, with at most 4096 nodes and depth 32 per item.</remarks>
    public static HtmlBrowserlessExtractionRecipe CaptureRecipeBaseline(
        HtmlBrowserlessExtractionRecipe recipe, HtmlBrowserlessExtractionResult acceptedResult) {
        if (recipe == null) throw new ArgumentNullException(nameof(recipe));
        if (IsDomRecipe(recipe)) {
            throw new ArgumentException("DOM baselines require the accepted HTML. Use CaptureDomRecipeBaseline.", nameof(recipe));
        }
        HtmlBrowserlessExtractionRecipe copy = DeserializeRecipe(SerializeRecipe(recipe));
        copy.Baseline = CreateExtractionBaseline(copy, acceptedResult);
        return copy;
    }

    /// <summary>Returns a DOM recipe copy with accepted item structure and collection confidence from complete structural evidence.</summary>
    /// <param name="recipe">DOM recipe to evaluate.</param>
    /// <param name="html">HTML whose extracted dataset has been inspected and accepted.</param>
    /// <returns>A detached recipe containing metadata only. Existing count and field rules remain in force.</returns>
    /// <remarks>Ordinary values and record ordering are ignored. Baselines accept up to 256 distinct shapes,
    /// with at most 4096 nodes and depth 32 per item. Capturing a new baseline is an explicit acceptance action.</remarks>
    public static HtmlBrowserlessExtractionRecipe CaptureDomRecipeBaseline(
        HtmlBrowserlessExtractionRecipe recipe, string html) {
        if (recipe == null) throw new ArgumentNullException(nameof(recipe));
        if (html == null) throw new ArgumentNullException(nameof(html));
        if (!IsDomRecipe(recipe)) throw new ArgumentException("Expected a DOM recipe.", nameof(recipe));
        HtmlBrowserlessExtractionRecipe copy = DeserializeRecipe(SerializeRecipe(recipe));
        copy.Baseline = null;
        copy.Baseline = CreateExtractionBaseline(copy, ExtractDomRecipe(copy, html));
        copy.Baseline.CollectionConfidence = HtmlDomExtraction.GetSelectorConfidence(html, copy.Selector);
        return copy;
    }

    private static HtmlExtractionBaseline CreateExtractionBaseline(
        HtmlBrowserlessExtractionRecipe recipe, HtmlBrowserlessExtractionResult result) {
        if (result == null) throw new ArgumentNullException(nameof(result));
        if (!result.Success || result.Items.Count == 0) {
            throw new ArgumentException("Only a successful non-empty extraction can be accepted as a baseline.", nameof(result));
        }
        if (!string.Equals(result.Source?.Kind, recipe.SourceKind, StringComparison.OrdinalIgnoreCase)) {
            throw new ArgumentException("The accepted extraction must have the recipe's source kind.", nameof(result));
        }
        if (result.ResponseShapeError != null) throw new InvalidDataException(result.ResponseShapeError);
        return new HtmlExtractionBaseline {
            SourceKind = recipe.SourceKind, ItemCount = result.Items.Count, ItemShapes = GetExtractionShapes(result),
            ResponseShape = result.ResponseShape
        };
    }

    private static async Task<HtmlBrowserlessExtractionResult> ExtractRecipeWithBaselineAsync(
        HtmlBrowserlessExtractionRecipe recipe, HtmlBrowserlessDataSource source,
        HtmlBrowserlessExtractionOptions? options, HttpClient? client, CancellationToken cancellationToken) {
        HtmlBrowserlessExtractionResult result = await ExtractAsync(source, options, client, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return ApplyRecipeBaseline(recipe, result, cancellationToken: cancellationToken);
    }

    private static HtmlBrowserlessExtractionResult ApplyRecipeBaseline(
        HtmlBrowserlessExtractionRecipe recipe, HtmlBrowserlessExtractionResult result, string? html = null,
        CancellationToken cancellationToken = default) {
        HtmlExtractionBaseline? baseline = recipe.Baseline;
        if (baseline == null || !result.Success) return result;
        string[] shapes;
        try {
            if (result.ResponseShapeError != null) throw new InvalidDataException(result.ResponseShapeError);
            shapes = GetExtractionShapes(result, cancellationToken);
        } catch (InvalidDataException error) {
            result.DriftReport = new HtmlExtractionDriftReport {
                BaselineItemCount = baseline.ItemCount, CurrentItemCount = result.Items.Count,
                BaselineConfidence = baseline.CollectionConfidence, ComparisonError = error.Message
            };
            result.Success = false;
            result.Warnings = result.Warnings.Concat(new[] { "Baseline comparison could not complete: " + error.Message }).ToArray();
            return result;
        }
        string? currentConfidence = baseline.CollectionConfidence == null ? null
            : HtmlDomExtraction.GetSelectorConfidence(html!, recipe.Selector);
        HtmlExtractionDriftReport drift = new() {
            BaselineItemCount = baseline.ItemCount, CurrentItemCount = result.Items.Count,
            AddedShapes = shapes.Except(baseline.ItemShapes, StringComparer.Ordinal).ToArray(),
            MissingShapes = baseline.ItemShapes.Except(shapes, StringComparer.Ordinal).ToArray(),
            ResponseShapeChanged = baseline.ResponseShape != null
                && !string.Equals(baseline.ResponseShape, result.ResponseShape, StringComparison.Ordinal),
            BaselineConfidence = baseline.CollectionConfidence, CurrentConfidence = currentConfidence,
            ConfidenceReduced = baseline.CollectionConfidence != null
                && ConfidenceRank(currentConfidence) < ConfidenceRank(baseline.CollectionConfidence)
        };
        result.DriftReport = drift;
        if (!drift.IsCompatible) {
            result.Success = false;
            result.Warnings = result.Warnings.Concat(new[] {
                "Extraction structure or collection confidence changed from the accepted baseline. Inspect DriftReport before accepting the dataset."
            }).ToArray();
        }
        return result;
    }

    private static void ValidateRecipeBaseline(HtmlBrowserlessExtractionRecipe recipe) {
        HtmlExtractionBaseline? baseline = recipe.Baseline;
        if (baseline == null) return;
        if (baseline.Version != 1 || baseline.ItemCount <= 0 || baseline.ItemShapes == null
            || baseline.ItemShapes.Count == 0 || baseline.ItemShapes.Count > MaximumBaselineShapes
            || baseline.ItemShapes.Any(string.IsNullOrWhiteSpace)
            || !string.Equals(baseline.SourceKind, recipe.SourceKind, StringComparison.OrdinalIgnoreCase)) {
            throw new ArgumentException("The recipe baseline must have a supported version, matching source kind and a non-empty bounded shape set.", nameof(recipe));
        }
        if (baseline.CollectionConfidence != null
            && (!IsDomRecipe(recipe) || ConfidenceRank(baseline.CollectionConfidence) == 0)) {
            throw new ArgumentException("Collection confidence is High, Medium or Low and applies only to DOM recipes.", nameof(recipe));
        }
    }

    private static int ConfidenceRank(string? value) => value switch {
        "High" => 3, "Medium" => 2, "Low" => 1, _ => 0
    };

    private static string[] GetExtractionShapes(HtmlBrowserlessExtractionResult result, CancellationToken cancellationToken = default) {
        HashSet<string> shapes = new(StringComparer.Ordinal);
        ILookup<int, HtmlDomFieldDiagnostic>? domFields = result.DomReport?.Fields.ToLookup(static field => field.ItemIndex);
        foreach (HtmlBrowserlessExtractionItem item in result.Items) {
            cancellationToken.ThrowIfCancellationRequested();
            int nodes = 0;
            object shape = DescribeValueShape(item.Value, 0, ref nodes);
            string path = Regex.Replace(item.Path, @"\[\d+\]", "[]");
            object? fields = domFields?[item.Index].OrderBy(static field => field.PropertyName, StringComparer.Ordinal)
                .Select(static field => new { field.PropertyName, field.MatchCount, field.ValueCount, status = field.Status.ToString() }).ToArray();
            shapes.Add(JsonSerializer.Serialize(new { path, shape, fields }, ShapeJsonOptions));
            if (shapes.Count > MaximumBaselineShapes) {
                throw new InvalidDataException($"Extraction has more than {MaximumBaselineShapes} distinct item shapes.");
            }
        }
        return shapes.OrderBy(static shape => shape, StringComparer.Ordinal).ToArray();
    }

    private static object DescribeValueShape(object? value, int depth, ref int nodes) {
        if (++nodes > MaximumShapeNodes || depth > MaximumShapeDepth) {
            throw new InvalidDataException($"Extraction item shape exceeds {MaximumShapeNodes} nodes or depth {MaximumShapeDepth}.");
        }
        if (value == null) return "null";
        if (value is string || value is char) return "string";
        if (value is bool) return "boolean";
        if (value is DateTime || value is DateTimeOffset) return "date-time";
        if (value.GetType().IsEnum) return "enum:" + value.GetType().FullName;
        if (value is byte || value is sbyte || value is short || value is ushort || value is int || value is uint
            || value is long || value is ulong || value is float || value is double || value is decimal) return "number";
        if (value is IReadOnlyDictionary<string, object?> dictionary) {
            SortedDictionary<string, object> fields = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object?> field in dictionary) {
                fields[field.Key] = DescribeValueShape(field.Value, depth + 1, ref nodes);
            }
            return new { kind = "object", fields };
        }
        if (value is IEnumerable sequence) {
            SortedSet<string> elements = new(StringComparer.Ordinal);
            foreach (object? element in sequence) {
                elements.Add(JsonSerializer.Serialize(DescribeValueShape(element, depth + 1, ref nodes), ShapeJsonOptions));
            }
            return new { kind = "array", elements };
        }
        throw new ArgumentException("Extraction baselines support normalized scalar, dictionary and list values.", nameof(value));
    }
}
