namespace HtmlTinkerX;

/// <summary>
/// Records a likely runtime dependency that may keep a saved page from working fully offline.
/// </summary>
public sealed class HtmlCrawlOfflineDependencyDiagnostic {
    /// <summary>Machine-friendly category of the detected runtime dependency.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Severity label for the diagnostic.</summary>
    public string Severity { get; set; } = "warning";

    /// <summary>Human-readable explanation of why this page may still depend on live runtime behavior.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Short evidence snippet captured from the page markup or inline script.</summary>
    public string? Evidence { get; set; }
}
