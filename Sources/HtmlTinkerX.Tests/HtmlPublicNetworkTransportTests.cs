#if NET8_0_OR_GREATER
using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed class HtmlPublicNetworkTransportTests {
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("localhost")]
    public async Task LocalDestinationsAreRefusedWithoutConnecting(string host) {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        Task<TcpClient> contacted = listener.AcceptTcpClientAsync(deadline.Token).AsTask();
        using HttpClient client = new(HtmlPublicNetworkTransport.Create());

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAsync($"http://{host}:{((IPEndPoint)listener.LocalEndpoint).Port}/license", deadline.Token));

        Assert.False(contacted.IsCompleted);
    }

    [Theory]
    [InlineData("10.0.0.5")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("fd00::1")]
    [InlineData("::ffff:192.168.1.1")]
    public async Task PrivateAndSpecialPurposeLiteralsAreRefused(string literal) =>
        await Assert.ThrowsAsync<HttpRequestException>(() => HtmlPublicNetworkTransport.PublicAddressesAsync(literal, [], NoResolver, default));

    [Fact]
    public async Task ANameIsRefusedWhenAnyAddressIsPrivateAndKeptWhenAllArePublic() {
        await Assert.ThrowsAsync<HttpRequestException>(() => HtmlPublicNetworkTransport.PublicAddressesAsync("mixed.example", [],
            (_, _) => Task.FromResult(new[] { IPAddress.Parse("93.184.216.34"), IPAddress.Parse("192.168.0.10") }), default));

        var addresses = await HtmlPublicNetworkTransport.PublicAddressesAsync("public.example", [],
            (_, _) => Task.FromResult(new[] { IPAddress.Parse("93.184.216.34"), IPAddress.Parse("2606:2800:220:1::1") }), default);

        Assert.Equal(2, addresses.Count);
    }

    [Theory]
    [InlineData("64:ff9b::a00:1")]
    [InlineData("2001:4860:64::a9fe:a9fe")]
    public async Task Nat64AddressesAreCheckedByTheIpv4AddressTheyEmbed(string literal) {
        HtmlBrowserNat64Prefix[] configured = [HtmlBrowserNat64Prefix.Parse("2001:4860:64::/96")];

        await Assert.ThrowsAsync<HttpRequestException>(() => HtmlPublicNetworkTransport.PublicAddressesAsync(literal, configured, NoResolver, default));
    }

    [Fact]
    public async Task ARequestThatRequiresHttp3IsRefusedBecauseQuicSkipsTheAddressCheck() {
        using HttpClient client = new(HtmlPublicNetworkTransport.Create());
        using HttpRequestMessage request = new(HttpMethod.Get, "https://public.example/license") {
            Version = HttpVersion.Version30, VersionPolicy = HttpVersionPolicy.RequestVersionExact
        };

        await Assert.ThrowsAsync<NotSupportedException>(() => client.SendAsync(request));
    }

    [Fact]
    public void ConnectTimeoutOutsideItsRangeIsRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => HtmlPublicNetworkTransport.Create(connectTimeoutSeconds: 0));

    private static Task<IPAddress[]> NoResolver(string host, CancellationToken token) =>
        throw new InvalidOperationException("Literals are not resolved.");
}
#endif
