using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Provides validation helpers for microdata items against simple schema definitions.
/// </summary>
public static class MicrodataSchemaValidator {
    private static readonly Dictionary<string, HashSet<string>> Schema = new() {
        ["https://schema.org/Person"] = new(new[] { "name", "jobTitle", "url" }),
        ["https://schema.org/Product"] = new(new[] { "name", "description", "image", "brand", "sku" })
    };

    /// <summary>
    /// Compares parsed microdata items with built-in schema definitions and returns mismatched properties.
    /// </summary>
    /// <param name="items">Microdata items to validate.</param>
    /// <returns>List of mismatches found.</returns>
    /// <example>
    /// <code>
    /// var mismatches = MicrodataSchemaValidator.Validate(items);
    /// foreach (var mismatch in mismatches)
    ///     Console.WriteLine($"{mismatch.Type}: {string.Join(", ", mismatch.Properties)}");
    /// </code>
    /// </example>
    public static List<MicrodataSchemaMismatch> Validate(List<HtmlMicrodataItem> items) {
        List<MicrodataSchemaMismatch> mismatches = new();
        foreach (var item in items) {
            if (item.Type == null || !Schema.TryGetValue(item.Type, out var allowed)) {
                continue;
            }

            List<string> unknown = new();
            foreach (var property in item.Properties.Keys) {
                if (!allowed.Contains(property)) {
                    unknown.Add(property);
                }
            }

            if (unknown.Count > 0) {
                mismatches.Add(new MicrodataSchemaMismatch(item.Type, unknown));
            }
        }
        return mismatches;
    }
}
/// <summary>
/// Represents mismatched properties for a particular microdata item type.
/// </summary>
public sealed class MicrodataSchemaMismatch {
    /// <summary>
    /// Initializes a new instance of the <see cref="MicrodataSchemaMismatch"/> class.
    /// </summary>
    /// <param name="type">Microdata item type that contained unknown properties.</param>
    /// <param name="properties">The properties that were not defined in the schema.</param>
    public MicrodataSchemaMismatch(string type, List<string> properties) {
        Type = type;
        Properties = properties;
    }

    /// <summary>
    /// Gets the microdata item type that caused the mismatch.
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// Gets the list of properties that did not match the known schema.
    /// </summary>
    public List<string> Properties { get; }
}
