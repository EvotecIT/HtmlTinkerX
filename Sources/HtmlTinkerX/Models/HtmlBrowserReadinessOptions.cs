namespace HtmlTinkerX;

/// <summary>
/// Describes browser readiness conditions to wait for before continuing an automation workflow.
/// </summary>
public sealed class HtmlBrowserReadinessOptions {
    /// <summary>Load state to wait for. Defaults to network idle.</summary>
    public HtmlBrowserLoadState LoadState { get; set; } = HtmlBrowserLoadState.NetworkIdle;

    /// <summary>Skip the load-state wait when the caller only needs selector, function, or stability checks.</summary>
    public bool SkipLoadState { get; set; }

    /// <summary>Optional selector that must exist before readiness completes.</summary>
    public string? Selector { get; set; }

    /// <summary>Optional JavaScript predicate that must evaluate truthy before readiness completes.</summary>
    public string? Function { get; set; }

    /// <summary>Wait until the document HTML remains unchanged for <see cref="StableMilliseconds"/>.</summary>
    public bool Stable { get; set; }

    /// <summary>Stable interval in milliseconds.</summary>
    public int StableMilliseconds { get; set; } = 500;

    /// <summary>Polling interval in milliseconds for stability checks.</summary>
    public int PollMilliseconds { get; set; } = 100;

    /// <summary>Timeout in milliseconds for each readiness condition.</summary>
    public int Timeout { get; set; } = 10000;
}
