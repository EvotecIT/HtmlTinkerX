using System;
using System.IO;
using System.Threading.Tasks;

namespace HtmlTinkerX.Examples;

/// <summary>
/// Demonstrates writing HAR data to a file using <see cref="HtmlHarViewer"/>.
/// </summary>
public static class WriteHarExample {
    /// <summary>Executes the example logic.</summary>
    public static async Task RunAsync() {
        string harPath = Path.Combine("..", "..", "..", "Examples", "example.har");
        Har har = await HtmlHarViewer.ReadHarAsync(harPath).ConfigureAwait(false);
        string outFile = "copy.har";
        await using FileStream fs = File.Create(outFile);
        await HtmlHarViewer.WriteHarAsync(har, fs).ConfigureAwait(false);
        Console.WriteLine($"HAR written to {outFile}");
    }
}
