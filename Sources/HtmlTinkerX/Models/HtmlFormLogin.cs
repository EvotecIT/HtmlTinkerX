namespace HtmlTinkerX;

/// <summary>
/// Options controlling form based authentication.
/// </summary>
public sealed class HtmlFormLogin {
    /// <summary>URL of the login page.</summary>
    public string LoginUrl { get; set; } = string.Empty;

    /// <summary>CSS selector for the username field.</summary>
    public string UsernameSelector { get; set; } = string.Empty;

    /// <summary>CSS selector for the password field.</summary>
    public string PasswordSelector { get; set; } = string.Empty;

    /// <summary>CSS selector for the submit element.</summary>
    public string SubmitSelector { get; set; } = string.Empty;
}
