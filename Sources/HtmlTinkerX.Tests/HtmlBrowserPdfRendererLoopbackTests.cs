using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererLiveTests {
    [Fact]
    public async Task PopupFixture_PreconnectedClientCanCloseWithoutSendingRequest() {
        await using LoopbackPopupServer server = new();
        Uri address = new(server.HeaderUrl);
        using TcpClient client = new();
        await client.ConnectAsync(address.Host, address.Port);
        using NetworkStream stream = client.GetStream();
        client.Client.Shutdown(SocketShutdown.Send);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        byte[] buffer = new byte[1];

        Assert.Equal(0, await stream.ReadAsync(buffer, 0, buffer.Length, timeout.Token));
        // Disposing the fixture also observes its client tasks, including the closed connection.
    }
}
