using HtmlTinkerX;
using System.Threading;

namespace HtmlTinkerX.Tests;

public class PreMailerClientAsyncTests {
    private const string HtmlWithMediaQuery = "<html><head><style>h1{color:red;}@media(max-width:600px){h1{font-size:14px;}}</style></head><body><h1>Hello</h1></body></html>";

    [Fact]
    public async Task MoveCssInlineAsync_RemovesStyleElements_WhenEnabled() {
        var options = new PreMailerOptions { RemoveStyleElements = true };
        PreMailerResult result = await PreMailerClient.MoveCssInlineAsync(HtmlWithMediaQuery, options, CancellationToken.None);
        Assert.DoesNotContain("<style", result.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MoveCssInlineFromFileAsync_ProcessesFile() {
        var options = new PreMailerOptions { RemoveStyleElements = true };
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".html");
#if FRAMEWORK
        await WriteAllTextAsync(path, HtmlWithMediaQuery);
#else
        await File.WriteAllTextAsync(path, HtmlWithMediaQuery);
#endif
        try {
            PreMailerResult result = await PreMailerClient.MoveCssInlineFromFileAsync(path, options, CancellationToken.None);
            Assert.DoesNotContain("<style", result.Html, StringComparison.OrdinalIgnoreCase);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task MoveCssInlineAsync_CanceledToken_Throws() {
        using CancellationTokenSource cts = new();
        cts.Cancel();
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => PreMailerClient.MoveCssInlineAsync(HtmlWithMediaQuery, null, cts.Token));
    }

    [Fact]
    public async Task MoveCssInlineFromFileAsync_CanceledToken_Throws() {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".html");
#if FRAMEWORK
        await WriteAllTextAsync(path, HtmlWithMediaQuery);
#else
        await File.WriteAllTextAsync(path, HtmlWithMediaQuery);
#endif
        using CancellationTokenSource cts = new();
        cts.Cancel();
        try {
            await Assert.ThrowsAsync<TaskCanceledException>(
                () => PreMailerClient.MoveCssInlineFromFileAsync(path, null, cts.Token));
        } finally {
            File.Delete(path);
        }
    }
}