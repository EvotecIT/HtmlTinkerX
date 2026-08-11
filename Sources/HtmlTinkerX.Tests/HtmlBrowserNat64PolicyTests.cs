using System;
using System.Net;
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
    public void ConfiguredPrefixesClassifyEmbeddedIpv4Destinations(string prefix) {
        IPAddress privateDestination = CreateNat64Address(prefix, IPAddress.Parse("10.0.0.1"));
        IPAddress publicDestination = CreateNat64Address(prefix, IPAddress.Parse("8.8.8.8"));
        HtmlBrowserNetworkPolicy policy = new(nat64Prefixes: new[] { prefix });

        Assert.False(HtmlBrowserNetworkAddressClassifier.IsGloballyReachable(privateDestination, policy.ParsedNat64Prefixes));
        Assert.True(HtmlBrowserNetworkAddressClassifier.IsGloballyReachable(publicDestination, policy.ParsedNat64Prefixes));
        Assert.Equal(prefix, Assert.Single(policy.Nat64Prefixes));
    }

    [Theory]
    [InlineData("2001:4860::/36")]
    [InlineData("2001:4860::1/96")]
    [InlineData("10.0.0.0/8")]
    public void InvalidPrefixesAreRejected(string prefix) {
        Assert.Throws<ArgumentException>(() => new HtmlBrowserNetworkPolicy(nat64Prefixes: new[] { prefix }));
    }

    [Fact]
    public void MostSpecificConfiguredPrefixOwnsOverlappingNat64Address() {
        const string broadPrefix = "2001:4860::/32";
        // The broad /32 decodes 8.8.8.8 from this address, while the overlapping
        // /96 decodes the actual private destination supplied below.
        const string specificPrefix = "2001:4860:808:808::/96";
        IPAddress destination = CreateNat64Address(specificPrefix, IPAddress.Parse("10.0.0.1"));
        HtmlBrowserNetworkPolicy policy = new(nat64Prefixes: new[] { broadPrefix, specificPrefix });

        Assert.False(HtmlBrowserNetworkAddressClassifier.IsGloballyReachable(destination, policy.ParsedNat64Prefixes));
        Assert.Equal(new[] { specificPrefix, broadPrefix }, policy.Nat64Prefixes);
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
