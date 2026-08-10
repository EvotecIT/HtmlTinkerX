using AngleSharp;
using AngleSharp.Io;
using AngleSharp.Io.Network;
using AngleSharp.Js;
using HtmlTinkerX;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlScriptRunnerTests {
    [Fact]
    public async Task RunAsync_ReturnsResult() {
        const string html = "<html></html>";
        const string script = "1 + 2";
        int? result = await HtmlScriptRunner.RunAsync<int>(html, script);
        Assert.Equal(3, result);
    }

    [Fact]
    public async Task RunAsync_PreservesNavigatorWithoutExposingNetworkCapableBrowserApis() {
        const string script = "[typeof navigator, typeof XMLHttpRequest, typeof fetch, typeof WebSocket].join('|')";
        string? result = await HtmlScriptRunner.RunAsync<string>("<html></html>", script);
        Assert.Equal("object|undefined|undefined|undefined", result);
    }

    [Fact]
    public async Task RunAsync_WithExplicitIoConfiguration_InvokesConfiguredXmlHttpRequestTransport() {
        using var handler = new StubHttpMessageHandler();
        using var httpClient = new HttpClient(handler, disposeHandler: false);
        var configuration = Configuration.Default
            .With(new HttpClientRequester(httpClient))
            .WithDefaultLoader()
            .WithJs();
        using var context = BrowsingContext.New(configuration);
        const string script = "var request = new XMLHttpRequest(); request.open('GET', 'https://example.test/data', false); request.send(); typeof XMLHttpRequest;";

        IRequester[] requesters = configuration.Services.OfType<IRequester>().ToArray();
        string? result = await HtmlScriptRunner.RunAsync<string>("<html></html>", script, context);

        Assert.Collection(requesters, requester => Assert.IsType<HttpClientRequester>(requester));
        Assert.Equal(new Uri("https://example.test/data"), handler.LastRequestUri);
        Assert.Equal("function", result);
    }

    [Fact]
    public async Task RunAsync_NullHtml_Throws() {
        await Assert.ThrowsAsync<ArgumentNullException>(() => HtmlScriptRunner.RunAsync<int>(null!, "1"));
    }

    [Fact]
    public async Task RunAsync_NullScript_Throws() {
        await Assert.ThrowsAsync<ArgumentNullException>(() => HtmlScriptRunner.RunAsync<int>("<html></html>", null!));
    }

    [Fact]
    public async Task RunAsync_ContextWithoutJavaScript_Throws() {
        using var context = BrowsingContext.New(Configuration.Default);
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            HtmlScriptRunner.RunAsync<int>("<html></html>", "1", context));

        Assert.Equal("context", exception.ParamName);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("configured response")
            });
        }
    }
}
