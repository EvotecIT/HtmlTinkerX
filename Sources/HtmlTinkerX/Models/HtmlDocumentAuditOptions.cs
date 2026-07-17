namespace HtmlTinkerX;

/// <summary>
/// Controls the reusable correctness, safety, and accessibility checks applied to an HTML document.
/// </summary>
public sealed class HtmlDocumentAuditOptions {
    /// <summary>Checks document title and language metadata.</summary>
    public bool CheckDocumentMetadata { get; set; } = true;

    /// <summary>Checks that element identifiers are unique.</summary>
    public bool CheckDuplicateIds { get; set; } = true;

    /// <summary>Checks images, interactive controls, and form fields for accessible names.</summary>
    public bool CheckAccessibleNames { get; set; } = true;

    /// <summary>Checks links and resource attributes for executable URL schemes.</summary>
    public bool CheckUnsafeUrls { get; set; } = true;

    /// <summary>Checks heading levels for skipped hierarchy.</summary>
    public bool CheckHeadingOrder { get; set; } = true;
}
