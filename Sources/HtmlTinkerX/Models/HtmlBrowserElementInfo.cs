using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Browser-observed state for one DOM element.
/// </summary>
public sealed class HtmlBrowserElementInfo {
    /// <summary>Zero-based position in the returned element list.</summary>
    public int Index { get; set; }

    /// <summary>CSS selector used to query the element, when known.</summary>
    public string QuerySelector { get; set; } = string.Empty;

    /// <summary>Generated selector that can usually find the same element again.</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>Lowercase tag name.</summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>Normalized visible text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Inner HTML when explicitly requested.</summary>
    public string? InnerHtml { get; set; }

    /// <summary>Outer HTML when explicitly requested.</summary>
    public string? OuterHtml { get; set; }

    /// <summary>Element id attribute.</summary>
    public string? Id { get; set; }

    /// <summary>Element class attribute.</summary>
    public string? Class { get; set; }

    /// <summary>Element name attribute.</summary>
    public string? Name { get; set; }

    /// <summary>Element type attribute.</summary>
    public string? Type { get; set; }

    /// <summary>Element role attribute.</summary>
    public string? Role { get; set; }

    /// <summary>Element href attribute when applicable.</summary>
    public string? Href { get; set; }

    /// <summary>Element value when applicable.</summary>
    public string? Value { get; set; }

    /// <summary>Selected element attributes when requested.</summary>
    public IReadOnlyDictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();

    /// <summary>Whether Playwright reports the element as visible.</summary>
    public bool Visible { get; set; }

    /// <summary>Whether the element is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Whether the element is editable.</summary>
    public bool Editable { get; set; }

    /// <summary>Checked state for checkbox/radio-like elements.</summary>
    public bool? Checked { get; set; }

    /// <summary>Selected state for option-like elements.</summary>
    public bool? Selected { get; set; }

    /// <summary>Whether the element's bounding rectangle intersects the viewport.</summary>
    public bool InViewport { get; set; }

    /// <summary>Element left coordinate relative to the viewport.</summary>
    public double X { get; set; }

    /// <summary>Element top coordinate relative to the viewport.</summary>
    public double Y { get; set; }

    /// <summary>Element width in CSS pixels.</summary>
    public double Width { get; set; }

    /// <summary>Element height in CSS pixels.</summary>
    public double Height { get; set; }
}
