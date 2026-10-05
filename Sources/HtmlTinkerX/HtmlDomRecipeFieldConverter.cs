using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HtmlTinkerX;

// Recipe fields use portable names rather than assembly-qualified System.Type serialization.
internal sealed class HtmlDomRecipeFieldConverter : JsonConverter<HtmlDomFieldDefinition> {
    private static readonly IReadOnlyDictionary<string, Type> ScalarTypes = new[] {
        typeof(string), typeof(bool), typeof(char), typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
        typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal),
        typeof(DateTime), typeof(DateTimeOffset)
    }.ToDictionary(type => type.Name, StringComparer.OrdinalIgnoreCase);

    public override HtmlDomFieldDefinition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        FieldRecipe saved = JsonSerializer.Deserialize<FieldRecipe>(ref reader, options)
            ?? throw new JsonException("A DOM field definition must be an object.");
        return new HtmlDomFieldDefinition {
            Selector = saved.Selector, Attribute = saved.Attribute, ValueKind = saved.ValueKind,
            DataType = saved.DataType == null ? null : ResolveType(saved.DataType), Culture = saved.Culture,
            TreatEmptyAsMissing = saved.TreatEmptyAsMissing, All = saved.All, Required = saved.Required,
            DefaultValue = saved.DefaultValue == null ? null : RestoreDefault(saved.DefaultValue, saved.DefaultValueType ?? "String"),
            ResolveUrl = saved.ResolveUrl, MinimumValueCount = saved.MinimumValueCount, MaximumValueCount = saved.MaximumValueCount
        };
    }

    public override void Write(Utf8JsonWriter writer, HtmlDomFieldDefinition value, JsonSerializerOptions options) {
        JsonSerializer.Serialize(writer, new FieldRecipe {
            Selector = value.Selector, Attribute = value.Attribute, ValueKind = value.ValueKind,
            DataType = value.DataType == null ? null : GetTypeName(value.DataType), Culture = value.Culture,
            TreatEmptyAsMissing = value.TreatEmptyAsMissing, All = value.All, Required = value.Required,
            DefaultValueType = value.DefaultValue == null ? null : GetTypeName(value.DefaultValue.GetType()),
            DefaultValue = value.DefaultValue == null ? null : SaveDefault(value.DefaultValue),
            ResolveUrl = value.ResolveUrl, MinimumValueCount = value.MinimumValueCount, MaximumValueCount = value.MaximumValueCount
        }, options);
    }

    private static string GetTypeName(Type type) {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (ScalarTypes.TryGetValue(type.Name, out Type? scalar) && type == scalar) {
            return type.Name;
        }
        if (type.IsEnum && !type.IsGenericType && type.FullName != null) {
            return "enum:" + type.FullName;
        }
        throw new JsonException("DOM recipe field types and defaults must be supported scalar types or enums.");
    }

    private static Type ResolveType(string name) {
        if (ScalarTypes.TryGetValue(name, out Type? scalar)) {
            return scalar;
        }
        if (name.StartsWith("enum:", StringComparison.Ordinal)) {
            string fullName = name.Substring(5);
            if (string.IsNullOrWhiteSpace(fullName) || fullName.IndexOf(',') >= 0 || fullName.IndexOf('[') >= 0) {
                throw new JsonException("Recipe enum names must be unqualified, non-generic full type names.");
            }
            Type[] candidates = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, throwOnError: false))
                .Where(type => type?.IsEnum == true && !type.IsGenericType).Cast<Type>().Distinct().ToArray();
            if (candidates.Length == 1) {
                return candidates[0];
            }
            throw new JsonException("Recipe enum names must identify exactly one enum in the loaded assemblies.");
        }
        throw new JsonException("Unsupported DOM recipe type name.");
    }

    private static string SaveDefault(object value) {
        if (value.GetType().IsEnum && !Enum.IsDefined(value.GetType(), value)) {
            throw new JsonException("DOM recipe enum defaults must be declared enum members.");
        }
        return value switch {
            DateTimeOffset date => date.ToString("O", CultureInfo.InvariantCulture),
            DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
            float number => number.ToString("R", CultureInfo.InvariantCulture),
            double number => number.ToString("R", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private static object RestoreDefault(string value, string name) {
        Type type = ResolveType(name);
        try {
            if (type.IsEnum) {
                string? member = Enum.GetNames(type).FirstOrDefault(candidate =>
                    string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase));
                if (member == null) {
                    throw new FormatException();
                }
                return Enum.Parse(type, member);
            }
            if (type == typeof(DateTimeOffset)) {
                return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.None);
            }
            if (type == typeof(DateTime)) {
                return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }
            return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        } catch (Exception exception) when (exception is FormatException || exception is OverflowException || exception is InvalidCastException) {
            throw new JsonException("DOM recipe default value does not match its declared scalar type.");
        }
    }

    private sealed class FieldRecipe {
        public FieldRecipe() { }
        public string Selector { get; set; } = string.Empty;
        public string? Attribute { get; set; }
        public string ValueKind { get; set; } = "Text";
        public string? DataType { get; set; }
        public string? Culture { get; set; }
        public bool TreatEmptyAsMissing { get; set; }
        public bool All { get; set; }
        public bool Required { get; set; }
        public string? DefaultValue { get; set; }
        public string? DefaultValueType { get; set; }
        public bool ResolveUrl { get; set; }
        public int? MinimumValueCount { get; set; }
        public int? MaximumValueCount { get; set; }
    }
}