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
    private static IDocument BuildStructuredSectionDocument(IElement heading) {
        int headingLevel = GetHeadingLevel(heading);
        StringBuilder builder = new();
        builder.Append("<div>")
            .Append(heading.OuterHtml);
        IElement? sibling = heading.NextElementSibling;
        while (sibling != null) {
            if (GetHeadingLevel(sibling) is int siblingLevel && siblingLevel <= headingLevel) {
                break;
            }

            builder.Append(sibling.OuterHtml);
            sibling = sibling.NextElementSibling;
        }

        builder.Append("</div>");
        return HtmlParser.ParseWithAngleSharp(builder.ToString());
    }

    private static int GetHeadingLevel(IElement element) {
        if (element == null || element.LocalName.Length != 2 || element.LocalName[0] != 'h' || !char.IsDigit(element.LocalName[1])) {
            return int.MaxValue;
        }

        return element.LocalName[1] - '0';
    }

    private static List<HtmlCrawlStructuredApiParameter> BuildStructuredApiParameters(IDocument sectionDocument) {
        List<HtmlCrawlStructuredApiParameter> parameters = new();
        foreach (IElement table in sectionDocument.QuerySelectorAll("table")) {
            List<HtmlTableResult> parsedTables = HtmlParser.ParseTablesWithAngleSharpDetailed(table.OuterHtml);
            HtmlTableResult? parsed = parsedTables.FirstOrDefault();
            if (parsed == null || !LooksLikeApiParameterTable(parsed, table)) {
                continue;
            }

            string? location = DetectApiParameterLocation(table, parsed);
            foreach (Dictionary<string, string?> row in parsed.Data) {
                HtmlCrawlStructuredApiParameter? parameter = BuildStructuredApiParameter(row, location, BuildElementSelectorHint(table));
                if (parameter != null) {
                    parameters.Add(parameter);
                }
            }
        }

        return parameters;
    }

    private static bool LooksLikeApiParameterTable(HtmlTableResult table, IElement tableElement) {
        string headers = string.Join(" ", table.Metadata.Headers);
        string nearbyHeading = FindNearbyHeadingText(tableElement) ?? string.Empty;
        bool hasNameColumn = ContainsAnyToken(headers, "parameter", "name", "field");
        bool hasDetailColumn = ContainsAnyToken(headers, "type", "required", "description", "default", "location");
        bool headingSignal = ContainsAnyToken(nearbyHeading, "parameter", "request body", "query parameter", "path parameter", "header");
        return hasNameColumn && (hasDetailColumn || headingSignal);
    }

    private static List<HtmlCrawlStructuredRequestExample> BuildStructuredRequestExamples(
        IReadOnlyList<HtmlCrawlStructuredCodeSample> codeSamples) {
        List<HtmlCrawlStructuredRequestExample> requestExamples = new();
        foreach (HtmlCrawlStructuredCodeSample sample in codeSamples) {
            HtmlCrawlStructuredRequestExample? requestExample = BuildStructuredRequestExample(sample);
            if (requestExample == null) {
                continue;
            }

            requestExamples.Add(requestExample);
        }

        return requestExamples;
    }

    private static HtmlCrawlStructuredApiParameter? BuildStructuredApiParameter(Dictionary<string, string?> row, string? fallbackLocation, string? selectorHint) {
        string? name = GetStructuredRowValue(row, "parameter", "parameter name", "name", "field", "field name");
        if (string.IsNullOrWhiteSpace(name)) {
            return null;
        }

        string? type = GetStructuredRowValue(row, "type", "data type");
        string? description = GetStructuredRowValue(row, "description", "details", "summary");
        string? format = NormalizeStructuredApiParameterFormat(
            GetStructuredRowValue(row, "format", "data format"),
            type,
            name,
            description,
            GetStructuredRowValue(row, "example", "example value", "sample", "sample value"));
        string? exampleValue = GetStructuredRowValue(row, "example", "example value", "sample", "sample value");
        string? pattern = GetStructuredRowValue(row, "pattern", "regex", "regexp");
        IList<string> enumValues = ParseStructuredApiEnumValues(
            GetStructuredRowValue(row, "enum", "allowed values", "allowed", "values"),
            description);
        string? defaultValue = GetStructuredRowValue(row, "default", "default value");
        string? location = GetStructuredRowValue(row, "location", "in") ?? fallbackLocation;
        bool? required = ParseNullableBoolean(GetStructuredRowValue(row, "required", "mandatory"));
        bool? nullable = ParseNullableBoolean(GetStructuredRowValue(row, "nullable", "allow null", "allows null", "null"));
        nullable ??= InferStructuredApiNullable(description);

        return new HtmlCrawlStructuredApiParameter {
            Name = NormalizeWhitespace(name),
            Type = NormalizeWhitespace(type),
            Format = NormalizeWhitespace(format),
            Location = NormalizeWhitespace(location),
            Required = required,
            Nullable = nullable,
            Description = NormalizeWhitespace(description),
            DefaultValue = NormalizeWhitespace(defaultValue),
            ExampleValue = NormalizeWhitespace(exampleValue),
            Pattern = NormalizeWhitespace(pattern),
            EnumValues = enumValues,
            SelectorHint = selectorHint
        };
    }

    private static HtmlCrawlStructuredApiAuthentication BuildStructuredApiAuthentication(
        IDocument sectionDocument,
        IReadOnlyList<HtmlCrawlStructuredCodeSample> codeSamples,
        IEnumerable<HtmlCrawlStructuredApiParameter> parameters) {
        HtmlCrawlStructuredApiAuthentication authentication = new();
        string sectionText = NormalizeWhitespace(sectionDocument.DocumentElement?.TextContent);
        bool apiKeyNegated = IsStructuredApiKeyNegated(sectionText);

        foreach (HtmlCrawlStructuredApiParameter parameter in parameters) {
            AppendStructuredApiAuthenticationSignals(authentication, parameter.Name);
            AppendStructuredApiAuthenticationSignals(authentication, parameter.Description);
            AppendStructuredApiAuthenticationSignals(authentication, parameter.DefaultValue);

            string? headerName = NormalizeStructuredAuthenticationHeader(parameter.Name);
            if (string.Equals(parameter.Location, "header", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(headerName)) {
                AppendDistinct(authentication.Headers, headerName!);
                authentication.Required ??= parameter.Required;
            }
        }

        foreach (HtmlCrawlStructuredCodeSample sample in codeSamples) {
            AppendStructuredApiAuthenticationSignals(authentication, sample.Heading);
            AppendStructuredApiAuthenticationSignals(authentication, sample.Title);
            AppendStructuredApiAuthenticationSignals(authentication, sample.Code);
        }

        AppendStructuredApiAuthenticationSignals(authentication, sectionText);

        if (apiKeyNegated) {
            RemoveStructuredAuthenticationSignal(authentication.Headers, "X-API-Key");
            RemoveStructuredAuthenticationSignal(authentication.Schemes, "api-key");
            if (authentication.Headers.Count == 0 && authentication.Schemes.Count == 0) {
                authentication.Required = false;
            }
        }

        if (!authentication.Required.HasValue
            && (authentication.Schemes.Count > 0 || authentication.Headers.Count > 0)) {
            authentication.Required = true;
        }

        authentication.Summary = FindFirstStructuredSignalText(sectionDocument,
            "authentication",
            "authorization",
            "bearer",
            "api key",
            "x-api-key",
            "oauth",
            "jwt",
            "basic auth",
            "token");

        if (string.IsNullOrWhiteSpace(authentication.Summary)
            && (authentication.Required.HasValue || authentication.Schemes.Count > 0 || authentication.Headers.Count > 0)) {
            List<string> parts = new();
            if (authentication.Required == true) {
                parts.Add("Authentication required");
            } else if (authentication.Required == false) {
                parts.Add("No authentication required");
            }

            if (authentication.Schemes.Count > 0) {
                parts.Add("schemes: " + string.Join(", ", authentication.Schemes));
            }
            if (authentication.Headers.Count > 0) {
                parts.Add("headers: " + string.Join(", ", authentication.Headers));
            }

            authentication.Summary = string.Join("; ", parts);
        }

        return authentication;
    }

    private static void MergeStructuredApiAuthentication(
        HtmlCrawlStructuredApiAuthentication target,
        HtmlCrawlStructuredApiAuthentication source) {
        bool sourceIndicatesRequired = source.Required == true || source.Schemes.Count > 0 || source.Headers.Count > 0;
        if (sourceIndicatesRequired) {
            target.Required = true;
        } else if (!target.Required.HasValue && source.Required.HasValue) {
            target.Required = source.Required;
        }

        foreach (string scheme in source.Schemes) {
            AppendDistinct(target.Schemes, scheme);
        }
        foreach (string header in source.Headers) {
            AppendDistinct(target.Headers, header);
        }

        target.Summary ??= source.Summary;
    }

    private static void MergeStructuredApiParameter(
        HtmlCrawlStructuredApiParameter target,
        HtmlCrawlStructuredApiParameter source) {
        target.Type = MergeStructuredTypeValues(target.Type, source.Type);
        target.Format ??= source.Format;
        target.Location ??= source.Location;
        target.Required = target.Required == true || source.Required == true
            ? true
            : target.Required ?? source.Required;
        target.Nullable = target.Nullable == true || source.Nullable == true
            ? true
            : target.Nullable ?? source.Nullable;
        target.Description ??= source.Description;
        target.DefaultValue ??= source.DefaultValue;
        target.ExampleValue ??= source.ExampleValue;
        target.Pattern ??= source.Pattern;
        target.SelectorHint ??= source.SelectorHint;
        foreach (string enumValue in source.EnumValues) {
            AppendDistinct(target.EnumValues, enumValue);
        }
    }

    private static void ApplyStructuredApiParameterGrouping(HtmlCrawlStructuredApiEndpoint endpoint, string pageUrl) {
        endpoint.PathParameters = endpoint.Parameters
            .Where(parameter => string.Equals(ResolveStructuredApiParameterLocation(endpoint.Path, endpoint.Method, parameter), "path", StringComparison.OrdinalIgnoreCase))
            .ToList();
        endpoint.QueryParameters = endpoint.Parameters
            .Where(parameter => string.Equals(ResolveStructuredApiParameterLocation(endpoint.Path, endpoint.Method, parameter), "query", StringComparison.OrdinalIgnoreCase))
            .ToList();
        endpoint.HeaderParameters = endpoint.Parameters
            .Where(parameter => string.Equals(ResolveStructuredApiParameterLocation(endpoint.Path, endpoint.Method, parameter), "header", StringComparison.OrdinalIgnoreCase))
            .ToList();
        endpoint.BodyParameters = endpoint.Parameters
            .Where(parameter => string.Equals(ResolveStructuredApiParameterLocation(endpoint.Path, endpoint.Method, parameter), "body", StringComparison.OrdinalIgnoreCase))
            .ToList();

        endpoint.RequestBodySchema = endpoint.BodyParameters
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Name))
            .GroupBy(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(parameter => parameter.Type).FirstOrDefault(type => !string.IsNullOrWhiteSpace(type)),
                StringComparer.OrdinalIgnoreCase);
        endpoint.RequestBodyFields = FinalizeStructuredFieldConfidence(FinalizeStructuredFieldRelationships(endpoint.BodyParameters
            .Select(parameter => BuildStructuredRequestBodyField(parameter, pageUrl)))
            )
            .OrderBy(field => field.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (HtmlCrawlStructuredApiParameter parameter in endpoint.HeaderParameters) {
            string? headerName = NormalizeStructuredAuthenticationHeader(parameter.Name);
            if (!string.IsNullOrWhiteSpace(headerName)) {
                AppendDistinct(endpoint.Authentication.Headers, headerName!);
                endpoint.Authentication.Required ??= parameter.Required;
            }

            AppendStructuredApiAuthenticationSignals(endpoint.Authentication, parameter.Name);
            AppendStructuredApiAuthenticationSignals(endpoint.Authentication, parameter.Description);
        }

        if (!endpoint.Authentication.Required.HasValue
            && (endpoint.Authentication.Schemes.Count > 0 || endpoint.Authentication.Headers.Count > 0)) {
            endpoint.Authentication.Required = true;
        }

        ApplyStructuredAuthenticationNegations(endpoint.Authentication);
    }

    private static string ResolveStructuredApiParameterLocation(string endpointPath, string? endpointMethod, HtmlCrawlStructuredApiParameter parameter) {
        if (!string.IsNullOrWhiteSpace(parameter.Location)) {
            string explicitLocation = parameter.Location!.Trim().ToLowerInvariant();
            if (ContainsAnyToken(explicitLocation, "path")) {
                return "path";
            }
            if (ContainsAnyToken(explicitLocation, "query")) {
                return "query";
            }
            if (ContainsAnyToken(explicitLocation, "header")) {
                return "header";
            }
            if (ContainsAnyToken(explicitLocation, "cookie")) {
                return "cookie";
            }
            if (ContainsAnyToken(explicitLocation, "body", "payload")) {
                return "body";
            }
        }

        if (!string.IsNullOrWhiteSpace(parameter.Name)
            && endpointPath.IndexOf("{" + parameter.Name + "}", StringComparison.OrdinalIgnoreCase) >= 0) {
            return "path";
        }

        return endpointMethod?.Trim().ToUpperInvariant() is "GET" or "HEAD" or "DELETE" or "OPTIONS"
            ? "query"
            : "body";
    }

    private static string? DetectApiParameterLocation(IElement tableElement, HtmlTableResult table) {
        string heading = FindNearbyHeadingText(tableElement) ?? string.Empty;
        string headers = string.Join(" ", table.Metadata.Headers);
        string combined = heading + " " + headers;
        if (ContainsAnyToken(combined, "path")) {
            return "path";
        }
        if (ContainsAnyToken(combined, "query")) {
            return "query";
        }
        if (ContainsAnyToken(combined, "cookie")) {
            return "cookie";
        }
        if (ContainsAnyToken(combined, "header")) {
            return "header";
        }
        if (ContainsAnyToken(combined, "body", "request")) {
            return "body";
        }

        return null;
    }

    private static string? GetStructuredRowValue(Dictionary<string, string?> row, params string[] names) {
        foreach (string name in names) {
            foreach (KeyValuePair<string, string?> item in row) {
                if (string.Equals(item.Key, name, StringComparison.OrdinalIgnoreCase)) {
                    return item.Value;
                }
            }
        }

        return null;
    }

    private static bool? ParseNullableBoolean(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }

        string normalized = value!.Trim();
        if (normalized.Equals("true", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("required", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }
        if (normalized.Equals("false", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("no", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("optional", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        return null;
    }

    private static bool? InferStructuredApiNullable(string? description) {
        if (string.IsNullOrWhiteSpace(description)) {
            return null;
        }

        string normalized = NormalizeWhitespace(description);
        if (Regex.IsMatch(normalized, @"\b(nullable|may be null|can be null|or null)\b", RegexOptions.IgnoreCase)) {
            return true;
        }
        if (Regex.IsMatch(normalized, @"\b(not null|non-null|must not be null|cannot be null)\b", RegexOptions.IgnoreCase)) {
            return false;
        }

        return null;
    }

    private static string? NormalizeStructuredApiParameterFormat(
        string? explicitFormat,
        string? type,
        string? name,
        string? description,
        string? exampleValue) {
        foreach (string? candidate in new[] { explicitFormat, type, name, description, exampleValue }) {
            string? normalized = MapStructuredApiParameterFormat(candidate);
            if (!string.IsNullOrWhiteSpace(normalized)) {
                return normalized;
            }
        }

        return NormalizeWhitespace(explicitFormat);
    }

    private static string? MapStructuredApiParameterFormat(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }

        string normalized = NormalizeWhitespace(value).ToLowerInvariant();
        if (normalized.Contains("uuid") || normalized.Contains("guid")) {
            return "uuid";
        }
        if (normalized.Contains("date-time") || normalized.Contains("datetime") || normalized.Contains("timestamp")) {
            return "date-time";
        }
        if (Regex.IsMatch(normalized, @"\bdate\b", RegexOptions.IgnoreCase)) {
            return "date";
        }
        if (normalized.Contains("email") || Regex.IsMatch(normalized, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")) {
            return "email";
        }
        if (normalized.Contains("uri") || normalized.Contains("url") || Uri.TryCreate(value!.Trim(), UriKind.Absolute, out _)) {
            return "uri";
        }
        if (normalized.Contains("hostname")) {
            return "hostname";
        }
        if (normalized.Contains("ipv4")) {
            return "ipv4";
        }
        if (normalized.Contains("ipv6")) {
            return "ipv6";
        }
        if (normalized.Contains("slug")) {
            return "slug";
        }
        if (normalized.Contains("base64")) {
            return "base64";
        }

        return null;
    }

    private static IList<string> ParseStructuredApiEnumValues(string? rawValues, string? description) {
        List<string> values = new();

        void AppendCandidates(string? input, bool prose) {
            if (string.IsNullOrWhiteSpace(input)) {
                return;
            }

            string normalized = NormalizeWhitespace(input);
            if (string.IsNullOrWhiteSpace(normalized)) {
                return;
            }

            if (prose) {
                Match match = Regex.Match(normalized, @"\b(?:one of|allowed values?|valid values?)\s*[:\-]\s*(.+)$", RegexOptions.IgnoreCase);
                if (match.Success) {
                    normalized = match.Groups[1].Value;
                } else {
                    return;
                }
            }

            normalized = normalized.Trim('[', ']', '(', ')');
            foreach (string part in Regex.Split(normalized, @"\s*(?:,|\||/|;)\s*")) {
                string candidate = NormalizeWhitespace(part.Trim('\"', '\'', '`'));
                if (!string.IsNullOrWhiteSpace(candidate) && !candidate.Contains(' ')) {
                    AppendDistinct(values, candidate);
                }
            }
        }

        AppendCandidates(rawValues, prose: false);
        if (values.Count == 0) {
            AppendCandidates(description, prose: true);
        }

        return values;
    }

    private static HtmlCrawlStructuredApiRateLimit BuildStructuredApiRateLimit(
        IDocument sectionDocument,
        IReadOnlyList<HtmlCrawlStructuredCodeSample> codeSamples,
        IEnumerable<HtmlCrawlStructuredResponseExample> responseExamples) {
        HtmlCrawlStructuredApiRateLimit rateLimit = new();
        string sectionText = NormalizeWhitespace(sectionDocument.DocumentElement?.TextContent);

        foreach (HtmlCrawlStructuredCodeSample sample in codeSamples) {
            AppendStructuredApiRateLimitSignals(rateLimit, sample.Heading);
            AppendStructuredApiRateLimitSignals(rateLimit, sample.Title);
            AppendStructuredApiRateLimitSignals(rateLimit, sample.Code);
        }

        foreach (HtmlCrawlStructuredResponseExample responseExample in responseExamples) {
            if (responseExample.StatusCode == 429) {
                rateLimit.Mentioned = true;
                rateLimit.StatusCode ??= 429;
            }

            AppendStructuredApiRateLimitSignals(rateLimit, responseExample.Title);
            AppendStructuredApiRateLimitSignals(rateLimit, responseExample.Body);
        }

        AppendStructuredApiRateLimitSignals(rateLimit, sectionText);
        rateLimit.Summary = FindFirstStructuredSignalText(sectionDocument,
            "rate limit",
            "rate-limit",
            "quota",
            "throttle",
            "throttling",
            "retry-after",
            "too many requests",
            "x-ratelimit",
            "ratelimit");

        if (string.IsNullOrWhiteSpace(rateLimit.Summary)
            && (rateLimit.Mentioned || rateLimit.StatusCode.HasValue || rateLimit.Headers.Count > 0 || !string.IsNullOrWhiteSpace(rateLimit.Limit))) {
            List<string> parts = new();
            if (!string.IsNullOrWhiteSpace(rateLimit.Limit)) {
                parts.Add(rateLimit.Limit!);
            }
            if (rateLimit.StatusCode.HasValue) {
                parts.Add("status " + rateLimit.StatusCode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            if (rateLimit.Headers.Count > 0) {
                parts.Add("headers: " + string.Join(", ", rateLimit.Headers));
            }

            rateLimit.Summary = string.Join("; ", parts);
        }

        return rateLimit;
    }

    private static void MergeStructuredApiRateLimit(
        HtmlCrawlStructuredApiRateLimit target,
        HtmlCrawlStructuredApiRateLimit source) {
        target.Mentioned |= source.Mentioned;
        target.StatusCode ??= source.StatusCode;
        target.Limit ??= source.Limit;
        target.Window ??= source.Window;
        foreach (string header in source.Headers) {
            AppendDistinct(target.Headers, header);
        }

        target.Summary ??= source.Summary;
    }

    private static List<HtmlCrawlStructuredResponseExample> BuildStructuredResponseExamples(
        IDocument sectionDocument,
        IReadOnlyList<HtmlCrawlStructuredCodeSample> codeSamples,
        string pageUrl) {
        List<HtmlCrawlStructuredResponseExample> responseExamples = new();
        foreach (HtmlCrawlStructuredCodeSample sample in codeSamples) {
            if (!LooksLikeResponseExample(sample)) {
                continue;
            }

            List<HtmlCrawlStructuredHttpHeader> headers = new();
            string body = sample.Code;
            int? parsedStatusCode = null;
            string? parsedStatusText = null;
            string? contentType = null;
            if (TryParseStructuredHttpResponseSample(sample.Code, out int? sampleStatusCode, out string? sampleStatusText, out List<HtmlCrawlStructuredHttpHeader> sampleHeaders, out string sampleBody)) {
                parsedStatusCode = sampleStatusCode;
                parsedStatusText = sampleStatusText;
                headers = sampleHeaders;
                body = sampleBody;
                contentType = sampleHeaders
                    .FirstOrDefault(header => string.Equals(header.Name, "Content-Type", StringComparison.OrdinalIgnoreCase))
                    ?.Value;
            }

            int? statusCode = parsedStatusCode ?? ExtractStatusCode(sample.Heading) ?? ExtractStatusCode(sample.Title);
            string? statusText = parsedStatusText
                ?? ExtractStatusText(sample.Heading)
                ?? ExtractStatusText(sample.Title)
                ?? (statusCode.HasValue ? GetDefaultHttpStatusText(statusCode.Value) : null);
            object? jsonBody = null;
            Dictionary<string, string?> bodySchema = new(StringComparer.OrdinalIgnoreCase);
            List<string> topLevelKeys = new();
            List<HtmlCrawlStructuredField> bodyFields = new();
            if (TryParseStructuredJsonPayload(body, out object? parsedJsonBody, out Dictionary<string, string?> parsedBodySchema, out List<string> parsedTopLevelKeys)) {
                jsonBody = parsedJsonBody;
                bodySchema = parsedBodySchema;
                topLevelKeys = parsedTopLevelKeys;
                bodyFields = BuildStructuredFieldsFromJsonPayload(
                    parsedJsonBody,
                    "JsonResponse",
                    pageUrl,
                    sample.SelectorHint,
                    sample.Title ?? sample.Heading ?? (statusCode.HasValue ? "Response " + statusCode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : null));
            }

            responseExamples.Add(new HtmlCrawlStructuredResponseExample {
                Title = sample.Title,
                Description = sample.Heading,
                Language = sample.Language,
                Kind = sample.Kind,
                StatusCode = statusCode,
                StatusText = statusText,
                Headers = headers,
                ContentType = contentType ?? InferStructuredResponseContentType(sample.Language, sample.Kind, body),
                IsError = statusCode is >= 400,
                Body = body,
                BodySchema = bodySchema,
                TopLevelKeys = topLevelKeys,
                JsonBody = jsonBody,
                BodyFields = bodyFields,
                SelectorHint = sample.SelectorHint
            });
        }

        foreach (HtmlCrawlStructuredResponseExample response in BuildStructuredDocumentedErrorResponses(sectionDocument, responseExamples)) {
            if (!responseExamples.Any(existing =>
                    existing.StatusCode == response.StatusCode
                    && string.Equals(existing.Description, response.Description, StringComparison.OrdinalIgnoreCase))) {
                responseExamples.Add(response);
            }
        }

        return responseExamples;
    }

    private static List<HtmlCrawlStructuredHttpHeader> BuildStructuredEndpointRequestHeaders(HtmlCrawlStructuredApiEndpoint endpoint) {
        List<HtmlCrawlStructuredHttpHeader> headers = new();
        foreach (HtmlCrawlStructuredRequestExample requestExample in endpoint.RequestExamples) {
            foreach (HtmlCrawlStructuredHttpHeader header in requestExample.Headers) {
                AppendStructuredHeader(headers, header.Name, header.Value);
            }

            if (!string.IsNullOrWhiteSpace(requestExample.ContentType)) {
                AppendStructuredHeader(headers, "Content-Type", requestExample.ContentType);
            }
        }

        foreach (HtmlCrawlStructuredApiParameter parameter in endpoint.HeaderParameters) {
            AppendStructuredHeader(headers, parameter.Name, parameter.ExampleValue ?? parameter.DefaultValue);
        }

        foreach (string headerName in endpoint.Authentication.Headers) {
            HtmlCrawlStructuredApiParameter? parameter = endpoint.HeaderParameters
                .FirstOrDefault(item => string.Equals(item.Name, headerName, StringComparison.OrdinalIgnoreCase));
            AppendStructuredHeader(headers, headerName, parameter?.ExampleValue ?? parameter?.DefaultValue);
        }

        if (endpoint.RequestBodySchema.Count > 0
            && !headers.Any(header => string.Equals(header.Name, "Content-Type", StringComparison.OrdinalIgnoreCase))) {
            AppendStructuredHeader(headers, "Content-Type", "application/json");
        }

        return headers;
    }

    private static List<HtmlCrawlStructuredHttpHeader> BuildStructuredEndpointResponseHeaders(HtmlCrawlStructuredApiEndpoint endpoint) {
        List<HtmlCrawlStructuredHttpHeader> headers = new();
        foreach (HtmlCrawlStructuredResponseExample response in endpoint.ResponseExamples) {
            foreach (HtmlCrawlStructuredHttpHeader header in response.Headers) {
                AppendStructuredHeader(headers, header.Name, header.Value);
            }
        }

        foreach (string headerName in endpoint.RateLimit.Headers) {
            AppendStructuredHeader(headers, headerName, null);
        }

        return headers;
    }

    private static IDictionary<string, string?> BuildStructuredEndpointResponseSchema(IEnumerable<HtmlCrawlStructuredResponseExample> responses) {
        Dictionary<string, string?> schema = new(StringComparer.OrdinalIgnoreCase);
        foreach (HtmlCrawlStructuredResponseExample response in responses) {
            MergeStructuredSchemaMaps(schema, response.BodySchema);
        }

        return schema;
    }

    private static IList<HtmlCrawlStructuredField> BuildStructuredEndpointResponseFields(IEnumerable<HtmlCrawlStructuredResponseExample> responses) {
        List<HtmlCrawlStructuredResponseExample> responseList = responses.ToList();
        Dictionary<string, HtmlCrawlStructuredField> fields = new(StringComparer.OrdinalIgnoreCase);
        foreach (HtmlCrawlStructuredResponseExample response in responseList) {
            foreach (HtmlCrawlStructuredField field in response.BodyFields) {
                if (!fields.TryGetValue(field.Path, out HtmlCrawlStructuredField? existing)) {
                    existing = new HtmlCrawlStructuredField {
                        Name = field.Name,
                        Path = field.Path,
                        ParentPath = field.ParentPath,
                        ChildPaths = new List<string>(field.ChildPaths),
                        Kind = field.Kind,
                        Depth = field.Depth,
                        Type = field.Type,
                        Format = field.Format,
                        Required = true,
                        Nullable = field.Nullable,
                        ExampleValue = field.ExampleValue,
                        EnumValues = new List<string>(field.EnumValues),
                        Source = field.Source,
                        Provenance = field.Provenance.Select(CloneStructuredFieldProvenanceEntry).ToList(),
                        EvidenceCount = field.EvidenceCount,
                        ConfidenceScore = field.ConfidenceScore
                    };
                    fields[field.Path] = existing;
                    continue;
                }

                existing.Type = MergeStructuredTypeValues(existing.Type, field.Type);
                existing.Format ??= field.Format;
                existing.ParentPath ??= field.ParentPath;
                existing.Kind = MergeStructuredFieldKinds(existing.Kind, field.Kind);
                existing.Depth = Math.Min(existing.Depth, field.Depth);
                existing.Nullable = existing.Nullable == true || field.Nullable == true
                    ? true
                    : existing.Nullable ?? field.Nullable;
                existing.ExampleValue ??= field.ExampleValue;
                existing.Source ??= field.Source;
                MergeStructuredFieldProvenance(existing, field);
                existing.EvidenceCount = Math.Max(existing.EvidenceCount, field.EvidenceCount);
                existing.ConfidenceScore = Math.Max(existing.ConfidenceScore, field.ConfidenceScore);
                foreach (string enumValue in field.EnumValues) {
                    AppendDistinct(existing.EnumValues, enumValue);
                }
                foreach (string childPath in field.ChildPaths) {
                    AppendDistinct(existing.ChildPaths, childPath);
                }
            }
        }

        foreach (HtmlCrawlStructuredField field in fields.Values) {
            field.Required = responseList.Count > 0 && responseList.All(response =>
                response.BodyFields.Any(candidate => string.Equals(candidate.Path, field.Path, StringComparison.OrdinalIgnoreCase)));
        }

        return FinalizeStructuredFieldConfidence(FinalizeStructuredFieldRelationships(fields.Values))
            .OrderBy(field => field.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IList<HtmlCrawlStructuredApiError> BuildStructuredEndpointErrorCatalog(IEnumerable<HtmlCrawlStructuredResponseExample> errorResponses) {
        return errorResponses
            .GroupBy(response => $"{response.StatusCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown"}|{NormalizeWhitespace(response.StatusText) ?? string.Empty}", StringComparer.OrdinalIgnoreCase)
            .Select(group => {
                List<HtmlCrawlStructuredResponseExample> groupedResponses = group.ToList();
                HtmlCrawlStructuredResponseExample primary = groupedResponses[0];
                List<HtmlCrawlStructuredHttpHeader> headers = new();
                foreach (HtmlCrawlStructuredResponseExample response in groupedResponses) {
                    foreach (HtmlCrawlStructuredHttpHeader header in response.Headers) {
                        AppendStructuredHeader(headers, header.Name, header.Value);
                    }
                }

                return new HtmlCrawlStructuredApiError {
                    StatusCode = primary.StatusCode,
                    StatusText = primary.StatusText,
                    Summary = groupedResponses
                        .Select(response => NormalizeWhitespace(response.Description) ?? NormalizeWhitespace(response.Title))
                        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                        ?? primary.StatusText
                        ?? "Error response",
                    Headers = headers,
                    ContentType = groupedResponses
                        .Select(response => response.ContentType)
                        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                    Schema = new Dictionary<string, string?>(BuildStructuredEndpointResponseSchema(groupedResponses), StringComparer.OrdinalIgnoreCase),
                    Fields = BuildStructuredEndpointResponseFields(groupedResponses),
                    SampleCount = groupedResponses.Count,
                    SelectorHint = groupedResponses
                        .Select(response => response.SelectorHint)
                        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                };
            })
            .OrderBy(error => error.StatusCode ?? int.MaxValue)
            .ThenBy(error => error.StatusText, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HtmlCrawlStructuredField BuildStructuredRequestBodyField(HtmlCrawlStructuredApiParameter parameter, string pageUrl) {
        HtmlCrawlStructuredField field = new() {
            Name = parameter.Name,
            Path = parameter.Name,
            ParentPath = GetStructuredParentPath(parameter.Name),
            Kind = "field",
            Depth = GetStructuredFieldDepth(parameter.Name),
            Type = parameter.Type,
            Format = parameter.Format,
            Required = parameter.Required,
            Nullable = parameter.Nullable,
            ExampleValue = parameter.ExampleValue ?? parameter.DefaultValue,
            EnumValues = new List<string>(parameter.EnumValues),
            Source = "ParameterTable"
        };
        AppendStructuredFieldProvenance(field, pageUrl, "ParameterTable", parameter.SelectorHint, parameter.Name);
        return field;
    }

    private static List<HtmlCrawlStructuredField> FinalizeStructuredFieldRelationships(IEnumerable<HtmlCrawlStructuredField> fields) {
        Dictionary<string, HtmlCrawlStructuredField> byPath = fields
            .GroupBy(field => field.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (HtmlCrawlStructuredField field in byPath.Values) {
            field.ParentPath ??= GetStructuredParentPath(field.Path);
            field.Depth = field.Depth > 0 ? field.Depth : GetStructuredFieldDepth(field.Path);
        }

        foreach (HtmlCrawlStructuredField field in byPath.Values) {
            if (string.IsNullOrWhiteSpace(field.ParentPath)) {
                continue;
            }

            if (byPath.TryGetValue(field.ParentPath!, out HtmlCrawlStructuredField? parent)) {
                AppendDistinct(parent.ChildPaths, field.Path);
            }
        }

        return byPath.Values.ToList();
    }

    private static string? GetStructuredParentPath(string? path) {
        if (string.IsNullOrWhiteSpace(path)) {
            return null;
        }

        string normalized = path!;
        if (normalized.EndsWith("[]", StringComparison.Ordinal)) {
            return normalized.Substring(0, normalized.Length - 2);
        }

        int separatorIndex = normalized.LastIndexOf('.');
        if (separatorIndex < 0) {
            return null;
        }

        return normalized.Substring(0, separatorIndex);
    }

    private static int GetStructuredFieldDepth(string? path) {
        if (string.IsNullOrWhiteSpace(path)) {
            return 0;
        }

        int depth = 0;
        foreach (string segment in path!.Split('.')) {
            if (string.IsNullOrWhiteSpace(segment)) {
                continue;
            }

            depth++;
        }

        return depth;
    }

    private static string MergeStructuredFieldKinds(string current, string incoming) {
        if (string.IsNullOrWhiteSpace(current)) {
            return string.IsNullOrWhiteSpace(incoming) ? "field" : incoming;
        }
        if (string.IsNullOrWhiteSpace(incoming) || string.Equals(current, incoming, StringComparison.OrdinalIgnoreCase)) {
            return current;
        }

        if (string.Equals(current, "field", StringComparison.OrdinalIgnoreCase)) {
            return incoming;
        }
        if (string.Equals(incoming, "field", StringComparison.OrdinalIgnoreCase)) {
            return current;
        }

        return current;
    }

}
