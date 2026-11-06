using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlBoundedTextFileTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadFileCheckedAsync_BoundsDecodedCharactersWithUtf8AndUtf16(bool utf16) {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".html");
        const string content = "<p>Zażółć gęślą jaźń😀</p>";
        try {
            File.WriteAllText(path, content, utf16 ? Encoding.Unicode : new UTF8Encoding(true));
            Assert.Equal(content, await HtmlUtilities.ReadFileCheckedAsync(path, content.Length, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                HtmlUtilities.ReadFileCheckedAsync(path, content.Length - 1, CancellationToken.None));
            // A rejected read has released the handle, including on Windows.
            using var reopened = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        } finally { File.Delete(path); }
    }

    [Fact]
    public async Task ReadFileCheckedAsync_CanceledInputDoesNotOpenFile() {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".html");
        try {
            File.WriteAllText(path, "<p>Bounded</p>");
            using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
                HtmlUtilities.ReadFileCheckedAsync(path, 100, cancellation.Token));
            Assert.Equal(cancellation.Token, exception.CancellationToken);
        } finally { File.Delete(path); }
    }
}
