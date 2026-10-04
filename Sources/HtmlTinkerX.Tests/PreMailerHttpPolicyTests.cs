using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class PreMailerHttpPolicyTests {
    private const string Html = "<link rel='stylesheet' href='https://example.test/site.css'><p>Text</p>";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemoteCss_EnforcesDefaultStreamingLimitAndPreservesTheLink(bool asynchronous) {
        using var handler = new StylesheetHandler(new byte[HtmlHttpFetchOptions.DefaultMaximumResponseBytes + 1]);
        using var client = new HttpClient(handler);
        var options = new PreMailerOptions { DownloadRemoteCss = true, HttpClient = client };
        var result = await InlineAsync(options, asynchronous);
        Assert.Contains("href=\"https://example.test/site.css\"", result.Html);
        // Caller-owned clients remain usable after the optional download fails.
        using var response = await client.GetAsync("https://example.test/site.css", HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemoteCss_UsesTheExplicitByteLimit(bool asynchronous) {
        byte[] css = Encoding.UTF8.GetBytes("p { color: red; }");
        using var handler = new StylesheetHandler(css);
        using var client = new HttpClient(handler);
        var options = new PreMailerOptions {
            DownloadRemoteCss = true, HttpClient = client,
            FetchOptions = new HtmlHttpFetchOptions { MaximumResponseBytes = css.Length - 1 }
        };
        Assert.Contains("href=\"https://example.test/site.css\"", (await InlineAsync(options, asynchronous)).Html);
        options.FetchOptions.MaximumResponseBytes = css.Length;
        var result = await InlineAsync(options, asynchronous);
        Assert.Contains("color: red", result.Html);
        Assert.DoesNotContain("<link", result.Html);
    }

    private static Task<PreMailerResult> InlineAsync(PreMailerOptions options, bool asynchronous) {
        var inliner = PreMailerClient.FromHtml(Html, options);
        return asynchronous ? inliner.MoveCssInlineAsync() : Task.FromResult(inliner.MoveCssInline());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidResponsePolicy_IsNotSwallowedAsAnOptionalDownloadFailure(bool asynchronous) {
        var options = new PreMailerOptions { DownloadRemoteCss = true, FetchOptions = new HtmlHttpFetchOptions { MaximumResponseBytes = 0 } };
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => InlineAsync(options, asynchronous));
    }

    private sealed class StylesheetHandler(byte[] bytes) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StreamContent(new MemoryStream(bytes)), RequestMessage = request
            });
    }
}
