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

    private static int GetFreePort() {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static HttpListener StartCookieServer(out string url) {
        int port = GetFreePort();
        string prefix = $"http://localhost:{port}/";
        url = prefix;
        HttpListener listener = new();
        listener.Prefixes.Add(prefix);
        listener.Start();
        _ = Task.Run(async () => {
            var context = await listener.GetContextAsync();
            context.Response.Headers.Add("Set-Cookie", "session=abc");
            byte[] data = System.Text.Encoding.UTF8.GetBytes("ok");
            context.Response.ContentLength64 = data.Length;
            await context.Response.OutputStream.WriteAsync(data, 0, data.Length);
            context.Response.OutputStream.Close();
        });
        return listener;
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
    public async Task Shared_IsThreadSafe() {
        HtmlHttpClientFactory.ResetShared();
        const int count = 20;
        HttpClient?[] clients = new HttpClient?[count];
        Task[] tasks = new Task[count];
        TaskCompletionSource<bool> start = new();
        for (int i = 0; i < count; i++) {
            int index = i;
            tasks[i] = Task.Run(async () => {
                await start.Task;
                clients[index] = HtmlHttpClientFactory.Shared;
            });
        }
        start.SetResult(true);
        await Task.WhenAll(tasks);
        HttpClient? first = clients[0];
        Assert.NotNull(first);
        HttpClient firstClient = first!;
        foreach (HttpClient? client in clients) {
            Assert.NotNull(client);
            Assert.Same(firstClient, client!);
        }
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

    [Fact]
    public void Create_WithCookieContainer_ReturnsSameInstance() {
        using HttpClient client = HtmlHttpClientFactory.Create(out CookieContainer cookies);
        HttpClientHandler? handler = GetHandler(client);
        if (handler == null) {
            return;
        }
        Assert.Same(cookies, handler.CookieContainer);
    }

    [Fact]
    public async Task Create_WithCookieContainer_StoresCookies() {
        HttpListener server = StartCookieServer(out string url);
        try {
            using HttpClient client = HtmlHttpClientFactory.Create(out CookieContainer container);
            HttpResponseMessage response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            Cookie cookie = Assert.IsType<Cookie>(container.GetCookies(new System.Uri(url))["session"]);
            Assert.Equal("abc", cookie.Value);
        } finally {
            server.Stop();
            server.Close();
        }
    }
}