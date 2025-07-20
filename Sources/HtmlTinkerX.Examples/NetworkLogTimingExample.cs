using System;
using System.IO;
using System.Threading.Tasks;

namespace HtmlTinkerX.Examples;

/// <summary>
/// Demonstrates retrieving network log entries with timing information.
/// </summary>
public static class NetworkLogTimingExample {
    /// <summary>Executes the example logic.</summary>
    public static async Task RunAsync() {
        string path = Path.Combine("..", "..", "..", "Examples", "Input", "route_page.html");
        string url = new Uri(Path.GetFullPath(path)).AbsoluteUri;
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync(url).ConfigureAwait(false);
        foreach (HtmlNetworkEntry entry in HtmlBrowser.GetNetworkLog(session)) {
            Console.WriteLine($"{entry.Method} {entry.Url} -> {entry.Status} in {entry.Duration?.TotalMilliseconds:F0} ms");
        }
    }
}
