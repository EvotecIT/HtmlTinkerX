using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Structured rate-limit hints inferred for an API endpoint.
/// </summary>
public sealed class HtmlCrawlStructuredApiRateLimit {
    /// <summary>Whether rate limiting was explicitly mentioned for the endpoint.</summary>
    public bool Mentioned { get; set; }

    /// <summary>Status code associated with throttling behavior when one was documented.</summary>
    public int? StatusCode { get; set; }

    /// <summary>Relevant response headers such as Retry-After or X-RateLimit-Remaining.</summary>
    public IList<string> Headers { get; set; } = new List<string>();

    /// <summary>Short textual quota limit such as 60 requests per minute.</summary>
    public string? Limit { get; set; }

    /// <summary>Normalized quota window such as second, minute, hour, or day.</summary>
    public string? Window { get; set; }

    /// <summary>Short nearby summary that mentioned rate limiting.</summary>
    public string? Summary { get; set; }
}
