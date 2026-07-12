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
    private static List<HtmlCrawlStructuredApiEndpoint> BuildStructuredApiEndpoints(
        IDocument selectedDocument,
        IReadOnlyList<HtmlCrawlStructuredCodeSample> codeSamples,
        string pageUrl) {
        Dictionary<string, HtmlCrawlStructuredApiEndpoint> endpoints = new(StringComparer.OrdinalIgnoreCase);

        foreach (IElement heading in selectedDocument.QuerySelectorAll("h1, h2, h3, h4, h5, h6")) {
            string text = NormalizeWhitespace(heading.TextContent);
            if (!TryParseApiMethodAndPath(text, out string? method, out string? path)) {
                continue;
            }

            HtmlCrawlStructuredApiEndpoint endpoint = GetOrCreateStructuredApiEndpoint(endpoints, method!, path!);
            endpoint.Title ??= text;
            endpoint.Description ??= FindFollowingParagraphText(heading);
            endpoint.SelectorHint ??= BuildElementSelectorHint(heading);
            AppendDistinct(endpoint.Sources, "Heading");

            IDocument sectionDocument = BuildStructuredSectionDocument(heading);
            List<HtmlCrawlStructuredCodeSample> sectionCodeSamples = BuildStructuredCodeSamples(sectionDocument);
            foreach (HtmlCrawlStructuredApiParameter parameter in BuildStructuredApiParameters(sectionDocument)) {
                if (!endpoint.Parameters.Any(existing =>
                        string.Equals(existing.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(existing.Location, parameter.Location, StringComparison.OrdinalIgnoreCase))) {
                    endpoint.Parameters.Add(parameter);
                }
            }

            foreach (HtmlCrawlStructuredRequestExample requestExample in BuildStructuredRequestExamples(sectionCodeSamples)) {
                if (!endpoint.RequestExamples.Any(existing =>
                        string.Equals(existing.Method, requestExample.Method, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(existing.Path, requestExample.Path, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(existing.Body, requestExample.Body, StringComparison.Ordinal)
                        && string.Equals(existing.Title, requestExample.Title, StringComparison.OrdinalIgnoreCase))) {
                    endpoint.RequestExamples.Add(requestExample);
                }
            }

            foreach (HtmlCrawlStructuredResponseExample responseExample in BuildStructuredResponseExamples(sectionDocument, sectionCodeSamples, pageUrl)) {
                if (!endpoint.ResponseExamples.Any(existing =>
                        string.Equals(existing.Body, responseExample.Body, StringComparison.Ordinal)
                        && string.Equals(existing.Title, responseExample.Title, StringComparison.OrdinalIgnoreCase)
                        && existing.StatusCode == responseExample.StatusCode)) {
                    endpoint.ResponseExamples.Add(responseExample);
                }
            }

            MergeStructuredApiAuthentication(endpoint.Authentication, BuildStructuredApiAuthentication(sectionDocument, sectionCodeSamples, endpoint.Parameters));
            MergeStructuredApiRateLimit(endpoint.RateLimit, BuildStructuredApiRateLimit(sectionDocument, sectionCodeSamples, endpoint.ResponseExamples));
        }

        foreach (HtmlCrawlStructuredCodeSample sample in codeSamples.Where(sample => !string.IsNullOrWhiteSpace(sample.Method) && !string.IsNullOrWhiteSpace(sample.Path))) {
            HtmlCrawlStructuredApiEndpoint endpoint = GetOrCreateStructuredApiEndpoint(endpoints, sample.Method!, sample.Path!);
            endpoint.Title ??= sample.Title;
            endpoint.SelectorHint ??= sample.SelectorHint;
            if (!string.IsNullOrWhiteSpace(sample.Language)) {
                AppendDistinct(endpoint.ExampleLanguages, sample.Language!);
            }
            AppendDistinct(endpoint.Sources, "CodeSample");

            HtmlCrawlStructuredRequestExample? requestExample = BuildStructuredRequestExample(sample);
            if (requestExample != null
                && !endpoint.RequestExamples.Any(existing =>
                    string.Equals(existing.Method, requestExample.Method, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.Path, requestExample.Path, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.Body, requestExample.Body, StringComparison.Ordinal)
                    && string.Equals(existing.Title, requestExample.Title, StringComparison.OrdinalIgnoreCase))) {
                endpoint.RequestExamples.Add(requestExample);
            }
        }

        foreach (HtmlCrawlStructuredApiEndpoint endpoint in endpoints.Values) {
            endpoint.Resource ??= BuildStructuredApiPrimaryResource(endpoint.Path);
            foreach (string tag in BuildStructuredApiTags(endpoint.Path, endpoint.Title, endpoint.Description)) {
                AppendDistinct(endpoint.Tags, tag);
            }
            endpoint.OperationId ??= BuildStructuredApiOperationId(endpoint.Method, endpoint.Path, endpoint.Title);
            ApplyStructuredApiParameterGrouping(endpoint, pageUrl);
            if (!endpoint.Authentication.Required.HasValue
                && (endpoint.Authentication.Schemes.Count > 0 || endpoint.Authentication.Headers.Count > 0)) {
                endpoint.Authentication.Required = true;
            }
            endpoint.RequestHeaders = BuildStructuredEndpointRequestHeaders(endpoint);
            endpoint.ResponseHeaders = BuildStructuredEndpointResponseHeaders(endpoint);
            endpoint.ErrorResponses = endpoint.ResponseExamples
                .Where(response => response.IsError)
                .OrderBy(response => response.StatusCode ?? int.MaxValue)
                .ThenBy(response => response.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
            endpoint.ErrorCatalog = BuildStructuredEndpointErrorCatalog(endpoint.ErrorResponses);
            endpoint.SuccessResponseSchema = BuildStructuredEndpointResponseSchema(endpoint.ResponseExamples.Where(response => !response.IsError));
            endpoint.ErrorResponseSchema = BuildStructuredEndpointResponseSchema(endpoint.ErrorResponses);
            endpoint.SuccessResponseFields = BuildStructuredEndpointResponseFields(endpoint.ResponseExamples.Where(response => !response.IsError));
            endpoint.ErrorResponseFields = BuildStructuredEndpointResponseFields(endpoint.ErrorResponses);
        }

        return endpoints.Values
            .OrderBy(endpoint => endpoint.Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(endpoint => endpoint.Method, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HtmlCrawlStructuredApiCatalog BuildStructuredApiCatalog(
        HtmlCrawlStructuredMetadata metadata,
        IReadOnlyList<HtmlCrawlStructuredApiEndpoint> endpoints) {
        List<HtmlCrawlStructuredApiEndpoint> endpointList = endpoints
            .Where(endpoint => !string.IsNullOrWhiteSpace(endpoint.Method) && !string.IsNullOrWhiteSpace(endpoint.Path))
            .ToList();
        return new HtmlCrawlStructuredApiCatalog {
            Title = metadata.Title,
            Description = metadata.Description,
            OperationCount = endpointList.Count,
            PathCount = endpointList.Select(endpoint => endpoint.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            AuthenticatedOperationCount = endpointList.Count(endpoint =>
                endpoint.Authentication.Required == true
                || endpoint.Authentication.Schemes.Count > 0
                || endpoint.Authentication.Headers.Count > 0),
            RateLimitedOperationCount = endpointList.Count(endpoint =>
                endpoint.RateLimit.Mentioned
                || endpoint.RateLimit.StatusCode.HasValue
                || endpoint.RateLimit.Headers.Count > 0
                || !string.IsNullOrWhiteSpace(endpoint.RateLimit.Limit)),
            ErrorCatalogCount = endpointList.Sum(endpoint => endpoint.ErrorCatalog.Count),
            Resources = endpointList.Select(endpoint => endpoint.Resource)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToList(),
            Tags = endpointList.SelectMany(endpoint => endpoint.Tags)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            OperationIds = endpointList.Select(endpoint => endpoint.OperationId)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToList(),
            Paths = endpointList.Select(endpoint => endpoint.Path)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static HtmlCrawlStructuredOpenApiLike BuildStructuredOpenApiLike(
        HtmlCrawlPage page,
        HtmlCrawlStructuredMetadata metadata,
        IReadOnlyList<HtmlCrawlStructuredApiEndpoint> endpoints) {
        Dictionary<string, HtmlCrawlStructuredOpenApiPathItem> paths = new(StringComparer.OrdinalIgnoreCase);
        foreach (HtmlCrawlStructuredApiEndpoint endpoint in endpoints
                     .Where(endpoint => !string.IsNullOrWhiteSpace(endpoint.Method) && !string.IsNullOrWhiteSpace(endpoint.Path))
                     .OrderBy(endpoint => endpoint.Path, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(endpoint => endpoint.Method, StringComparer.OrdinalIgnoreCase)) {
            if (!paths.TryGetValue(endpoint.Path, out HtmlCrawlStructuredOpenApiPathItem? pathItem)) {
                pathItem = new HtmlCrawlStructuredOpenApiPathItem {
                    Path = endpoint.Path
                };
                paths[endpoint.Path] = pathItem;
            }

            if (!string.IsNullOrWhiteSpace(endpoint.Resource)) {
                AppendDistinct(pathItem.Resources, endpoint.Resource!);
            }

            pathItem.Operations[endpoint.Method.ToLowerInvariant()] = BuildStructuredOpenApiOperation(endpoint, page.Url);
        }

        HtmlCrawlStructuredOpenApiLike openApiLike = new() {
            Title = metadata.Title,
            Description = metadata.Description,
            Servers = BuildStructuredOpenApiServers(page, metadata),
            Tags = endpoints.SelectMany(endpoint => endpoint.Tags)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Resources = endpoints.Select(endpoint => endpoint.Resource)
                .Where(resource => !string.IsNullOrWhiteSpace(resource))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(resource => resource, StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToList(),
            Paths = paths
        };
        ApplyStructuredOpenApiComponents(openApiLike);
        AnnotateStructuredOpenApiPromotion(openApiLike);
        return openApiLike;
    }

    private static HtmlCrawlStructuredOpenApiLike BuildResultOpenApiLike(HtmlCrawlResult result) {
        List<(HtmlCrawlPage Page, HtmlCrawlStructuredApiEndpoint Endpoint)> endpointEntries = result.Pages
            .Where(page => page.StructuredJson != null)
            .SelectMany(page => (page.StructuredJson?.ApiEndpoints ?? Array.Empty<HtmlCrawlStructuredApiEndpoint>())
                .Where(endpoint => !string.IsNullOrWhiteSpace(endpoint.Method) && !string.IsNullOrWhiteSpace(endpoint.Path))
                .Select(endpoint => (Page: page, Endpoint: endpoint)))
            .ToList();

        Dictionary<string, HtmlCrawlStructuredOpenApiPathItem> paths = new(StringComparer.OrdinalIgnoreCase);
        foreach ((HtmlCrawlPage Page, HtmlCrawlStructuredApiEndpoint Endpoint) entry in endpointEntries
                     .OrderBy(item => item.Endpoint.Path, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Endpoint.Method, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Page.Url, StringComparer.OrdinalIgnoreCase)) {
            if (!paths.TryGetValue(entry.Endpoint.Path, out HtmlCrawlStructuredOpenApiPathItem? pathItem)) {
                pathItem = new HtmlCrawlStructuredOpenApiPathItem {
                    Path = entry.Endpoint.Path
                };
                paths[entry.Endpoint.Path] = pathItem;
            }

            if (!string.IsNullOrWhiteSpace(entry.Endpoint.Resource)) {
                AppendDistinct(pathItem.Resources, entry.Endpoint.Resource!);
            }

            string methodKey = entry.Endpoint.Method.ToLowerInvariant();
            if (!pathItem.Operations.TryGetValue(methodKey, out HtmlCrawlStructuredOpenApiOperation? operation)) {
                operation = BuildStructuredOpenApiOperation(entry.Endpoint, entry.Page.Url);
                pathItem.Operations[methodKey] = operation;
                continue;
            }

            MergeStructuredOpenApiOperation(operation, entry.Endpoint, entry.Page.Url);
        }

        HtmlCrawlStructuredMetadata? primaryMetadata = result.Pages
            .Select(page => page.StructuredJson?.Metadata)
            .FirstOrDefault(metadata => metadata != null && (!string.IsNullOrWhiteSpace(metadata.Title) || !string.IsNullOrWhiteSpace(metadata.Description)));

        HtmlCrawlStructuredOpenApiLike openApiLike = new() {
            Title = primaryMetadata?.Title,
            Description = primaryMetadata?.Description,
            Servers = BuildStructuredOpenApiServers(result.Pages.Select(page => page.Url)),
            Tags = endpointEntries.SelectMany(item => item.Endpoint.Tags)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Resources = endpointEntries.Select(item => item.Endpoint.Resource)
                .Where(resource => !string.IsNullOrWhiteSpace(resource))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(resource => resource, StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToList(),
            Paths = paths
        };
        ApplyStructuredOpenApiComponents(openApiLike);
        AnnotateStructuredOpenApiPromotion(openApiLike);
        return openApiLike;
    }

    private static HtmlCrawlStructuredOpenApiOperation BuildStructuredOpenApiOperation(HtmlCrawlStructuredApiEndpoint endpoint, string pageUrl) {
        return new HtmlCrawlStructuredOpenApiOperation {
            OperationId = endpoint.OperationId,
            Method = endpoint.Method.ToLowerInvariant(),
            Path = endpoint.Path,
            Summary = endpoint.Title,
            Description = endpoint.Description,
            Resource = endpoint.Resource,
            Tags = new List<string>(endpoint.Tags),
            Authentication = CloneStructuredApiAuthentication(endpoint.Authentication),
            RateLimit = CloneStructuredApiRateLimit(endpoint.RateLimit),
            Parameters = endpoint.Parameters.Select(CloneStructuredApiParameter).ToList(),
            RequestHeaders = endpoint.RequestHeaders.Select(CloneStructuredHttpHeader).ToList(),
            ResponseHeaders = endpoint.ResponseHeaders.Select(CloneStructuredHttpHeader).ToList(),
            RequestExamples = endpoint.RequestExamples.Select(CloneStructuredRequestExample).ToList(),
            ResponseExamples = endpoint.ResponseExamples.Select(CloneStructuredResponseExample).ToList(),
            ErrorCatalog = endpoint.ErrorCatalog.Select(CloneStructuredApiError).ToList(),
            RequestBodySchema = new Dictionary<string, string?>(endpoint.RequestBodySchema, StringComparer.OrdinalIgnoreCase),
            RequestBodyFields = endpoint.RequestBodyFields.Select(CloneStructuredField).ToList(),
            SuccessResponseSchema = new Dictionary<string, string?>(endpoint.SuccessResponseSchema, StringComparer.OrdinalIgnoreCase),
            SuccessResponseFields = endpoint.SuccessResponseFields.Select(CloneStructuredField).ToList(),
            ErrorResponseSchema = new Dictionary<string, string?>(endpoint.ErrorResponseSchema, StringComparer.OrdinalIgnoreCase),
            ErrorResponseFields = endpoint.ErrorResponseFields.Select(CloneStructuredField).ToList(),
            Provenance = BuildStructuredOpenApiProvenance(endpoint, pageUrl)
        };
    }

    private static void AnnotateStructuredOpenApiPromotion(HtmlCrawlStructuredOpenApiLike openApiLike) {
        List<HtmlCrawlStructuredOpenApiOperation> operations = openApiLike.Paths.Values
            .SelectMany(path => path.Operations.Values)
            .ToList();

        foreach (HtmlCrawlStructuredOpenApiOperation operation in operations) {
            AnnotateStructuredOpenApiPromotion(operation);
        }

        openApiLike.StrictOpenApiPromotionThreshold = StrictOpenApiPromotionThreshold;
        openApiLike.StrictOpenApiEligibleOperationCount = operations.Count(operation => operation.StrictOpenApiEligible);
        openApiLike.StrictOpenApiSkippedOperationCount = operations.Count - openApiLike.StrictOpenApiEligibleOperationCount;
        openApiLike.StrictOpenApiAverageScore = operations.Count == 0
            ? 0
            : Math.Round(operations.Average(operation => operation.StrictOpenApiScore), 2, MidpointRounding.AwayFromZero);
    }

    private static void AnnotateStructuredOpenApiPromotion(HtmlCrawlStructuredOpenApiOperation operation) {
        List<string> warnings = new();
        int score = 0;

        bool hasMethod = !string.IsNullOrWhiteSpace(operation.Method);
        bool hasPath = !string.IsNullOrWhiteSpace(operation.Path);
        bool hasOperationId = !string.IsNullOrWhiteSpace(operation.OperationId);
        bool hasSummary = !string.IsNullOrWhiteSpace(operation.Summary);
        bool hasDescription = !string.IsNullOrWhiteSpace(operation.Description);
        bool hasGrouping = !string.IsNullOrWhiteSpace(operation.Resource) || operation.Tags.Count > 0;
        bool hasRequestContract = operation.Parameters.Count > 0
            || operation.RequestBodyFields.Count > 0
            || operation.RequestBodySchema.Count > 0
            || operation.RequestExamples.Count > 0;
        bool hasResponseContract = operation.ResponseExamples.Count > 0
            || operation.SuccessResponseFields.Count > 0
            || operation.SuccessResponseSchema.Count > 0
            || operation.ErrorResponseFields.Count > 0
            || operation.ErrorResponseSchema.Count > 0;
        bool hasSuccessfulResponse = operation.ResponseExamples.Any(example => !example.IsError && example.StatusCode.GetValueOrDefault() < 400)
            || operation.SuccessResponseFields.Count > 0
            || operation.SuccessResponseSchema.Count > 0;
        bool hasErrorCoverage = operation.ResponseExamples.Any(example => example.IsError)
            || operation.ErrorCatalog.Count > 0
            || operation.ErrorResponseFields.Count > 0
            || operation.ErrorResponseSchema.Count > 0;
        bool hasAuthentication = operation.Authentication.Required != false
            || operation.Authentication.Headers.Count > 0
            || operation.Authentication.Schemes.Count > 0;
        bool hasRateLimit = operation.RateLimit.Mentioned
            || operation.RateLimit.StatusCode != null
            || operation.RateLimit.Headers.Count > 0;
        bool hasHeaders = operation.RequestHeaders.Count > 0 || operation.ResponseHeaders.Count > 0;

        if (hasMethod) {
            score += 10;
        } else {
            warnings.Add("missing method");
        }

        if (hasPath) {
            score += 10;
        } else {
            warnings.Add("missing path");
        }

        if (hasOperationId) {
            score += 10;
        } else {
            warnings.Add("missing operationId");
        }

        if (hasSummary) {
            score += 10;
        } else {
            warnings.Add("missing summary");
        }

        if (hasDescription) {
            score += 5;
        } else {
            warnings.Add("missing description");
        }

        if (hasGrouping) {
            score += 5;
        }

        if (operation.Parameters.Count > 0) {
            score += 8;
        }
        if (operation.RequestBodyFields.Count > 0 || operation.RequestBodySchema.Count > 0) {
            score += 8;
        }
        if (operation.RequestExamples.Count > 0) {
            score += 8;
        }
        if (!hasRequestContract) {
            warnings.Add("missing request contract");
        }

        if (hasSuccessfulResponse) {
            score += 20;
        } else {
            warnings.Add("missing success response contract");
        }

        if (hasResponseContract) {
            score += 8;
        } else {
            warnings.Add("missing response contract");
        }

        if (hasErrorCoverage) {
            score += 4;
        }
        if (hasAuthentication) {
            score += 4;
        }
        if (hasRateLimit) {
            score += 2;
        }
        if (hasHeaders) {
            score += 2;
        }

        operation.StrictOpenApiScore = Math.Min(score, 100);
        operation.StrictOpenApiEligible = hasMethod
            && hasPath
            && hasSummary
            && hasSuccessfulResponse
            && operation.StrictOpenApiScore >= StrictOpenApiPromotionThreshold;

        if (!operation.StrictOpenApiEligible && operation.StrictOpenApiScore < StrictOpenApiPromotionThreshold) {
            warnings.Add("promotion score below threshold");
        }

        operation.StrictOpenApiWarnings = warnings
            .Where(warning => !string.IsNullOrWhiteSpace(warning))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(warning => warning, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HtmlCrawlStructuredOpenApiProvenance BuildStructuredOpenApiProvenance(HtmlCrawlStructuredApiEndpoint endpoint, string pageUrl) {
        HtmlCrawlStructuredOpenApiProvenance provenance = new();
        AppendStructuredOpenApiProvenanceEntry(provenance, pageUrl, "Endpoint", endpoint.SelectorHint, endpoint.Title);

        foreach (string sourceKind in endpoint.Sources) {
            AppendDistinct(provenance.SourceKinds, sourceKind);
            AppendStructuredOpenApiProvenanceEntry(provenance, pageUrl, sourceKind, endpoint.SelectorHint, endpoint.Title);
        }

        foreach (HtmlCrawlStructuredRequestExample example in endpoint.RequestExamples) {
            AppendStructuredOpenApiProvenanceEntry(provenance, pageUrl, "RequestExample", example.SelectorHint, example.Title ?? example.Method ?? example.Path);
        }

        foreach (HtmlCrawlStructuredResponseExample example in endpoint.ResponseExamples) {
            string? label = example.Title
                ?? (example.StatusCode.HasValue ? $"Response {example.StatusCode.Value}" : null)
                ?? example.Description;
            AppendStructuredOpenApiProvenanceEntry(provenance, pageUrl, "ResponseExample", example.SelectorHint, label);
        }

        foreach (HtmlCrawlStructuredApiError error in endpoint.ErrorCatalog) {
            string? label = error.Summary
                ?? (error.StatusCode.HasValue ? $"Error {error.StatusCode.Value}" : null)
                ?? error.StatusText;
            AppendStructuredOpenApiProvenanceEntry(provenance, pageUrl, "ErrorCatalog", error.SelectorHint, label);
        }

        if (endpoint.Parameters.Count > 0) {
            string? parameterSource = endpoint.Parameters
                .Select(parameter => parameter.Location)
                .FirstOrDefault(location => !string.IsNullOrWhiteSpace(location));
            AppendStructuredOpenApiProvenanceEntry(provenance, pageUrl, "ParameterTable", endpoint.SelectorHint, parameterSource);
        }

        return provenance;
    }

    private static void MergeStructuredOpenApiProvenance(HtmlCrawlStructuredOpenApiProvenance target, HtmlCrawlStructuredOpenApiProvenance source) {
        foreach (string pageUrl in source.PageUrls) {
            AppendDistinct(target.PageUrls, pageUrl);
        }

        foreach (string kind in source.SourceKinds) {
            AppendDistinct(target.SourceKinds, kind);
        }

        foreach (HtmlCrawlStructuredOpenApiProvenanceEntry entry in source.Entries) {
            if (target.Entries.Any(existing =>
                    string.Equals(existing.PageUrl, entry.PageUrl, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.Kind, entry.Kind, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.SelectorHint, entry.SelectorHint, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.Label, entry.Label, StringComparison.OrdinalIgnoreCase))) {
                continue;
            }

            target.Entries.Add(CloneStructuredOpenApiProvenanceEntry(entry));
        }
    }

    private static void AppendStructuredOpenApiProvenanceEntry(
        HtmlCrawlStructuredOpenApiProvenance provenance,
        string pageUrl,
        string kind,
        string? selectorHint,
        string? label) {
        if (string.IsNullOrWhiteSpace(pageUrl) || string.IsNullOrWhiteSpace(kind)) {
            return;
        }

        AppendDistinct(provenance.PageUrls, pageUrl);
        AppendDistinct(provenance.SourceKinds, kind);

        if (provenance.Entries.Any(existing =>
                string.Equals(existing.PageUrl, pageUrl, StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.Kind, kind, StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.SelectorHint, selectorHint, StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.Label, label, StringComparison.OrdinalIgnoreCase))) {
            return;
        }

        provenance.Entries.Add(new HtmlCrawlStructuredOpenApiProvenanceEntry {
            PageUrl = pageUrl,
            Kind = kind,
            SelectorHint = selectorHint,
            Label = label
        });
    }

    private static void MergeStructuredOpenApiOperation(HtmlCrawlStructuredOpenApiOperation target, HtmlCrawlStructuredApiEndpoint source, string pageUrl) {
        target.OperationId ??= source.OperationId;
        target.Summary ??= source.Title;
        target.Description ??= source.Description;
        target.Resource ??= source.Resource;
        foreach (string tag in source.Tags) {
            AppendDistinct(target.Tags, tag);
        }

        MergeStructuredApiAuthentication(target.Authentication, source.Authentication);
        MergeStructuredApiRateLimit(target.RateLimit, source.RateLimit);

        foreach (HtmlCrawlStructuredApiParameter parameter in source.Parameters) {
            string incomingLocation = ResolveStructuredApiParameterLocation(target.Path, parameter);
            HtmlCrawlStructuredApiParameter? existing = target.Parameters.FirstOrDefault(current =>
                string.Equals(current.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(ResolveStructuredApiParameterLocation(target.Path, current), incomingLocation, StringComparison.OrdinalIgnoreCase));
            if (existing == null) {
                target.Parameters.Add(CloneStructuredApiParameter(parameter));
                continue;
            }

            MergeStructuredApiParameter(existing, parameter);
        }

        foreach (HtmlCrawlStructuredHttpHeader header in source.RequestHeaders) {
            AppendStructuredHeader(target.RequestHeaders, header.Name, header.Value);
        }
        foreach (HtmlCrawlStructuredHttpHeader header in source.ResponseHeaders) {
            AppendStructuredHeader(target.ResponseHeaders, header.Name, header.Value);
        }

        foreach (HtmlCrawlStructuredRequestExample example in source.RequestExamples) {
            if (!target.RequestExamples.Any(existing =>
                    string.Equals(existing.Method, example.Method, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.Path, example.Path, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.Body, example.Body, StringComparison.Ordinal)
                    && string.Equals(existing.Title, example.Title, StringComparison.OrdinalIgnoreCase))) {
                target.RequestExamples.Add(CloneStructuredRequestExample(example));
            }
        }

        foreach (HtmlCrawlStructuredResponseExample example in source.ResponseExamples) {
            if (!target.ResponseExamples.Any(existing =>
                    existing.StatusCode == example.StatusCode
                    && string.Equals(existing.Body, example.Body, StringComparison.Ordinal)
                    && string.Equals(existing.Title, example.Title, StringComparison.OrdinalIgnoreCase))) {
                target.ResponseExamples.Add(CloneStructuredResponseExample(example));
            }
        }

        foreach (HtmlCrawlStructuredApiError error in source.ErrorCatalog) {
            HtmlCrawlStructuredApiError? existing = target.ErrorCatalog.FirstOrDefault(item =>
                item.StatusCode == error.StatusCode
                && string.Equals(item.StatusText, error.StatusText, StringComparison.OrdinalIgnoreCase));
            if (existing == null) {
                target.ErrorCatalog.Add(CloneStructuredApiError(error));
                continue;
            }

            existing.Summary ??= error.Summary;
            existing.ContentType ??= error.ContentType;
            existing.SelectorHint ??= error.SelectorHint;
            existing.SampleCount += error.SampleCount;
            foreach (HtmlCrawlStructuredHttpHeader header in error.Headers) {
                AppendStructuredHeader(existing.Headers, header.Name, header.Value);
            }
            MergeStructuredSchemaMaps(existing.Schema, error.Schema);
            existing.Fields = MergeStructuredFieldCollections(existing.Fields, error.Fields);
        }

        MergeStructuredSchemaMaps(target.RequestBodySchema, source.RequestBodySchema);
        MergeStructuredSchemaMaps(target.SuccessResponseSchema, source.SuccessResponseSchema);
        MergeStructuredSchemaMaps(target.ErrorResponseSchema, source.ErrorResponseSchema);
        target.RequestBodyFields = MergeStructuredFieldCollections(target.RequestBodyFields, source.RequestBodyFields);
        target.SuccessResponseFields = MergeStructuredFieldCollections(target.SuccessResponseFields, source.SuccessResponseFields);
        target.ErrorResponseFields = MergeStructuredFieldCollections(target.ErrorResponseFields, source.ErrorResponseFields);
        MergeStructuredOpenApiProvenance(target.Provenance, BuildStructuredOpenApiProvenance(source, pageUrl));
    }

    private static IList<HtmlCrawlStructuredField> MergeStructuredFieldCollections(
        IEnumerable<HtmlCrawlStructuredField> first,
        IEnumerable<HtmlCrawlStructuredField> second) {
        Dictionary<string, HtmlCrawlStructuredField> fields = new(StringComparer.OrdinalIgnoreCase);
        foreach (HtmlCrawlStructuredField field in first.Concat(second)) {
            if (!fields.TryGetValue(field.Path, out HtmlCrawlStructuredField? existing)) {
                fields[field.Path] = CloneStructuredField(field);
                continue;
            }

            if (string.IsNullOrWhiteSpace(existing.Name) && !string.IsNullOrWhiteSpace(field.Name)) {
                existing.Name = field.Name;
            }
            existing.ParentPath ??= field.ParentPath;
            existing.Kind = MergeStructuredFieldKinds(existing.Kind, field.Kind);
            existing.Depth = Math.Min(existing.Depth, field.Depth);
            existing.Type = MergeStructuredTypeValues(existing.Type, field.Type);
            existing.Format ??= field.Format;
            existing.Required = existing.Required == true && field.Required == true
                ? true
                : existing.Required ?? field.Required;
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

        return FinalizeStructuredFieldConfidence(FinalizeStructuredFieldRelationships(fields.Values))
            .OrderBy(field => field.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AppendStructuredFieldProvenance(
        HtmlCrawlStructuredField field,
        string pageUrl,
        string kind,
        string? selectorHint,
        string? label) {
        if (field == null || string.IsNullOrWhiteSpace(pageUrl) || string.IsNullOrWhiteSpace(kind)) {
            return;
        }

        if (field.Provenance.Any(existing =>
                string.Equals(existing.PageUrl, pageUrl, StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.Kind, kind, StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.SelectorHint, selectorHint, StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.Label, label, StringComparison.OrdinalIgnoreCase))) {
            return;
        }

        field.Provenance.Add(new HtmlCrawlStructuredFieldProvenanceEntry {
            PageUrl = pageUrl,
            Kind = kind,
            SelectorHint = selectorHint,
            Label = label
        });
    }

    private static void MergeStructuredFieldProvenance(HtmlCrawlStructuredField target, HtmlCrawlStructuredField source) {
        foreach (HtmlCrawlStructuredFieldProvenanceEntry provenance in source.Provenance) {
            AppendStructuredFieldProvenance(target, provenance.PageUrl, provenance.Kind, provenance.SelectorHint, provenance.Label);
        }
    }

    private static IList<HtmlCrawlStructuredField> FinalizeStructuredFieldConfidence(IEnumerable<HtmlCrawlStructuredField> fields) {
        List<HtmlCrawlStructuredField> fieldList = fields.ToList();
        foreach (HtmlCrawlStructuredField field in fieldList) {
            field.EvidenceCount = field.Provenance
                .Select(entry => string.Join("|",
                    entry.PageUrl ?? string.Empty,
                    entry.Kind ?? string.Empty,
                    entry.SelectorHint ?? string.Empty,
                    entry.Label ?? string.Empty))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            field.ConfidenceScore = ComputeStructuredFieldConfidence(field);
        }

        return fieldList;
    }

    private static int ComputeStructuredFieldConfidence(HtmlCrawlStructuredField field) {
        int score = 20;
        int evidenceCount = field.EvidenceCount > 0 ? field.EvidenceCount : field.Provenance.Count;
        int sourceKindCount = field.Provenance
            .Select(entry => entry.Kind)
            .Where(kind => !string.IsNullOrWhiteSpace(kind))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        if (field.Provenance.Any(entry => string.Equals(entry.Kind, "ParameterTable", StringComparison.OrdinalIgnoreCase))) {
            score += 40;
        }
        if (field.Provenance.Any(entry => string.Equals(entry.Kind, "JsonResponse", StringComparison.OrdinalIgnoreCase))) {
            score += 25;
        }
        if (field.Provenance.Any(entry => string.Equals(entry.Kind, "JsonSchemaMap", StringComparison.OrdinalIgnoreCase))) {
            score += 10;
        }
        if (field.Required == true) {
            score += 10;
        }
        if (!string.IsNullOrWhiteSpace(field.Type)) {
            score += 5;
        }
        if (!string.IsNullOrWhiteSpace(field.Format)) {
            score += 5;
        }
        if (!string.IsNullOrWhiteSpace(field.ExampleValue)) {
            score += 5;
        }
        if (field.EnumValues.Count > 0) {
            score += 5;
        }
        if (field.ChildPaths.Count > 0) {
            score += 5;
        }

        score += Math.Min(evidenceCount * 5, 15);
        score += Math.Min(sourceKindCount * 5, 10);

        return Math.Min(score, 100);
    }

    private static void ApplyStructuredOpenApiComponents(HtmlCrawlStructuredOpenApiLike openApiLike) {
        HtmlCrawlStructuredOpenApiComponents components = new();
        Dictionary<string, string> schemaRefs = new(StringComparer.Ordinal);
        Dictionary<string, string> fieldSetRefs = new(StringComparer.Ordinal);
        Dictionary<string, string> authRefs = new(StringComparer.Ordinal);
        Dictionary<string, string> rateLimitRefs = new(StringComparer.Ordinal);
        Dictionary<string, string> parameterSetRefs = new(StringComparer.Ordinal);
        Dictionary<string, string> requestHeaderSetRefs = new(StringComparer.Ordinal);
        Dictionary<string, string> responseHeaderSetRefs = new(StringComparer.Ordinal);
        Dictionary<string, string> requestExampleSetRefs = new(StringComparer.Ordinal);
        Dictionary<string, string> responseExampleSetRefs = new(StringComparer.Ordinal);
        Dictionary<string, string> errorCatalogRefs = new(StringComparer.Ordinal);

        foreach (HtmlCrawlStructuredOpenApiOperation operation in openApiLike.Paths.Values
                     .SelectMany(path => path.Operations.Values)
                     .OrderBy(operation => operation.Path, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(operation => operation.Method, StringComparer.OrdinalIgnoreCase)) {
            if (HasStructuredAuthProfile(operation.Authentication)) {
                operation.AuthenticationRef = GetOrAddStructuredAuthProfileComponent(components, authRefs, operation.Authentication);
            }
            if (HasStructuredRateLimitProfile(operation.RateLimit)) {
                operation.RateLimitRef = GetOrAddStructuredRateLimitProfileComponent(components, rateLimitRefs, operation.RateLimit);
            }
            if (operation.Parameters.Count > 0) {
                operation.ParametersRef = GetOrAddStructuredParameterSetComponent(components, parameterSetRefs, operation.Parameters);
            }
            if (operation.RequestHeaders.Count > 0) {
                operation.RequestHeadersRef = GetOrAddStructuredHeaderSetComponent(components, requestHeaderSetRefs, "requestHeaderSet", operation.RequestHeaders);
            }
            if (operation.ResponseHeaders.Count > 0) {
                operation.ResponseHeadersRef = GetOrAddStructuredHeaderSetComponent(components, responseHeaderSetRefs, "responseHeaderSet", operation.ResponseHeaders);
            }
            if (operation.RequestExamples.Count > 0) {
                operation.RequestExamplesRef = GetOrAddStructuredRequestExampleSetComponent(components, requestExampleSetRefs, operation.RequestExamples);
            }
            if (operation.ResponseExamples.Count > 0) {
                operation.ResponseExamplesRef = GetOrAddStructuredResponseExampleSetComponent(components, responseExampleSetRefs, operation.ResponseExamples);
            }
            if (operation.ErrorCatalog.Count > 0) {
                operation.ErrorCatalogRef = GetOrAddStructuredErrorCatalogComponent(components, errorCatalogRefs, operation.ErrorCatalog);
            }
            if (operation.RequestBodySchema.Count > 0) {
                operation.RequestBodySchemaRef = GetOrAddStructuredSchemaComponent(components, schemaRefs, "requestBodySchema", operation.RequestBodySchema);
            }
            if (operation.SuccessResponseSchema.Count > 0) {
                operation.SuccessResponseSchemaRef = GetOrAddStructuredSchemaComponent(components, schemaRefs, "successResponseSchema", operation.SuccessResponseSchema);
            }
            if (operation.ErrorResponseSchema.Count > 0) {
                operation.ErrorResponseSchemaRef = GetOrAddStructuredSchemaComponent(components, schemaRefs, "errorResponseSchema", operation.ErrorResponseSchema);
            }
            if (operation.RequestBodyFields.Count > 0) {
                operation.RequestBodyFieldsRef = GetOrAddStructuredFieldSetComponent(components, fieldSetRefs, "requestBodyFields", operation.RequestBodyFields);
            }
            if (operation.SuccessResponseFields.Count > 0) {
                operation.SuccessResponseFieldsRef = GetOrAddStructuredFieldSetComponent(components, fieldSetRefs, "successResponseFields", operation.SuccessResponseFields);
            }
            if (operation.ErrorResponseFields.Count > 0) {
                operation.ErrorResponseFieldsRef = GetOrAddStructuredFieldSetComponent(components, fieldSetRefs, "errorResponseFields", operation.ErrorResponseFields);
            }
        }

        openApiLike.Components = components;
    }

}
