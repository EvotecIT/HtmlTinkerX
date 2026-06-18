namespace HtmlTinkerX;

/// <summary>
/// One claim from a decoded JSON Web Token payload.
/// </summary>
public sealed class HtmlJsonWebTokenClaim {
    /// <summary>Claim name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Claim value rendered for PowerShell-friendly display.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>JSON value kind such as String, Number, Array, Object, True, or False.</summary>
    public string ValueKind { get; set; } = string.Empty;

    /// <summary>Whether the value was redacted.</summary>
    public bool Redacted { get; set; }
}
