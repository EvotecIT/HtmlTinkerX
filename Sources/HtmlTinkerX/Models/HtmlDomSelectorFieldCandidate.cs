using System;

namespace HtmlTinkerX;

/// <summary>
/// Candidate field discovered inside a repeated HTML structure.
/// </summary>
public sealed class HtmlDomSelectorFieldCandidate {
    /// <summary>Suggested PowerShell property name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>CSS selector evaluated relative to each repeated item.</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>Attribute to read, or an empty string when text should be read.</summary>
    public string Attribute { get; set; } = string.Empty;

    /// <summary>Number of repeated items containing at least one matching field.</summary>
    public int ItemMatchCount { get; set; }

    /// <summary>Percentage of repeated items containing the field.</summary>
    public int CoveragePercent { get; set; }

    /// <summary>Whether more than one value was observed in at least one item.</summary>
    public bool MultiplePerItem { get; set; }

    /// <summary>Representative decoded values.</summary>
    public string[] SampleValues { get; set; } = Array.Empty<string>();

    /// <summary>Field quality score used for ranking.</summary>
    public int Score { get; set; }
}
