using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Ranked browser locator candidate for resilient automation.
/// </summary>
public sealed class HtmlBrowserLocatorCandidate {
    /// <summary>Zero-based candidate index after ranking.</summary>
    public int Index { get; set; }

    /// <summary>Locator strategy, such as TestId, Id, Name, AriaLabel, Placeholder, Href, Text, or Css.</summary>
    public string Strategy { get; set; } = string.Empty;

    /// <summary>CSS selector or Playwright-friendly selector text.</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>Human-readable locator expression.</summary>
    public string Locator { get; set; } = string.Empty;

    /// <summary>Score from 0 to 100; higher is more stable.</summary>
    public int Score { get; set; }

    /// <summary>Short explanation for why this candidate was ranked this way.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Visible text associated with the element.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>HTML tag name.</summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>Whether the element is visible.</summary>
    public bool Visible { get; set; }

    /// <summary>Whether the element is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Whether the element is editable.</summary>
    public bool Editable { get; set; }

    /// <summary>Whether the element intersects the viewport.</summary>
    public bool InViewport { get; set; }

    /// <summary>Suggested automation action for this candidate, such as Click, SetInput, or Inspect.</summary>
    public string SuggestedAction { get; set; } = string.Empty;

    /// <summary>Copy-ready PowerShell command for the suggested action.</summary>
    public string SuggestedCommand { get; set; } = string.Empty;

    /// <summary>Copy-ready PowerShell command that verifies the selector before acting on it.</summary>
    public string TestCommand { get; set; } = string.Empty;

    /// <summary>Warnings that explain why this locator may need operator review before use.</summary>
    public List<string> Warnings { get; set; } = new();
}
