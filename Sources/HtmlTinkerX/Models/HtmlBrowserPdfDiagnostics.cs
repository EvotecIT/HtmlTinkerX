namespace HtmlTinkerX;

using System;
using System.Collections.Generic;

/// <summary>Diagnostics for one browser-backed PDF capture.</summary>
public sealed class HtmlBrowserPdfDiagnostics {
    internal HtmlBrowserPdfDiagnostics(
        HtmlBrowserPdfSourceKind sourceKind,
        long browserInstanceId,
        bool browserReused,
        bool retriedAfterBrowserFailure,
        string? finalUrl,
        string browserVersion,
        TimeSpan queueDuration,
        TimeSpan navigationDuration,
        TimeSpan readinessDuration,
        TimeSpan pdfDuration,
        TimeSpan totalDuration,
        int blockedRequestCount,
        IReadOnlyList<string> blockedRequests,
        IReadOnlyList<string> warnings) {
        SourceKind = sourceKind;
        BrowserInstanceId = browserInstanceId;
        BrowserReused = browserReused;
        RetriedAfterBrowserFailure = retriedAfterBrowserFailure;
        FinalUrl = finalUrl;
        BrowserVersion = browserVersion;
        QueueDuration = queueDuration;
        NavigationDuration = navigationDuration;
        ReadinessDuration = readinessDuration;
        PdfDuration = pdfDuration;
        TotalDuration = totalDuration;
        BlockedRequestCount = blockedRequestCount;
        BlockedRequests = Array.AsReadOnly(new List<string>(blockedRequests).ToArray());
        Warnings = Array.AsReadOnly(new List<string>(warnings).ToArray());
    }

    /// <summary>Gets the source kind.</summary>
    public HtmlBrowserPdfSourceKind SourceKind { get; }
    /// <summary>Gets the renderer-local browser instance identifier.</summary>
    public long BrowserInstanceId { get; }
    /// <summary>Gets whether the leased browser had completed an earlier render.</summary>
    public bool BrowserReused { get; }
    /// <summary>Gets whether capture retried once after a browser process failure.</summary>
    public bool RetriedAfterBrowserFailure { get; }
    /// <summary>Gets the final page URL without credentials or query data.</summary>
    public string? FinalUrl { get; }
    /// <summary>Gets the Chromium version reported by Playwright.</summary>
    public string BrowserVersion { get; }
    /// <summary>Gets time spent waiting for a browser lease.</summary>
    public TimeSpan QueueDuration { get; }
    /// <summary>Gets time spent loading the input.</summary>
    public TimeSpan NavigationDuration { get; }
    /// <summary>Gets time spent in pre-capture preparation and readiness checks.</summary>
    public TimeSpan ReadinessDuration { get; }
    /// <summary>Gets time spent in Chromium PDF generation.</summary>
    public TimeSpan PdfDuration { get; }
    /// <summary>Gets end-to-end capture time.</summary>
    public TimeSpan TotalDuration { get; }
    /// <summary>Gets the total number of blocked resource requests.</summary>
    public int BlockedRequestCount { get; }
    /// <summary>Gets a bounded, sanitized sample of blocked resource URLs.</summary>
    public IReadOnlyList<string> BlockedRequests { get; }
    /// <summary>Gets non-fatal capture warnings.</summary>
    public IReadOnlyList<string> Warnings { get; }
}
