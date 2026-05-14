using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Options for selecting parsed HTML tables by common metadata.
/// </summary>
public sealed class HtmlTableSelectionOptions {
    /// <summary>Zero-based table indexes to include.</summary>
    public IList<int> TableIndexes { get; set; } = new List<int>();

    /// <summary>Table id to match.</summary>
    public string? Id { get; set; }

    /// <summary>CSS class token to match.</summary>
    public string? ClassName { get; set; }

    /// <summary>Caption text fragment to match.</summary>
    public string? CaptionContains { get; set; }

    /// <summary>Header text to match.</summary>
    public string? Header { get; set; }

    /// <summary>Use case-insensitive comparisons.</summary>
    public bool IgnoreCase { get; set; } = true;
}
