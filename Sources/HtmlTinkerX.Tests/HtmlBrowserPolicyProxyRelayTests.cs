using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed class HtmlBrowserPolicyProxyRelayTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResetTunnelPeerClosesTheOtherConnection(bool resetBrowser) {
        TcpListener origin = new(IPAddress.Loopback, 0);
        origin.Start();
        try {
            int originPort = ((IPEndPoint)origin.LocalEndpoint).Port;
            HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "render.invalid" });
            HtmlBrowserNetworkPolicyEvaluator evaluator = new(policy, _ => Task.FromResult(new[] { IPAddress.Loopback }));
            await using HtmlBrowserPolicyProxy proxy = new(evaluator);
            using TcpClient browser = new();
            await browser.ConnectAsync(IPAddress.Loopback, new Uri(proxy.Server).Port);
            using NetworkStream browserStream = browser.GetStream();
            byte[] connect = Encoding.ASCII.GetBytes($"CONNECT render.invalid:{originPort} HTTP/1.1\r\nHost: render.invalid:{originPort}\r\n\r\n");
            await browserStream.WriteAsync(connect, 0, connect.Length);
            using TcpClient remote = await origin.AcceptTcpClientAsync();
            using NetworkStream remoteStream = remote.GetStream();
            Assert.Equal("HTTP/1.1 200 Connection Established\r\n\r\n", await ReadConnectResponseAsync(browserStream));

            // Observe both relay directions before resetting either socket.
            byte[] payload = { 42 };
            await browserStream.WriteAsync(payload, 0, payload.Length);
            Assert.Equal(1, await remoteStream.ReadAsync(payload, 0, payload.Length));
            Assert.Equal(42, payload[0]);
            payload[0] = 43;
            await remoteStream.WriteAsync(payload, 0, payload.Length);
            Assert.Equal(1, await browserStream.ReadAsync(payload, 0, payload.Length));
            Assert.Equal(43, payload[0]);

            TcpClient failingPeer = resetBrowser ? browser : remote;
            Stream survivingPeer = resetBrowser ? remoteStream : browserStream;
            Stream failingStream = resetBrowser ? browserStream : remoteStream;
            // Closing a peer with unread tunnel data is an abort, rather than a half-close.
            byte[] interruptedTransfer = new byte[4096];
            await survivingPeer.WriteAsync(interruptedTransfer, 0, interruptedTransfer.Length);
            Assert.Equal(1, await failingStream.ReadAsync(interruptedTransfer, 0, 1));
            failingPeer.Client.LingerState = new LingerOption(true, 0);
            failingPeer.Close();

            Task closed = AssertConnectionClosedAsync(survivingPeer);
            Assert.Same(closed, await Task.WhenAny(closed, Task.Delay(TimeSpan.FromSeconds(2))));
            await closed;
        } finally {
            origin.Stop();
        }
    }

    private static async Task<string> ReadConnectResponseAsync(Stream stream) {
        StringBuilder response = new();
        byte[] next = new byte[1];
        while (!response.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal)) {
            Assert.Equal(1, await stream.ReadAsync(next, 0, next.Length));
            response.Append((char)next[0]);
            Assert.True(response.Length < 128);
        }
        return response.ToString();
    }

    private static async Task AssertConnectionClosedAsync(Stream stream) {
        try {
            Assert.Equal(0, await stream.ReadAsync(new byte[1], 0, 1));
        } catch (IOException) {
            // A reset and an orderly close both terminate the surviving connection.
        }
    }
}
