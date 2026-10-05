using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlHttpResponsePolicyTests {
    private const string Url = "https://example.test/submit";
    private const string Relay = "<form action='/submit' method='post'><input type='hidden' name='SAMLResponse' value='test'></form><script>document.forms[0].submit()</script>";

    private static async Task<string> ReadAsync(string consumer, HttpClient client, HtmlHttpFetchOptions? options, CancellationToken token = default) {
        if (consumer == "url") {
            return options == null ? await HtmlUtilities.GetStringWithProperEncodingAsync(client, Url, token)
                : await HtmlUtilities.GetStringWithProperEncodingAsync(client, Url, options, token);
        }
        if (consumer == "relay") {
            var result = await HtmlFormRelayClient.FollowAsync(Relay, new Uri(Url), client, new HtmlFormRelayOptions { FetchOptions = options }, token);
            return result.FinalContent;
        }
        var fields = new Dictionary<string, string> { ["field"] = "value" };
        FormMethod method = consumer == "get" ? FormMethod.Get : FormMethod.Post;
        return options == null ? await HtmlFormSubmitter.SubmitAsync(Url, method, fields, client, token)
            : await HtmlFormSubmitter.SubmitAsync(Url, method, fields, client, options, token);
    }

    [Theory]
    [InlineData("url")]
    [InlineData("get")]
    [InlineData("post")]
    [InlineData("relay")]
    public async Task LegacyConsumers_RejectOversizeStreamingResponses(string consumer) {
        byte[] bytes = new byte[HtmlHttpFetchOptions.DefaultMaximumResponseBytes + 1];
        using var handler = new ResponseHandler(() => new StreamContent(new MemoryStream(bytes)));
        using var client = new HttpClient(handler);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(consumer, client, null));
    }

    [Theory]
    [InlineData("url")]
    [InlineData("get")]
    [InlineData("post")]
    [InlineData("relay")]
    public async Task ConsumerLimits_RejectDeclaredAndChunkedBodiesAndAllowExplicitOverride(string consumer) {
        foreach (bool declared in new[] { false, true }) {
            using var handler = new ResponseHandler(() => {
                HttpContent content = declared ? new ByteArrayContent(Encoding.UTF8.GetBytes("response"))
                    : new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes("response")));
                return content;
            });
            using var client = new HttpClient(handler);
            await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(consumer, client, new HtmlHttpFetchOptions { MaximumResponseBytes = 7 }));
            Assert.Equal("response", await ReadAsync(consumer, client, new HtmlHttpFetchOptions { MaximumResponseBytes = 8 }));
        }
    }

    [Theory]
    [InlineData("url")]
    [InlineData("get")]
    [InlineData("post")]
    [InlineData("relay")]
    public async Task Consumers_UseBomBeforeConflictingHeaders(string consumer) {
        foreach (Encoding encoding in new[] { Encoding.UTF8, Encoding.Unicode, Encoding.BigEndianUnicode }) {
            byte[] bytes = encoding.GetPreamble().Concat(encoding.GetBytes("Zażółć")).ToArray();
            using var handler = new ResponseHandler(() => {
                var content = new ByteArrayContent(bytes);
                content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/html; charset=windows-1252");
                return content;
            });
            using var client = new HttpClient(handler);
            Assert.Equal("Zażółć", await ReadAsync(consumer, client, null));
        }
    }

    [Theory]
    [InlineData("url")]
    [InlineData("get")]
    [InlineData("post")]
    [InlineData("relay")]
    public async Task Consumers_CancelStalledStreamsThatIgnoreReadTokens(string consumer) {
        foreach (bool callerCancellation in new[] { false, true }) {
            using var stream = new BlockingStream();
            using var handler = new ResponseHandler(() => new StreamContent(stream));
            using var client = new HttpClient(handler) { Timeout = callerCancellation ? TimeSpan.FromSeconds(5) : TimeSpan.FromMilliseconds(100) };
            using var cancellation = new CancellationTokenSource();
            Task<string> reading = ReadAsync(consumer, client, null, cancellation.Token);
            Assert.Same(stream.Entered.Task, await Task.WhenAny(stream.Entered.Task, Task.Delay(2000)));
            if (callerCancellation) cancellation.Cancel();
            Assert.Same(reading, await Task.WhenAny(reading, Task.Delay(2000)));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading);
            Assert.True(stream.Disposed);
        }
    }

    [Fact]
    public async Task FormResponse_MetaEncodingAndExplicitLimitsAreShared() {
        const string html = "<meta charset='windows-1250'><p>Zażółć</p>";
        // The decoder registers code-page support on modern .NET.
        HtmlUtilities.DecodeHtmlResponse(Array.Empty<byte>(), "windows-1250");
        using var handler = new ResponseHandler(() => new ByteArrayContent(Encoding.GetEncoding(1250).GetBytes(html)));
        using var client = new HttpClient(handler);
        Assert.Equal(html, await ReadAsync("post", client, null));
        Assert.Equal(html, await ReadAsync("relay", client, null));
    }

    private sealed class ResponseHandler(Func<HttpContent> createContent) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = createContent(), RequestMessage = request });
    }

    [Fact]
    public async Task BrowserlessExtraction_CancelsStalledBodyAndPreservesTimeoutFailureResult() {
        foreach (bool callerCancellation in new[] { false, true }) {
            using var stream = new BlockingStream();
            using var handler = new ResponseHandler(() => new StreamContent(stream));
            using var client = new HttpClient(handler) { Timeout = callerCancellation ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(100) };
            using var cancellation = new CancellationTokenSource();
            var source = new HtmlBrowserlessDataSource {
                Kind = "ApiEndpoint", Method = "GET", PageUrl = Url,
                ResolvedUrl = Url, RequiresHttpFetch = true, CanExtractDirectly = true
            };
            Task<HtmlBrowserlessExtractionResult> reading = HtmlBrowserlessExtraction.ExtractAsync(source,
                new HtmlBrowserlessExtractionOptions { AllowHttpFetch = true }, client, cancellation.Token);
            Assert.Same(stream.Entered.Task, await Task.WhenAny(stream.Entered.Task, Task.Delay(2000)));
            if (callerCancellation) cancellation.Cancel();
            Assert.Same(reading, await Task.WhenAny(reading, Task.Delay(2000)));
            if (callerCancellation) {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading);
            } else {
                var result = await reading;
                Assert.False(result.Success);
                Assert.False(Assert.Single(result.Requests).Success);
            }
            Assert.True(stream.Disposed);
        }
    }

    [Fact]
    public async Task DownloadToFile_CancelsStalledBodyWithoutReplacingExistingFile() {
        string folder = Path.Combine(Path.GetTempPath(), "HtmlTinkerX-http-policy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "existing.html");
        File.WriteAllText(path, "original");
        try {
            using var stream = new BlockingStream();
            using var handler = new ResponseHandler(() => new StreamContent(stream));
            using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            using var cancellation = new CancellationTokenSource();
            Task downloading = HtmlUtilities.DownloadToFileAsync(client, new Uri(Url), path, null, cancellation.Token);
            Assert.Same(stream.Entered.Task, await Task.WhenAny(stream.Entered.Task, Task.Delay(2000)));
            cancellation.Cancel();
            Assert.Same(downloading, await Task.WhenAny(downloading, Task.Delay(2000)));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => downloading);
            Assert.True(stream.Disposed);
            Assert.Equal("original", File.ReadAllText(path));
            Assert.Equal(path, Assert.Single(Directory.GetFiles(folder)));
        } finally { Directory.Delete(folder, recursive: true); }
    }

#if !NETFRAMEWORK
    [Theory]
    [InlineData("url")]
    [InlineData("get")]
    [InlineData("post")]
    [InlineData("relay")]
    public async Task UnsupportedTransportEncoding_FallsBackToMetaOrUtf8(string consumer) {
        foreach (bool includeMeta in new[] { false, true }) {
            string html = (includeMeta ? "<meta charset='utf-8'>" : "") + "<p>Zażółć</p>";
            using var handler = new ResponseHandler(() => {
                var content = new ByteArrayContent(Encoding.UTF8.GetBytes(html));
                content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/html; charset=utf-7");
                return content;
            });
            using var client = new HttpClient(handler);
            Assert.Equal(html, await ReadAsync(consumer, client, null));
        }
    }
#endif

    private sealed class BlockingStream : Stream {
        internal TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<int> closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Disposed { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            Entered.TrySetResult(true);
            // Model Framework transports that cannot cancel an already-started read.
            return await closed.Task;
        }
        protected override void Dispose(bool disposing) {
            Disposed = true;
            closed.TrySetException(new ObjectDisposedException(nameof(BlockingStream)));
            base.Dispose(disposing);
        }
    }
}
