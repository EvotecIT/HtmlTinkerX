using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Structured authentication hints inferred for an API endpoint.
/// </summary>
public sealed class HtmlCrawlStructuredApiAuthentication {
    /// <summary>Whether the endpoint appears to require authentication.</summary>
    public bool? Required { get; set; }

    /// <summary>Normalized authentication schemes such as bearer, basic, oauth2, or api-key.</summary>
    public IList<string> Schemes { get; set; } = new List<string>();

    /// <summary>Relevant request headers such as Authorization or X-API-Key.</summary>
    public IList<string> Headers { get; set; } = new List<string>();

    /// <summary>Short nearby summary that mentioned the authentication requirement.</summary>
    public string? Summary { get; set; }
}
