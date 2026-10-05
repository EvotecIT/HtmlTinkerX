using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlAtomicExportTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task WriteAtomicallyAsync_DoesNotPublishCanceledOrFailedExports(bool cancel, bool existingFile) {
        string directory = Path.Combine(Path.GetTempPath(), "HtmlAtomicExportTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "export.json");
        if (existingFile) File.WriteAllText(path, "previous export");
        using CancellationTokenSource cancellation = new();
        try {
            Task writing = HtmlUtilities.WriteAtomicallyAsync(path, async (stream, token) => {
                byte[] partial = Encoding.UTF8.GetBytes("{\"partial\":");
                await stream.WriteAsync(partial, 0, partial.Length, token);
                if (cancel) cancellation.Cancel();
                else throw new InvalidDataException("The export producer failed.");
            }, cancellation.Token);
            if (cancel) {
                OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writing);
                Assert.Equal(cancellation.Token, error.CancellationToken);
            } else await Assert.ThrowsAsync<InvalidDataException>(() => writing);
            if (existingFile) Assert.Equal("previous export", File.ReadAllText(path));
            else Assert.False(File.Exists(path));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }
}
