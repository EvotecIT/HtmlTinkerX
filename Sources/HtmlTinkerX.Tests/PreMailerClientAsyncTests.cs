using HtmlTinkerX;
using System.Net;
using System.Net.Http;
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
    public async Task MoveCssInlineAsync_PreservesLinkedStylesheetWhenDownloadIsDisabled() {
        const string html = "<html><head><link rel='stylesheet' href='https://example.org/site.css'></head><body>Hello</body></html>";

        PreMailerResult result = await PreMailerClient.MoveCssInlineAsync(html, new PreMailerOptions(), CancellationToken.None);

        Assert.Contains("https://example.org/site.css", result.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MoveCssInlineAsync_PreservesLinkedStylesheetWhenDownloadFails() {
        const string html = "<html><head><link rel='stylesheet' href='https://example.org/site.css'></head><body>Hello</body></html>";
        using HttpClient client = new(new DelegateHandler((_, _) => throw new HttpRequestException("offline")));
        PreMailerOptions options = new() {
            DownloadRemoteCss = true,
            HttpClient = client
        };

        PreMailerResult result = await PreMailerClient.MoveCssInlineAsync(html, options, CancellationToken.None);

        Assert.Contains("https://example.org/site.css", result.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MoveCssInlineAsync_UsesCallerHttpClientAndRemovesSuccessfullyInlinedLink() {
        const string html = "<html><head><link rel='stylesheet' href='https://example.org/site.css'></head><body><p>Hello</p></body></html>";
        using HttpClient client = new(new DelegateHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("p{color:green}")
        })));
        PreMailerOptions options = new() {
            DownloadRemoteCss = true,
            HttpClient = client
        };

        PreMailerResult result = await PreMailerClient.MoveCssInlineAsync(html, options, CancellationToken.None);

        Assert.Contains("color: green", result.Html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://example.org/site.css", result.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MoveCssInlineAsync_PropagatesCancellationDuringStylesheetDownload() {
        const string html = "<html><head><link rel='stylesheet' href='https://example.org/site.css'></head><body>Hello</body></html>";
        TaskCompletionSource<bool> requestStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using HttpClient client = new(new DelegateHandler(async (_, cancellationToken) => {
            requestStarted.TrySetResult(true);
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        PreMailerOptions options = new() {
            DownloadRemoteCss = true,
            HttpClient = client
        };
        using CancellationTokenSource cts = new();

        Task<PreMailerResult> operation = PreMailerClient.MoveCssInlineAsync(html, options, cts.Token);
        await requestStarted.Task;
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
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

    private sealed class DelegateHandler : HttpMessageHandler {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler;

        internal DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) {
            this.handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }
}
