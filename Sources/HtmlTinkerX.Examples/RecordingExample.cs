using System;
using System.Threading.Tasks;

namespace HtmlTinkerX.Examples;

/// <summary>
/// Demonstrates starting and stopping video recording.
/// </summary>
public static class RecordingExample {
    /// <summary>
    /// Executes the example logic.
    /// </summary>
    public static async Task RunAsync() {
        await using HtmlBrowserSession session = await HtmlBrowser.StartRecordingAsync(
            "https://example.com",
            "recording.webm");
        string path = await HtmlBrowser.StopRecordingAsync(session);
        Console.WriteLine($"Recording saved to {path}");
    }
}
