namespace HtmlTinkerX;

/// <summary>
/// Controls evidence capture after a browser automation failure.
/// </summary>
public sealed class HtmlBrowserFailureEvidenceOptions {
    /// <summary>Root folder where a per-failure evidence folder is created.</summary>
    public string OutFolder { get; set; } = "HtmlBrowserFailureEvidence";

    /// <summary>Logical operation name, such as Navigation, Click, Input, or ReadyWait.</summary>
    public string Operation { get; set; } = "BrowserOperation";

    /// <summary>Base file name for page artifacts.</summary>
    public string BaseFileName { get; set; } = "failure";

    /// <summary>Capture the current viewport screenshot.</summary>
    public bool Screenshot { get; set; } = true;

    /// <summary>Capture the whole page screenshot.</summary>
    public bool FullPageScreenshot { get; set; } = true;

    /// <summary>Write rendered HTML.</summary>
    public bool Html { get; set; } = true;

    /// <summary>Write visible text.</summary>
    public bool VisibleText { get; set; } = true;

    /// <summary>Write Markdown converted from rendered HTML.</summary>
    public bool Markdown { get; set; } = true;

    /// <summary>Write redacted network summary.</summary>
    public bool NetworkSummary { get; set; } = true;

    /// <summary>Write redacted locator suggestions to help recover from selector, click, input, or readiness failures.</summary>
    public bool LocatorSuggestions { get; set; } = true;

    /// <summary>Maximum number of locator suggestions to include when <see cref="LocatorSuggestions"/> is enabled.</summary>
    public int LocatorSuggestionLimit { get; set; } = 10;

    /// <summary>Write an evidence manifest.</summary>
    public bool Manifest { get; set; } = true;
}
