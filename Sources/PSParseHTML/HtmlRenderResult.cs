using System.Collections.Generic;

namespace PSParseHTML;

/// <summary>
/// Result of rendering a page with optional downloads.
/// </summary>
public class HtmlRenderResult {
    /// <summary>The rendered HTML markup.</summary>
    public string Html { get; set; } = string.Empty;

    /// <summary>Paths of any files downloaded while rendering.</summary>
    public List<string> Downloads { get; set; } = new();
}
