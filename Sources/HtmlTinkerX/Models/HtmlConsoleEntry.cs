using System;

namespace HtmlTinkerX;

/// <summary>
/// Represents a console message captured from the browser.
/// </summary>
public sealed class HtmlConsoleEntry {
    /// <summary>Console message text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Message type like log, error, warning.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Location of the message if available.</summary>
    public string? Location { get; set; }
}


