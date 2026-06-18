using System;
using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Safe metadata summary for a JSON Web Token captured from an OAuth or OpenID Connect handoff.
/// </summary>
public sealed class HtmlJsonWebTokenSummary {
    /// <summary>Whether the token was decoded and parsed successfully.</summary>
    public bool IsValid { get; set; }

    /// <summary>Error reported when the token could not be decoded or parsed.</summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>JOSE header algorithm.</summary>
    public string Algorithm { get; set; } = string.Empty;

    /// <summary>JOSE header token type.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>JOSE key identifier.</summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>Issuer claim.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Subject claim, redacted by default.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Audience claim values.</summary>
    public List<string> Audiences { get; set; } = new();

    /// <summary>Expiration time from the exp claim.</summary>
    public DateTimeOffset? Expires { get; set; }

    /// <summary>Not-before time from the nbf claim.</summary>
    public DateTimeOffset? NotBefore { get; set; }

    /// <summary>Issued-at time from the iat claim.</summary>
    public DateTimeOffset? IssuedAt { get; set; }

    /// <summary>Tenant identifier when present.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Application or client identifier when present.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Scopes or roles found in common OAuth/OIDC claim names.</summary>
    public List<string> Scopes { get; set; } = new();

    /// <summary>Claim names and safe claim values.</summary>
    public List<HtmlJsonWebTokenClaim> Claims { get; set; } = new();

    /// <summary>Whether sensitive claim values were present.</summary>
    public bool ContainsSensitiveValues { get; set; }

    /// <summary>Decoded JOSE header JSON when requested.</summary>
    public string HeaderJson { get; set; } = string.Empty;

    /// <summary>Decoded payload JSON when requested. Sensitive values are redacted unless explicitly revealed.</summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>Warnings about expiry, redaction, unsigned tokens, or verification limits.</summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>Suggested next command for an operator.</summary>
    public string SuggestedCommand { get; set; } = string.Empty;
}
