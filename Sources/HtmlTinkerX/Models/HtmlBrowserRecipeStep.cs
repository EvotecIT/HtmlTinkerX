namespace HtmlTinkerX;

using System.Collections.Generic;

/// <summary>
/// One step in a replayable browser automation recipe.
/// </summary>
public sealed class HtmlBrowserRecipeStep {
    /// <summary>Optional step name used in run results.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Action to execute.</summary>
    public HtmlBrowserRecipeAction Action { get; set; }

    /// <summary>URL used by Navigate steps.</summary>
    public string? Url { get; set; }

    /// <summary>CSS selector used by selector-based steps.</summary>
    public string? Selector { get; set; }

    /// <summary>Fallback CSS selectors tried in order when <see cref="Selector"/> does not work during replay.</summary>
    public List<string> SelectorAlternates { get; set; } = new();

    /// <summary>Visible text or query used by text and locator steps.</summary>
    public string? Text { get; set; }

    /// <summary>Input value used by Input and TypeInput steps.</summary>
    public string? Value { get; set; }

    /// <summary>Whether a recorded input value was redacted before the recipe was stored.</summary>
    public bool? ValueRedacted { get; set; }

    /// <summary>Reason a recorded input value was redacted.</summary>
    public string? ValueRedactionReason { get; set; }

    /// <summary>Runtime variable name used to replace a redacted or parameterized value during replay.</summary>
    public string? ValueVariable { get; set; }

    /// <summary>Input values used by multi-value steps such as SelectOption.</summary>
    public List<string> Values { get; set; } = new();

    /// <summary>Checked state used by SetChecked steps.</summary>
    public bool? Checked { get; set; }

    /// <summary>Keyboard key expression used by Key steps, such as Enter or Control+A.</summary>
    public string? Keys { get; set; }

    /// <summary>JavaScript expression or function used by Script and WaitReady steps.</summary>
    public string? Script { get; set; }

    /// <summary>Output file path used by Screenshot steps.</summary>
    public string? OutFile { get; set; }

    /// <summary>Output folder used by Evidence steps.</summary>
    public string? OutFolder { get; set; }

    /// <summary>Base file name for Evidence steps.</summary>
    public string? BaseFileName { get; set; }

    /// <summary>Whether Evidence steps capture the viewport screenshot.</summary>
    public bool? Screenshot { get; set; }

    /// <summary>Whether Evidence steps capture the full-page screenshot.</summary>
    public bool? FullPageScreenshot { get; set; }

    /// <summary>Whether Evidence steps export a PDF print.</summary>
    public bool? Pdf { get; set; }

    /// <summary>Whether Evidence steps write rendered HTML.</summary>
    public bool? Html { get; set; }

    /// <summary>Whether Evidence steps write visible page text.</summary>
    public bool? VisibleText { get; set; }

    /// <summary>Whether Evidence steps write Markdown converted from rendered HTML.</summary>
    public bool? Markdown { get; set; }

    /// <summary>Whether Evidence steps write a network summary.</summary>
    public bool? NetworkSummary { get; set; }

    /// <summary>Whether Evidence steps write a redacted SSO handoff summary.</summary>
    public bool? SsoHandoffSummary { get; set; }

    /// <summary>Whether Evidence steps mask common sensitive fields in screenshots.</summary>
    public bool? MaskSensitiveScreenshotElements { get; set; }

    /// <summary>Additional selectors masked in Evidence step screenshots.</summary>
    public List<string> ScreenshotMaskSelectors { get; set; } = new();

    /// <summary>CSS color used for Evidence step screenshot masks.</summary>
    public string? ScreenshotMaskColor { get; set; }

    /// <summary>Whether Evidence steps redact common secrets from text artifacts and manifests.</summary>
    public bool? RedactSensitiveValues { get; set; }

    /// <summary>Whether Evidence steps write an evidence manifest.</summary>
    public bool? Manifest { get; set; }

    /// <summary>Use exact text matching where supported.</summary>
    public bool Exact { get; set; }

    /// <summary>Wait for a navigation event after click steps where supported.</summary>
    public bool WaitForNavigation { get; set; }

    /// <summary>Expected post-click navigation URL glob for click steps that wait for navigation.</summary>
    public string? NavigationUrl { get; set; }

    /// <summary>Use keyboard typing instead of direct fill for TypeInput steps.</summary>
    public int DelayMilliseconds { get; set; } = 40;

    /// <summary>Fixed wait duration for WaitMilliseconds steps.</summary>
    public int Milliseconds { get; set; }

    /// <summary>Skip load-state wait in WaitReady steps.</summary>
    public bool NoLoadState { get; set; }

    /// <summary>Load state for WaitReady steps.</summary>
    public HtmlBrowserLoadState LoadState { get; set; } = HtmlBrowserLoadState.NetworkIdle;

    /// <summary>Wait for document stability in WaitReady steps.</summary>
    public bool Stable { get; set; }

    /// <summary>Stable interval in milliseconds.</summary>
    public int StableMilliseconds { get; set; } = 500;

    /// <summary>Polling interval in milliseconds.</summary>
    public int PollMilliseconds { get; set; } = 100;

    /// <summary>Timeout in milliseconds for this step. When null, the recipe default is used.</summary>
    public int? Timeout { get; set; }

    /// <summary>Continue with the next step when this step fails.</summary>
    public bool ContinueOnError { get; set; }

    /// <summary>Capture a full-page screenshot for Screenshot steps.</summary>
    public bool FullPage { get; set; }

    /// <summary>Include hidden locator candidates in Locator steps.</summary>
    public bool IncludeHidden { get; set; }

    /// <summary>Maximum locator candidates returned by Locator steps.</summary>
    public int Limit { get; set; } = 25;
}
