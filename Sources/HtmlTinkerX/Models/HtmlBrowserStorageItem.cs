namespace HtmlTinkerX;

/// <summary>
/// Browser storage entry from localStorage or sessionStorage.
/// </summary>
public sealed class HtmlBrowserStorageItem {
    /// <summary>Storage scope: Local or Session.</summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>Storage key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Storage value.</summary>
    public string? Value { get; set; }
}
