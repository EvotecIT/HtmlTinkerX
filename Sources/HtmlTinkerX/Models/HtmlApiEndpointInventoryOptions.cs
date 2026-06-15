namespace HtmlTinkerX;

/// <summary>
/// Options controlling API endpoint inventory generation.
/// </summary>
public sealed class HtmlApiEndpointInventoryOptions {
    /// <summary>Includes form actions as endpoint records.</summary>
    public bool IncludeForms { get; set; } = true;

    /// <summary>Includes endpoints discovered from inline or linked JavaScript.</summary>
    public bool IncludeScriptEndpoints { get; set; } = true;
}
