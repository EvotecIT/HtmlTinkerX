using System.Collections.Generic;

namespace PSParseHTML;

/// <summary>
/// Result of form parsing with metadata and list of fields.
/// </summary>
public class HtmlFormResult {
    /// <summary>Metadata about the form.</summary>
    public HtmlFormMetadata Metadata { get; set; } = new();

    /// <summary>Fields contained in the form.</summary>
    public List<HtmlFormField> Fields { get; set; } = new();
}
