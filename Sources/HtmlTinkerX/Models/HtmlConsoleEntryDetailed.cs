using System;
using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Represents detailed console message information captured from the browser.
/// </summary>
public sealed class HtmlConsoleEntryDetailed {
    /// <summary>Console message text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Message type like log, error, warning.</summary>
    public HtmlConsoleMessageType Type { get; set; }

    /// <summary>Location of the message if available.</summary>
    public string? Location { get; set; }
    /// <summary>Timestamp when the message was logged.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Stack trace if available (for errors and warnings).</summary>
    public string? StackTrace { get; set; }

    /// <summary>Source URL where the message originated.</summary>
    public string? SourceUrl { get; set; }

    /// <summary>Line number in the source file.</summary>
    public int? LineNumber { get; set; }

    /// <summary>Column number in the source file.</summary>
    public int? ColumnNumber { get; set; }

    /// <summary>Additional arguments passed to console method.</summary>
    public IList<object>? Arguments { get; set; }

    /// <summary>Indicates if this is an error message.</summary>
    public bool IsError => Type == HtmlConsoleMessageType.Error || Type == HtmlConsoleMessageType.Assert;

    /// <summary>Indicates if this is a warning message.</summary>
    public bool IsWarning => Type == HtmlConsoleMessageType.Warning;

    /// <summary>Indicates if this is an informational message.</summary>
    public bool IsInfo => Type == HtmlConsoleMessageType.Info || Type == HtmlConsoleMessageType.Log;

    /// <summary>Gets the severity level (1=Info, 2=Warning, 3=Error).</summary>
    public int SeverityLevel => Type switch {
        HtmlConsoleMessageType.Error => 3,
        HtmlConsoleMessageType.Assert => 3,
        HtmlConsoleMessageType.Warning => 2,
        _ => 1
    };

    /// <summary>Full location string combining source URL and line/column.</summary>
    public string? FullLocation {
        get {
            if (string.IsNullOrEmpty(SourceUrl))
                return Location;
            
            string loc = SourceUrl!;
            if (LineNumber.HasValue) {
                loc += $":{LineNumber}";
                if (ColumnNumber.HasValue)
                    loc += $":{ColumnNumber}";
            }
            return loc;
        }
    }
}