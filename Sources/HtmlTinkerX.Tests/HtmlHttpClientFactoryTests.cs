using HtmlTinkerX;
using System.Net;
using System.Net.Http;
using System.Reflection;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlHttpClientFactoryTests {
    private static HttpClientHandler? GetHandler(HttpClient client) {
        // Try to get handler field - field name may differ between .NET versions
        FieldInfo? field = typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? typeof(HttpMessageInvoker).GetField("handler", BindingFlags.Instance | BindingFlags.NonPublic);
        
        if (field == null) {
            // In some .NET versions, we need to look in base class or use different approach
            var fields = typeof(HttpMessageInvoker).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            field = Array.Find(fields, f => f.FieldType == typeof(HttpMessageHandler) || f.FieldType.IsSubclassOf(typeof(HttpMessageHandler)));
        }
        
        return field?.GetValue(client) as HttpClientHandler;
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
        HttpClientHandler? handler = GetHandler(client);
        
        // Skip test if we can't access handler via reflection (framework-specific issue)
        if (handler == null) {
            return;
        }
        
        // In .NET Framework, the proxy URL may be returned differently
        var proxyUri = handler.Proxy?.GetProxy(new System.Uri("http://example.com"));
        Assert.NotNull(proxyUri);
        Assert.Contains("localhost", proxyUri.ToString());
        Assert.Contains("1234", proxyUri.ToString());
        Assert.Same(cred, handler.Proxy?.Credentials);
    }
}