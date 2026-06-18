namespace HtmlTinkerX;

/// <summary>
/// Field observed in an SSO handoff form.
/// </summary>
public sealed class HtmlBrowserSsoField {
    /// <summary>Input field name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Input field type or element tag.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Field value. Sensitive values are redacted unless explicitly requested.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Original value length before redaction or truncation.</summary>
    public int ValueLength { get; set; }

    /// <summary>Whether the field name is treated as sensitive authentication material.</summary>
    public bool IsSensitive { get; set; }

    /// <summary>Whether the value was replaced with a redaction marker.</summary>
    public bool Redacted { get; set; }

    /// <summary>Whether the value was shortened because it exceeded the requested maximum length.</summary>
    public bool Truncated { get; set; }
}
