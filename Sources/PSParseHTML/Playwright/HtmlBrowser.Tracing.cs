using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML;

public static partial class HtmlBrowser {
    public static Task StartTracingAsync(HtmlBrowserSession session, bool screenshots = true, bool snapshots = true, bool sources = true, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return session.Context.Tracing.StartAsync(new Microsoft.Playwright.TracingStartOptions { Screenshots = screenshots, Snapshots = snapshots, Sources = sources });
    }

    public static Task StopTracingAsync(HtmlBrowserSession session, string path, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        string full = HtmlUtilities.ResolvePath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        return session.Context.Tracing.StopAsync(new Microsoft.Playwright.TracingStopOptions { Path = full });
    }

    public static Task ExportHarAsync(HtmlBrowserSession session, string path, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        string full = HtmlUtilities.ResolvePath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
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
