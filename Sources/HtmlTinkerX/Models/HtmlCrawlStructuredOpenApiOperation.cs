using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Compact OpenAPI-like operation derived from an inferred endpoint.
/// </summary>
public sealed class HtmlCrawlStructuredOpenApiOperation {
    /// <summary>Stable operation identifier derived from method and path.</summary>
    public string? OperationId { get; set; }

    /// <summary>Lowercase HTTP method.</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>Normalized path for the operation.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Short operation summary.</summary>
    public string? Summary { get; set; }

    /// <summary>Longer operation description.</summary>
    public string? Description { get; set; }

    /// <summary>Primary resource inferred from the path.</summary>
    public string? Resource { get; set; }

    /// <summary>Tags inferred for grouping and filtering.</summary>
    public IList<string> Tags { get; set; } = new List<string>();

    /// <summary>Authentication hints for the operation.</summary>
    public HtmlCrawlStructuredApiAuthentication Authentication { get; set; } = new();

    /// <summary>Reusable authentication component reference when one was assigned.</summary>
    public string? AuthenticationRef { get; set; }

    /// <summary>Rate-limit hints for the operation.</summary>
    public HtmlCrawlStructuredApiRateLimit RateLimit { get; set; } = new();

    /// <summary>Reusable rate-limit component reference when one was assigned.</summary>
    public string? RateLimitRef { get; set; }

    /// <summary>All parameters documented for the operation.</summary>
    public IList<HtmlCrawlStructuredApiParameter> Parameters { get; set; } = new List<HtmlCrawlStructuredApiParameter>();

    /// <summary>Reusable parameter-set component reference when one was assigned.</summary>
    public string? ParametersRef { get; set; }

    /// <summary>Request headers documented for the operation.</summary>
    public IList<HtmlCrawlStructuredHttpHeader> RequestHeaders { get; set; } = new List<HtmlCrawlStructuredHttpHeader>();

    /// <summary>Reusable request-header-set component reference when one was assigned.</summary>
    public string? RequestHeadersRef { get; set; }

    /// <summary>Response headers documented for the operation.</summary>
    public IList<HtmlCrawlStructuredHttpHeader> ResponseHeaders { get; set; } = new List<HtmlCrawlStructuredHttpHeader>();

    /// <summary>Reusable response-header-set component reference when one was assigned.</summary>
    public string? ResponseHeadersRef { get; set; }

    /// <summary>Request examples documented for the operation.</summary>
    public IList<HtmlCrawlStructuredRequestExample> RequestExamples { get; set; } = new List<HtmlCrawlStructuredRequestExample>();

    /// <summary>Reusable request-example-set component reference when one was assigned.</summary>
    public string? RequestExamplesRef { get; set; }

    /// <summary>Response examples documented for the operation.</summary>
    public IList<HtmlCrawlStructuredResponseExample> ResponseExamples { get; set; } = new List<HtmlCrawlStructuredResponseExample>();

    /// <summary>Reusable response-example-set component reference when one was assigned.</summary>
    public string? ResponseExamplesRef { get; set; }

    /// <summary>Aggregated documented error families for the operation.</summary>
    public IList<HtmlCrawlStructuredApiError> ErrorCatalog { get; set; } = new List<HtmlCrawlStructuredApiError>();

    /// <summary>Reusable error-catalog component reference when one was assigned.</summary>
    public string? ErrorCatalogRef { get; set; }

    /// <summary>Flattened request body schema keyed by field name.</summary>
    public IDictionary<string, string?> RequestBodySchema { get; set; } = new Dictionary<string, string?>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Reusable request-body schema component reference when one was assigned.</summary>
    public string? RequestBodySchemaRef { get; set; }

    /// <summary>First-class request body fields.</summary>
    public IList<HtmlCrawlStructuredField> RequestBodyFields { get; set; } = new List<HtmlCrawlStructuredField>();

    /// <summary>Reusable request-body field-tree component reference when one was assigned.</summary>
    public string? RequestBodyFieldsRef { get; set; }

    /// <summary>Flattened schema merged from successful JSON response examples.</summary>
    public IDictionary<string, string?> SuccessResponseSchema { get; set; } = new Dictionary<string, string?>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Reusable successful-response schema component reference when one was assigned.</summary>
    public string? SuccessResponseSchemaRef { get; set; }

    /// <summary>First-class fields merged from successful JSON response examples.</summary>
    public IList<HtmlCrawlStructuredField> SuccessResponseFields { get; set; } = new List<HtmlCrawlStructuredField>();

    /// <summary>Reusable successful-response field-tree component reference when one was assigned.</summary>
    public string? SuccessResponseFieldsRef { get; set; }

    /// <summary>Flattened schema merged from error JSON response examples.</summary>
    public IDictionary<string, string?> ErrorResponseSchema { get; set; } = new Dictionary<string, string?>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Reusable error-response schema component reference when one was assigned.</summary>
    public string? ErrorResponseSchemaRef { get; set; }

    /// <summary>First-class fields merged from error JSON response examples.</summary>
    public IList<HtmlCrawlStructuredField> ErrorResponseFields { get; set; } = new List<HtmlCrawlStructuredField>();

    /// <summary>Reusable error-response field-tree component reference when one was assigned.</summary>
    public string? ErrorResponseFieldsRef { get; set; }

    /// <summary>Provenance metadata describing which page artifacts contributed to this operation.</summary>
    public HtmlCrawlStructuredOpenApiProvenance Provenance { get; set; } = new();

    /// <summary>Promotion score used when deciding whether this operation is strong enough for strict OpenAPI export.</summary>
    public int StrictOpenApiScore { get; set; }

    /// <summary>Indicates whether this operation is considered strong enough for strict OpenAPI export.</summary>
    public bool StrictOpenApiEligible { get; set; }

    /// <summary>Reasons this operation was considered incomplete for strict OpenAPI export.</summary>
    public IList<string> StrictOpenApiWarnings { get; set; } = new List<string>();
}
