using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Lightweight content-analysis metadata for a structured crawl document.
/// </summary>
public sealed class HtmlCrawlStructuredContent {
    /// <summary>Number of words in the extracted document text.</summary>
    public int WordCount { get; set; }

    /// <summary>Number of characters in the extracted document text.</summary>
    public int CharacterCount { get; set; }

    /// <summary>Number of generated chunks for the extracted document text.</summary>
    public int ChunkCount { get; set; }

    /// <summary>Short summary of the extracted document text.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Headings extracted from the selected content.</summary>
    public IList<string> Headings { get; set; } = new List<string>();

    /// <summary>Keywords extracted from the document text.</summary>
    public IList<string> Keywords { get; set; } = new List<string>();
}
