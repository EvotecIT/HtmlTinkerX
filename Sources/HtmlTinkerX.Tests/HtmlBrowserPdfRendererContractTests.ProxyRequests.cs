using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererContractTests {
    [Theory]
    [InlineData("GET\r\n\r\n", "400 Bad Request")]
    [InlineData("CONNECT ::: HTTP/1.1\r\n\r\n", "400 Bad CONNECT Target")]
    [InlineData("GET ftp://render.invalid/ HTTP/1.1\r\n\r\n", "400 Bad Proxy Target")]
    public async Task PolicyProxyRejectsMalformedRequests(string request, string status) {
        string response = await SendRawProxyRequestAsync(request);

        Assert.StartsWith("HTTP/1.1 " + status, response, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PolicyProxyRejectsHeadersOverTheLimit() {
        string response = await SendRawProxyRequestAsync(new string('x', 64 * 1024));

        Assert.StartsWith("HTTP/1.1 431 Request Header Fields Too Large", response, StringComparison.Ordinal);
    }

    private static async Task<string> SendRawProxyRequestAsync(string request) {
        await using HtmlBrowserPolicyProxy proxy = new(HtmlBrowserNetworkPolicy.Offline);
        using TcpClient client = new();
        await client.ConnectAsync(IPAddress.Loopback, new Uri(proxy.Server).Port);
        using NetworkStream stream = client.GetStream();
        byte[] payload = Encoding.ASCII.GetBytes(request);
        await stream.WriteAsync(payload, 0, payload.Length);

        using MemoryStream response = new();
        Task copy = stream.CopyToAsync(response);
        Assert.Same(copy, await Task.WhenAny(copy, Task.Delay(TimeSpan.FromSeconds(5))));
        await copy;
        return Encoding.ASCII.GetString(response.ToArray());
    }
}
