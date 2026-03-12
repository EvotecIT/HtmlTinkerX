namespace HtmlTinkerX;

/// <summary>
/// Categorizes why a page stayed static or switched to browser rendering.
/// </summary>
public enum HtmlCrawlRenderReasonCode {
    /// <summary>No specific render reason was recorded.</summary>
    None,

    /// <summary>Kept static because browser rendering was not enabled.</summary>
    StaticRenderDisabled,

    /// <summary>Kept static because extracted text met the auto-render threshold.</summary>
    StaticThresholdMet,

    /// <summary>Kept static because auto-render heuristics did not trigger.</summary>
    StaticHeuristicsNotTriggered,

    /// <summary>Rendered because browser mode was explicitly requested.</summary>
    ExplicitRender,

    /// <summary>Auto-render triggered because the selector produced no usable content in static mode.</summary>
    AutoRenderSelectorMiss,

    /// <summary>Auto-render triggered because a wait-for selector was configured and static text looked too thin.</summary>
    AutoRenderWaitForSelectorThin,

    /// <summary>Auto-render triggered because the static fetch produced no stored HTML.</summary>
    AutoRenderNoHtml,

    /// <summary>Auto-render triggered because the page looked like a JavaScript shell.</summary>
    AutoRenderJavaScriptShell,

    /// <summary>Auto-render triggered because the page had many scripts and too little extracted text.</summary>
    AutoRenderManyScripts,

    /// <summary>Auto-render triggered because the page had no headings and too little extracted text.</summary>
    AutoRenderNoHeadings,

    /// <summary>Kept static because the page did not fetch successfully.</summary>
    StaticStatusNotSuccess
}
