namespace HtmlTinkerX;

/// <summary>
/// Browser page readiness state used after navigation.
/// </summary>
public enum HtmlBrowserLoadState {
    /// <summary>Wait only until the initial response is committed.</summary>
    Commit,

    /// <summary>Wait until the DOMContentLoaded event fires.</summary>
    DomContentLoaded,

    /// <summary>Wait until the load event fires.</summary>
    Load,

    /// <summary>Wait until Playwright observes no network connections for a short idle window.</summary>
    NetworkIdle
}
