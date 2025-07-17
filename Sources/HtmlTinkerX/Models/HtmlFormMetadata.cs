namespace HtmlTinkerX;

/// <summary>
/// Metadata about a parsed HTML form.
/// </summary>
public class HtmlFormMetadata {
    /// <summary>Index of the form in the document.</summary>
    public int FormIndex { get; set; }

    /// <summary>Form id attribute.</summary>
    public string? Id { get; set; }

    /// <summary>Class attribute value.</summary>
    public string? Classes { get; set; }

    /// <summary>Form action URL.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Submission method (GET/POST).</summary>
    public string Method { get; set; } = "GET";
}
