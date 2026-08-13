using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererLiveTests {
    [Fact]
    public async Task ManagedPolicyProxyBlocksWebRtcUdpBypass() {
        using (UdpClient unrestrictedListener = new(new IPEndPoint(IPAddress.Loopback, 0))) {
            int port = ((IPEndPoint)unrestrictedListener.Client.LocalEndPoint!).Port;
            Task<UdpReceiveResult> received = unrestrictedListener.ReceiveAsync();
            await using HtmlBrowserPdfRenderer unrestricted = new(new HtmlBrowserPdfRendererOptions(
                maximumBrowserInstances: 1,
                networkPolicy: HtmlBrowserNetworkPolicy.CreatePrivateNetworkAllowed()));

            await unrestricted.CaptureAsync(CreateWebRtcProbeRequest(port));

            Assert.Same(received, await Task.WhenAny(received, Task.Delay(TimeSpan.FromSeconds(5))));
            await received;
        }

        using UdpClient protectedListener = new(new IPEndPoint(IPAddress.Loopback, 0));
        int protectedPort = ((IPEndPoint)protectedListener.Client.LocalEndPoint!).Port;
        Task<UdpReceiveResult> blocked = protectedListener.ReceiveAsync();
        await using HtmlBrowserPdfRenderer protectedRenderer = new(new HtmlBrowserPdfRendererOptions(maximumBrowserInstances: 1));

        await protectedRenderer.CaptureAsync(CreateWebRtcProbeRequest(protectedPort));

        Assert.NotSame(blocked, await Task.WhenAny(blocked, Task.Delay(TimeSpan.FromSeconds(1))));
        protectedListener.Dispose();
        try { await blocked; } catch (ObjectDisposedException) { } catch (SocketException) { }
    }

    private static HtmlBrowserPdfRequest CreateWebRtcProbeRequest(int port) {
        string html = $"<html><body data-ice='pending'><p>WebRTC probe</p><script>(async()=>{{const pc=new RTCPeerConnection({{iceServers:[{{urls:'stun:127.0.0.1:{port}'}}]}});window.__probePeer=pc;pc.createDataChannel('probe');await pc.setLocalDescription(await pc.createOffer());document.body.dataset.ice='started';}})();</script></body></html>";
        return new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml(html),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.body.dataset.ice === 'started'",
                delayMilliseconds: 1000,
                timeout: 10000));
    }
}
