using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed class HtmlBrowserSessionNetworkPolicyTests {
    [Fact]
    public async Task NavigationSessionNullOptionsUseThePublicArgumentContract() {
        ArgumentNullException error = await Assert.ThrowsAsync<ArgumentNullException>(() => HtmlBrowser.OpenSessionForNavigationAsync(null!));
        Assert.Equal("options", error.ParamName);
    }

    [Theory]
    [InlineData("https://source.example/data", "93.184.215.14", true)]
    [InlineData("https://cdn.source.example/data", "93.184.215.14", true)]
    [InlineData("https://other.example/data", "93.184.215.14", false)]
    [InlineData("http://source.example/data", "93.184.215.14", false)]
    [InlineData("https://source.example/data", "127.0.0.1", false)]
    [InlineData("https://source.example/data", "169.254.169.254", false)]
    [InlineData("https://source.example/data", "::1", false)]
    [InlineData("https://user:password@source.example/data", "93.184.215.14", false)]
    public async Task StrictHostsRestrictDestinationsWithoutGrantingPrivateNetworkExceptions(string url, string address, bool expected) {
        HtmlBrowserNetworkPolicy policy = new(new[] { "source.example", "*.source.example" });
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(policy, _ => Task.FromResult(new[] { IPAddress.Parse(address) }));

        Assert.Equal(expected, await evaluator.IsAllowedAsync(url, null, CancellationToken.None));
        Assert.Empty(policy.AllowedHosts);
    }

    [Fact]
    public async Task ExistingAllowedHostExceptionsAndConstructorRemainCompatible() {
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "fixture.example" });
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(policy, _ => Task.FromResult(new[] { IPAddress.Loopback }));

        Assert.Null(policy.StrictDestinationHosts);
        Assert.False(policy.RequireHttps);
        Assert.True(await evaluator.IsAllowedAsync("http://fixture.example/data", null, CancellationToken.None));
        Assert.NotNull(typeof(HtmlBrowserNetworkPolicy).GetConstructor(new[] {
            typeof(bool), typeof(bool), typeof(bool), typeof(System.Collections.Generic.IEnumerable<string>),
            typeof(System.Collections.Generic.IEnumerable<string>), typeof(System.Collections.Generic.IEnumerable<string>),
            typeof(int), typeof(System.Collections.Generic.IEnumerable<string>)
        }));
    }

    [Theory]
    [InlineData("firefox")]
    [InlineData("persistent")]
    [InlineData("cdp")]
    [InlineData("proxy")]
    [InlineData("arguments")]
    [InlineData("tls")]
    public async Task UnsupportedRestrictedLaunchModesFailBeforeLaunching(string mode) {
        HtmlBrowserLaunchOptions options = new() { NetworkPolicy = HtmlBrowserNetworkPolicy.PublicNetworkOnly };
        switch (mode) {
            case "firefox": options.Browser = HtmlBrowserEngine.Firefox; break;
            case "persistent": options.UserDataDirectory = "unused-profile"; break;
            case "cdp": options.CdpEndpointUrl = "http://127.0.0.1:9222"; break;
            case "proxy": options.Proxy = "http://proxy.example:8080"; break;
            case "arguments": options.BrowserArguments.Add("--proxy-server=direct://"); break;
            case "tls":
                options.NetworkPolicy = new HtmlBrowserNetworkPolicy(new[] { "source.example" });
                options.IgnoreHTTPSErrors = true;
                break;
        }
        HtmlBrowserSession? unexpected = null;
        try {
            await Assert.ThrowsAsync<ArgumentException>(async () => unexpected = await HtmlBrowser.OpenSessionForNavigationAsync(options));
        } finally {
            if (unexpected != null) await unexpected.DisposeAsync();
        }
    }

    [Theory]
    [InlineData("http://source.example/")]
    [InlineData("https://127.0.0.1/")]
    [InlineData("file:///C:/Windows/win.ini")]
    public async Task InitialDestinationsFailBeforeLaunching(string url) {
        HtmlBrowserLaunchOptions options = new() {
            NetworkPolicy = new HtmlBrowserNetworkPolicy(new[] { "source.example", "127.0.0.1" })
        };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => HtmlBrowser.OpenSessionAsync(url, options));
    }

    [Fact]
    public void EmptyStrictHostListsCannotBecomeUnrestricted() {
        Assert.Throws<ArgumentException>(() => new HtmlBrowserNetworkPolicy(Array.Empty<string>()));
        Assert.Throws<ArgumentException>(() => new HtmlBrowserNetworkPolicy(new[] { "https://source.example/path" }));
    }
}
