using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Structured API endpoint inferred from docs headings and request examples.
/// </summary>
public sealed class HtmlCrawlStructuredApiEndpoint {
    /// <summary>HTTP method in uppercase.</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>Normalized endpoint path or URL path.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Absolute API server origins discovered from endpoint headings or request examples.</summary>
    public IList<string> Servers { get; set; } = new List<string>();

    /// <summary>Short title, typically from a nearby heading.</summary>
    public string? Title { get; set; }

    /// <summary>Nearby descriptive paragraph when available.</summary>
    public string? Description { get; set; }

    /// <summary>Stable operation identifier derived from the method and path.</summary>
    public string? OperationId { get; set; }

    /// <summary>Primary resource or group name derived from the endpoint path.</summary>
    public string? Resource { get; set; }

    /// <summary>Tags inferred for the endpoint to support grouping and filtering.</summary>
    public IList<string> Tags { get; set; } = new List<string>();

    /// <summary>Parameters documented for the endpoint.</summary>
    public IList<HtmlCrawlStructuredApiParameter> Parameters { get; set; } = new List<HtmlCrawlStructuredApiParameter>();

    /// <summary>Path parameters for the endpoint.</summary>
    public IList<HtmlCrawlStructuredApiParameter> PathParameters { get; set; } = new List<HtmlCrawlStructuredApiParameter>();

    /// <summary>Query parameters for the endpoint.</summary>
    public IList<HtmlCrawlStructuredApiParameter> QueryParameters { get; set; } = new List<HtmlCrawlStructuredApiParameter>();

    /// <summary>Header parameters for the endpoint.</summary>
    public IList<HtmlCrawlStructuredApiParameter> HeaderParameters { get; set; } = new List<HtmlCrawlStructuredApiParameter>();

    /// <summary>Request body fields for the endpoint.</summary>
    public IList<HtmlCrawlStructuredApiParameter> BodyParameters { get; set; } = new List<HtmlCrawlStructuredApiParameter>();

    /// <summary>Flattened request body schema keyed by field name.</summary>
    public IDictionary<string, string?> RequestBodySchema { get; set; } = new Dictionary<string, string?>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>First-class request body fields inferred from parameter tables.</summary>
    public IList<HtmlCrawlStructuredField> RequestBodyFields { get; set; } = new List<HtmlCrawlStructuredField>();

    /// <summary>Authentication requirements and hints inferred for the endpoint.</summary>
    public HtmlCrawlStructuredApiAuthentication Authentication { get; set; } = new();

    /// <summary>Rate-limit requirements and hints inferred for the endpoint.</summary>
    public HtmlCrawlStructuredApiRateLimit RateLimit { get; set; } = new();

    /// <summary>Request examples documented for the endpoint.</summary>
    public IList<HtmlCrawlStructuredRequestExample> RequestExamples { get; set; } = new List<HtmlCrawlStructuredRequestExample>();

    /// <summary>Request headers documented for the endpoint.</summary>
    public IList<HtmlCrawlStructuredHttpHeader> RequestHeaders { get; set; } = new List<HtmlCrawlStructuredHttpHeader>();

    /// <summary>Response examples documented for the endpoint.</summary>
    public IList<HtmlCrawlStructuredResponseExample> ResponseExamples { get; set; } = new List<HtmlCrawlStructuredResponseExample>();

    /// <summary>Response headers documented for the endpoint.</summary>
    public IList<HtmlCrawlStructuredHttpHeader> ResponseHeaders { get; set; } = new List<HtmlCrawlStructuredHttpHeader>();

    /// <summary>Error responses documented for the endpoint.</summary>
    public IList<HtmlCrawlStructuredResponseExample> ErrorResponses { get; set; } = new List<HtmlCrawlStructuredResponseExample>();

    /// <summary>Aggregated error catalog entries grouped by error response shape.</summary>
    public IList<HtmlCrawlStructuredApiError> ErrorCatalog { get; set; } = new List<HtmlCrawlStructuredApiError>();

    /// <summary>Flattened schema merged from successful JSON response examples.</summary>
    public IDictionary<string, string?> SuccessResponseSchema { get; set; } = new Dictionary<string, string?>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>First-class fields merged from successful JSON response examples.</summary>
    public IList<HtmlCrawlStructuredField> SuccessResponseFields { get; set; } = new List<HtmlCrawlStructuredField>();

    /// <summary>Flattened schema merged from error JSON response examples.</summary>
    public IDictionary<string, string?> ErrorResponseSchema { get; set; } = new Dictionary<string, string?>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>First-class fields merged from error JSON response examples.</summary>
    public IList<HtmlCrawlStructuredField> ErrorResponseFields { get; set; } = new List<HtmlCrawlStructuredField>();

    /// <summary>Languages or formats of related request examples.</summary>
    public IList<string> ExampleLanguages { get; set; } = new List<string>();

    /// <summary>Sources that contributed to this endpoint, such as heading or code-sample.</summary>
    public IList<string> Sources { get; set; } = new List<string>();

    /// <summary>Compact selector-like hint for the primary source element.</summary>
    public string? SelectorHint { get; set; }
}
