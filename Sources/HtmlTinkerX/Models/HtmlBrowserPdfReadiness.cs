namespace HtmlTinkerX;

using System;

/// <summary>Immutable readiness conditions evaluated before PDF capture.</summary>
public sealed class HtmlBrowserPdfReadiness {
    /// <summary>Initializes readiness conditions.</summary>
    public HtmlBrowserPdfReadiness(
        HtmlBrowserLoadState loadState = HtmlBrowserLoadState.NetworkIdle,
        bool skipLoadState = false,
        string? selector = null,
        string? function = null,
        bool stable = false,
        int stableMilliseconds = 500,
        int pollMilliseconds = 100,
        int timeout = 30000,
        int delayMilliseconds = 0) {
        if (timeout < 0) throw new ArgumentOutOfRangeException(nameof(timeout));
        if (stableMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(stableMilliseconds));
        if (pollMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(pollMilliseconds));
        if (delayMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(delayMilliseconds));

        LoadState = loadState;
        SkipLoadState = skipLoadState;
        Selector = selector;
        Function = function;
        Stable = stable;
        StableMilliseconds = stableMilliseconds;
        PollMilliseconds = pollMilliseconds;
        Timeout = timeout;
        DelayMilliseconds = delayMilliseconds;
    }

    /// <summary>Gets the load state.</summary>
    public HtmlBrowserLoadState LoadState { get; }
    /// <summary>Gets whether the load-state wait is skipped.</summary>
    public bool SkipLoadState { get; }
    /// <summary>Gets the selector that must appear.</summary>
    public string? Selector { get; }
    /// <summary>Gets the JavaScript predicate that must become truthy.</summary>
    public string? Function { get; }
    /// <summary>Gets whether markup stability is required.</summary>
    public bool Stable { get; }
    /// <summary>Gets the required stable interval.</summary>
    public int StableMilliseconds { get; }
    /// <summary>Gets the stability polling interval.</summary>
    public int PollMilliseconds { get; }
    /// <summary>Gets the timeout for each readiness condition.</summary>
    public int Timeout { get; }
    /// <summary>Gets the final fixed delay before printing.</summary>
    public int DelayMilliseconds { get; }
}
