using System;
using System.IO;
using System.Threading.Tasks;
using HtmlTinkerX;

namespace HtmlTinkerX.Examples;

/// <summary>
/// Demonstrates retrieving network log entries with timing information.
/// </summary>
public static class NetworkLogTimingExample {
    /// <summary>Executes the example logic.</summary>
    public static async Task RunAsync() {
        string path = Path.Combine("..", "..", "..", "Examples", "Input", "route_page.html");
        string url = new Uri(Path.GetFullPath(path)).AbsoluteUri;
        var result = await HtmlBrowserTester.TestUrlAsync(url);
        foreach (HtmlNetworkEntryDetailed entry in result.NetworkEntries) {
            Console.WriteLine($"{entry.Method} {entry.Url} -> {entry.Status} {entry.ProtocolVersion} in {entry.Duration?.TotalMilliseconds:F0} ms");
        }
    }
}