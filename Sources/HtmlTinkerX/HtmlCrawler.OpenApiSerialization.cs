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
    private static Dictionary<string, object?> BuildResultOpenApiDocument(HtmlCrawlStructuredOpenApiLike openApiLike, HtmlCrawlResult result) {
        Dictionary<string, object?> paths = BuildStrictOpenApiPaths(openApiLike);
        Dictionary<string, object?> document = new(StringComparer.OrdinalIgnoreCase) {
            ["openapi"] = "3.1.0",
            ["info"] = new Dictionary<string, object?> {
                ["title"] = openApiLike.Title ?? "Offline API",
                ["description"] = openApiLike.Description,
                ["version"] = "0.0.0-offline"
            },
            ["servers"] = openApiLike.Servers.Select(server => new Dictionary<string, object?> {
                ["url"] = server
            }).ToList(),
            ["paths"] = paths
        };

        Dictionary<string, object?> components = BuildStrictOpenApiComponents(openApiLike);
        if (components.Count > 0) {
            document["components"] = components;
        }

        document["x-htmltinkerx-openApiLikePath"] = result.OpenApiLikePath;
        document["x-htmltinkerx-startUrl"] = result.StartUrl;
        document["x-htmltinkerx-operationCount"] = openApiLike.Paths.Values.Sum(path => path.Operations.Count);
        document["x-htmltinkerx-promotion"] = BuildStrictOpenApiPromotionMetadata(openApiLike, paths);
        return document;
    }

    private static Dictionary<string, object?> BuildStrictOpenApiPaths(HtmlCrawlStructuredOpenApiLike openApiLike) {
        Dictionary<string, object?> paths = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, HtmlCrawlStructuredOpenApiPathItem> pathItem in openApiLike.Paths.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
            Dictionary<string, object?> operations = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, HtmlCrawlStructuredOpenApiOperation> operationItem in pathItem.Value.Operations.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
                if (!operationItem.Value.StrictOpenApiEligible) {
                    continue;
                }

                operations[operationItem.Key] = BuildStrictOpenApiOperation(operationItem.Value);
            }

            if (operations.Count > 0) {
                paths[pathItem.Key] = operations;
            }
        }

        return paths;
    }

    private static Dictionary<string, object?> BuildStrictOpenApiOperation(HtmlCrawlStructuredOpenApiOperation operation) {
        Dictionary<string, object?> value = new(StringComparer.OrdinalIgnoreCase) {
            ["operationId"] = operation.OperationId,
            ["summary"] = operation.Summary,
            ["description"] = operation.Description,
            ["tags"] = operation.Tags
        };

        List<object> parameters = BuildStrictOpenApiParameters(operation);
        if (parameters.Count > 0) {
            value["parameters"] = parameters;
        }

        object? requestBody = BuildStrictOpenApiRequestBody(operation);
        if (requestBody != null) {
            value["requestBody"] = requestBody;
        }

        value["responses"] = BuildStrictOpenApiResponses(operation);

        if (operation.Authentication.Required != false && !string.IsNullOrWhiteSpace(operation.AuthenticationRef)) {
            value["security"] = new List<object> {
                new Dictionary<string, object?> {
                    [operation.AuthenticationRef!] = Array.Empty<string>()
                }
            };
        }

        AddStrictOpenApiExtension(value, "x-htmltinkerx-resource", operation.Resource);
        AddStrictOpenApiExtension(value, "x-htmltinkerx-rateLimitRef", operation.RateLimitRef);
        AddStrictOpenApiExtension(value, "x-htmltinkerx-parametersRef", operation.ParametersRef);
        AddStrictOpenApiExtension(value, "x-htmltinkerx-requestHeadersRef", operation.RequestHeadersRef);
        AddStrictOpenApiExtension(value, "x-htmltinkerx-responseHeadersRef", operation.ResponseHeadersRef);
        AddStrictOpenApiExtension(value, "x-htmltinkerx-requestExamplesRef", operation.RequestExamplesRef);
        AddStrictOpenApiExtension(value, "x-htmltinkerx-responseExamplesRef", operation.ResponseExamplesRef);
        AddStrictOpenApiExtension(value, "x-htmltinkerx-errorCatalogRef", operation.ErrorCatalogRef);
        AddStrictOpenApiExtension(value, "x-htmltinkerx-promotionScore", operation.StrictOpenApiScore);
        if (operation.StrictOpenApiWarnings.Count > 0) {
            value["x-htmltinkerx-promotionWarnings"] = operation.StrictOpenApiWarnings.ToList();
        }
        if (operation.Provenance.PageUrls.Count > 0) {
            value["x-htmltinkerx-sourcePages"] = operation.Provenance.PageUrls.ToList();
        }
        if (operation.Provenance.Entries.Count > 0) {
            value["x-htmltinkerx-provenance"] = operation.Provenance.Entries
                .Select(entry => new Dictionary<string, object?> {
                    ["pageUrl"] = entry.PageUrl,
                    ["kind"] = entry.Kind,
                    ["selectorHint"] = entry.SelectorHint,
                    ["label"] = entry.Label
                })
                .ToList();
        }
        return value;
    }

    private static Dictionary<string, object?> BuildStrictOpenApiPromotionMetadata(
        HtmlCrawlStructuredOpenApiLike openApiLike,
        IReadOnlyDictionary<string, object?> strictPaths) {
        List<Dictionary<string, object?>> skippedOperations = openApiLike.Paths
            .SelectMany(path => path.Value.Operations.Values)
            .Where(operation => !operation.StrictOpenApiEligible)
            .OrderByDescending(operation => operation.StrictOpenApiScore)
            .ThenBy(operation => operation.Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(operation => operation.Method, StringComparer.OrdinalIgnoreCase)
            .Select(operation => new Dictionary<string, object?> {
                ["operationId"] = operation.OperationId,
                ["method"] = operation.Method,
                ["path"] = operation.Path,
                ["score"] = operation.StrictOpenApiScore,
                ["warnings"] = operation.StrictOpenApiWarnings.ToList()
            })
            .ToList();

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) {
            ["threshold"] = openApiLike.StrictOpenApiPromotionThreshold,
            ["eligibleOperationCount"] = openApiLike.StrictOpenApiEligibleOperationCount,
            ["skippedOperationCount"] = openApiLike.StrictOpenApiSkippedOperationCount,
            ["promotedPathCount"] = strictPaths.Count,
            ["averageScore"] = openApiLike.StrictOpenApiAverageScore,
            ["skippedOperations"] = skippedOperations
        };
    }

    private static List<object> BuildStrictOpenApiParameters(HtmlCrawlStructuredOpenApiOperation operation) {
        return operation.Parameters
            .Where(parameter => !string.Equals(ResolveStructuredApiParameterLocation(operation.Path, parameter), "body", StringComparison.OrdinalIgnoreCase))
            .OrderBy(parameter => ResolveStructuredApiParameterLocation(operation.Path, parameter), StringComparer.OrdinalIgnoreCase)
            .ThenBy(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase)
            .Select(parameter => {
                string location = ResolveStructuredApiParameterLocation(operation.Path, parameter);
                Dictionary<string, object?> value = new(StringComparer.OrdinalIgnoreCase) {
                    ["name"] = parameter.Name,
                    ["in"] = NormalizeStrictOpenApiParameterLocation(location),
                    ["required"] = string.Equals(location, "path", StringComparison.OrdinalIgnoreCase) || parameter.Required == true,
                    ["description"] = parameter.Description,
                    ["schema"] = BuildStrictOpenApiParameterSchema(parameter)
                };

                if (!string.IsNullOrWhiteSpace(parameter.ExampleValue)) {
                    value["example"] = ParseStrictOpenApiExampleValue(parameter.ExampleValue);
                }
                if (!string.IsNullOrWhiteSpace(parameter.DefaultValue)) {
                    value["x-htmltinkerx-default"] = parameter.DefaultValue;
                }
                return (object)value;
            })
            .ToList();
    }

    private static object BuildStrictOpenApiParameterSchema(HtmlCrawlStructuredApiParameter parameter) {
        Dictionary<string, object?> schema = new(StringComparer.OrdinalIgnoreCase);
        ApplyStrictOpenApiType(schema, parameter.Type, parameter.Format);
        if (parameter.Nullable == true) {
            schema["nullable"] = true;
        }
        if (!string.IsNullOrWhiteSpace(parameter.Pattern)) {
            schema["pattern"] = parameter.Pattern;
        }
        if (parameter.EnumValues.Count > 0) {
            schema["enum"] = parameter.EnumValues.Cast<object>().ToList();
        }

        return schema;
    }

    private static object? BuildStrictOpenApiRequestBody(HtmlCrawlStructuredOpenApiOperation operation) {
        List<HtmlCrawlStructuredRequestExample> bodyExamples = operation.RequestExamples
            .Where(example => !string.IsNullOrWhiteSpace(example.Body))
            .ToList();
        bool hasSchema = !string.IsNullOrWhiteSpace(operation.RequestBodyFieldsRef) || !string.IsNullOrWhiteSpace(operation.RequestBodySchemaRef);
        bool hasExamples = bodyExamples.Count > 0;
        if (!hasSchema && !hasExamples) {
            return null;
        }

        string contentType = operation.RequestHeaders
            .FirstOrDefault(header => string.Equals(header.Name, "Content-Type", StringComparison.OrdinalIgnoreCase))
            ?.Value
            ?? bodyExamples.Select(example => example.ContentType).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? "application/json";

        Dictionary<string, object?> mediaType = new(StringComparer.OrdinalIgnoreCase);
        object? schema = BuildStrictOpenApiSchemaReference(operation.RequestBodyFieldsRef, operation.RequestBodySchemaRef);
        if (schema != null) {
            mediaType["schema"] = schema;
        }

        Dictionary<string, object?> examples = BuildStrictOpenApiRequestExamples(bodyExamples);
        if (examples.Count > 0) {
            mediaType["examples"] = examples;
        }

        return new Dictionary<string, object?> {
            ["required"] = operation.Parameters.Any(parameter => string.Equals(ResolveStructuredApiParameterLocation(operation.Path, parameter), "body", StringComparison.OrdinalIgnoreCase) && parameter.Required == true),
            ["content"] = new Dictionary<string, object?> {
                [contentType] = mediaType
            }
        };
    }

    private static Dictionary<string, object?> BuildStrictOpenApiResponses(HtmlCrawlStructuredOpenApiOperation operation) {
        Dictionary<string, object?> responses = new(StringComparer.OrdinalIgnoreCase);

        foreach (IGrouping<string, HtmlCrawlStructuredResponseExample> group in operation.ResponseExamples
                     .GroupBy(example => GetStrictOpenApiResponseCode(example.StatusCode), StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)) {
            bool isError = group.Any(example => example.IsError);
            responses[group.Key] = BuildStrictOpenApiResponse(group.ToList(), isError, operation);
        }

        if (responses.Count == 0) {
            responses["default"] = new Dictionary<string, object?> {
                ["description"] = operation.Description ?? "Documented response"
            };
        }

        return responses;
    }

    private static Dictionary<string, object?> BuildStrictOpenApiResponse(
        IReadOnlyList<HtmlCrawlStructuredResponseExample> examples,
        bool isError,
        HtmlCrawlStructuredOpenApiOperation operation) {
        HtmlCrawlStructuredResponseExample primary = examples[0];
        Dictionary<string, object?> response = new(StringComparer.OrdinalIgnoreCase) {
            ["description"] = primary.StatusText ?? primary.Title ?? primary.Description ?? (isError ? "Error response" : "Successful response")
        };

        Dictionary<string, object?> headers = BuildStrictOpenApiHeaderDefinitions(examples.SelectMany(example => example.Headers));
        if (headers.Count > 0) {
            response["headers"] = headers;
        }

        string? contentType = examples.Select(example => example.ContentType).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        object? schema = BuildStrictOpenApiSchemaReference(
            isError ? operation.ErrorResponseFieldsRef : operation.SuccessResponseFieldsRef,
            isError ? operation.ErrorResponseSchemaRef : operation.SuccessResponseSchemaRef);
        Dictionary<string, object?> exampleEntries = BuildStrictOpenApiResponseExamples(examples);
        if (schema != null || exampleEntries.Count > 0) {
            Dictionary<string, object?> mediaType = new(StringComparer.OrdinalIgnoreCase);
            if (schema != null) {
                mediaType["schema"] = schema;
            }
            if (exampleEntries.Count > 0) {
                mediaType["examples"] = exampleEntries;
            }

            response["content"] = new Dictionary<string, object?> {
                [contentType ?? "application/json"] = mediaType
            };
        }

        return response;
    }

    private static Dictionary<string, object?> BuildStrictOpenApiComponents(HtmlCrawlStructuredOpenApiLike openApiLike) {
        Dictionary<string, object?> components = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, object?> securitySchemes = BuildStrictOpenApiSecuritySchemes(openApiLike.Components.AuthProfiles);
        if (securitySchemes.Count > 0) {
            components["securitySchemes"] = securitySchemes;
        }

        Dictionary<string, object?> schemas = BuildStrictOpenApiSchemas(openApiLike.Components);
        if (schemas.Count > 0) {
            components["schemas"] = schemas;
        }

        AddStrictOpenApiComponentExtension(components, "x-htmltinkerx-rateLimitProfiles", openApiLike.Components.RateLimitProfiles);
        AddStrictOpenApiComponentExtension(components, "x-htmltinkerx-parameterSets", openApiLike.Components.ParameterSets);
        AddStrictOpenApiComponentExtension(components, "x-htmltinkerx-requestHeaderSets", openApiLike.Components.RequestHeaderSets);
        AddStrictOpenApiComponentExtension(components, "x-htmltinkerx-responseHeaderSets", openApiLike.Components.ResponseHeaderSets);
        AddStrictOpenApiComponentExtension(components, "x-htmltinkerx-requestExampleSets", openApiLike.Components.RequestExampleSets);
        AddStrictOpenApiComponentExtension(components, "x-htmltinkerx-responseExampleSets", openApiLike.Components.ResponseExampleSets);
        AddStrictOpenApiComponentExtension(components, "x-htmltinkerx-errorCatalogs", openApiLike.Components.ErrorCatalogs);
        AddStrictOpenApiComponentExtension(components, "x-htmltinkerx-schemaProvenance", BuildStrictOpenApiSchemaComponentProvenance(openApiLike.Components.FieldSets));
        return components;
    }

    private static Dictionary<string, object?> BuildStrictOpenApiSecuritySchemes(IDictionary<string, HtmlCrawlStructuredApiAuthentication> authProfiles) {
        Dictionary<string, object?> securitySchemes = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, HtmlCrawlStructuredApiAuthentication> item in authProfiles.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
            Dictionary<string, object?> scheme = new(StringComparer.OrdinalIgnoreCase);
            string? primaryHeader = item.Value.Headers.FirstOrDefault();
            if (item.Value.Schemes.Any(schemeName => string.Equals(schemeName, "oauth2", StringComparison.OrdinalIgnoreCase))) {
                scheme["type"] = "oauth2";
                scheme["flows"] = new Dictionary<string, object?> {
                    ["clientCredentials"] = new Dictionary<string, object?> {
                        ["tokenUrl"] = "https://example.invalid/token",
                        ["scopes"] = new Dictionary<string, object?>()
                    }
                };
                scheme["x-htmltinkerx-oauth2FlowPlaceholder"] = true;
            } else if (item.Value.Schemes.Any(schemeName => string.Equals(schemeName, "bearer", StringComparison.OrdinalIgnoreCase))) {
                scheme["type"] = "http";
                scheme["scheme"] = "bearer";
            } else if (item.Value.Schemes.Any(schemeName => string.Equals(schemeName, "basic", StringComparison.OrdinalIgnoreCase))) {
                scheme["type"] = "http";
                scheme["scheme"] = "basic";
            } else {
                scheme["type"] = "apiKey";
                scheme["in"] = "header";
                scheme["name"] = primaryHeader ?? "Authorization";
            }

            if (!string.IsNullOrWhiteSpace(item.Value.Summary)) {
                scheme["description"] = item.Value.Summary;
            }
            securitySchemes[item.Key] = scheme;
        }

        return securitySchemes;
    }

    private static Dictionary<string, object?> BuildStrictOpenApiSchemas(HtmlCrawlStructuredOpenApiComponents components) {
        Dictionary<string, object?> schemas = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, IList<HtmlCrawlStructuredField>> item in components.FieldSets.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
            schemas[item.Key] = BuildStrictOpenApiSchemaFromFields(item.Value);
        }

        foreach (KeyValuePair<string, IDictionary<string, string?>> item in components.Schemas.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
            if (!schemas.ContainsKey(item.Key)) {
                schemas[item.Key] = BuildStrictOpenApiSchemaFromFlatMap(item.Value);
            }
        }

        return schemas;
    }

    private static Dictionary<string, object?> BuildStrictOpenApiSchemaComponentProvenance(IDictionary<string, IList<HtmlCrawlStructuredField>> fieldSets) {
        Dictionary<string, object?> provenance = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, IList<HtmlCrawlStructuredField>> item in fieldSets.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)) {
            List<Dictionary<string, object?>> entries = BuildStrictOpenApiFieldProvenance(item.Value.SelectMany(field => field.Provenance));
            if (entries.Count > 0) {
                provenance[item.Key] = entries;
            }
        }

        return provenance;
    }

    private static string BuildPageManifestJson(
        HtmlCrawlPage page,
        IEnumerable<HtmlCrawlAsset> assets,
        IDictionary<string, string> localPageMap,
        IDictionary<string, string> assetMap) {
        string? manifestPath = page.ManifestPath;
        PageSearchMetadata searchMetadata = BuildPageSearchMetadata(page);
        object manifest = new {
            page.Url,
            page.RequestedUrl,
            page.ParentUrl,
            page.CanonicalUrl,
            page.Depth,
            page.Status,
            page.SkipReason,
            page.StatusCode,
            page.ContentType,
            page.Title,
            page.Rendered,
            page.RenderMode,
            page.RenderReasonCode,
            page.RenderReason,
            page.AppliedScenario,
            page.AppliedProfileName,
            page.AppliedProfileReasonCode,
            page.AppliedProfileReason,
            Extraction = new {
                page.ContentModeUsed,
                page.ContentSelectionReasonCode,
                page.ContentSelectionReason,
                page.ContentElementTag,
                page.ContentElementId,
                page.ContentElementClasses,
                page.ContentElementSelectorHint,
                page.ContentSelectionScore,
                page.ReaderCandidateCount,
                page.ReaderRootElementSelectorHint
            },
            BestContentComparison = page.BestContentComparisonMode == null ? null : new {
                page.BestContentComparisonMode,
                page.BestContentComparisonReasonCode,
                page.BestContentComparisonWordCount,
                page.RunnerUpContentComparisonMode,
                page.BestContentComparisonWordDelta,
                page.ContentComparisonDeltaSummary
            },
            page.ContentComparisonPreviewSummary,
            ContentComparisons = page.ContentComparisons
                .OrderBy(comparison => comparison.Mode.ToString(), StringComparer.OrdinalIgnoreCase)
                .Select(comparison => new {
                    comparison.Mode,
                    comparison.ReasonCode,
                    comparison.Reason,
                    comparison.ElementSelectorHint,
                    comparison.WordCount,
                    comparison.CharacterCount,
                    comparison.Summary,
                    comparison.Score,
                    comparison.ReaderCandidateCount,
                    comparison.ReaderRootElementSelectorHint
                })
                .ToArray(),
            page.AppliedInteractions,
            page.Started,
            page.Finished,
            DurationMs = (long)page.Duration.TotalMilliseconds,
            PageFiles = new {
                HtmlPath = BuildRelativeOptionalPath(manifestPath, page.HtmlPath),
                TextPath = BuildRelativeOptionalPath(manifestPath, page.TextPath),
                MarkdownPath = BuildRelativeOptionalPath(manifestPath, page.MarkdownPath),
                StructuredJsonPath = BuildRelativeOptionalPath(manifestPath, page.StructuredJsonPath)
            },
            page.StructuredJson,
            Search = new {
                searchMetadata.WordCount,
                searchMetadata.CharacterCount,
                searchMetadata.ChunkCount,
                searchMetadata.Summary,
                searchMetadata.Headings,
                searchMetadata.Keywords
            },
            Links = page.Links
                .Where(link => !string.IsNullOrWhiteSpace(link))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(link => link, StringComparer.OrdinalIgnoreCase)
                .Select(link => new {
                    Url = link,
                    LocalPagePath = localPageMap.TryGetValue(link, out string? localPagePath) ? BuildRelativeOptionalPath(manifestPath, localPagePath) : null
                })
                .ToArray(),
            ReferencedAssets = page.AssetUrls
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(url => url, StringComparer.OrdinalIgnoreCase)
                .Select(url => new {
                    Url = url,
                    LocalFilePath = assetMap.TryGetValue(url, out string? localAssetPath) ? BuildRelativeOptionalPath(manifestPath, localAssetPath) : null
                })
                .ToArray(),
            DownloadedAssets = assets
                .Where(asset => string.Equals(asset.PageUrl, page.Url, StringComparison.OrdinalIgnoreCase))
                .Where(asset => !string.IsNullOrWhiteSpace(asset.Url))
                .OrderBy(asset => asset.Url, StringComparer.OrdinalIgnoreCase)
                .Select(asset => new {
                    asset.Url,
                    asset.ContentType,
                    asset.StatusCode,
                    asset.Error,
                    LocalFilePath = BuildRelativeOptionalPath(manifestPath, asset.FilePath),
                    asset.ContentLength
                })
                .ToArray(),
            page.OfflineReadinessGrade,
            page.HighestOfflineRiskSeverity,
            page.OfflineDependencyDiagnosticCount,
            page.OfflineDependencyKinds,
            page.OfflineDependencyKindsSummary,
            OfflineDependencyDiagnostics = page.OfflineDependencyDiagnostics
                .OrderBy(diagnostic => diagnostic.Kind, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            page.Error
        };

        return JsonSerializer.Serialize(manifest, CreateJsonOptions());
    }

}
