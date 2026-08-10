namespace HtmlTinkerX;

/// <summary>Point-in-time lifecycle counters for a browser PDF renderer.</summary>
public sealed class HtmlBrowserPdfRendererMetrics {
    internal HtmlBrowserPdfRendererMetrics(long accepted, long succeeded, long failed, long cancelled, long rejected, long retries, long created, long recycled, int active, int queued, int idle) {
        AcceptedCaptures = accepted;
        SucceededCaptures = succeeded;
        FailedCaptures = failed;
        CancelledCaptures = cancelled;
        RejectedCaptures = rejected;
        BrowserFailureRetries = retries;
        BrowsersCreated = created;
        BrowsersRecycled = recycled;
        ActiveCaptures = active;
        QueuedCaptures = queued;
        IdleBrowsers = idle;
    }

    /// <summary>Gets the number of captures admitted by the bounded renderer.</summary>
    public long AcceptedCaptures { get; }
    /// <summary>Gets the number of successful captures.</summary>
    public long SucceededCaptures { get; }
    /// <summary>Gets the number of failed captures.</summary>
    public long FailedCaptures { get; }
    /// <summary>Gets the number of cancelled captures.</summary>
    public long CancelledCaptures { get; }
    /// <summary>Gets the number of captures rejected because the queue was full.</summary>
    public long RejectedCaptures { get; }
    /// <summary>Gets the number of browser-failure retry attempts.</summary>
    public long BrowserFailureRetries { get; }
    /// <summary>Gets the number of Chromium processes created.</summary>
    public long BrowsersCreated { get; }
    /// <summary>Gets the number of Chromium processes recycled or invalidated.</summary>
    public long BrowsersRecycled { get; }
    /// <summary>Gets the number of active captures.</summary>
    public int ActiveCaptures { get; }
    /// <summary>Gets the number of captures waiting for a browser lease.</summary>
    public int QueuedCaptures { get; }
    /// <summary>Gets the number of idle warm browsers.</summary>
    public int IdleBrowsers { get; }
}
