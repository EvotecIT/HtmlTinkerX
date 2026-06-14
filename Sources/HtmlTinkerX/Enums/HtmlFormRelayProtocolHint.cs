namespace HtmlTinkerX;

/// <summary>
/// Protocol family inferred from a hidden-form relay page.
/// </summary>
public enum HtmlFormRelayProtocolHint {
    /// <summary>No specific protocol markers were recognized.</summary>
    Generic,
    /// <summary>WS-Federation style fields such as wa, wresult, and wctx were found.</summary>
    WsFederation,
    /// <summary>SAML fields such as SAMLRequest, SAMLResponse, or RelayState were found.</summary>
    Saml
}
