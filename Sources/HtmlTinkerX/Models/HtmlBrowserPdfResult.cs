namespace HtmlTinkerX;

using System;

/// <summary>Browser-generated PDF bytes and capture diagnostics.</summary>
public sealed class HtmlBrowserPdfResult {
    private readonly byte[] _pdfBytes;

    internal HtmlBrowserPdfResult(byte[] pdfBytes, HtmlBrowserPdfDiagnostics diagnostics) {
        _pdfBytes = pdfBytes ?? throw new ArgumentNullException(nameof(pdfBytes));
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    /// <summary>Gets a defensive copy of the generated PDF bytes.</summary>
    public byte[] PdfBytes => (byte[])_pdfBytes.Clone();
    /// <summary>Gets the PDF payload length without allocating a copy.</summary>
    public int Length => _pdfBytes.Length;
    /// <summary>Gets diagnostics for the browser stage.</summary>
    public HtmlBrowserPdfDiagnostics Diagnostics { get; }

    internal HtmlBrowserPdfResult WithTotalDuration(TimeSpan totalDuration) =>
        new(_pdfBytes, Diagnostics.WithTotalDuration(totalDuration));
}
