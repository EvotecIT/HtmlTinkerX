namespace HtmlTinkerX;

/// <summary>
/// Decoded token summary associated with a field in an SSO handoff form.
/// </summary>
public sealed class HtmlSsoHandoffTokenSummary {
    /// <summary>Form field name that contained the token.</summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>Safe JSON Web Token summary.</summary>
    public HtmlJsonWebTokenSummary Summary { get; set; } = new();
}
