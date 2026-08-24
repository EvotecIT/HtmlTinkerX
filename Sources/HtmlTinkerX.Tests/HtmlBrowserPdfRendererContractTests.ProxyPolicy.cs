using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererContractTests {
    [Fact]
    public void ReleasedPolicyAndRendererOptionConstructorsRemainAvailable() {
        Assert.NotNull(typeof(HtmlBrowserNetworkPolicy).GetConstructor(new[] {
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(IEnumerable<string>),
            typeof(IEnumerable<string>),
            typeof(IEnumerable<string>),
            typeof(int),
            typeof(IEnumerable<string>)
        }));
        Assert.NotNull(typeof(HtmlBrowserPdfRendererOptions).GetConstructor(new[] {
            typeof(HtmlBrowserEngine),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(TimeSpan?),
            typeof(bool),
            typeof(bool),
            typeof(string),
            typeof(string),
            typeof(IEnumerable<string>),
            typeof(bool?),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(int?),
            typeof(int?),
            typeof(HtmlBrowserNetworkPolicy),
            typeof(TimeSpan?)
        }));
    }

    [Fact]
    public void OfflinePolicyAlwaysOwnsTheBrowserNetworkBoundary() {
        HtmlBrowserPdfRendererOptions options = new(networkPolicy: HtmlBrowserNetworkPolicy.Offline);

        Assert.False(options.NetworkPolicy.AllowNetworkAccess);
        Assert.False(options.NetworkPolicy.AllowPrivateNetworks);
        Assert.True(options.RequiresManagedPolicyProxy);
        Assert.False(options.ProxyOwnsNetworkResolution);
        Assert.Contains("--force-webrtc-ip-handling-policy=disable_non_proxied_udp", options.CreateLaunchOptions().BrowserArguments);
        Assert.Contains("--disable-quic", options.CreateLaunchOptions().BrowserArguments);
        Assert.Throws<ArgumentException>(() => new HtmlBrowserPdfRenderer(new HtmlBrowserPdfRendererOptions(
            proxy: "http://proxy.example:8080",
            networkPolicy: HtmlBrowserNetworkPolicy.Offline)));
    }

    [Fact]
    public async Task LocallyFulfilledHtmlStillRejectsUriCredentialsByDefault() {
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            networkPolicy: HtmlBrowserNetworkPolicy.Offline));
        HtmlBrowserPdfRequest request = new(HtmlBrowserPdfSource.FromHtml(
            "<p>credential boundary</p>",
            new Uri("https://user:secret@offline.example/report")));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => renderer.CaptureAsync(request));
    }

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
