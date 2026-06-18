namespace HtmlTinkerX;

/// <summary>
/// Describes a single file produced by a browser evidence export.
/// </summary>
public sealed class HtmlBrowserEvidenceArtifact {
    /// <summary>Logical artifact kind, such as Screenshot, Html, Text, Markdown, Pdf, NetworkSummary, or Manifest.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Absolute artifact path.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Artifact path relative to the evidence output folder.</summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>Content type for the artifact when known.</summary>
    public string? ContentType { get; set; }

    /// <summary>Artifact size in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>SHA-256 hash of the artifact bytes, encoded as lowercase hexadecimal.</summary>
    public string Sha256 { get; set; } = string.Empty;
}
