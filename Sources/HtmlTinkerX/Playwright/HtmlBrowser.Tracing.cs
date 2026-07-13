using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for retrieving HTML content using a headless browser.
/// </summary>
public static partial class HtmlBrowser {
    private static readonly JsonSerializerOptions HarJsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Starts Playwright tracing for the given session.
    /// </summary>
    /// <param name="session">Active browser session.</param>
    /// <param name="screenshots">Capture screenshots while tracing.</param>
    /// <param name="snapshots">Capture DOM snapshots while tracing.</param>
    /// <param name="sources">Include source code in the trace.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A running task for the tracing start operation.</returns>
    public static Task StartTracingAsync(HtmlBrowserSession session, bool screenshots = true, bool snapshots = true, bool sources = true, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        return session.Context.Tracing.StartAsync(new Microsoft.Playwright.TracingStartOptions { Screenshots = screenshots, Snapshots = snapshots, Sources = sources });
    }

    /// <summary>
    /// Stops tracing and saves the resulting trace file.
    /// </summary>
    /// <param name="session">Active browser session.</param>
    /// <param name="path">Target path for the trace file.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task StopTracingAsync(HtmlBrowserSession session, string path, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        string full = HtmlUtilities.EnsureDirectoryExists(path);
        return session.Context.Tracing.StopAsync(new Microsoft.Playwright.TracingStopOptions { Path = full });
    }

    /// <summary>
    /// Exports the network log of a session to a HAR file.
    /// </summary>
    /// <param name="session">Active browser session.</param>
    /// <param name="path">Destination HAR file path.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the file has been written.</returns>
    public static Task ExportHarAsync(HtmlBrowserSession session, string path, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        string full = HtmlUtilities.EnsureDirectoryExists(path);
        var log = new {
            log = new {
                version = "1.2",
                creator = new { name = "HtmlTinkerX" },
                entries = session.NetworkLog.Select(e => new {
                    startedDateTime = e.Started.ToString("o"),
                    request = new {
                        method = e.Method.ToString().ToUpperInvariant(),
                        url = e.Url,
                        headers = e.RequestHeaders.Select(h => new { name = h.Key, value = h.Value })
                    },
                    response = new {
                        status = e.Status.HasValue ? (int)e.Status.Value : 0,
                        headers = (e.ResponseHeaders ?? new Dictionary<string, string>()).Select(h => new { name = h.Key, value = h.Value })
                    },
                    timings = new { wait = e.Duration?.TotalMilliseconds ?? 0 }
                })
            }
        };
        string json = JsonSerializer.Serialize(log, HarJsonOptions);
#if NETSTANDARD2_0 || NETFRAMEWORK
        File.WriteAllText(full, json);
        return Task.CompletedTask;
#else
        return File.WriteAllTextAsync(full, json, cancellationToken);
#endif
    }
}
