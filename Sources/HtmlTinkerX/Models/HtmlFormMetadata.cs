using System;

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

    /// <summary>Requested document address, when supplied by the caller or URL parser.</summary>
    public Uri? SourceUri { get; set; }

    /// <summary>Document address after HTTP redirects, or the supplied address for HTML content.</summary>
    public Uri? FinalUri { get; set; }

    /// <summary>Document base address including the first applicable HTML base element.</summary>
    public Uri? BaseUri { get; set; }

    /// <summary>Absolute HTTP submission address; null when no usable address can be resolved.</summary>
    public Uri? ResolvedActionUri { get; set; }

    /// <summary>Submission method.</summary>
    public FormMethod Method { get; set; } = FormMethod.Get;
}
