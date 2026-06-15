namespace HtmlTinkerX;

/// <summary>
/// Options controlling single-page dataset chunk generation.
/// </summary>
public sealed class HtmlPageDatasetOptions {
    /// <summary>Maximum number of words per text chunk.</summary>
    public int MaxChunkWords { get; set; } = 350;

    /// <summary>Includes markdown content beside plain text when available.</summary>
    public bool IncludeMarkdown { get; set; } = true;

    /// <summary>Includes provenance entries from structured data and interaction surfaces.</summary>
    public bool IncludeProvenance { get; set; } = true;

    /// <summary>Includes redaction hints for sensitive surfaces such as hidden fields and tokens.</summary>
    public bool IncludeRedactionHints { get; set; } = true;
}
