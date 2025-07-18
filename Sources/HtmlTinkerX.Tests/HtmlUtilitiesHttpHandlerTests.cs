using HtmlTinkerX;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlUtilitiesHttpHandlerTests {
    private sealed class StaticResponseHandler : HttpMessageHandler {
        private readonly byte[] _bytes;
        private readonly string _contentType;

        public StaticResponseHandler(string content, string contentType, string encoding) {
            _bytes = System.Text.Encoding.GetEncoding(encoding).GetBytes(content);
            _contentType = contentType;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            HttpResponseMessage response = new(HttpStatusCode.OK) {
                Content = new ByteArrayContent(_bytes)
            };
            response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(_contentType);
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task GetStringWithProperEncodingAsync_UsesHeaderEncoding() {
        using HttpClient client = new(new StaticResponseHandler("Hello", "text/plain; charset=utf-16", "utf-16"), false);
        string result = await HtmlUtilities.GetStringWithProperEncodingAsync(client, "http://localhost/");
        Assert.Equal("Hello", result);
    }

    [Fact]
    public async Task GetStringWithProperEncodingAsync_TrimsQuotedCharset() {
        using HttpClient client = new(new StaticResponseHandler("Hello", "text/plain; charset=\"utf-8\"", "utf-8"), false);
        string result = await HtmlUtilities.GetStringWithProperEncodingAsync(client, "http://localhost/");
        Assert.Equal("Hello", result);
    }

    [Fact]
    public async Task GetStringWithProperEncodingAsync_CanBeCancelled() {
        using HttpClient client = new(new StaticResponseHandler("Hello", "text/plain", "utf-8"), false);
        using CancellationTokenSource cts = new();
        cts.Cancel();
        await Assert.ThrowsAsync<TaskCanceledException>(() => HtmlUtilities.GetStringWithProperEncodingAsync(client, "http://localhost/", cts.Token));
    }
}