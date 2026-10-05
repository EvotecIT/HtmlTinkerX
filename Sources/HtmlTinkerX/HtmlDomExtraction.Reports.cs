using AngleSharp.Dom;
using System;
using System.Collections.Generic;

namespace HtmlTinkerX;

public static partial class HtmlDomExtraction {
    /// <summary>
    /// Extracts repeated records with field provenance and quality checks. Missing required
    /// fields, count violations, and conversion failures are reported without stopping other
    /// fields. Invalid configuration and selectors still throw.
    /// </summary>
    /// <param name="html">HTML markup to parse.</param>
    /// <param name="itemSelector">CSS selector matching repeated items.</param>
    /// <param name="properties">Field definitions evaluated relative to each item.</param>
    /// <param name="options">Optional expected item-count bounds.</param>
    /// <param name="baseUri">Optional page URL used to resolve relative URL attributes.</param>
    /// <returns>Records and diagnostics; inspect IsValid before accepting the dataset.</returns>
    public static HtmlDomExtractionReport ExtractReport(
        string html, string itemSelector, IReadOnlyDictionary<string, HtmlDomFieldDefinition> properties,
        HtmlDomExtractionReportOptions? options = null, Uri? baseUri = null) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }
        if (string.IsNullOrWhiteSpace(itemSelector)) {
            throw new ArgumentException("Item selector cannot be empty.", nameof(itemSelector));
        }
        if (properties == null || properties.Count == 0) {
            throw new ArgumentException("At least one property definition is required.", nameof(properties));
        }
        ValidateCountBounds(options?.MinimumItemCount, options?.MaximumItemCount, "items");
        foreach (KeyValuePair<string, HtmlDomFieldDefinition> property in properties) {
            if (string.IsNullOrWhiteSpace(property.Key) || property.Value == null) {
                throw new ArgumentException("Each property must have a name and a field definition.", nameof(properties));
            }
            ValidateFieldConversion(property.Value, property.Key);
            ValidateCountBounds(property.Value.MinimumValueCount, property.Value.MaximumValueCount, property.Key);
        }

        using IDocument document = HtmlParser.ParseWithAngleSharp(html);
        IElement selectorContext = document.CreateElement("div");
        foreach (KeyValuePair<string, HtmlDomFieldDefinition> property in properties) {
            ReadFieldValues(selectorContext, property.Value, property.Key, null, out _);
        }
        Uri? effectiveBaseUri = GetEffectiveBaseUri(document, baseUri);
        IHtmlCollection<IElement> items = document.QuerySelectorAll(itemSelector);
        List<HtmlDomExtractionRecord> records = new(items.Length);
        List<HtmlDomFieldDiagnostic> fields = new();
        for (int index = 0; index < items.Length; index++) {
            Dictionary<string, object?> values = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, HtmlDomFieldDefinition> property in properties) {
                HtmlDomFieldDefinition definition = property.Value;
                object?[] selected = ReadFieldValues(items[index], definition, property.Key, effectiveBaseUri, out int matchCount);
                HtmlDomFieldDiagnostic field = new() {
                    ItemIndex = index, PropertyName = property.Key, Selector = definition.Selector,
                    Attribute = definition.Attribute, ValueKind = definition.ValueKind,
                    DataType = GetFieldDataType(definition)?.FullName, Culture = GetFieldCulture(definition).Name,
                    MatchCount = matchCount, ValueCount = selected.Length,
                    MinimumValueCount = definition.MinimumValueCount, MaximumValueCount = definition.MaximumValueCount
                };
                object? value = null;
                try {
                    value = ConvertFieldValues(selected, definition, property.Key, index);
                    field.Status = selected.Length > 0 ? HtmlDomFieldStatus.Extracted
                        : !definition.All && definition.DefaultValue != null ? HtmlDomFieldStatus.Defaulted
                        : HtmlDomFieldStatus.Missing;
                } catch (FormatException) {
                    field.Status = HtmlDomFieldStatus.InvalidValue;
                    field.Error = FieldConversionError(property.Key, index, GetFieldDataType(definition)!).Message;
                } catch (InvalidOperationException exception) when (GetFieldCountViolation(selected.Length, definition).HasValue) {
                    field.Status = GetFieldCountViolation(selected.Length, definition)!.Value;
                    field.Error = exception.Message;
                }
                values[property.Key] = value;
                fields.Add(field);
            }
            records.Add(new HtmlDomExtractionRecord { Index = index, ItemSelector = itemSelector, Values = values });
        }

        return new HtmlDomExtractionReport {
            ItemSelector = itemSelector, BaseUri = effectiveBaseUri, Records = records, Fields = fields,
            MinimumItemCount = options?.MinimumItemCount, MaximumItemCount = options?.MaximumItemCount
        };
    }
}