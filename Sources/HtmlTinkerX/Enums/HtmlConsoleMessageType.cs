namespace HtmlTinkerX;

/// <summary>
/// Console message categories emitted by the browser.
/// </summary>
public enum HtmlConsoleMessageType {
    /// <summary>Standard log message.</summary>
    Log,
    /// <summary>Debug message.</summary>
    Debug,
    /// <summary>Info message.</summary>
    Info,
    /// <summary>Error message.</summary>
    Error,
    /// <summary>Warning message.</summary>
    Warning,
    /// <summary>Directory listing.</summary>
    Dir,
    /// <summary>XML directory listing.</summary>
    DirXml,
    /// <summary>Table output.</summary>
    Table,
    /// <summary>Trace output.</summary>
    Trace,
    /// <summary>Console cleared.</summary>
    Clear,
    /// <summary>Start group message.</summary>
    StartGroup,
    /// <summary>Collapsed group start.</summary>
    StartGroupCollapsed,
    /// <summary>Group end.</summary>
    EndGroup,
    /// <summary>Assertion.</summary>
    Assert,
    /// <summary>Profile message.</summary>
    Profile,
    /// <summary>Profile end message.</summary>
    ProfileEnd,
    /// <summary>Count message.</summary>
    Count,
    /// <summary>Time end message.</summary>
    TimeEnd,
    /// <summary>Unrecognized message type.</summary>
    Unknown
}
