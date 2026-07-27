using System;
using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// One repeated record set inferred from a page without requiring a caller-provided selector.
/// </summary>
public sealed class HtmlPageCollection {
    /// <summary>Zero-based collection index after ranking and duplicate removal.</summary>
    public int Index { get; set; }

    /// <summary>Human-readable collection name inferred from the repeated structure.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>High, Medium, or Low confidence derived from the discovery score.</summary>
    public string Confidence { get; set; } = string.Empty;

    /// <summary>Number of repeated items.</summary>
    public int Count => Items.Count;

    /// <summary>Ranked quality score used to select this collection.</summary>
    public int Score { get; set; }

    /// <summary>Short explanation of the evidence used to recognize this collection.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Fields inferred inside each repeated item.</summary>
    public IReadOnlyList<HtmlDomSelectorFieldCandidate> Fields { get; set; } =
        Array.Empty<HtmlDomSelectorFieldCandidate>();

    /// <summary>Extracted items in document order.</summary>
    public IReadOnlyList<HtmlPageCollectionItem> Items { get; set; } =
        Array.Empty<HtmlPageCollectionItem>();

    /// <summary>
    /// CSS selector retained as advanced provenance for troubleshooting or generating a reusable extraction.
    /// Callers do not need to supply it.
    /// </summary>
    public string Selector { get; set; } = string.Empty;
}

/// <summary>
/// One object inferred from a repeated page collection.
/// </summary>
public sealed class HtmlPageCollectionItem {
    /// <summary>Zero-based item index in document order.</summary>
    public int Index { get; set; }

    /// <summary>Inferred values keyed by semantic field name.</summary>
    public IReadOnlyDictionary<string, object?> Values { get; set; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Reads an inferred value by name, returning <see langword="null"/> when absent.</summary>
    public object? this[string name] =>
        name != null && Values.TryGetValue(name, out object? value) ? value : null;
}
