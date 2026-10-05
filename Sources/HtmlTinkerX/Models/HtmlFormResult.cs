using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Result of form parsing with metadata and list of fields.
/// </summary>
public class HtmlFormResult {
    /// <summary>Metadata about the form.</summary>
    public HtmlFormMetadata Metadata { get; set; } = new();

    /// <summary>Named controls associated with the form, including disabled and external controls.</summary>
    public List<HtmlFormField> Fields { get; set; } = new();

    /// <summary>
    /// Successful named values in document order, retaining repeated names and selected options.
    /// Disabled controls, unchecked checkboxes and radios, files, and submit buttons are omitted.
    /// </summary>
    public List<KeyValuePair<string, string>> SuccessfulFields { get; set; } = new();
}
