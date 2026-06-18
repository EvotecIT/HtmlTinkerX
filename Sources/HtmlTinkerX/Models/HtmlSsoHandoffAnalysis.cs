using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Safe protocol-aware analysis of an SSO handoff form.
/// </summary>
public sealed class HtmlSsoHandoffAnalysis {
    /// <summary>Whether at least one protocol artifact was found and analyzed or identified.</summary>
    public bool HasProtocolArtifact { get; set; }

    /// <summary>Protocol family inferred from the handoff form.</summary>
    public HtmlBrowserSsoHandoffKind Kind { get; set; } = HtmlBrowserSsoHandoffKind.Unknown;

    /// <summary>Current browser page URL from the handoff capture.</summary>
    public string PageUrl { get; set; } = string.Empty;

    /// <summary>Current browser page title from the handoff capture.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Resolved form action URL.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Form HTTP method.</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>Observed form field names.</summary>
    public List<string> FieldNames { get; set; } = new();

    /// <summary>Decoded SAML response summary when a SAMLResponse field is present.</summary>
    public HtmlSamlResponseSummary? SamlResponse { get; set; }

    /// <summary>Decoded JWT summaries for id_token and access_token fields.</summary>
    public List<HtmlSsoHandoffTokenSummary> JsonWebTokens { get; set; } = new();

    /// <summary>Whether an OAuth authorization code field was present.</summary>
    public bool AuthorizationCodePresent { get; set; }

    /// <summary>Whether a state or RelayState field was present.</summary>
    public bool StatePresent { get; set; }

    /// <summary>OAuth or OpenID Connect error code returned by the authorization server.</summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>OAuth or OpenID Connect error description returned by the authorization server.</summary>
    public string ErrorDescription { get; set; } = string.Empty;

    /// <summary>Whether any captured field is redacted.</summary>
    public bool ContainsRedactedValues { get; set; }

    /// <summary>Whether any captured field was truncated.</summary>
    public bool ContainsTruncatedValues { get; set; }

    /// <summary>Operator guidance about analysis limitations, redaction, or replay risk.</summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>Suggested next command for an operator.</summary>
    public string SuggestedCommand { get; set; } = string.Empty;
}
