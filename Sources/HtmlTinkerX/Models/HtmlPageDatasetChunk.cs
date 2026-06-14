using System;
using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Single-page dataset chunk suitable for JSONL export and downstream retrieval workflows.
/// </summary>
public sealed class HtmlPageDatasetChunk {
    /// <summary>Stable chunk identifier within the generated dataset.</summary>
    public string ChunkId { get; set; } = string.Empty;

    /// <summary>Zero-based chunk index.</summary>
    public int ChunkIndex { get; set; }

    /// <summary>Source URL or base URL used during page analysis.</summary>
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>Final URL after rendering or redirects, when known.</summary>
    public string FinalUrl { get; set; } = string.Empty;

    /// <summary>Page title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Workbench analysis mode that produced the chunk, such as Static or RenderedSnapshot.</summary>
    public string AnalysisMode { get; set; } = string.Empty;

    /// <summary>Plain text content for embedding or retrieval.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Markdown content when requested and available.</summary>
    public string Markdown { get; set; } = string.Empty;

    /// <summary>Short plain-text preview of the chunk.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Approximate word count.</summary>
    public int WordCount { get; set; }

    /// <summary>Character count of <see cref="Text"/>.</summary>
    public int CharacterCount { get; set; }

    /// <summary>Headings found near the page content.</summary>
    public IReadOnlyList<string> Headings { get; set; } = Array.Empty<string>();

    /// <summary>Structured data families available on the page.</summary>
    public IReadOnlyList<string> DataKinds { get; set; } = Array.Empty<string>();

    /// <summary>Number of forms available on the page.</summary>
    public int FormCount { get; set; }

    /// <summary>Number of endpoint surfaces available on the page.</summary>
    public int EndpointCount { get; set; }

    /// <summary>Redaction hints that downstream systems should respect before logging or prompting.</summary>
    public IReadOnlyList<string> RedactionHints { get; set; } = Array.Empty<string>();

    /// <summary>Provenance entries explaining the page surfaces represented by this chunk.</summary>
    public IReadOnlyList<HtmlPageDatasetProvenanceEntry> Provenance { get; set; } = Array.Empty<HtmlPageDatasetProvenanceEntry>();
}
