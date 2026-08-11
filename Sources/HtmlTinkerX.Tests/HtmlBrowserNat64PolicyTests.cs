using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed class HtmlBrowserNat64PolicyTests {
    [Theory]
    [InlineData("2001:4860::/32")]
    [InlineData("2001:4860:ab00::/40")]
    [InlineData("2001:4860:abcd::/48")]
    [InlineData("2001:4860:abcd:ef00::/56")]
    [InlineData("2001:4860:abcd:ef01::/64")]
    [InlineData("2001:4860:abcd:ef01:2345:6789::/96")]
    public async Task ConfiguredPrefixesClassifyEmbeddedIpv4Destinations(string prefix) {
        IPAddress privateDestination = CreateNat64Address(prefix, IPAddress.Parse("10.0.0.1"));
        IPAddress publicDestination = CreateNat64Address(prefix, IPAddress.Parse("8.8.8.8"));
        HtmlBrowserNetworkPolicy policy = new(nat64Prefixes: new[] { prefix });

        HtmlBrowserNetworkPolicyEvaluator privateEvaluator = new(policy, _ => Task.FromResult(new[] { privateDestination }));
        HtmlBrowserNetworkPolicyEvaluator publicEvaluator = new(policy, _ => Task.FromResult(new[] { publicDestination }));

        Assert.False(await privateEvaluator.IsAllowedAsync("https://translated.example/report", null, CancellationToken.None));
        Assert.True(await publicEvaluator.IsAllowedAsync("https://translated.example/report", null, CancellationToken.None));
        Assert.Equal(prefix, Assert.Single(policy.Nat64Prefixes));
    }

    [Theory]
    [InlineData("2001:4860::/36")]
    [InlineData("2001:4860::1/96")]
    [InlineData("10.0.0.0/8")]
    public void InvalidPrefixesAreRejected(string prefix) {
        Assert.Throws<ArgumentException>(() => new HtmlBrowserNetworkPolicy(nat64Prefixes: new[] { prefix }));
    }

    private static IPAddress CreateNat64Address(string prefix, IPAddress ipv4) {
        string[] parts = prefix.Split('/');
        byte[] bytes = IPAddress.Parse(parts[0]).GetAddressBytes();
        int prefixLength = int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
        byte[] embedded = ipv4.GetAddressBytes();
        if (prefixLength == 96) {
            Buffer.BlockCopy(embedded, 0, bytes, 12, 4);
        } else {
            int prefixBytes = prefixLength / 8;
            int leadingIpv4Bytes = (64 - prefixLength) / 8;
            Buffer.BlockCopy(embedded, 0, bytes, prefixBytes, leadingIpv4Bytes);
            bytes[8] = 0;
            Buffer.BlockCopy(embedded, leadingIpv4Bytes, bytes, 9, 4 - leadingIpv4Bytes);
        }
        return new IPAddress(bytes);
    }
}
