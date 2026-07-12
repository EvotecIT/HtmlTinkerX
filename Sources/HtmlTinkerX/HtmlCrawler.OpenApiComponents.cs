using AngleSharp.Dom;
using Microsoft.Playwright;
using OfficeIMO.Markdown;
using OfficeIMO.Markdown.Html;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace HtmlTinkerX;

public static partial class HtmlCrawler {
    private static bool HasStructuredAuthProfile(HtmlCrawlStructuredApiAuthentication value) =>
        value.Required.HasValue
        || value.Schemes.Count > 0
        || value.Headers.Count > 0
        || !string.IsNullOrWhiteSpace(value.Summary);

    private static bool HasStructuredRateLimitProfile(HtmlCrawlStructuredApiRateLimit value) =>
        value.Mentioned
        || value.StatusCode.HasValue
        || value.Headers.Count > 0
        || !string.IsNullOrWhiteSpace(value.Limit)
        || !string.IsNullOrWhiteSpace(value.Window)
        || !string.IsNullOrWhiteSpace(value.Summary);

    private static string GetOrAddStructuredSchemaComponent(
        HtmlCrawlStructuredOpenApiComponents components,
        IDictionary<string, string> refs,
        string prefix,
        IDictionary<string, string?> schema) {
        string signature = BuildStructuredSchemaSignature(schema);
        if (refs.TryGetValue(signature, out string? existingRef)) {
            return existingRef;
        }

        string key = BuildStructuredComponentKey(prefix, components.Schemas.Keys);
        components.Schemas[key] = new Dictionary<string, string?>(schema, StringComparer.OrdinalIgnoreCase);
        refs[signature] = key;
        return key;
    }

    private static string GetOrAddStructuredFieldSetComponent(
        HtmlCrawlStructuredOpenApiComponents components,
        IDictionary<string, string> refs,
        string prefix,
        IEnumerable<HtmlCrawlStructuredField> fields) {
        List<HtmlCrawlStructuredField> clonedFields = fields.Select(CloneStructuredField).ToList();
        string signature = BuildStructuredFieldSetSignature(clonedFields);
        if (refs.TryGetValue(signature, out string? existingRef)) {
            return existingRef;
        }

        string key = BuildStructuredComponentKey(prefix, components.FieldSets.Keys);
        components.FieldSets[key] = clonedFields;
        refs[signature] = key;
        return key;
    }

    private static string GetOrAddStructuredAuthProfileComponent(
        HtmlCrawlStructuredOpenApiComponents components,
        IDictionary<string, string> refs,
        HtmlCrawlStructuredApiAuthentication auth) {
        string signature = BuildStructuredAuthProfileSignature(auth);
        if (refs.TryGetValue(signature, out string? existingRef)) {
            return existingRef;
        }

        string key = BuildStructuredComponentKey("authProfile", components.AuthProfiles.Keys);
        components.AuthProfiles[key] = CloneStructuredApiAuthentication(auth);
        refs[signature] = key;
        return key;
    }

    private static string GetOrAddStructuredRateLimitProfileComponent(
        HtmlCrawlStructuredOpenApiComponents components,
        IDictionary<string, string> refs,
        HtmlCrawlStructuredApiRateLimit rateLimit) {
        string signature = BuildStructuredRateLimitProfileSignature(rateLimit);
        if (refs.TryGetValue(signature, out string? existingRef)) {
            return existingRef;
        }

        string key = BuildStructuredComponentKey("rateLimitProfile", components.RateLimitProfiles.Keys);
        components.RateLimitProfiles[key] = CloneStructuredApiRateLimit(rateLimit);
        refs[signature] = key;
        return key;
    }

    private static string GetOrAddStructuredParameterSetComponent(
        HtmlCrawlStructuredOpenApiComponents components,
        IDictionary<string, string> refs,
        IEnumerable<HtmlCrawlStructuredApiParameter> parameters) {
        List<HtmlCrawlStructuredApiParameter> clonedParameters = parameters.Select(CloneStructuredApiParameter).ToList();
        string signature = BuildStructuredParameterSetSignature(clonedParameters);
        if (refs.TryGetValue(signature, out string? existingRef)) {
            return existingRef;
        }

        string key = BuildStructuredComponentKey("parameterSet", components.ParameterSets.Keys);
        components.ParameterSets[key] = clonedParameters;
        refs[signature] = key;
        return key;
    }

    private static string GetOrAddStructuredHeaderSetComponent(
        HtmlCrawlStructuredOpenApiComponents components,
        IDictionary<string, string> refs,
        string prefix,
        IEnumerable<HtmlCrawlStructuredHttpHeader> headers) {
        List<HtmlCrawlStructuredHttpHeader> clonedHeaders = headers.Select(CloneStructuredHttpHeader).ToList();
        string signature = BuildStructuredHeaderSetSignature(clonedHeaders);
        if (refs.TryGetValue(signature, out string? existingRef)) {
            return existingRef;
        }

        string key = BuildStructuredComponentKey(prefix, prefix.StartsWith("request", StringComparison.OrdinalIgnoreCase)
            ? components.RequestHeaderSets.Keys
            : components.ResponseHeaderSets.Keys);
        if (prefix.StartsWith("request", StringComparison.OrdinalIgnoreCase)) {
            components.RequestHeaderSets[key] = clonedHeaders;
        } else {
            components.ResponseHeaderSets[key] = clonedHeaders;
        }
        refs[signature] = key;
        return key;
    }

    private static string GetOrAddStructuredRequestExampleSetComponent(
        HtmlCrawlStructuredOpenApiComponents components,
        IDictionary<string, string> refs,
        IEnumerable<HtmlCrawlStructuredRequestExample> examples) {
        List<HtmlCrawlStructuredRequestExample> clonedExamples = examples.Select(CloneStructuredRequestExample).ToList();
        string signature = BuildStructuredRequestExampleSetSignature(clonedExamples);
        if (refs.TryGetValue(signature, out string? existingRef)) {
            return existingRef;
        }

        string key = BuildStructuredComponentKey("requestExampleSet", components.RequestExampleSets.Keys);
        components.RequestExampleSets[key] = clonedExamples;
        refs[signature] = key;
        return key;
    }

    private static string GetOrAddStructuredResponseExampleSetComponent(
        HtmlCrawlStructuredOpenApiComponents components,
        IDictionary<string, string> refs,
        IEnumerable<HtmlCrawlStructuredResponseExample> examples) {
        List<HtmlCrawlStructuredResponseExample> clonedExamples = examples.Select(CloneStructuredResponseExample).ToList();
        string signature = BuildStructuredResponseExampleSetSignature(clonedExamples);
        if (refs.TryGetValue(signature, out string? existingRef)) {
            return existingRef;
        }

        string key = BuildStructuredComponentKey("responseExampleSet", components.ResponseExampleSets.Keys);
        components.ResponseExampleSets[key] = clonedExamples;
        refs[signature] = key;
        return key;
    }

    private static string GetOrAddStructuredErrorCatalogComponent(
        HtmlCrawlStructuredOpenApiComponents components,
        IDictionary<string, string> refs,
        IEnumerable<HtmlCrawlStructuredApiError> errors) {
        List<HtmlCrawlStructuredApiError> clonedErrors = errors.Select(CloneStructuredApiError).ToList();
        string signature = BuildStructuredErrorCatalogSignature(clonedErrors);
        if (refs.TryGetValue(signature, out string? existingRef)) {
            return existingRef;
        }

        string key = BuildStructuredComponentKey("errorCatalog", components.ErrorCatalogs.Keys);
        components.ErrorCatalogs[key] = clonedErrors;
        refs[signature] = key;
        return key;
    }

    private static string BuildStructuredComponentKey(string prefix, IEnumerable<string> existingKeys) {
        HashSet<string> keys = new(existingKeys, StringComparer.OrdinalIgnoreCase);
        int index = 1;
        string candidate;
        do {
            candidate = prefix + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            index++;
        } while (keys.Contains(candidate));

        return candidate;
    }

    private static string BuildStructuredSchemaSignature(IEnumerable<KeyValuePair<string, string?>> schema) {
        return string.Join("|", schema
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => $"{item.Key}={item.Value ?? string.Empty}"));
    }

    private static string BuildStructuredFieldSetSignature(IEnumerable<HtmlCrawlStructuredField> fields) {
        return string.Join("|", fields
            .OrderBy(field => field.Path, StringComparer.OrdinalIgnoreCase)
            .Select(field => string.Join("~", new[] {
                field.Path,
                field.ParentPath ?? string.Empty,
                field.Kind ?? string.Empty,
                field.Type ?? string.Empty,
                field.Format ?? string.Empty,
                field.Required?.ToString() ?? string.Empty,
                field.Nullable?.ToString() ?? string.Empty,
                string.Join(",", field.ChildPaths.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                string.Join(",", field.EnumValues.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
            })));
    }

    private static string BuildStructuredAuthProfileSignature(HtmlCrawlStructuredApiAuthentication auth) {
        return string.Join("|", new[] {
            auth.Required?.ToString() ?? string.Empty,
            string.Join(",", auth.Schemes.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
            string.Join(",", auth.Headers.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        });
    }

    private static string BuildStructuredRateLimitProfileSignature(HtmlCrawlStructuredApiRateLimit rateLimit) {
        return string.Join("|", new[] {
            rateLimit.Mentioned.ToString(),
            rateLimit.StatusCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            rateLimit.Limit ?? string.Empty,
            rateLimit.Window ?? string.Empty,
            string.Join(",", rateLimit.Headers.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        });
    }

    private static string BuildStructuredParameterSetSignature(IEnumerable<HtmlCrawlStructuredApiParameter> parameters) {
        return string.Join("|", parameters
            .OrderBy(parameter => parameter.Location, StringComparer.OrdinalIgnoreCase)
            .ThenBy(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase)
            .Select(parameter => string.Join("~", new[] {
                parameter.Name,
                parameter.Type ?? string.Empty,
                parameter.Format ?? string.Empty,
                parameter.Location ?? string.Empty,
                parameter.Required?.ToString() ?? string.Empty,
                parameter.Nullable?.ToString() ?? string.Empty,
                parameter.Pattern ?? string.Empty,
                parameter.DefaultValue ?? string.Empty,
                parameter.ExampleValue ?? string.Empty,
                string.Join(",", parameter.EnumValues.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
            })));
    }

    private static string BuildStructuredHeaderSetSignature(IEnumerable<HtmlCrawlStructuredHttpHeader> headers) {
        return string.Join("|", headers
            .OrderBy(header => header.Name, StringComparer.OrdinalIgnoreCase)
            .Select(header => $"{header.Name}={header.Value ?? string.Empty}"));
    }

    private static string BuildStructuredRequestExampleSetSignature(IEnumerable<HtmlCrawlStructuredRequestExample> examples) {
        return string.Join("|", examples
            .OrderBy(example => example.Method, StringComparer.OrdinalIgnoreCase)
            .ThenBy(example => example.Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(example => example.Title, StringComparer.OrdinalIgnoreCase)
            .Select(example => string.Join("~", new[] {
                example.Method ?? string.Empty,
                example.Path ?? string.Empty,
                example.ContentType ?? string.Empty,
                example.Kind,
                BuildStructuredHeaderSetSignature(example.Headers),
                example.Body
            })));
    }

    private static string BuildStructuredResponseExampleSetSignature(IEnumerable<HtmlCrawlStructuredResponseExample> examples) {
        return string.Join("|", examples
            .OrderBy(example => example.StatusCode ?? int.MaxValue)
            .ThenBy(example => example.Title, StringComparer.OrdinalIgnoreCase)
            .Select(example => string.Join("~", new[] {
                example.StatusCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                example.StatusText ?? string.Empty,
                example.ContentType ?? string.Empty,
                example.Kind,
                BuildStructuredHeaderSetSignature(example.Headers),
                example.Body
            })));
    }

    private static string BuildStructuredErrorCatalogSignature(IEnumerable<HtmlCrawlStructuredApiError> errors) {
        return string.Join("|", errors
            .OrderBy(error => error.StatusCode ?? int.MaxValue)
            .ThenBy(error => error.StatusText, StringComparer.OrdinalIgnoreCase)
            .Select(error => string.Join("~", new[] {
                error.StatusCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                error.StatusText ?? string.Empty,
                error.ContentType ?? string.Empty,
                BuildStructuredHeaderSetSignature(error.Headers),
                BuildStructuredSchemaSignature(error.Schema),
                BuildStructuredFieldSetSignature(error.Fields)
            })));
    }

    private static string NormalizeStrictOpenApiParameterLocation(string? location) {
        string normalized = NormalizeWhitespace(location)?.ToLowerInvariant() ?? string.Empty;
        return normalized is "path" or "query" or "header" or "cookie" ? normalized : "query";
    }

    private static object? BuildStrictOpenApiSchemaReference(string? fieldSetRef, string? schemaRef) {
        string? reference = !string.IsNullOrWhiteSpace(fieldSetRef) ? fieldSetRef : schemaRef;
        if (string.IsNullOrWhiteSpace(reference)) {
            return null;
        }

        return new Dictionary<string, object?> {
            ["$ref"] = $"#/components/schemas/{reference}"
        };
    }

    private static Dictionary<string, object?> BuildStrictOpenApiRequestExamples(IEnumerable<HtmlCrawlStructuredRequestExample> examples) {
        Dictionary<string, object?> values = new(StringComparer.OrdinalIgnoreCase);
        int index = 1;
        foreach (HtmlCrawlStructuredRequestExample example in examples) {
            values["example" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)] = new Dictionary<string, object?> {
                ["summary"] = example.Title ?? example.Description,
                ["value"] = ParseStrictOpenApiExampleValue(example.Body)
            };
            index++;
        }

        return values;
    }

    private static Dictionary<string, object?> BuildStrictOpenApiResponseExamples(IEnumerable<HtmlCrawlStructuredResponseExample> examples) {
        Dictionary<string, object?> values = new(StringComparer.OrdinalIgnoreCase);
        int index = 1;
        foreach (HtmlCrawlStructuredResponseExample example in examples) {
            values["example" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)] = new Dictionary<string, object?> {
                ["summary"] = example.Title ?? example.Description ?? example.StatusText,
                ["value"] = ParseStrictOpenApiExampleValue(example.Body)
            };
            index++;
        }

        return values;
    }

    private static Dictionary<string, object?> BuildStrictOpenApiHeaderDefinitions(IEnumerable<HtmlCrawlStructuredHttpHeader> headers) {
        Dictionary<string, object?> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (HtmlCrawlStructuredHttpHeader header in headers.Where(header => !string.IsNullOrWhiteSpace(header.Name)).GroupBy(header => header.Name, StringComparer.OrdinalIgnoreCase).Select(group => group.First())) {
            Dictionary<string, object?> headerDefinition = new(StringComparer.OrdinalIgnoreCase) {
                ["schema"] = new Dictionary<string, object?> {
                    ["type"] = "string"
                }
            };
            if (!string.IsNullOrWhiteSpace(header.Value)) {
                headerDefinition["example"] = header.Value;
            }
            values[header.Name] = headerDefinition;
        }

        return values;
    }

    private static object? ParseStrictOpenApiExampleValue(string? body) {
        if (string.IsNullOrWhiteSpace(body)) {
            return null;
        }

        if (TryParseStructuredJsonPayload(body!, out object? jsonBody, out _, out _)) {
            return jsonBody;
        }

        return body;
    }

    private static object BuildStrictOpenApiSchemaFromFields(IList<HtmlCrawlStructuredField> fields) {
        Dictionary<string, HtmlCrawlStructuredField> byPath = fields
            .Where(field => !string.IsNullOrWhiteSpace(field.Path))
            .GroupBy(field => field.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        bool rootArray = byPath.Keys.Any(path => path.StartsWith("$[]", StringComparison.Ordinal));
        Dictionary<string, object?> schema = rootArray
            ? BuildStrictOpenApiArraySchema(byPath, "$[]", null)
            : BuildStrictOpenApiObjectSchema(byPath, null);
        AddStrictOpenApiSchemaProvenance(schema, fields);
        AddStrictOpenApiSchemaConfidenceSummary(schema, fields);
        return schema;
    }

    private static object BuildStrictOpenApiSchemaFromFlatMap(IDictionary<string, string?> schemaMap) {
        IList<HtmlCrawlStructuredField> fields = BuildStructuredFieldsFromSchemaMap(schemaMap);
        return fields.Count > 0 ? BuildStrictOpenApiSchemaFromFields(fields) : new Dictionary<string, object?> {
            ["type"] = "object"
        };
    }

    private static IList<HtmlCrawlStructuredField> BuildStructuredFieldsFromSchemaMap(IDictionary<string, string?> schemaMap) {
        List<HtmlCrawlStructuredField> fields = new();
        foreach (KeyValuePair<string, string?> item in schemaMap.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
            if (string.IsNullOrWhiteSpace(item.Key) || string.Equals(item.Key, "$", StringComparison.Ordinal)) {
                continue;
            }

            fields.Add(new HtmlCrawlStructuredField {
                Name = ExtractStructuredFieldName(item.Key),
                Path = item.Key,
                ParentPath = GetStructuredParentPath(item.Key),
                Kind = item.Key.EndsWith("[]", StringComparison.Ordinal)
                    ? "array-item"
                    : string.Equals(item.Value, "object", StringComparison.OrdinalIgnoreCase)
                        ? "object"
                        : string.Equals(item.Value, "array", StringComparison.OrdinalIgnoreCase)
                            ? "array"
                            : "field",
                Depth = GetStructuredFieldDepth(item.Key),
                Type = item.Value,
                Source = "JsonSchemaMap"
            });
        }

        return FinalizeStructuredFieldConfidence(FinalizeStructuredFieldRelationships(fields))
            .OrderBy(field => field.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, object?> BuildStrictOpenApiObjectSchema(
        IDictionary<string, HtmlCrawlStructuredField> byPath,
        string? path) {
        Dictionary<string, object?> schema = new(StringComparer.OrdinalIgnoreCase) {
            ["type"] = "object"
        };

        IEnumerable<HtmlCrawlStructuredField> children = byPath.Values
            .Where(field => string.Equals(field.ParentPath, path, StringComparison.OrdinalIgnoreCase))
            .OrderBy(field => field.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        Dictionary<string, object?> properties = new(StringComparer.OrdinalIgnoreCase);
        List<string> required = new();

        foreach (HtmlCrawlStructuredField child in children) {
            string propertyName = child.Name;
            if (string.IsNullOrWhiteSpace(propertyName) || string.Equals(propertyName, "$[]", StringComparison.Ordinal)) {
                propertyName = ExtractStructuredFieldName(child.Path);
            }

            properties[propertyName] = BuildStrictOpenApiSchemaNode(byPath, child.Path, child);
            if (child.Required == true) {
                required.Add(propertyName);
            }
        }

        schema["properties"] = properties;
        if (required.Count > 0) {
            schema["required"] = required;
        }

        if (!string.IsNullOrWhiteSpace(path) && byPath.TryGetValue(path!, out HtmlCrawlStructuredField? field)) {
            AddStrictOpenApiFieldProvenance(schema, field);
            AddStrictOpenApiFieldConfidence(schema, field);
        }

        return schema;
    }

    private static Dictionary<string, object?> BuildStrictOpenApiArraySchema(
        IDictionary<string, HtmlCrawlStructuredField> byPath,
        string path,
        HtmlCrawlStructuredField? ownerField) {
        Dictionary<string, object?> schema = new(StringComparer.OrdinalIgnoreCase) {
            ["type"] = "array"
        };

        if (ownerField != null) {
            AddStrictOpenApiFieldProvenance(schema, ownerField);
            AddStrictOpenApiFieldConfidence(schema, ownerField);
        }

        if (byPath.TryGetValue(path, out HtmlCrawlStructuredField? itemField)) {
            schema["items"] = BuildStrictOpenApiSchemaNode(byPath, path, itemField);
        } else {
            IEnumerable<HtmlCrawlStructuredField> children = byPath.Values
                .Where(field => string.Equals(field.ParentPath, path, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (children.Any()) {
                schema["items"] = BuildStrictOpenApiObjectSchema(byPath, path);
            } else {
                schema["items"] = new Dictionary<string, object?> {
                    ["type"] = "string"
                };
            }
        }

        return schema;
    }

    private static object BuildStrictOpenApiSchemaNode(
        IDictionary<string, HtmlCrawlStructuredField> byPath,
        string path,
        HtmlCrawlStructuredField field) {
        if (string.Equals(field.Kind, "array", StringComparison.OrdinalIgnoreCase)) {
            return BuildStrictOpenApiArraySchema(byPath, path + "[]", field);
        }
        if (string.Equals(field.Kind, "object", StringComparison.OrdinalIgnoreCase)) {
            return BuildStrictOpenApiObjectSchema(byPath, path);
        }
        if (string.Equals(field.Kind, "array-item", StringComparison.OrdinalIgnoreCase)
            && byPath.Values.Any(candidate => string.Equals(candidate.ParentPath, path, StringComparison.OrdinalIgnoreCase))) {
            return BuildStrictOpenApiObjectSchema(byPath, path);
        }

        Dictionary<string, object?> schema = new(StringComparer.OrdinalIgnoreCase);
        ApplyStrictOpenApiType(schema, field.Type, field.Format);
        if (field.Nullable == true) {
            ApplyStrictOpenApiNullable(schema);
        }
        if (field.EnumValues.Count > 0) {
            schema["enum"] = field.EnumValues.Cast<object>().ToList();
        }
        if (!string.IsNullOrWhiteSpace(field.ExampleValue)) {
            schema["example"] = ParseStrictOpenApiExampleValue(field.ExampleValue);
        }
        AddStrictOpenApiFieldProvenance(schema, field);
        AddStrictOpenApiFieldConfidence(schema, field);

        return schema;
    }

    private static void AddStrictOpenApiSchemaProvenance(IDictionary<string, object?> schema, IEnumerable<HtmlCrawlStructuredField> fields) {
        List<Dictionary<string, object?>> provenance = BuildStrictOpenApiFieldProvenance(fields.SelectMany(field => field.Provenance));
        if (provenance.Count > 0) {
            schema["x-htmltinkerx-schemaProvenance"] = provenance;
        }
    }

    private static void AddStrictOpenApiFieldProvenance(IDictionary<string, object?> schema, HtmlCrawlStructuredField field) {
        List<Dictionary<string, object?>> provenance = BuildStrictOpenApiFieldProvenance(field.Provenance);
        if (provenance.Count > 0) {
            schema["x-htmltinkerx-fieldProvenance"] = provenance;
        }
    }

    private static void AddStrictOpenApiFieldConfidence(IDictionary<string, object?> schema, HtmlCrawlStructuredField field) {
        if (field.ConfidenceScore > 0) {
            schema["x-htmltinkerx-confidence"] = field.ConfidenceScore;
        }
        if (field.EvidenceCount > 0) {
            schema["x-htmltinkerx-evidenceCount"] = field.EvidenceCount;
        }
    }

    private static void AddStrictOpenApiSchemaConfidenceSummary(IDictionary<string, object?> schema, IEnumerable<HtmlCrawlStructuredField> fields) {
        List<HtmlCrawlStructuredField> scoredFields = fields
            .Where(field => field.ConfidenceScore > 0)
            .ToList();
        if (scoredFields.Count == 0) {
            return;
        }

        schema["x-htmltinkerx-confidenceSummary"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) {
            ["average"] = Math.Round(scoredFields.Average(field => field.ConfidenceScore), 2, MidpointRounding.AwayFromZero),
            ["min"] = scoredFields.Min(field => field.ConfidenceScore),
            ["max"] = scoredFields.Max(field => field.ConfidenceScore),
            ["fieldCount"] = scoredFields.Count,
            ["evidenceCount"] = scoredFields.Sum(field => field.EvidenceCount)
        };
    }

    private static List<Dictionary<string, object?>> BuildStrictOpenApiFieldProvenance(IEnumerable<HtmlCrawlStructuredFieldProvenanceEntry> entries) {
        return entries
            .GroupBy(entry => string.Join("|",
                entry.PageUrl ?? string.Empty,
                entry.Kind ?? string.Empty,
                entry.SelectorHint ?? string.Empty,
                entry.Label ?? string.Empty), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(entry => entry.PageUrl, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Kind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Label, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) {
                ["pageUrl"] = entry.PageUrl,
                ["kind"] = entry.Kind,
                ["selectorHint"] = entry.SelectorHint,
                ["label"] = entry.Label
            })
            .ToList();
    }

    private static void ApplyStrictOpenApiType(Dictionary<string, object?> schema, string? type, string? format) {
        string normalizedType = NormalizeWhitespace(type)?.ToLowerInvariant() ?? string.Empty;
        switch (normalizedType) {
            case "integer":
                schema["type"] = "integer";
                break;
            case "number":
                schema["type"] = "number";
                break;
            case "boolean":
                schema["type"] = "boolean";
                break;
            case "array":
                schema["type"] = "array";
                schema["items"] = new Dictionary<string, object?> {
                    ["type"] = "string"
                };
                break;
            case "object":
                schema["type"] = "object";
                break;
            default:
                schema["type"] = "string";
                break;
        }

        if (!string.IsNullOrWhiteSpace(format)) {
            schema["format"] = format;
        }
    }

    private static void ApplyStrictOpenApiNullable(Dictionary<string, object?> schema) {
        string type = schema.TryGetValue("type", out object? value) && value is string schemaType
            ? schemaType
            : "string";
        schema["type"] = new[] { type, "null" };
    }

    private static string GetStrictOpenApiResponseCode(int? statusCode) =>
        statusCode.HasValue
            ? statusCode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "default";

    private static void AddStrictOpenApiExtension(IDictionary<string, object?> value, string key, object? extensionValue) {
        if (extensionValue == null) {
            return;
        }

        if (extensionValue is string stringValue && string.IsNullOrWhiteSpace(stringValue)) {
            return;
        }

        value[key] = extensionValue;
    }

    private static void AddStrictOpenApiComponentExtension(IDictionary<string, object?> components, string key, object? extensionValue) {
        if (extensionValue == null) {
            return;
        }

        switch (extensionValue) {
            case IDictionary<string, object?> objectDictionary when objectDictionary.Count == 0:
                return;
            case System.Collections.IDictionary dictionary when dictionary.Count == 0:
                return;
            case System.Collections.ICollection collection when collection.Count == 0:
                return;
        }

        components[key] = extensionValue;
    }

    private static IList<string> BuildStructuredOpenApiServers(HtmlCrawlPage page, HtmlCrawlStructuredMetadata metadata) =>
        BuildStructuredOpenApiServers(new[] { page.Url, metadata.CanonicalUrl, metadata.ImageUrl });

    private static IList<string> BuildStructuredOpenApiServers(IEnumerable<string?> values) {
        List<string> servers = new();

        foreach (string? value in values) {
            if (string.IsNullOrWhiteSpace(value)) {
                continue;
            }

            if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)) {
                string origin = uri.GetLeftPart(UriPartial.Authority);
                if (!string.IsNullOrWhiteSpace(origin)) {
                    AppendDistinct(servers, origin);
                }
            }
        }

        return servers;
    }

}
