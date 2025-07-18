using System;
using System.IO;
using System.Threading.Tasks;

namespace HtmlTinkerX.Examples;

/// <summary>
/// Demonstrates exporting and importing browser storage state.
/// </summary>
public static class BrowserStateExample {
    /// <summary>Executes the example logic.</summary>
    public static async Task RunAsync() {
        string stateFile = Path.Combine(Path.GetTempPath(), "state.json");
        await using HtmlBrowserSession session1 = await HtmlBrowser.OpenSessionAsync("about:blank").ConfigureAwait(false);
        await HtmlBrowser.ExportBrowserStateAsync(session1, stateFile).ConfigureAwait(false);
        await HtmlBrowser.CloseSessionAsync(session1).ConfigureAwait(false);

        await using HtmlBrowserSession session2 = await HtmlBrowser.ImportBrowserStateAsync("about:blank", stateFile).ConfigureAwait(false);
        Console.WriteLine($"Restored URL: {session2.Page.Url}");
        await HtmlBrowser.CloseSessionAsync(session2).ConfigureAwait(false);
    }
}