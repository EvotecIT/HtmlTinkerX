using System;
using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Values extracted from one repeated HTML item.
/// </summary>
public sealed class HtmlDomExtractionRecord {
    /// <summary>Zero-based item index in document order.</summary>
    public int Index { get; set; }

    /// <summary>CSS selector used to select the repeated items.</summary>
    public string ItemSelector { get; set; } = string.Empty;

    /// <summary>Extracted property values keyed by caller-provided property name.</summary>
    public IReadOnlyDictionary<string, object?> Values { get; set; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}
