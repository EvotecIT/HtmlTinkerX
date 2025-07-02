using System;

namespace PSParseHTML;

/// <summary>
/// Basic statistics about an HTML document.
/// </summary>
public sealed class HtmlDocumentStatistics {
    /// <summary>Total number of words in visible text.</summary>
    public int WordCount { get; set; }

    /// <summary>Total number of &lt;a&gt; elements.</summary>
    public int LinkCount { get; set; }

    /// <summary>Total number of &lt;img&gt; elements.</summary>
    public int ImageCount { get; set; }
}
