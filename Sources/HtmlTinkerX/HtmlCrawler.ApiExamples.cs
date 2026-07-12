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
    private static List<HtmlCrawlStructuredResponseExample> BuildStructuredDocumentedErrorResponses(
        IDocument sectionDocument,
        IReadOnlyList<HtmlCrawlStructuredResponseExample> existingResponses) {
        List<HtmlCrawlStructuredResponseExample> responses = new();
        foreach (IElement element in sectionDocument.QuerySelectorAll("p, li, td, th, dd, dt, aside, [class*='callout' i], [class*='notice' i], [class*='warning' i], [class*='alert' i]")) {
            string text = NormalizeWhitespace(element.TextContent);
            if (string.IsNullOrWhiteSpace(text)) {
                continue;
            }

            int? statusCode = ExtractStatusCode(text);
            if (statusCode is not >= 400) {
                continue;
            }

            if (!LooksLikeDocumentedErrorText(text)) {
                continue;
            }

            if (existingResponses.Any(response => response.StatusCode == statusCode && response.IsError)) {
                continue;
            }

            List<HtmlCrawlStructuredHttpHeader> headers = BuildStructuredHeadersFromText(text);
            responses.Add(new HtmlCrawlStructuredResponseExample {
                Title = "Error " + statusCode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Description = text,
                Kind = "text",
                StatusCode = statusCode,
                StatusText = ExtractStatusText(text) ?? GetDefaultHttpStatusText(statusCode.Value),
                Headers = headers,
                ContentType = "text/plain",
                IsError = true,
                Body = string.Empty,
                SelectorHint = BuildElementSelectorHint(element)
            });
        }

        return responses;
    }

    private static void AppendStructuredApiRateLimitSignals(HtmlCrawlStructuredApiRateLimit rateLimit, string? text) {
        if (string.IsNullOrWhiteSpace(text)) {
            return;
        }

        string normalized = NormalizeWhitespace(text);
        if (string.IsNullOrWhiteSpace(normalized)) {
            return;
        }

        if (ContainsAnyToken(normalized, "rate limit", "rate-limit", "quota", "throttle", "throttling", "retry-after", "too many requests", "x-ratelimit", "ratelimit")) {
            rateLimit.Mentioned = true;
        }

        if (!rateLimit.StatusCode.HasValue
            && (ContainsAnyToken(normalized, "too many requests")
                || Regex.IsMatch(normalized, @"\b429\b", RegexOptions.IgnoreCase))) {
            rateLimit.StatusCode = 429;
        }

        foreach (string header in new[] { "Retry-After", "X-RateLimit-Limit", "X-RateLimit-Remaining", "X-RateLimit-Reset", "RateLimit-Limit", "RateLimit-Remaining", "RateLimit-Reset" }) {
            if (normalized.IndexOf(header, StringComparison.OrdinalIgnoreCase) >= 0) {
                AppendDistinct(rateLimit.Headers, header);
                rateLimit.Mentioned = true;
            }
        }

        Match requestsPerWindow = Regex.Match(normalized, @"\b(\d[\d,]*)\s+requests?\s+per\s+(second|minute|hour|day|month)\b", RegexOptions.IgnoreCase);
        if (!requestsPerWindow.Success) {
            requestsPerWindow = Regex.Match(normalized, @"\b(\d[\d,]*)\s*/\s*(second|minute|hour|day|month)\b", RegexOptions.IgnoreCase);
        }

        if (requestsPerWindow.Success) {
            string amount = requestsPerWindow.Groups[1].Value;
            string window = requestsPerWindow.Groups[2].Value.ToLowerInvariant();
            rateLimit.Mentioned = true;
            rateLimit.Window ??= window;
            rateLimit.Limit ??= amount + " requests per " + window;
        }
    }

    private static string? FindFirstStructuredSignalText(IDocument sectionDocument, params string[] tokens) {
        foreach (IElement element in sectionDocument.QuerySelectorAll("p, li, td, th, dd, dt, aside, [class*='callout' i], [class*='notice' i], [class*='warning' i], [class*='alert' i], pre, code")) {
            string text = NormalizeWhitespace(element.TextContent);
            if (!string.IsNullOrWhiteSpace(text) && ContainsAnyToken(text, tokens)) {
                return text;
            }
        }

        string fallback = NormalizeWhitespace(sectionDocument.DocumentElement?.TextContent);
        return ContainsAnyToken(fallback, tokens) ? fallback : null;
    }

    private static bool LooksLikeResponseExample(HtmlCrawlStructuredCodeSample sample) {
        string heading = sample.Heading ?? sample.Title ?? string.Empty;
        if (ContainsAnyToken(heading, "response", "example response", "success response", "error response")) {
            return true;
        }
        if (ExtractStatusCode(heading).HasValue) {
            return true;
        }

        return sample.Method == null && (string.Equals(sample.Kind, "json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sample.Kind, "http", StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeRequestExample(HtmlCrawlStructuredCodeSample sample) {
        if (LooksLikeResponseExample(sample)) {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(sample.Method) && !string.IsNullOrWhiteSpace(sample.Path)) {
            return true;
        }

        return string.Equals(sample.Kind, "curl", StringComparison.OrdinalIgnoreCase)
            || (string.Equals(sample.Kind, "http", StringComparison.OrdinalIgnoreCase)
                && Regex.IsMatch(sample.Code, @"(?im)^\s*(GET|POST|PUT|PATCH|DELETE|OPTIONS|HEAD)\s+((?:https?://[^\s'""]+)?/[^\s'""]+)(?:\s+HTTP/\d(?:\.\d)?)?\s*$"));
    }

    private static HtmlCrawlStructuredRequestExample? BuildStructuredRequestExample(HtmlCrawlStructuredCodeSample sample) {
        if (!LooksLikeRequestExample(sample)) {
            return null;
        }

        List<HtmlCrawlStructuredHttpHeader> headers = new();
        string body = string.Empty;
        string? method = sample.Method;
        string? path = sample.Path;
        string? contentType = null;

        if (TryParseStructuredHttpRequestSample(sample.Code, out string? parsedMethod, out string? parsedPath, out List<HtmlCrawlStructuredHttpHeader> parsedHeaders, out string parsedBody)) {
            method = parsedMethod ?? method;
            path = parsedPath ?? path;
            headers = parsedHeaders;
            body = parsedBody;
            contentType = parsedHeaders
                .FirstOrDefault(header => string.Equals(header.Name, "Content-Type", StringComparison.OrdinalIgnoreCase))
                ?.Value;
        } else if (TryParseStructuredCurlRequestSample(sample.Code, out parsedMethod, out parsedPath, out parsedHeaders, out parsedBody)) {
            method = parsedMethod ?? method;
            path = parsedPath ?? path;
            headers = parsedHeaders;
            body = parsedBody;
            contentType = parsedHeaders
                .FirstOrDefault(header => string.Equals(header.Name, "Content-Type", StringComparison.OrdinalIgnoreCase))
                ?.Value;
        } else {
            body = sample.Code;
        }

        if (string.IsNullOrWhiteSpace(method) || string.IsNullOrWhiteSpace(path)) {
            return null;
        }

        return new HtmlCrawlStructuredRequestExample {
            Title = sample.Title,
            Description = sample.Heading,
            Language = sample.Language,
            Kind = sample.Kind,
            Method = method,
            Path = path,
            Headers = headers,
            ContentType = contentType ?? InferStructuredRequestContentType(sample.Language, sample.Kind, body),
            Body = body,
            SelectorHint = sample.SelectorHint
        };
    }

    private static bool LooksLikeDocumentedErrorText(string text) =>
        ContainsAnyToken(text,
            "error",
            "returns",
            "response",
            "too many requests",
            "unauthorized",
            "forbidden",
            "not found",
            "invalid",
            "failed");

    private static List<HtmlCrawlStructuredHttpHeader> BuildStructuredHeadersFromText(string text) {
        List<HtmlCrawlStructuredHttpHeader> headers = new();
        foreach (string headerName in BuildStructuredDocumentedResponseHeaderNames(text)) {
            AppendStructuredHeader(headers, headerName, null);
        }

        return headers;
    }

    private static bool TryParseStructuredJsonPayload(
        string body,
        out object? jsonBody,
        out Dictionary<string, string?> bodySchema,
        out List<string> topLevelKeys) {
        jsonBody = null;
        bodySchema = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        topLevelKeys = new List<string>();

        if (!LooksLikeJson(body)) {
            return false;
        }

        try {
            using JsonDocument document = JsonDocument.Parse(body);
            jsonBody = ConvertStructuredJsonElement(document.RootElement);
            if (document.RootElement.ValueKind == JsonValueKind.Object) {
                foreach (JsonProperty property in document.RootElement.EnumerateObject()) {
                    topLevelKeys.Add(property.Name);
                }
            }

            BuildStructuredJsonSchema(bodySchema, jsonBody, null);
            return true;
        } catch (JsonException) {
            return false;
        }
    }

    private static List<HtmlCrawlStructuredField> BuildStructuredFieldsFromJsonPayload(
        object? jsonBody,
        string source,
        string pageUrl,
        string? selectorHint,
        string? label) {
        List<HtmlCrawlStructuredField> fields = new();
        AppendStructuredFieldsFromJsonValue(fields, jsonBody, null, source, pageUrl, selectorHint, label);
        return FinalizeStructuredFieldConfidence(FinalizeStructuredFieldRelationships(fields))
            .OrderBy(field => field.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AppendStructuredFieldsFromJsonValue(
        IList<HtmlCrawlStructuredField> fields,
        object? value,
        string? path,
        string source,
        string pageUrl,
        string? selectorHint,
        string? label) {
        if (value is IDictionary<string, object?> dictionary) {
            if (!string.IsNullOrWhiteSpace(path)) {
                HtmlCrawlStructuredField field = new() {
                    Name = ExtractStructuredFieldName(path!),
                    Path = path!,
                    ParentPath = GetStructuredParentPath(path),
                    Kind = "object",
                    Depth = GetStructuredFieldDepth(path),
                    Type = "object",
                    Nullable = false,
                    Source = source
                };
                AppendStructuredFieldProvenance(field, pageUrl, source, selectorHint, label);
                fields.Add(field);
            }

            foreach (KeyValuePair<string, object?> item in dictionary) {
                string childPath = string.IsNullOrWhiteSpace(path) ? item.Key : path + "." + item.Key;
                AppendStructuredFieldsFromJsonValue(fields, item.Value, childPath, source, pageUrl, selectorHint, label);
            }
            return;
        }

        if (value is IList list) {
            string arrayPath = string.IsNullOrWhiteSpace(path) ? "$" : path!;
            if (!string.IsNullOrWhiteSpace(path)) {
                HtmlCrawlStructuredField field = new() {
                    Name = ExtractStructuredFieldName(arrayPath),
                    Path = arrayPath,
                    ParentPath = GetStructuredParentPath(arrayPath),
                    Kind = "array",
                    Depth = GetStructuredFieldDepth(arrayPath),
                    Type = "array",
                    Nullable = false,
                    Source = source
                };
                AppendStructuredFieldProvenance(field, pageUrl, source, selectorHint, label);
                fields.Add(field);
            }

            foreach (object? item in list) {
                AppendStructuredFieldsFromJsonValue(fields, item, arrayPath + "[]", source, pageUrl, selectorHint, label);
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(path)) {
            return;
        }

        string? exampleValue = value switch {
            null => null,
            string text => text,
            bool boolean => boolean ? "true" : "false",
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
        };

        HtmlCrawlStructuredField valueField = new() {
            Name = ExtractStructuredFieldName(path!),
            Path = path!,
            ParentPath = GetStructuredParentPath(path),
            Kind = path!.EndsWith("[]", StringComparison.Ordinal) ? "array-item" : "field",
            Depth = GetStructuredFieldDepth(path),
            Type = GetStructuredSchemaTypeName(value),
            Format = NormalizeStructuredApiParameterFormat(null, null, path, null, exampleValue),
            Required = true,
            Nullable = value == null,
            ExampleValue = exampleValue,
            Source = source
        };
        AppendStructuredFieldProvenance(valueField, pageUrl, source, selectorHint, label);
        fields.Add(valueField);
    }

    private static string ExtractStructuredFieldName(string path) {
        if (string.IsNullOrWhiteSpace(path)) {
            return string.Empty;
        }

        string normalized = path.EndsWith("[]", StringComparison.Ordinal) ? path.Substring(0, path.Length - 2) : path;
        int separatorIndex = normalized.LastIndexOf('.');
        return separatorIndex >= 0 ? normalized.Substring(separatorIndex + 1) : normalized;
    }

    private static object? ConvertStructuredJsonElement(JsonElement element) {
        switch (element.ValueKind) {
            case JsonValueKind.Object:
                Dictionary<string, object?> obj = new(StringComparer.OrdinalIgnoreCase);
                foreach (JsonProperty property in element.EnumerateObject()) {
                    obj[property.Name] = ConvertStructuredJsonElement(property.Value);
                }
                return obj;
            case JsonValueKind.Array:
                return element.EnumerateArray()
                    .Select(ConvertStructuredJsonElement)
                    .ToList();
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                if (element.TryGetInt64(out long longValue)) {
                    return longValue;
                }
                if (element.TryGetDecimal(out decimal decimalValue)) {
                    return decimalValue;
                }
                return element.GetDouble();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            default:
                return element.GetRawText();
        }
    }

    private static void BuildStructuredJsonSchema(
        IDictionary<string, string?> schema,
        object? value,
        string? path) {
        if (value is IDictionary<string, object?> dictionary) {
            if (!string.IsNullOrWhiteSpace(path)) {
                MergeStructuredSchemaValue(schema, path!, "object");
            }

            foreach (KeyValuePair<string, object?> item in dictionary) {
                string childPath = string.IsNullOrWhiteSpace(path) ? item.Key : path + "." + item.Key;
                BuildStructuredJsonSchema(schema, item.Value, childPath);
            }
            return;
        }

        if (value is IList list) {
            string arrayPath = string.IsNullOrWhiteSpace(path) ? "$" : path!;
            MergeStructuredSchemaValue(schema, arrayPath, "array");
            if (list.Count == 0) {
                return;
            }

            foreach (object? item in list) {
                BuildStructuredJsonSchema(schema, item, arrayPath + "[]");
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(path)) {
            return;
        }

        MergeStructuredSchemaValue(schema, path!, GetStructuredSchemaTypeName(value));
    }

    private static string GetStructuredSchemaTypeName(object? value) {
        return value switch {
            null => "null",
            string => "string",
            bool => "boolean",
            byte or sbyte or short or ushort or int or uint or long or ulong => "integer",
            float or double or decimal => "number",
            IDictionary<string, object?> => "object",
            IList => "array",
            _ => "string"
        };
    }

    private static void MergeStructuredSchemaMaps(
        IDictionary<string, string?> target,
        IEnumerable<KeyValuePair<string, string?>> source) {
        foreach (KeyValuePair<string, string?> item in source) {
            if (string.IsNullOrWhiteSpace(item.Key)) {
                continue;
            }

            MergeStructuredSchemaValue(target, item.Key, item.Value);
        }
    }

    private static void MergeStructuredSchemaValue(
        IDictionary<string, string?> target,
        string key,
        string? value) {
        if (!target.TryGetValue(key, out string? existing) || string.IsNullOrWhiteSpace(existing)) {
            target[key] = value;
            return;
        }

        if (string.IsNullOrWhiteSpace(value) || string.Equals(existing, value, StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        HashSet<string> types = new(existing!.Split('|'), StringComparer.OrdinalIgnoreCase);
        types.Add(value!);
        target[key] = string.Join("|", types.OrderBy(type => type, StringComparer.OrdinalIgnoreCase));
    }

    private static string? MergeStructuredTypeValues(string? first, string? second) {
        if (string.IsNullOrWhiteSpace(first)) {
            return second;
        }
        if (string.IsNullOrWhiteSpace(second) || string.Equals(first, second, StringComparison.OrdinalIgnoreCase)) {
            return first;
        }

        HashSet<string> types = new(first!.Split('|'), StringComparer.OrdinalIgnoreCase);
        foreach (string type in second!.Split('|')) {
            if (!string.IsNullOrWhiteSpace(type)) {
                types.Add(type);
            }
        }

        return string.Join("|", types.OrderBy(type => type, StringComparer.OrdinalIgnoreCase));
    }

    private static HtmlCrawlStructuredApiAuthentication CloneStructuredApiAuthentication(HtmlCrawlStructuredApiAuthentication value) {
        return new HtmlCrawlStructuredApiAuthentication {
            Required = value.Required,
            Schemes = new List<string>(value.Schemes),
            Headers = new List<string>(value.Headers),
            Summary = value.Summary
        };
    }

    private static HtmlCrawlStructuredApiRateLimit CloneStructuredApiRateLimit(HtmlCrawlStructuredApiRateLimit value) {
        return new HtmlCrawlStructuredApiRateLimit {
            Mentioned = value.Mentioned,
            Limit = value.Limit,
            Window = value.Window,
            Headers = new List<string>(value.Headers),
            StatusCode = value.StatusCode,
            Summary = value.Summary
        };
    }

    private static HtmlCrawlStructuredApiParameter CloneStructuredApiParameter(HtmlCrawlStructuredApiParameter value) {
        return new HtmlCrawlStructuredApiParameter {
            Name = value.Name,
            Type = value.Type,
            Format = value.Format,
            Location = value.Location,
            Required = value.Required,
            Nullable = value.Nullable,
            Description = value.Description,
            DefaultValue = value.DefaultValue,
            ExampleValue = value.ExampleValue,
            Pattern = value.Pattern,
            EnumValues = new List<string>(value.EnumValues),
            SelectorHint = value.SelectorHint
        };
    }

    private static HtmlCrawlStructuredHttpHeader CloneStructuredHttpHeader(HtmlCrawlStructuredHttpHeader value) {
        return new HtmlCrawlStructuredHttpHeader {
            Name = value.Name,
            Value = value.Value
        };
    }

    private static HtmlCrawlStructuredRequestExample CloneStructuredRequestExample(HtmlCrawlStructuredRequestExample value) {
        return new HtmlCrawlStructuredRequestExample {
            Title = value.Title,
            Description = value.Description,
            Language = value.Language,
            Kind = value.Kind,
            Method = value.Method,
            Path = value.Path,
            Headers = value.Headers.Select(CloneStructuredHttpHeader).ToList(),
            ContentType = value.ContentType,
            Body = value.Body,
            SelectorHint = value.SelectorHint
        };
    }

    private static HtmlCrawlStructuredResponseExample CloneStructuredResponseExample(HtmlCrawlStructuredResponseExample value) {
        return new HtmlCrawlStructuredResponseExample {
            Title = value.Title,
            Description = value.Description,
            Language = value.Language,
            Kind = value.Kind,
            StatusCode = value.StatusCode,
            StatusText = value.StatusText,
            Headers = value.Headers.Select(CloneStructuredHttpHeader).ToList(),
            ContentType = value.ContentType,
            IsError = value.IsError,
            Body = value.Body,
            BodySchema = new Dictionary<string, string?>(value.BodySchema, StringComparer.OrdinalIgnoreCase),
            TopLevelKeys = new List<string>(value.TopLevelKeys),
            JsonBody = value.JsonBody,
            BodyFields = value.BodyFields.Select(CloneStructuredField).ToList(),
            SelectorHint = value.SelectorHint
        };
    }

    private static HtmlCrawlStructuredApiError CloneStructuredApiError(HtmlCrawlStructuredApiError value) {
        return new HtmlCrawlStructuredApiError {
            StatusCode = value.StatusCode,
            StatusText = value.StatusText,
            Summary = value.Summary,
            Headers = value.Headers.Select(CloneStructuredHttpHeader).ToList(),
            ContentType = value.ContentType,
            Schema = new Dictionary<string, string?>(value.Schema, StringComparer.OrdinalIgnoreCase),
            Fields = value.Fields.Select(CloneStructuredField).ToList(),
            SampleCount = value.SampleCount,
            SelectorHint = value.SelectorHint
        };
    }

    private static HtmlCrawlStructuredOpenApiProvenance CloneStructuredOpenApiProvenance(HtmlCrawlStructuredOpenApiProvenance value) {
        return new HtmlCrawlStructuredOpenApiProvenance {
            PageUrls = new List<string>(value.PageUrls),
            SourceKinds = new List<string>(value.SourceKinds),
            Entries = value.Entries.Select(CloneStructuredOpenApiProvenanceEntry).ToList()
        };
    }

    private static HtmlCrawlStructuredOpenApiProvenanceEntry CloneStructuredOpenApiProvenanceEntry(HtmlCrawlStructuredOpenApiProvenanceEntry value) {
        return new HtmlCrawlStructuredOpenApiProvenanceEntry {
            PageUrl = value.PageUrl,
            Kind = value.Kind,
            SelectorHint = value.SelectorHint,
            Label = value.Label
        };
    }

    private static HtmlCrawlStructuredField CloneStructuredField(HtmlCrawlStructuredField value) {
        return new HtmlCrawlStructuredField {
            Name = value.Name,
            Path = value.Path,
            ParentPath = value.ParentPath,
            ChildPaths = new List<string>(value.ChildPaths),
            Kind = value.Kind,
            Depth = value.Depth,
            Type = value.Type,
            Format = value.Format,
            Required = value.Required,
            Nullable = value.Nullable,
            ExampleValue = value.ExampleValue,
            EnumValues = new List<string>(value.EnumValues),
            Source = value.Source,
            Provenance = value.Provenance.Select(CloneStructuredFieldProvenanceEntry).ToList(),
            EvidenceCount = value.EvidenceCount,
            ConfidenceScore = value.ConfidenceScore
        };
    }

    private static HtmlCrawlStructuredFieldProvenanceEntry CloneStructuredFieldProvenanceEntry(HtmlCrawlStructuredFieldProvenanceEntry value) {
        return new HtmlCrawlStructuredFieldProvenanceEntry {
            PageUrl = value.PageUrl,
            Kind = value.Kind,
            SelectorHint = value.SelectorHint,
            Label = value.Label
        };
    }

    private static IEnumerable<string> BuildStructuredDocumentedResponseHeaderNames(IDocument sectionDocument) =>
        BuildStructuredDocumentedResponseHeaderNames(NormalizeWhitespace(sectionDocument.DocumentElement?.TextContent));

    private static IEnumerable<string> BuildStructuredDocumentedResponseHeaderNames(string? text) {
        if (string.IsNullOrWhiteSpace(text)) {
            return Array.Empty<string>();
        }

        List<string> names = new();
        foreach (string headerName in new[] { "Retry-After", "WWW-Authenticate", "X-RateLimit-Limit", "X-RateLimit-Remaining", "X-RateLimit-Reset", "RateLimit-Limit", "RateLimit-Remaining", "RateLimit-Reset", "Content-Type" }) {
            if (text!.IndexOf(headerName, StringComparison.OrdinalIgnoreCase) >= 0) {
                AppendDistinct(names, headerName);
            }
        }

        return names;
    }

    private static void AppendStructuredHeader(IList<HtmlCrawlStructuredHttpHeader> headers, string? name, string? value) {
        if (string.IsNullOrWhiteSpace(name)) {
            return;
        }

        HtmlCrawlStructuredHttpHeader? existing = headers.FirstOrDefault(header => string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing == null) {
            headers.Add(new HtmlCrawlStructuredHttpHeader {
                Name = NormalizeWhitespace(name),
                Value = string.IsNullOrWhiteSpace(value) ? null : NormalizeWhitespace(value)
            });
            return;
        }

        if (string.IsNullOrWhiteSpace(existing.Value) && !string.IsNullOrWhiteSpace(value)) {
            existing.Value = NormalizeWhitespace(value);
        }
    }

    private static string? InferStructuredRequestContentType(string? language, string kind, string body) {
        if (LooksLikeJson(body) || string.Equals(language, "json", StringComparison.OrdinalIgnoreCase)) {
            return "application/json";
        }
        if (string.Equals(kind, "http", StringComparison.OrdinalIgnoreCase)
            || string.Equals(kind, "curl", StringComparison.OrdinalIgnoreCase)
            || string.Equals(kind, "command", StringComparison.OrdinalIgnoreCase)) {
            return LooksLikeJson(body) ? "application/json" : null;
        }

        return null;
    }

    private static string? InferStructuredResponseContentType(string? language, string kind, string body) {
        if (LooksLikeJson(body) || string.Equals(language, "json", StringComparison.OrdinalIgnoreCase) || string.Equals(kind, "json", StringComparison.OrdinalIgnoreCase)) {
            return "application/json";
        }
        if (string.Equals(language, "html", StringComparison.OrdinalIgnoreCase) || body.IndexOf("<html", StringComparison.OrdinalIgnoreCase) >= 0) {
            return "text/html";
        }
        if (string.Equals(kind, "http", StringComparison.OrdinalIgnoreCase)) {
            return "message/http";
        }

        return null;
    }

    private static bool TryParseStructuredHttpRequestSample(
        string code,
        out string? method,
        out string? path,
        out List<HtmlCrawlStructuredHttpHeader> headers,
        out string body) {
        method = null;
        path = null;
        headers = new List<HtmlCrawlStructuredHttpHeader>();
        body = code;

        if (string.IsNullOrWhiteSpace(code)) {
            return false;
        }

        string normalizedNewlines = code.Replace("\r\n", "\n");
        string[] lines = normalizedNewlines.Split('\n');
        if (lines.Length == 0) {
            return false;
        }

        Match requestLine = Regex.Match(lines[0].Trim(), @"^(GET|POST|PUT|PATCH|DELETE|OPTIONS|HEAD)\s+((?:https?://[^\s'""]+)?/(?:[^\s'""]*)?)(?:\s+HTTP/\d(?:\.\d)?)?$", RegexOptions.IgnoreCase);
        if (!requestLine.Success) {
            return false;
        }

        method = requestLine.Groups[1].Value.ToUpperInvariant();
        path = NormalizeStructuredApiPath(requestLine.Groups[2].Value);

        int index = 1;
        while (index < lines.Length) {
            string line = lines[index].Trim();
            index++;
            if (line.Length == 0) {
                break;
            }

            int separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0) {
                continue;
            }

            string name = NormalizeWhitespace(line.Substring(0, separatorIndex));
            string value = NormalizeWhitespace(line.Substring(separatorIndex + 1));
            AppendStructuredHeader(headers, name, value);
        }

        body = string.Join("\n", lines.Skip(index)).Trim();
        return true;
    }

    private static bool TryParseStructuredCurlRequestSample(
        string code,
        out string? method,
        out string? path,
        out List<HtmlCrawlStructuredHttpHeader> headers,
        out string body) {
        method = null;
        path = null;
        headers = new List<HtmlCrawlStructuredHttpHeader>();
        body = string.Empty;

        if (string.IsNullOrWhiteSpace(code) || !Regex.IsMatch(code, @"(?im)^\s*curl\b")) {
            return false;
        }

        TryExtractCurlMethod(code, out method);
        if (TryExtractCurlTarget(code, out string? target)) {
            path = NormalizeStructuredApiPath(target!);
        }

        foreach (Match headerMatch in Regex.Matches(code, @"(?is)(?<!\S)(?:-H|--header)\s+(?:""([^""]+)""|'([^']+)'|([^\s]+))")) {
            string rawHeader = NormalizeWhitespace(headerMatch.Groups[1].Value);
            if (string.IsNullOrWhiteSpace(rawHeader)) {
                rawHeader = NormalizeWhitespace(headerMatch.Groups[2].Value);
            }
            if (string.IsNullOrWhiteSpace(rawHeader)) {
                rawHeader = NormalizeWhitespace(headerMatch.Groups[3].Value);
            }
            if (string.IsNullOrWhiteSpace(rawHeader)) {
                continue;
            }

            int separatorIndex = rawHeader.IndexOf(':');
            if (separatorIndex <= 0) {
                continue;
            }

            AppendStructuredHeader(headers,
                rawHeader.Substring(0, separatorIndex),
                rawHeader.Substring(separatorIndex + 1));
        }

        Match bodyMatch = Regex.Match(code, @"(?is)(?<!\S)(?:--data-raw|--data-binary|--data|-d)\s+(?:""([\s\S]*?)""|'([\s\S]*?)'|([^\s]+))");
        if (bodyMatch.Success) {
            body = NormalizeWhitespace(bodyMatch.Groups[1].Value);
            if (string.IsNullOrWhiteSpace(body)) {
                body = NormalizeWhitespace(bodyMatch.Groups[2].Value);
            }
            if (string.IsNullOrWhiteSpace(body)) {
                body = NormalizeWhitespace(bodyMatch.Groups[3].Value);
            }
        }

        if (string.IsNullOrWhiteSpace(method)) {
            method = string.IsNullOrWhiteSpace(body) ? "GET" : "POST";
        }

        return !string.IsNullOrWhiteSpace(method) && !string.IsNullOrWhiteSpace(path);
    }

    private static bool TryParseStructuredHttpResponseSample(
        string code,
        out int? statusCode,
        out string? statusText,
        out List<HtmlCrawlStructuredHttpHeader> headers,
        out string body) {
        statusCode = null;
        statusText = null;
        headers = new List<HtmlCrawlStructuredHttpHeader>();
        body = code;

        if (string.IsNullOrWhiteSpace(code)) {
            return false;
        }

        string normalizedNewlines = code.Replace("\r\n", "\n");
        string[] lines = normalizedNewlines.Split('\n');
        if (lines.Length == 0) {
            return false;
        }

        Match statusLine = Regex.Match(lines[0].Trim(), @"^HTTP/\d(?:\.\d)?\s+([1-5][0-9]{2})(?:\s+(.+))?$", RegexOptions.IgnoreCase);
        if (!statusLine.Success) {
            return false;
        }

        if (int.TryParse(statusLine.Groups[1].Value, out int parsedStatusCode)) {
            statusCode = parsedStatusCode;
        }
        statusText = NormalizeWhitespace(statusLine.Groups[2].Value);

        int index = 1;
        while (index < lines.Length) {
            string line = lines[index].Trim();
            index++;
            if (line.Length == 0) {
                break;
            }

            int separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0) {
                continue;
            }

            string name = NormalizeWhitespace(line.Substring(0, separatorIndex));
            string value = NormalizeWhitespace(line.Substring(separatorIndex + 1));
            AppendStructuredHeader(headers, name, value);
        }

        body = string.Join("\n", lines.Skip(index)).Trim();
        return true;
    }

    private static int? ExtractStatusCode(string? text) {
        if (string.IsNullOrWhiteSpace(text)) {
            return null;
        }

        Match match = Regex.Match(text, @"\b([1-5][0-9]{2})\b");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int statusCode)) {
            return statusCode;
        }

        return null;
    }

    private static string? ExtractStatusText(string? text) {
        if (string.IsNullOrWhiteSpace(text)) {
            return null;
        }

        Match httpStatusMatch = Regex.Match(text, @"\b(?:HTTP/\d(?:\.\d)?\s+)?[1-5][0-9]{2}\s+([A-Za-z][A-Za-z0-9 _-]+)", RegexOptions.IgnoreCase);
        if (httpStatusMatch.Success) {
            return NormalizeWhitespace(httpStatusMatch.Groups[1].Value);
        }

        return null;
    }

    private static string? GetDefaultHttpStatusText(int statusCode) {
        return statusCode switch {
            200 => "OK",
            201 => "Created",
            202 => "Accepted",
            204 => "No Content",
            400 => "Bad Request",
            401 => "Unauthorized",
            403 => "Forbidden",
            404 => "Not Found",
            409 => "Conflict",
            422 => "Unprocessable Entity",
            429 => "Too Many Requests",
            500 => "Internal Server Error",
            502 => "Bad Gateway",
            503 => "Service Unavailable",
            504 => "Gateway Timeout",
            _ => null
        };
    }

}
