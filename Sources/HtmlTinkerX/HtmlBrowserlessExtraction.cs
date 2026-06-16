using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Browserless-first discovery, extraction, and recipe helpers built on the existing HtmlTinkerX parsers.
/// </summary>
public static class HtmlBrowserlessExtraction {
    private static readonly string[] DirectDataKinds = {
        "JsonLd",
        "AppState",
        "ScriptData",
        "Microdata",
        "OpenGraph"
    };

    /// <summary>
    /// Discovers browserless data sources from static HTML.
    /// </summary>
    public static async Task<IReadOnlyList<HtmlBrowserlessDataSource>> DiscoverAsync(
        string html,
        HtmlBrowserlessDiscoveryOptions? options = null,
        HttpClient? client = null,
        CancellationToken cancellationToken = default) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        HtmlBrowserlessDiscoveryOptions effectiveOptions = options ?? new HtmlBrowserlessDiscoveryOptions();
        HtmlPageWorkbenchResult workbench = await HtmlPageWorkbench.AnalyzeAsync(
            html,
            new HtmlPageWorkbenchOptions {
                BaseUri = effectiveOptions.BaseUri,
                IncludeLinkedScripts = effectiveOptions.IncludeLinkedScripts || effectiveOptions.IncludeExternalLinkedScripts,
                IncludeExternalLinkedScripts = effectiveOptions.IncludeExternalLinkedScripts
            },
            client,
            cancellationToken).ConfigureAwait(false);

        return Discover(workbench, effectiveOptions);
    }

    /// <summary>
    /// Discovers browserless data sources from an existing page workbench result.
    /// </summary>
    public static IReadOnlyList<HtmlBrowserlessDataSource> Discover(HtmlPageWorkbenchResult workbench, HtmlBrowserlessDiscoveryOptions? options = null) {
        if (workbench == null) {
            throw new ArgumentNullException(nameof(workbench));
        }

        HtmlBrowserlessDiscoveryOptions effectiveOptions = options ?? new HtmlBrowserlessDiscoveryOptions();
        List<HtmlBrowserlessDataSource> sources = new();
        if (effectiveOptions.IncludeStaticData) {
            foreach (HtmlDataItem item in workbench.Data.Where(static item => DirectDataKinds.Contains(item.Kind, StringComparer.OrdinalIgnoreCase))) {
                AddStaticDataSource(sources, item, workbench);
            }
        }

        if (effectiveOptions.IncludeApiEndpoints) {
            foreach (HtmlApiEndpointRecord endpoint in workbench.ApiEndpoints) {
                AddEndpointSource(sources, endpoint, workbench);
            }
        }

        IEnumerable<HtmlBrowserlessDataSource> query = sources
            .OrderByDescending(static source => source.CanExtractDirectly)
            .ThenBy(static source => source.RequiresHttpFetch)
            .ThenBy(static source => source.RiskLevel)
            .ThenBy(static source => source.Index);

        if (effectiveOptions.DirectOnly) {
            query = query.Where(static source => source.CanExtractDirectly);
        }

        if (effectiveOptions.MaxSources > 0) {
            query = query.Take(effectiveOptions.MaxSources);
        }

        return query.Select((source, index) => {
            source.Index = index;
            return source;
        }).ToArray();
    }

    /// <summary>
    /// Extracts a discovered browserless data source.
    /// </summary>
    public static async Task<HtmlBrowserlessExtractionResult> ExtractAsync(
        HtmlBrowserlessDataSource source,
        HtmlBrowserlessExtractionOptions? options = null,
        HttpClient? client = null,
        CancellationToken cancellationToken = default) {
        if (source == null) {
            throw new ArgumentNullException(nameof(source));
        }

        HtmlBrowserlessExtractionOptions effectiveOptions = options ?? new HtmlBrowserlessExtractionOptions();
        if (source.RequiresHttpFetch) {
            return await ExtractEndpointAsync(source, effectiveOptions, client, cancellationToken).ConfigureAwait(false);
        }

        string rawContent = source.RawContent ?? string.Empty;
        IReadOnlyList<HtmlBrowserlessExtractionItem> items = ExtractItemsFromPayload(source.Kind, rawContent);
        return new HtmlBrowserlessExtractionResult {
            Source = source,
            Success = items.Count > 0,
            Items = items,
            RawContent = effectiveOptions.IncludeRawContent ? rawContent : string.Empty,
            ContentType = LooksLikeJson(rawContent) ? "application/json" : "text/plain",
            Evidence = Combine(source.Evidence, $"Extracted {items.Count} item(s) from static {source.Kind} payload."),
            Warnings = source.Warnings
        };
    }

    /// <summary>
    /// Creates a portable extraction recipe from a discovered source.
    /// </summary>
    public static HtmlBrowserlessExtractionRecipe CreateRecipe(HtmlBrowserlessDataSource source, bool includeRawContent = false) {
        if (source == null) {
            throw new ArgumentNullException(nameof(source));
        }

        return new HtmlBrowserlessExtractionRecipe {
            PageUrl = source.PageUrl,
            SourceKind = source.Kind,
            SourceName = source.Name,
            SourceType = source.Type,
            Url = source.Url,
            ResolvedUrl = source.ResolvedUrl,
            Method = source.Method,
            RiskLevel = source.RiskLevel,
            IsExternal = source.IsExternal,
            RequiresAuthenticationHint = source.RequiresAuthenticationHint,
            Selector = source.Selector,
            RawContent = includeRawContent ? source.RawContent : string.Empty
        };
    }

    /// <summary>
    /// Serializes a browserless extraction recipe to JSON.
    /// </summary>
    public static string SerializeRecipe(HtmlBrowserlessExtractionRecipe recipe) {
        if (recipe == null) {
            throw new ArgumentNullException(nameof(recipe));
        }

        return JsonSerializer.Serialize(recipe, CreateJsonSerializerOptions());
    }

    /// <summary>
    /// Deserializes a browserless extraction recipe from JSON.
    /// </summary>
    public static HtmlBrowserlessExtractionRecipe DeserializeRecipe(string json) {
        if (string.IsNullOrWhiteSpace(json)) {
            throw new ArgumentException("Recipe JSON cannot be empty.", nameof(json));
        }

        return JsonSerializer.Deserialize<HtmlBrowserlessExtractionRecipe>(json, CreateJsonSerializerOptions())
            ?? throw new InvalidDataException("Recipe JSON did not contain a browserless extraction recipe.");
    }

    /// <summary>
    /// Extracts data from a browserless extraction recipe.
    /// </summary>
    public static Task<HtmlBrowserlessExtractionResult> ExtractRecipeAsync(
        HtmlBrowserlessExtractionRecipe recipe,
        HtmlBrowserlessExtractionOptions? options = null,
        HttpClient? client = null,
        CancellationToken cancellationToken = default) {
        if (recipe == null) {
            throw new ArgumentNullException(nameof(recipe));
        }

        HtmlBrowserlessDataSource source = CreateSourceFromRecipe(recipe);
        return ExtractAsync(source, options, client, cancellationToken);
    }

    private static void AddStaticDataSource(List<HtmlBrowserlessDataSource> sources, HtmlDataItem item, HtmlPageWorkbenchResult workbench) {
        string rawContent = item.RawValue;
        bool hasRawContent = !string.IsNullOrWhiteSpace(rawContent);
        sources.Add(new HtmlBrowserlessDataSource {
            Index = sources.Count,
            Kind = item.Kind,
            Name = FirstNonEmpty(item.Name, item.Id, item.Kind),
            Type = FirstNonEmpty(item.Type, item.Source),
            PageUrl = FirstNonEmpty(workbench.FinalUrl, workbench.SourceUrl),
            Selector = item.Selector,
            Source = item.Source,
            RawContent = rawContent,
            CanExtractDirectly = hasRawContent,
            Evidence = new[] {
                $"Static {item.Kind} payload found in {FirstNonEmpty(item.Selector, item.Source)}.",
                "No browser runtime is required to read this payload."
            },
            Warnings = hasRawContent
                ? Array.Empty<string>()
                : new[] { "Source was discovered but did not contain a raw payload." }
        });
    }

    private static void AddEndpointSource(List<HtmlBrowserlessDataSource> sources, HtmlApiEndpointRecord endpoint, HtmlPageWorkbenchResult workbench) {
        bool canFetch = CanFetchEndpoint(endpoint);
        sources.Add(new HtmlBrowserlessDataSource {
            Index = sources.Count,
            Kind = "ApiEndpoint",
            Name = FirstNonEmpty(endpoint.Name, endpoint.ResolvedUrl, endpoint.Url),
            Type = endpoint.Kind,
            PageUrl = FirstNonEmpty(workbench.FinalUrl, workbench.SourceUrl),
            Url = endpoint.Url,
            ResolvedUrl = endpoint.ResolvedUrl,
            Method = endpoint.Method,
            RiskLevel = endpoint.RiskLevel,
            CanExtractDirectly = canFetch,
            RequiresHttpFetch = true,
            IsExternal = endpoint.IsExternal,
            RequiresAuthenticationHint = endpoint.RequiresAuthenticationHint,
            Selector = endpoint.Selector,
            Source = endpoint.Source,
            Evidence = BuildEndpointEvidence(endpoint, canFetch),
            Warnings = BuildEndpointWarnings(endpoint, canFetch)
        });
    }

    private static bool CanFetchEndpoint(HtmlApiEndpointRecord endpoint) =>
        endpoint.Method.Equals("GET", StringComparison.OrdinalIgnoreCase)
        && endpoint.RiskLevel == HtmlApiEndpointRiskLevel.Low
        && !endpoint.IsExternal
        && !endpoint.IsStateChanging
        && !endpoint.HasSensitiveQuery
        && !endpoint.RequiresAuthenticationHint
        && !string.IsNullOrWhiteSpace(endpoint.ResolvedUrl);

    private static IReadOnlyList<string> BuildEndpointEvidence(HtmlApiEndpointRecord endpoint, bool canFetch) {
        List<string> evidence = new() {
            $"Endpoint discovered from {FirstNonEmpty(endpoint.Source, endpoint.Kind)}.",
            $"Risk classification: {endpoint.RiskLevel}."
        };
        evidence.Add(canFetch
            ? "Endpoint is a same-origin low-risk GET candidate and can be fetched when HTTP extraction is allowed."
            : "Endpoint needs operator review before direct HTTP extraction.");
        return evidence;
    }

    private static IReadOnlyList<string> BuildEndpointWarnings(HtmlApiEndpointRecord endpoint, bool canFetch) {
        if (canFetch) {
            return Array.Empty<string>();
        }

        List<string> warnings = new();
        if (!endpoint.Method.Equals("GET", StringComparison.OrdinalIgnoreCase)) {
            warnings.Add($"Endpoint method is {FirstNonEmpty(endpoint.Method, "UNKNOWN")}; direct extraction only auto-allows low-risk GET candidates.");
        }

        if (endpoint.IsExternal) {
            warnings.Add("Endpoint is external to the page origin.");
        }

        if (endpoint.RequiresAuthenticationHint) {
            warnings.Add("Endpoint or page contains authentication hints.");
        }

        if (endpoint.HasSensitiveQuery) {
            warnings.Add("Endpoint contains sensitive query parameter names.");
        }

        if (endpoint.RiskLevel != HtmlApiEndpointRiskLevel.Low) {
            warnings.Add($"Endpoint risk is {endpoint.RiskLevel}.");
        }

        return warnings;
    }

    private static async Task<HtmlBrowserlessExtractionResult> ExtractEndpointAsync(
        HtmlBrowserlessDataSource source,
        HtmlBrowserlessExtractionOptions options,
        HttpClient? client,
        CancellationToken cancellationToken) {
        List<HtmlBrowserlessExtractionRequest> requests = new();
        List<string> warnings = new(source.Warnings);
        if (!options.AllowHttpFetch) {
            warnings.Add("HTTP fetch was not allowed; rerun with AllowHttpFetch to extract this endpoint directly.");
            return new HtmlBrowserlessExtractionResult {
                Source = source,
                Success = false,
                Requests = requests,
                Evidence = source.Evidence,
                Warnings = warnings
            };
        }

        if (!EndpointAllowedByOptions(source, options, warnings)) {
            return new HtmlBrowserlessExtractionResult {
                Source = source,
                Success = false,
                Requests = requests,
                Evidence = source.Evidence,
                Warnings = warnings
            };
        }

        bool ownsClient = client == null;
        HttpClient effectiveClient = client ?? new HttpClient();
        try {
            using HttpRequestMessage request = new(HttpMethod.Get, source.ResolvedUrl);
            foreach (KeyValuePair<string, string> header in options.RequestHeaders) {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            using HttpResponseMessage response = await effectiveClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            string content = await HtmlUtilities.ReadResponseContentWithProperEncodingAsync(response, cancellationToken).ConfigureAwait(false);
            if (options.MaxResponseBytes > 0 && content.Length > options.MaxResponseBytes) {
                content = content.Substring(0, options.MaxResponseBytes);
                warnings.Add($"Response body was truncated to {options.MaxResponseBytes} characters.");
            }

            string contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            requests.Add(new HtmlBrowserlessExtractionRequest {
                Method = "GET",
                Url = source.ResolvedUrl,
                StatusCode = (int)response.StatusCode,
                ContentType = contentType,
                Success = response.IsSuccessStatusCode
            });

            IReadOnlyList<HtmlBrowserlessExtractionItem> items = ExtractItemsFromResponse(source, content, contentType);
            return new HtmlBrowserlessExtractionResult {
                Source = source,
                Success = response.IsSuccessStatusCode && items.Count > 0,
                Items = items,
                Requests = requests,
                RawContent = options.IncludeRawContent ? content : string.Empty,
                ContentType = contentType,
                Evidence = Combine(source.Evidence, $"Fetched endpoint directly and extracted {items.Count} item(s)."),
                Warnings = warnings
            };
        } catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is InvalidOperationException) {
            requests.Add(new HtmlBrowserlessExtractionRequest {
                Method = "GET",
                Url = source.ResolvedUrl,
                Success = false,
                Error = ex.Message
            });
            warnings.Add(ex.Message);
            return new HtmlBrowserlessExtractionResult {
                Source = source,
                Success = false,
                Requests = requests,
                Evidence = source.Evidence,
                Warnings = warnings
            };
        } finally {
            if (ownsClient) {
                effectiveClient.Dispose();
            }
        }
    }

    private static bool EndpointAllowedByOptions(HtmlBrowserlessDataSource source, HtmlBrowserlessExtractionOptions options, List<string> warnings) {
        if (!source.Method.Equals("GET", StringComparison.OrdinalIgnoreCase)) {
            warnings.Add("Only GET endpoint recipes can be fetched by browserless extraction.");
            return false;
        }

        if (source.IsExternal && !options.AllowExternalEndpoints) {
            warnings.Add("External endpoint fetch was not allowed.");
            return false;
        }

        if (source.RiskLevel == HtmlApiEndpointRiskLevel.High) {
            warnings.Add("High-risk endpoints are not fetched by browserless extraction.");
            return false;
        }

        if (source.RiskLevel == HtmlApiEndpointRiskLevel.Medium && !options.AllowMediumRiskEndpoints) {
            warnings.Add("Medium-risk endpoint fetch was not allowed.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(source.ResolvedUrl)) {
            warnings.Add("Endpoint does not have a resolved URL.");
            return false;
        }

        return true;
    }

    private static IReadOnlyList<HtmlBrowserlessExtractionItem> ExtractItemsFromResponse(HtmlBrowserlessDataSource source, string content, string contentType) {
        if (LooksLikeJson(content) || contentType.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0) {
            return ExtractItemsFromPayload(source.Kind, content);
        }

        if (contentType.IndexOf("html", StringComparison.OrdinalIgnoreCase) >= 0 || content.IndexOf("<html", StringComparison.OrdinalIgnoreCase) >= 0) {
            return HtmlParsingToolbox.SelectData(content)
                .Select((item, index) => new HtmlBrowserlessExtractionItem {
                    Index = index,
                    Kind = item.Kind,
                    Name = item.Name,
                    Type = FirstNonEmpty(item.Type, item.Source),
                    Path = item.Selector,
                    Value = item.Value,
                    RawValue = item.RawValue
                })
                .ToArray();
        }

        return new[] {
            new HtmlBrowserlessExtractionItem {
                Index = 0,
                Kind = source.Kind,
                Name = source.Name,
                Type = "Text",
                Path = "$",
                Value = content,
                RawValue = content
            }
        };
    }

    private static IReadOnlyList<HtmlBrowserlessExtractionItem> ExtractItemsFromPayload(string kind, string rawContent) {
        if (string.IsNullOrWhiteSpace(rawContent)) {
            return Array.Empty<HtmlBrowserlessExtractionItem>();
        }

        try {
            using JsonDocument document = JsonDocument.Parse(rawContent, HtmlModernParserUtilities.JsonOptions);
            List<HtmlBrowserlessExtractionItem> items = new();
            AddJsonItems(kind, document.RootElement, "$", items);
            return items;
        } catch (JsonException) {
            return new[] {
                new HtmlBrowserlessExtractionItem {
                    Index = 0,
                    Kind = kind,
                    Name = kind,
                    Type = "Text",
                    Path = "$",
                    Value = rawContent,
                    RawValue = rawContent
                }
            };
        }
    }

    private static void AddJsonItems(string kind, JsonElement root, string path, List<HtmlBrowserlessExtractionItem> items) {
        List<(JsonElement Element, string Path)> recordElements = new();
        CollectRecordArrays(root, path, recordElements, depth: 0);
        if (recordElements.Count == 0) {
            AddJsonItem(kind, root, path, items);
            return;
        }

        foreach ((JsonElement Element, string Path) item in recordElements) {
            AddJsonItem(kind, item.Element, item.Path, items);
        }
    }

    private static void CollectRecordArrays(JsonElement element, string path, List<(JsonElement Element, string Path)> items, int depth) {
        if (depth > 6) {
            return;
        }

        if (element.ValueKind == JsonValueKind.Array) {
            int index = 0;
            bool added = false;
            foreach (JsonElement child in element.EnumerateArray()) {
                if (IsRecordLike(child)) {
                    items.Add((child, $"{path}[{index}]"));
                    added = true;
                }

                index++;
            }

            if (added) {
                return;
            }
        }

        if (element.ValueKind != JsonValueKind.Object) {
            return;
        }

        foreach (JsonProperty property in element.EnumerateObject()) {
            CollectRecordArrays(property.Value, $"{path}.{property.Name}", items, depth + 1);
        }
    }

    private static bool IsRecordLike(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object
        || element.ValueKind == JsonValueKind.String
        || element.ValueKind == JsonValueKind.Number
        || element.ValueKind == JsonValueKind.True
        || element.ValueKind == JsonValueKind.False;

    private static void AddJsonItem(string kind, JsonElement element, string path, List<HtmlBrowserlessExtractionItem> items) {
        object? value = ConvertJsonElement(element);
        items.Add(new HtmlBrowserlessExtractionItem {
            Index = items.Count,
            Kind = kind,
            Name = ExtractName(element, path),
            Type = ExtractType(element),
            Path = path,
            Value = value,
            RawValue = element.GetRawText()
        });
    }

    private static string ExtractName(JsonElement element, string fallback) {
        if (element.ValueKind == JsonValueKind.Object) {
            foreach (string name in new[] { "name", "title", "id", "sku", "slug", "@id" }) {
                if (element.TryGetProperty(name, out JsonElement property) && property.ValueKind == JsonValueKind.String) {
                    return property.GetString() ?? fallback;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.String) {
            return element.GetString() ?? fallback;
        }

        return fallback;
    }

    private static string ExtractType(JsonElement element) {
        if (element.ValueKind != JsonValueKind.Object) {
            return element.ValueKind.ToString();
        }

        foreach (string name in new[] { "@type", "type", "kind" }) {
            if (element.TryGetProperty(name, out JsonElement property) && property.ValueKind == JsonValueKind.String) {
                return property.GetString() ?? "Object";
            }
        }

        return "Object";
    }

    private static object? ConvertJsonElement(JsonElement element) {
        switch (element.ValueKind) {
            case JsonValueKind.Object:
                Dictionary<string, object?> properties = new(StringComparer.Ordinal);
                foreach (JsonProperty property in element.EnumerateObject()) {
                    properties[property.Name] = ConvertJsonElement(property.Value);
                }
                return properties;
            case JsonValueKind.Array:
                return element.EnumerateArray().Select(ConvertJsonElement).ToList();
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                if (element.TryGetInt64(out long integer)) {
                    return integer;
                }

                return element.TryGetDecimal(out decimal number) ? number : element.GetDouble();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            default:
                return null;
        }
    }

    private static HtmlBrowserlessDataSource CreateSourceFromRecipe(HtmlBrowserlessExtractionRecipe recipe) {
        bool isEndpoint = recipe.SourceKind.Equals("ApiEndpoint", StringComparison.OrdinalIgnoreCase);
        return new HtmlBrowserlessDataSource {
            Kind = recipe.SourceKind,
            Name = recipe.SourceName,
            Type = recipe.SourceType,
            PageUrl = recipe.PageUrl,
            Url = recipe.Url,
            ResolvedUrl = recipe.ResolvedUrl,
            Method = recipe.Method,
            RiskLevel = recipe.RiskLevel,
            IsExternal = recipe.IsExternal,
            RequiresAuthenticationHint = recipe.RequiresAuthenticationHint,
            Selector = recipe.Selector,
            RawContent = recipe.RawContent,
            RequiresHttpFetch = isEndpoint,
            CanExtractDirectly = isEndpoint || !string.IsNullOrWhiteSpace(recipe.RawContent),
            Evidence = new[] { "Source recreated from browserless extraction recipe." },
            Warnings = isEndpoint || !string.IsNullOrWhiteSpace(recipe.RawContent)
                ? Array.Empty<string>()
                : new[] { "Static recipes require RawContent or rediscovery from the original page." }
        };
    }

    private static bool LooksLikeJson(string content) {
        string trimmed = (content ?? string.Empty).TrimStart();
        return trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal);
    }

    private static JsonSerializerOptions CreateJsonSerializerOptions() =>
        new() {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

    private static IReadOnlyList<string> Combine(IReadOnlyList<string> source, string value) =>
        source.Concat(new[] { value }).ToArray();

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
