using System.Threading.Tasks;

namespace HtmlTinkerX.Examples;

/// <summary>
/// Entry point for the examples application.
/// </summary>
public static class Program {
    /// <summary>
    /// Executes the browser and browserless extraction examples.
    /// </summary>
    public static async Task Main() {
        await BrowserExtractionModeExample.RunAsync().ConfigureAwait(false);
        await BrowserlessExtractionExample.RunAsync().ConfigureAwait(false);
    }
}
