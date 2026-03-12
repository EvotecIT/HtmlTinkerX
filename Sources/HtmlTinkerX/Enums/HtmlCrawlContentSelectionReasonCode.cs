namespace HtmlTinkerX;

/// <summary>
/// Describes how the crawler chose the HTML region used for stored content and text extraction.
/// </summary>
public enum HtmlCrawlContentSelectionReasonCode {
    /// <summary>No content-selection decision was recorded.</summary>
    None,

    /// <summary>Raw mode kept the full fetched document because no selector was supplied.</summary>
    RawDocument,

    /// <summary>Raw mode used the exact configured selector.</summary>
    RawSelector,

    /// <summary>Raw mode produced no content because the exact configured selector was not found.</summary>
    RawSelectorMiss,

    /// <summary>Focused mode used the exact configured selector.</summary>
    FocusedSelector,

    /// <summary>Focused mode fell back to a semantic content element such as <c>main</c> or <c>article</c>.</summary>
    FocusedSemanticFallback,

    /// <summary>Focused mode kept the full document because no configured or semantic content element was found.</summary>
    FocusedFullDocumentFallback,

    /// <summary>Reader mode selected the best-scoring article-like block from the candidate set.</summary>
    ReaderBestCandidate,

    /// <summary>Reader mode kept its root element because no stronger article-like candidate was found.</summary>
    ReaderRootFallback
}
