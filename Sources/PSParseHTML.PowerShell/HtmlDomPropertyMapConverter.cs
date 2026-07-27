using HtmlTinkerX;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Converts PowerShell-friendly selector maps into the shared HtmlTinkerX DOM extraction model.
/// </summary>
internal static class HtmlDomPropertyMapConverter {
    internal static IReadOnlyDictionary<string, HtmlDomFieldDefinition> Convert(IDictionary properties) {
        if (properties == null || properties.Count == 0) {
            throw new PSArgumentException("Property must contain at least one selector definition.");
        }

        Dictionary<string, HtmlDomFieldDefinition> definitions = new(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in properties) {
            string name = entry.Key?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name)) {
                throw new PSArgumentException("Property names cannot be empty.");
            }

            object? value = entry.Value == null ? null : HtmlPipelineInput.Unwrap(entry.Value);
            definitions[name] = value switch {
                string selector => new HtmlDomFieldDefinition { Selector = selector },
                HtmlDomFieldDefinition definition => definition,
                IDictionary map => ConvertDefinition(name, map),
                _ => throw new PSArgumentException(
                    $"Property '{name}' must be a CSS selector string, hashtable, or HtmlDomFieldDefinition.")
            };
        }

        return definitions;
    }

    private static HtmlDomFieldDefinition ConvertDefinition(string propertyName, IDictionary map) {
        HtmlDomFieldDefinition definition = new() {
            Selector = GetString(map, "Selector") ?? string.Empty,
            Attribute = GetString(map, "Attribute"),
            ValueKind = GetString(map, "ValueKind") ?? GetString(map, "Value") ?? "Text",
            All = GetBoolean(map, "All"),
            Required = GetBoolean(map, "Required"),
            ResolveUrl = GetBoolean(map, "ResolveUrl")
        };

        if (TryGetValue(map, "DefaultValue", out object? defaultValue)
            || TryGetValue(map, "Default", out defaultValue)) {
            definition.DefaultValue = defaultValue;
        }

        if (string.IsNullOrWhiteSpace(definition.Selector)
            && !GetBoolean(map, "Self")) {
            throw new PSArgumentException(
                $"Property '{propertyName}' must define Selector or set Self to true.");
        }

        return definition;
    }

    private static string? GetString(IDictionary map, string name) =>
        TryGetValue(map, name, out object? value) && value != null
            ? value.ToString()
            : null;

    private static bool GetBoolean(IDictionary map, string name) {
        if (!TryGetValue(map, name, out object? value) || value == null) {
            return false;
        }

        if (value is bool boolean) {
            return boolean;
        }

        return LanguagePrimitives.IsTrue(value);
    }

    private static bool TryGetValue(IDictionary map, string name, out object? value) {
        foreach (DictionaryEntry entry in map) {
            if (entry.Key != null
                && string.Equals(entry.Key.ToString(), name, StringComparison.OrdinalIgnoreCase)) {
                value = entry.Value;
                return true;
            }
        }

        value = null;
        return false;
    }
}
