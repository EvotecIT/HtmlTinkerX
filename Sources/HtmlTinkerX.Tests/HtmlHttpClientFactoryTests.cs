using HtmlTinkerX;
using System.Net;
using System.Net.Http;
using System.Reflection;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlHttpClientFactoryTests {
    private static HttpClientHandler GetHandler(HttpClient client) {
        FieldInfo? field = typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic);
        return (HttpClientHandler)field!.GetValue(client)!;
    }

    [Fact]
    public void Create_AppliesDefaultHeaders() {
        HtmlHttpClientFactory.DefaultHeaders["X-Test"] = "1";
        using HttpClient client = HtmlHttpClientFactory.Create();
        Assert.True(client.DefaultRequestHeaders.Contains("X-Test"));
        HtmlHttpClientFactory.DefaultHeaders.Clear();
    }

    [Fact]
    public void Shared_ReturnsSameInstance() {
        HtmlHttpClientFactory.ResetShared();
        HttpClient first = HtmlHttpClientFactory.Shared;
        HttpClient second = HtmlHttpClientFactory.Shared;
        Assert.Same(first, second);
    }

    [Fact]
    public void ResetShared_RecreatesInstance() {
        HttpClient first = HtmlHttpClientFactory.Shared;
        HtmlHttpClientFactory.ResetShared();
        HttpClient second = HtmlHttpClientFactory.Shared;
        Assert.NotSame(first, second);
    }

    [Fact]
    public void Create_WithProxy_ConfiguresProxy() {
        var cred = new NetworkCredential("u", "p");
        using HttpClient client = HtmlHttpClientFactory.Create("http://localhost:1234", cred);
        HttpClientHandler handler = GetHandler(client);
        Assert.Equal("http://localhost:1234/", handler.Proxy?.GetProxy(new System.Uri("http://localhost")).ToString());
        Assert.Same(cred, handler.Proxy?.Credentials);
    }
}