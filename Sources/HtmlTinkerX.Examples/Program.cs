using System;
using System.Threading.Tasks;

namespace HtmlTinkerX.Examples;

/// <summary>
/// Entry point for the examples application.
/// </summary>
public static class Program {
    /// <summary>
    /// Executes the browser and browserless extraction examples.
    /// </summary>
    public static async Task Main(string[] args) {
        if (args.Length > 0 && string.Equals(args[0], "browser-pdf", StringComparison.OrdinalIgnoreCase)) {
            string outputPath = args.Length > 1 ? args[1] : "browser-pdf-example.pdf";
            await BrowserPdfRendererExample.RunAsync(outputPath).ConfigureAwait(false);
            return;
        }

        await BrowserExtractionModeExample.RunAsync().ConfigureAwait(false);
        await BrowserlessExtractionExample.RunAsync().ConfigureAwait(false);
    }
}
