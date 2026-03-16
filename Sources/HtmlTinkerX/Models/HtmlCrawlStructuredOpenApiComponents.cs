using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Reusable component definitions deduped across OpenAPI-like operations.
/// </summary>
public sealed class HtmlCrawlStructuredOpenApiComponents {
    /// <summary>Reusable flattened schema maps keyed by component name.</summary>
    public IDictionary<string, IDictionary<string, string?>> Schemas { get; set; } = new Dictionary<string, IDictionary<string, string?>>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Reusable first-class field trees keyed by component name.</summary>
    public IDictionary<string, IList<HtmlCrawlStructuredField>> FieldSets { get; set; } = new Dictionary<string, IList<HtmlCrawlStructuredField>>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Reusable authentication profiles keyed by component name.</summary>
    public IDictionary<string, HtmlCrawlStructuredApiAuthentication> AuthProfiles { get; set; } = new Dictionary<string, HtmlCrawlStructuredApiAuthentication>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Reusable rate-limit profiles keyed by component name.</summary>
    public IDictionary<string, HtmlCrawlStructuredApiRateLimit> RateLimitProfiles { get; set; } = new Dictionary<string, HtmlCrawlStructuredApiRateLimit>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Reusable parameter sets keyed by component name.</summary>
    public IDictionary<string, IList<HtmlCrawlStructuredApiParameter>> ParameterSets { get; set; } = new Dictionary<string, IList<HtmlCrawlStructuredApiParameter>>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Reusable request-header sets keyed by component name.</summary>
    public IDictionary<string, IList<HtmlCrawlStructuredHttpHeader>> RequestHeaderSets { get; set; } = new Dictionary<string, IList<HtmlCrawlStructuredHttpHeader>>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Reusable response-header sets keyed by component name.</summary>
    public IDictionary<string, IList<HtmlCrawlStructuredHttpHeader>> ResponseHeaderSets { get; set; } = new Dictionary<string, IList<HtmlCrawlStructuredHttpHeader>>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Reusable request-example sets keyed by component name.</summary>
    public IDictionary<string, IList<HtmlCrawlStructuredRequestExample>> RequestExampleSets { get; set; } = new Dictionary<string, IList<HtmlCrawlStructuredRequestExample>>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Reusable response-example sets keyed by component name.</summary>
    public IDictionary<string, IList<HtmlCrawlStructuredResponseExample>> ResponseExampleSets { get; set; } = new Dictionary<string, IList<HtmlCrawlStructuredResponseExample>>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Reusable error-catalog sets keyed by component name.</summary>
    public IDictionary<string, IList<HtmlCrawlStructuredApiError>> ErrorCatalogs { get; set; } = new Dictionary<string, IList<HtmlCrawlStructuredApiError>>(System.StringComparer.OrdinalIgnoreCase);
}
