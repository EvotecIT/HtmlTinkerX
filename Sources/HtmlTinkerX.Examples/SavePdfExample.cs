using System.Threading.Tasks;

namespace HtmlTinkerX.Examples;

/// <summary>
/// Demonstrates generating a PDF from a page.
/// </summary>
public static class SavePdfExample {
    /// <summary>
    /// Executes the example logic.
    /// </summary>
    public static async Task RunAsync() {
        await HtmlBrowser.SavePagePdfAsync(
            "https://example.com",
            "page.pdf",
            format: PdfPageFormat.A4);
    }
}
