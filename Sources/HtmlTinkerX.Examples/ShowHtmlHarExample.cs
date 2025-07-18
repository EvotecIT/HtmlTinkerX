using System;
using System.IO;
using System.Threading.Tasks;

namespace HtmlTinkerX.Examples;

/// <summary>
/// Demonstrates generating an HTML viewer for a HAR file.
/// </summary>
public static class ShowHtmlHarExample {
    /// <summary>
    /// Executes the example logic.
    /// </summary>
    public static async Task RunAsync() {
        string harPath = Path.Combine("..", "..", "..", "Examples", "example.har");
        Har har = await HtmlHarViewer.ReadHarAsync(harPath).ConfigureAwait(false);
        string html = HtmlHarViewer.BuildViewerHtml(har);
        string outFile = "viewer.html";
        await File.WriteAllTextAsync(outFile, html).ConfigureAwait(false);
        Console.WriteLine($"Viewer written to {outFile}");
        if (har.Log?.Entries is { Length: > 0 } entries) {
            var first = entries[0];
            Console.WriteLine($"First entry: {first.Request?.Method} {first.Request?.Url} -> {first.Response?.Status}");
        }
    }
}