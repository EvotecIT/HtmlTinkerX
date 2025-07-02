using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML;

public static partial class HtmlBrowser {
    /// <summary>
    /// Starts Playwright tracing for the given session.
    /// </summary>
    /// <param name="session">Active browser session.</param>
    /// <param name="screenshots">Capture screenshots while tracing.</param>
    /// <param name="snapshots">Capture DOM snapshots while tracing.</param>
    /// <param name="sources">Include source code in the trace.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A running task for the tracing start operation.</returns>
    public static Task StartTracingAsync(HtmlBrowserSession session, bool screenshots = true, bool snapshots = true, bool sources = true, CancellationToken cancellationToken = default)
    {
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
        string full = HtmlUtilities.ResolvePath(path);
        string? dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir)) {
            Directory.CreateDirectory(dir);
        }
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
        string full = HtmlUtilities.ResolvePath(path);
        string? dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir)) {
            Directory.CreateDirectory(dir);
        }
        var log = new {
            log = new {
                version = "1.2",
                creator = new { name = "PSParseHTML" },
                entries = session.NetworkLog.Select(e => new {
                    startedDateTime = System.DateTime.UtcNow.ToString("o"),
                    request = new {
                        method = e.Method,
                        url = e.Url,
                        headers = e.RequestHeaders.Select(h => new { name = h.Key, value = h.Value })
                    },
                    response = new {
                        status = e.Status ?? 0,
                        headers = (e.ResponseHeaders ?? new Dictionary<string,string>()).Select(h => new { name = h.Key, value = h.Value })
                    },
                    timings = new { wait = 0 }
                })
            }
        };
        var opts = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(log, opts);
        File.WriteAllText(full, json);
        return Task.CompletedTask;
    }
}

