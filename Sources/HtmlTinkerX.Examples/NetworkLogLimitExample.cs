using System;
using System.IO;
using System.Threading.Tasks;

namespace HtmlTinkerX.Examples;

/// <summary>
/// Demonstrates limiting captured network log entries.
/// </summary>
public static class NetworkLogLimitExample {
    /// <summary>Executes the example logic.</summary>
    public static async Task RunAsync() {
        string path = Path.Combine("..", "..", "..", "Examples", "Input", "route_page.html");
        string url = new Uri(Path.GetFullPath(path)).AbsoluteUri;
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync(url).ConfigureAwait(false);
        session.NetworkLogLimit = 2;
        foreach (HtmlNetworkEntry entry in HtmlBrowser.GetNetworkLog(session)) {
            Console.WriteLine($"{entry.Method} {entry.Url}");
        }
    }
}
