using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlExtractionReliabilityTests {
    [Theory]
    [InlineData(250)]
    [InlineData(251)]
    [InlineData(600)]
    public void DiscoverCollections_ReturnsAllRecordsBeyondInferenceSample(int count) {
        string html = "<main>" + string.Concat(Enumerable.Range(0, count).Select(index =>
            $"<article class='card'><h2>Product {index}</h2><a href='/p/{index}'>Details</a><span class='price'>10 EUR</span></article>")) + "</main>";
        HtmlPageCollection collection = Assert.Single(HtmlDomExtraction.DiscoverCollections(html,
            baseUri: new Uri("https://example.org/")));
        Assert.Equal(count, collection.Items.Count);
        Assert.Contains(collection.Items[count - 1].Values.Values, value => Equals(value, $"https://example.org/p/{count - 1}"));
    }

    [Fact]
    public async Task SubmitGet_PreservesEncodedSpacesPlusSignsAndRepeatedQueryFields() {
        Uri? captured = null;
        using HttpClient client = new(new Handler(request => {
            captured = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
        }));
        await HtmlFormSubmitter.SubmitAsync("https://example.org/?q=hello+world&q=%2B&a+b=c%2Bd",
            FormMethod.Get, new Dictionary<string, string> { ["new"] = "x y+z" }, client);
        Assert.Equal("?q=hello+world&q=%2B&a+b=c%2Bd&new=x+y%2Bz", captured!.Query);
    }

    [Fact]
    public async Task ExtractEndpoint_StreamsOnlyLimitAndRejectsTruncatedJson() {
        using CountingStream stream = new(new byte[2 * 1024 * 1024]);
        using HttpClient client = new(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StreamingContent(stream)
        }));
        HtmlBrowserlessExtractionResult result = await HtmlBrowserlessExtraction.ExtractAsync(Endpoint(),
            new HtmlBrowserlessExtractionOptions { AllowHttpFetch = true, IncludeRawContent = true, MaxResponseBytes = 1024 }, client);
        Assert.Equal(1025, stream.BytesRead);
        Assert.Equal(1024, Encoding.UTF8.GetByteCount(result.RawContent));
        Assert.False(result.Success);
        Assert.False(Assert.Single(result.Requests).Success);
        Assert.Empty(result.Items);
        Assert.Contains(result.Warnings, warning => warning.Contains("truncated"));
    }

    [Fact]
    public async Task ExtractEndpoint_IntMaxLimitDoesNotOverflow() {
        using HttpClient client = new(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("[{\"name\":\"Product\"}]", Encoding.UTF8, "application/json")
        }));
        HtmlBrowserlessExtractionResult result = await HtmlBrowserlessExtraction.ExtractAsync(Endpoint(),
            new HtmlBrowserlessExtractionOptions { AllowHttpFetch = true, MaxResponseBytes = int.MaxValue }, client);
        Assert.True(result.Success);
        Assert.Single(result.Items);
    }

    private static HtmlBrowserlessDataSource Endpoint() => new() {
        Kind = "ApiEndpoint", Method = "GET", PageUrl = "https://example.org/",
        ResolvedUrl = "https://example.org/api", RequiresHttpFetch = true, CanExtractDirectly = true
    };

    [Fact]
    public async Task ExtractEndpoint_BodyReadHonorsClientTimeoutAndCallerCancellation() {
        using HttpClient client = new(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StreamingContent(new WaitingStream())
        })) { Timeout = TimeSpan.FromMilliseconds(50) };
        var options = new HtmlBrowserlessExtractionOptions { AllowHttpFetch = true };
        HtmlBrowserlessExtractionResult timeout = await HtmlBrowserlessExtraction.ExtractAsync(Endpoint(), options, client);
        Assert.False(timeout.Success);
        Assert.NotEmpty(Assert.Single(timeout.Requests).Error);

        using HttpClient callerClient = new(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StreamingContent(new WaitingStream())
        })) { Timeout = Timeout.InfiniteTimeSpan };
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            HtmlBrowserlessExtraction.ExtractAsync(Endpoint(), options, callerClient, cancellation.Token));
    }

    private sealed class WaitingStream : MemoryStream {
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            try { await Task.Delay(Timeout.Infinite, cancellationToken); } catch (OperationCanceledException) { }
            cancellationToken.ThrowIfCancellationRequested();
            return 0;
        }
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private sealed class CountingStream(byte[] bytes) : MemoryStream(bytes) {
        public int BytesRead { get; private set; }
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            int read = await base.ReadAsync(buffer, offset, count, cancellationToken);
            BytesRead += read;
            return read;
        }
    }

    private sealed class StreamingContent(Stream stream) : HttpContent {
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult(stream);
        protected override Task SerializeToStreamAsync(Stream target, TransportContext? context) =>
            throw new InvalidOperationException("The response must not be buffered before the bounded reader runs.");
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }
}
