namespace HtmlTinkerX;

/// <summary>
/// Describes the enterprise authentication protocol hinted by a browser handoff form.
/// </summary>
public enum HtmlBrowserSsoHandoffKind {
    /// <summary>The form contains fields, but they do not clearly identify a known SSO handoff.</summary>
    Unknown,

    /// <summary>The form contains SAML request, response, or relay-state fields.</summary>
    Saml,

    /// <summary>The form contains WS-Federation fields such as wa, wresult, or wctx.</summary>
    WsFederation,

    /// <summary>The form contains OAuth 2.0 authorization code or token fields.</summary>
    OAuth2,

    /// <summary>The form contains OpenID Connect identity-token fields.</summary>
    OpenIdConnect
}
