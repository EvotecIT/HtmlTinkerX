using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Options controlling screenshot capture.
/// </summary>
public sealed class ScreenshotOptions {
    /// <summary>Capture the entire document.</summary>
    public bool FullPage { get; set; }

    /// <summary>Additional wait time in milliseconds after the page loads.</summary>
    public int DelayMs { get; set; }

    /// <summary>Image file format.</summary>
    public ImageFormat Format { get; set; } = ImageFormat.Png;

    /// <summary>Encoder quality for JPEG output and compression for PNG.</summary>
    public int Quality { get; set; } = 100;

    /// <summary>Optional CSS selector to wait for before capturing.</summary>
    public string? Selector { get; set; }

    /// <summary>CSS selector of an element to capture.</summary>
    public string? ElementSelector { get; set; }

    /// <summary>Clip region X coordinate.</summary>
    public int? ClipX { get; set; }

    /// <summary>Clip region Y coordinate.</summary>
    public int? ClipY { get; set; }

    /// <summary>Clip region width.</summary>
    public int? ClipWidth { get; set; }

    /// <summary>Clip region height.</summary>
    public int? ClipHeight { get; set; }

    /// <summary>Selectors to highlight in the screenshot.</summary>
    public IEnumerable<string>? HighlightSelectors { get; set; }

    /// <summary>Text to overlay on the image.</summary>
    public string? OverlayText { get; set; }
}