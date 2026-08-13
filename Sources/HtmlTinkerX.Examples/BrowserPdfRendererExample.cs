using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX.Examples;

/// <summary>Demonstrates pooled, isolated Chromium PDF capture from an HTML string.</summary>
public static class BrowserPdfRendererExample {
    /// <summary>Generates a self-contained browser PDF example.</summary>
    public static async Task RunAsync(string outputPath, CancellationToken cancellationToken = default) {
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            minimumBrowserInstances: 1,
            maximumBrowserInstances: 2,
            maximumQueuedCaptures: 8));
        await renderer.PreWarmAsync(cancellationToken).ConfigureAwait(false);

        const string html = """
            <!doctype html>
            <html lang="en">
              <head>
                <meta charset="utf-8">
                <style>
                  :root { color-scheme: light; font-family: system-ui, sans-serif; }
                  body { margin: 0; color: #14213d; background: #f8fafc; }
                  main { padding: 36px; }
                  .summary { display: grid; grid-template-columns: repeat(3, 1fr); gap: 16px; }
                  .card { padding: 18px; border: 1px solid #cbd5e1; border-radius: 10px; background: white; }
                  .value { color: #0f766e; font-size: 28px; font-weight: 700; }
                </style>
              </head>
              <body>
                <main data-report-ready>
                  <h1>Browser PDF lifecycle report</h1>
                  <p>Generated in an isolated context on a warm Chromium process.</p>
                  <section class="summary">
                    <article class="card"><div class="value">2</div><div>active slots</div></article>
                    <article class="card"><div class="value">8</div><div>queued captures</div></article>
                    <article class="card"><div class="value">A4</div><div>page format</div></article>
                  </section>
                </main>
              </body>
            </html>
            """;

        HtmlBrowserPdfRequest request = new(
            HtmlBrowserPdfSource.FromHtml(html),
            pdfOptions: new HtmlBrowserPdfOptions(
                format: PdfPageFormat.A4,
                printBackground: true,
                marginTop: "12mm",
                marginRight: "12mm",
                marginBottom: "12mm",
                marginLeft: "12mm",
                tagged: true,
                outline: true),
            readiness: new HtmlBrowserPdfReadiness(selector: "[data-report-ready]"));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(request, cancellationToken).ConfigureAwait(false);
        string fullPath = Path.GetFullPath(outputPath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllBytes(fullPath, result.PdfBytes);

        Console.WriteLine($"Wrote {result.Length:N0} bytes to {fullPath}");
        Console.WriteLine($"Browser {result.Diagnostics.BrowserInstanceId}; total {result.Diagnostics.TotalDuration.TotalMilliseconds:N0} ms");
    }
}
