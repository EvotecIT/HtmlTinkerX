using System.IO;
using System.Threading.Tasks;

namespace HtmlTinkerX.Examples;

/// <summary>
/// Demonstrates retrieving PDF bytes from a page.
/// </summary>
public static class GetPdfExample {
    /// <summary>Executes the example logic.</summary>
    public static async Task RunAsync() {
        byte[] data = await HtmlBrowser.GetPagePdfAsync(
            "https://example.com",
            format: PdfPageFormat.A4);
        await File.WriteAllBytesAsync("page.pdf", data);
    }
}
