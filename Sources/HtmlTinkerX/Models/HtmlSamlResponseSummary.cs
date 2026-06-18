using System;
using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Safe summary of a SAML response captured from an SSO handoff form.
/// </summary>
public sealed class HtmlSamlResponseSummary {
    /// <summary>Whether the SAML response was decoded and parsed successfully.</summary>
    public bool IsValid { get; set; }

    /// <summary>Error reported when the response could not be decoded or parsed.</summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>SAML response identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>SAML protocol version.</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>Service provider destination URL.</summary>
    public string Destination { get; set; } = string.Empty;

    /// <summary>Request identifier this response answers.</summary>
    public string InResponseTo { get; set; } = string.Empty;

    /// <summary>Response issue instant.</summary>
    public DateTimeOffset? IssueInstant { get; set; }

    /// <summary>Identity provider issuer from the response.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>SAML status code value.</summary>
    public string StatusCode { get; set; } = string.Empty;

    /// <summary>Assertion identifier when a plain assertion is present.</summary>
    public string AssertionId { get; set; } = string.Empty;

    /// <summary>Assertion issue instant when a plain assertion is present.</summary>
    public DateTimeOffset? AssertionIssueInstant { get; set; }

    /// <summary>Assertion issuer when a plain assertion is present.</summary>
    public string AssertionIssuer { get; set; } = string.Empty;

    /// <summary>Subject NameID, redacted by default because it commonly contains a user identifier.</summary>
    public string SubjectNameId { get; set; } = string.Empty;

    /// <summary>Allowed audience values from audience restrictions.</summary>
    public List<string> Audiences { get; set; } = new();

    /// <summary>Earliest assertion validity time.</summary>
    public DateTimeOffset? NotBefore { get; set; }

    /// <summary>Latest assertion validity time.</summary>
    public DateTimeOffset? NotOnOrAfter { get; set; }

    /// <summary>Attribute names included in the assertion without revealing values.</summary>
    public List<string> AttributeNames { get; set; } = new();

    /// <summary>Whether the response contains an encrypted assertion.</summary>
    public bool ContainsEncryptedAssertion { get; set; }

    /// <summary>Whether the response or assertion contains an XML signature.</summary>
    public bool ContainsSignature { get; set; }

    /// <summary>Whether sensitive subject or attribute values were present.</summary>
    public bool ContainsSensitiveValues { get; set; }

    /// <summary>Decoded XML when explicitly requested. Sensitive values are redacted unless explicitly revealed.</summary>
    public string Xml { get; set; } = string.Empty;

    /// <summary>Warnings about expiry, signatures, encrypted assertions, or redaction.</summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>Suggested next command for an operator.</summary>
    public string SuggestedCommand { get; set; } = string.Empty;
}
