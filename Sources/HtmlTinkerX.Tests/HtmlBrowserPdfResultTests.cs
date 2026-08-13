using System;
using System.IO;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed class HtmlBrowserPdfResultTests {
    [Fact]
    public void OpenReadReturnsIndependentReadOnlyStreamsWithoutExposingTheBuffer() {
        byte[] payload = { 1, 2, 3, 4 };
        HtmlBrowserPdfResult result = new(payload, CreateDiagnostics(), tagged: false);

        using Stream first = result.OpenRead();
        using Stream second = result.OpenRead();

        Assert.True(first.CanRead);
        Assert.False(first.CanWrite);
        Assert.Equal(1, first.ReadByte());
        Assert.Equal(0, second.Position);
        Assert.Equal(1, second.ReadByte());
        Assert.Equal(payload.Length, result.Length);
        Assert.Throws<NotSupportedException>(() => first.WriteByte(5));
        Assert.Throws<UnauthorizedAccessException>(() => ((MemoryStream)first).GetBuffer());
    }

    [Fact]
    public void PdfBytesRemainsADefensiveCopyAlongsideOpenRead() {
        byte[] payload = { 1, 2, 3 };
        HtmlBrowserPdfResult result = new(payload, CreateDiagnostics(), tagged: true);

        byte[] copy = result.PdfBytes;
        copy[0] = 9;

        using Stream stream = result.OpenRead();
        Assert.Equal(1, stream.ReadByte());
        Assert.True(result.Tagged);
    }

    private static HtmlBrowserPdfDiagnostics CreateDiagnostics() => new(
        HtmlBrowserPdfSourceKind.Html,
        browserInstanceId: 1,
        browserReused: false,
        retriedAfterBrowserFailure: false,
        finalUrl: "about:blank",
        browserVersion: "test",
        queueDuration: TimeSpan.Zero,
        navigationDuration: TimeSpan.Zero,
        readinessDuration: TimeSpan.Zero,
        pdfDuration: TimeSpan.Zero,
        totalDuration: TimeSpan.Zero,
        blockedRequestCount: 0,
        blockedRequests: Array.Empty<string>(),
        warnings: Array.Empty<string>());
}
