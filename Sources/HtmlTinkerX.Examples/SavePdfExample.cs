using System.Threading.Tasks;

namespace HtmlTinkerX.Examples;

/// <summary>
/// Demonstrates saving a PDF document using <see cref="PdfPageFormat"/>.
/// </summary>
public static class SavePdfExample {
    /// <summary>Executes the example logic.</summary>
    public static async Task RunAsync() {
        string url = "https://example.com";
        await HtmlBrowser.SavePagePdfAsync(url, "example.pdf", format: PdfPageFormat.A4).ConfigureAwait(false);
    }
}
