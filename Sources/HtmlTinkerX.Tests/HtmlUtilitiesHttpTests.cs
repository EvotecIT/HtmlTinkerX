using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX.Tests;

public class HtmlUtilitiesHttpTests {
    [Fact]
    public async Task ReadResponseContent_RejectsDeclaredContentLengthBeforeReading() {
        using HttpResponseMessage response = new(HttpStatusCode.OK) {
            Content = new ByteArrayContent(new byte[11])
        };

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            HtmlUtilities.ReadResponseContentWithProperEncodingAsync(
                response,
                new HtmlHttpFetchOptions { MaximumResponseBytes = 10 }));

        Assert.Contains("10-byte limit", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadResponseContent_EnforcesLimitWhileStreamingUnknownLength() {
        using HttpResponseMessage response = new(HttpStatusCode.OK) {
            Content = new StreamContent(new MemoryStream(new byte[11]))
        };

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            HtmlUtilities.ReadResponseContentWithProperEncodingAsync(
                response,
                new HtmlHttpFetchOptions { MaximumResponseBytes = 10 }));

        Assert.Contains("supplied 11 bytes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadResponseContent_CancelsDuringBodyRead() {
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(100));
        using HttpResponseMessage response = new(HttpStatusCode.OK) {
            Content = new StreamContent(new BlockingReadStream())
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            HtmlUtilities.ReadResponseContentWithProperEncodingAsync(
                response,
                new HtmlHttpFetchOptions { MaximumResponseBytes = 10 },
                cancellation.Token));
    }

    [Fact]
    public async Task GetStringWithProperEncoding_UsesCallerLimitAndCancellationContract() {
        using HttpClient client = new(new ResponseHandler(new byte[11]));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            HtmlUtilities.GetStringWithProperEncodingAsync(
                client,
                "https://example.test/large",
                new HtmlHttpFetchOptions { MaximumResponseBytes = 10 }));
    }

    [Fact]
    public async Task PublicUrlParser_ForwardsCallerFetchLimit() {
        using HttpClient client = new(new ResponseHandler(new byte[11]));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            HtmlParser.ParseUrlMetaTagsAsync(
                "https://example.test/large",
                client,
                new HtmlHttpFetchOptions { MaximumResponseBytes = 10 }));
    }

    private sealed class ResponseHandler : HttpMessageHandler {
        private readonly byte[] _content;

        public ResponseHandler(byte[] content) => _content = content;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new ByteArrayContent(_content),
                RequestMessage = request
            });
    }

    private sealed class BlockingReadStream : Stream {
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
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            return 0;
        }
    }
}
