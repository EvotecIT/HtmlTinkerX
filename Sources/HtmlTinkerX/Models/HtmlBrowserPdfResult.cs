namespace HtmlTinkerX;

using System;
using System.IO;

/// <summary>Browser-generated PDF bytes and capture diagnostics.</summary>
public sealed class HtmlBrowserPdfResult {
    private readonly byte[] _pdfBytes;

    internal HtmlBrowserPdfResult(byte[] pdfBytes, HtmlBrowserPdfDiagnostics diagnostics, bool tagged) {
        _pdfBytes = pdfBytes ?? throw new ArgumentNullException(nameof(pdfBytes));
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        Tagged = tagged;
    }

    /// <summary>Gets a defensive copy of the generated PDF bytes.</summary>
    public byte[] PdfBytes => (byte[])_pdfBytes.Clone();
    /// <summary>Gets the PDF payload length without allocating a copy.</summary>
    public int Length => _pdfBytes.Length;
    /// <summary>Gets diagnostics for the browser stage.</summary>
    public HtmlBrowserPdfDiagnostics Diagnostics { get; }
    /// <summary>Gets whether Chromium was requested to generate a tagged PDF.</summary>
    public bool Tagged { get; }

    /// <summary>
    /// Opens an independent, read-only stream over the generated PDF payload without cloning it.
    /// Disposing the returned stream does not affect this result or other streams opened from it.
    /// </summary>
    public Stream OpenRead() => new MemoryStream(
        _pdfBytes,
        0,
        _pdfBytes.Length,
        writable: false,
        publiclyVisible: false);

    internal HtmlBrowserPdfResult WithTotalDuration(TimeSpan totalDuration) =>
        new(_pdfBytes, Diagnostics.WithTotalDuration(totalDuration), Tagged);
}
