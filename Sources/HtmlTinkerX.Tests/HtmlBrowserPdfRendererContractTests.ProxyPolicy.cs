using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererContractTests {
    [Fact]
    public void PublicNetworkEnforcementRejectsCallerProxyWhoseDnsCannotBeBound() {
        Assert.Throws<ArgumentException>(() => new HtmlBrowserPdfRenderer(
            new HtmlBrowserPdfRendererOptions(proxy: "http://proxy.example:8080")));
    }

    [Fact]
    public async Task TrustedCallerProxyCanResolveHostsUnavailableToTheRenderer() {
        int resolverCalls = 0;
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(
            new HtmlBrowserNetworkPolicy(allowPrivateNetworks: true),
            _ => {
                Interlocked.Increment(ref resolverCalls);
                throw new SocketException((int)SocketError.HostNotFound);
            });

        bool allowed = await evaluator.IsAllowedAsync(
            "http://renderer.proxy-only.invalid/report",
            selectedFileDirectory: null,
            deferNetworkResolutionToProxy: true,
            CancellationToken.None);

        Assert.True(allowed);
        Assert.Equal(0, resolverCalls);
    }

    [Fact]
    public void HostRulesRejectCallerProxyBecauseWebSocketTunnelsCannotBeEnforced() {
        HtmlBrowserNetworkPolicy policy = new(
            allowPrivateNetworks: true,
            deniedHosts: new[] { "internal.example" });

        Assert.Throws<ArgumentException>(() => new HtmlBrowserPdfRenderer(new HtmlBrowserPdfRendererOptions(
            proxy: "http://proxy.example:8080",
            networkPolicy: policy)));
    }

    [Fact]
    public void BrowserSessionsAndPooledRendererValidateHttpsByDefault() {
        Assert.False(new HtmlBrowserLaunchOptions().IgnoreHTTPSErrors);
        Assert.False(new HtmlBrowserPdfRendererOptions().IgnoreHttpsErrors);
    }
}
