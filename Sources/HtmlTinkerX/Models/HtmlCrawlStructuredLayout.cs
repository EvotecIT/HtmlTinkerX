using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Detected page-level layout regions kept separate from the extracted main document.
/// </summary>
public sealed class HtmlCrawlStructuredLayout {
    /// <summary>Detected layout regions.</summary>
    public IList<HtmlCrawlStructuredRegion> Regions { get; set; } = new List<HtmlCrawlStructuredRegion>();

    /// <summary>Detected header region count.</summary>
    public int HeaderCount { get; set; }

    /// <summary>Detected navigation or menu region count.</summary>
    public int NavigationCount { get; set; }

    /// <summary>Detected main region count.</summary>
    public int MainCount { get; set; }

    /// <summary>Detected article region count.</summary>
    public int ArticleCount { get; set; }

    /// <summary>Detected aside/sidebar region count.</summary>
    public int AsideCount { get; set; }

    /// <summary>Detected footer region count.</summary>
    public int FooterCount { get; set; }
}
