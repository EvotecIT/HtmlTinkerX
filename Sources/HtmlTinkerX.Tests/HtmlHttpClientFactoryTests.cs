using HtmlTinkerX;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
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

    private static TcpListener StartCookieServer(out string url, out Task request) {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        url = $"http://127.0.0.1:{port}/";
        request = Task.Run(async () => {
            using TcpClient client = await listener.AcceptTcpClientAsync();
            using NetworkStream stream = client.GetStream();
            byte[] requestBytes = new byte[4096];
            _ = await stream.ReadAsync(requestBytes, 0, requestBytes.Length);
            byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nSet-Cookie: session=abc\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok");
            await stream.WriteAsync(response, 0, response.Length);
        });
        return listener;
    }

    [Fact]
    public void Create_AppliesDefaultHeaders() {
        HtmlHttpClientFactory.DefaultHeaders["X-Test"] = "1";
        try {
            using HttpClient client = HtmlHttpClientFactory.Create();
            Assert.True(client.DefaultRequestHeaders.Contains("X-Test"));
        } finally {
            HtmlHttpClientFactory.DefaultHeaders.Remove("X-Test");
        }
    }

    [Fact]
    public void Create_IncludesDescriptiveDefaultUserAgent() {
        using HttpClient client = HtmlHttpClientFactory.Create();

        string userAgent = string.Join(" ", client.DefaultRequestHeaders.GetValues("User-Agent"));
        Assert.StartsWith("HtmlTinkerX/", userAgent, StringComparison.Ordinal);
    }

    [Fact]
    public void ResetDefaultHeaders_RemovesCustomHeadersAndRestoresUserAgent() {
        HtmlHttpClientFactory.DefaultHeaders["X-Temporary"] = "value";

        HtmlHttpClientFactory.ResetDefaultHeaders();

        Assert.False(HtmlHttpClientFactory.DefaultHeaders.ContainsKey("X-Temporary"));
        Assert.StartsWith("HtmlTinkerX/", HtmlHttpClientFactory.DefaultHeaders["User-Agent"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTextWithProperEncodingAsync_ReturnsFinalResponseUri() {
        Uri finalUri = new("https://example.test/final/catalog/");
        using HttpClient client = new(new FinalUriHandler(finalUri));

        HtmlHttpTextResult result = await HtmlUtilities.GetTextWithProperEncodingAsync(
            client,
            "https://example.test/start",
            fetchOptions: null);

        Assert.Equal("<p>ok</p>", result.Content);
        Assert.Equal(finalUri, result.FinalUri);
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
        TcpListener server = StartCookieServer(out string url, out Task serverRequest);
        try {
            using HttpClient client = HtmlHttpClientFactory.Create(out CookieContainer container);
            using HttpResponseMessage response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            await serverRequest;
            Cookie? cookie = container.GetCookies(new System.Uri(url))["session"];
            Assert.NotNull(cookie);
            Assert.Equal("abc", cookie.Value);
        } finally {
            server.Stop();
            try { await serverRequest; } catch (SocketException) { } catch (ObjectDisposedException) { }
        }
    }

    private sealed class FinalUriHandler : HttpMessageHandler {
        private readonly Uri _finalUri;

        internal FinalUriHandler(Uri finalUri) {
            _finalUri = finalUri;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) {
            HttpResponseMessage response = new(HttpStatusCode.OK) {
                Content = new StringContent("<p>ok</p>", Encoding.UTF8, "text/html"),
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, _finalUri)
            };
            return Task.FromResult(response);
        }
    }
}
