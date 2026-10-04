using System;
using System.Globalization;
using System.Linq;

namespace HtmlTinkerX;

public static partial class HtmlDomExtraction {
    private static object? ConvertFieldValues(
        object?[] values, HtmlDomFieldDefinition definition, string propertyName, int itemIndex) {
        HtmlDomFieldStatus? violation = GetFieldCountViolation(values.Length, definition);
        if (violation.HasValue) {
            string message = violation == HtmlDomFieldStatus.RequiredMissing
                ? $"Required property '{propertyName}' did not match item {itemIndex} using selector '{definition.Selector}'."
                : $"Property '{propertyName}' on item {itemIndex} has {values.Length} values, outside its declared count bounds.";
            throw new InvalidOperationException(message);
        }

        if (values.Length == 0) {
            return definition.All
                ? Array.Empty<object?>()
                : ConvertFieldValue(definition.DefaultValue, definition, propertyName, itemIndex);
        }

        return definition.All
            ? values.Select(value => ConvertFieldValue(value, definition, propertyName, itemIndex)).ToArray()
            : ConvertFieldValue(values[0], definition, propertyName, itemIndex);
    }

    private static HtmlDomFieldStatus? GetFieldCountViolation(int count, HtmlDomFieldDefinition definition) =>
        definition.Required && count == 0 ? HtmlDomFieldStatus.RequiredMissing
        : definition.MinimumValueCount.HasValue && count < definition.MinimumValueCount.Value ? HtmlDomFieldStatus.TooFewValues
        : definition.MaximumValueCount.HasValue && count > definition.MaximumValueCount.Value ? HtmlDomFieldStatus.TooManyValues
        : null;

    private static void ValidateCountBounds(int? minimum, int? maximum, string name) {
        if (minimum < 0 || maximum < 0 || (minimum.HasValue && maximum.HasValue && minimum > maximum)) {
            throw new ArgumentException($"Count bounds for '{name}' must be non-negative and minimum cannot exceed maximum.");
        }
    }

    private static void ValidateFieldConversion(HtmlDomFieldDefinition definition, string propertyName) {
        if (string.IsNullOrWhiteSpace(definition.Attribute)
            && !string.Equals(definition.ValueKind, "Text", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(definition.ValueKind, "Html", StringComparison.OrdinalIgnoreCase)) {
            throw new ArgumentException($"Property '{propertyName}' must use Text or Html as its value kind.", nameof(definition.ValueKind));
        }
        Type? type = GetFieldDataType(definition);
        if (type != null && type != typeof(string) && type != typeof(int) && type != typeof(long)
            && type != typeof(decimal) && type != typeof(bool) && type != typeof(DateTimeOffset)
            && !type.IsEnum) {
            throw new ArgumentException(
                $"Property '{propertyName}' has unsupported data type '{type.FullName}'. "
                + "Use string, int, long, decimal, bool, DateTimeOffset, or an enum.",
                nameof(definition.DataType));
        }

        try {
            GetFieldCulture(definition);
        } catch (CultureNotFoundException) {
            throw new ArgumentException(
                $"Property '{propertyName}' has an invalid conversion culture '{definition.Culture}'.",
                nameof(definition.Culture));
        }
    }

    private static object? ConvertFieldValue(
        object? value, HtmlDomFieldDefinition definition, string propertyName, int itemIndex) {
        Type? type = GetFieldDataType(definition);
        if (value == null || type == null) {
            return value;
        }

        if (type.IsInstanceOfType(value)) {
            if (!type.IsEnum || Enum.IsDefined(type, value)) {
                return value;
            }

            throw FieldConversionError(propertyName, itemIndex, type);
        }

        CultureInfo culture = GetFieldCulture(definition);
        string text = Convert.ToString(value, culture) ?? string.Empty;
        if (type == typeof(string)) {
            return text;
        }

        if (type == typeof(int) && int.TryParse(text, NumberStyles.Integer, culture, out int integer)) {
            return integer;
        }

        if (type == typeof(long) && long.TryParse(text, NumberStyles.Integer, culture, out long longInteger)) {
            return longInteger;
        }

        if (type == typeof(decimal) && decimal.TryParse(text, NumberStyles.Number, culture, out decimal number)) {
            return number;
        }

        if (type == typeof(bool) && bool.TryParse(text, out bool boolean)) {
            return boolean;
        }

        if (type == typeof(DateTimeOffset)
            && DateTimeOffset.TryParse(text, culture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                out DateTimeOffset date)) {
            return date;
        }

        if (type.IsEnum) {
            string? member = Enum.GetNames(type).FirstOrDefault(name =>
                string.Equals(name, text.Trim(), StringComparison.OrdinalIgnoreCase));
            if (member != null) {
                return Enum.Parse(type, member);
            }
        }

        throw FieldConversionError(propertyName, itemIndex, type);
    }

    private static Type? GetFieldDataType(HtmlDomFieldDefinition definition) =>
        definition.DataType == null ? null : Nullable.GetUnderlyingType(definition.DataType) ?? definition.DataType;

    private static CultureInfo GetFieldCulture(HtmlDomFieldDefinition definition) =>
        string.IsNullOrEmpty(definition.Culture)
            ? CultureInfo.InvariantCulture
            : CultureInfo.GetCultureInfo(definition.Culture);

    private static FormatException FieldConversionError(string propertyName, int itemIndex, Type type) =>
        new($"Value for property '{propertyName}' on item {itemIndex} cannot be converted to {type.Name}.");
}