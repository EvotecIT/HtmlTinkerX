namespace HtmlTinkerX;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

/// <summary>
/// Classifies addresses for the public-network-only browser boundary.
/// </summary>
internal static class HtmlBrowserNetworkAddressClassifier {
    /// <summary>
    /// Returns whether an address is globally reachable rather than private or special-purpose.
    /// </summary>
    internal static bool IsGloballyReachable(
        IPAddress address,
        IReadOnlyList<HtmlBrowserNat64Prefix>? nat64Prefixes = null) {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)
            || address.Equals(IPAddress.Any)
            || address.Equals(IPAddress.IPv6Any)
            || address.Equals(IPAddress.None)
            || address.Equals(IPAddress.IPv6None)) return false;

        byte[] bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork) {
            return IsGloballyReachableIpv4(bytes);
        }
        if (address.AddressFamily == AddressFamily.InterNetworkV6) {
            return IsGloballyReachableIpv6(address, bytes, nat64Prefixes);
        }
        return false;
    }

    private static bool IsGloballyReachableIpv4(byte[] bytes, int offset = 0) {
        byte first = bytes[offset];
        byte second = bytes[offset + 1];
        if (first == 0 || first == 10 || first == 127 || first >= 224) return false;
        if (first == 100 && second >= 64 && second <= 127) return false;
        if (first == 169 && second == 254) return false;
        if (first == 172 && second >= 16 && second <= 31) return false;
        if (first == 192 && second == 168) return false;
        if (first == 192 && second == 0 && bytes[offset + 2] == 0) {
            return bytes[offset + 3] == 9 || bytes[offset + 3] == 10;
        }
        if (first == 192 && second == 0 && bytes[offset + 2] == 2) return false;
        if (first == 192 && second == 88 && bytes[offset + 2] == 99) return false;
        if (first == 198 && (second == 18 || second == 19)) return false;
        if (first == 198 && second == 51 && bytes[offset + 2] == 100) return false;
        if (first == 203 && second == 0 && bytes[offset + 2] == 113) return false;
        return true;
    }

    private static bool IsGloballyReachableIpv6(
        IPAddress address,
        byte[] bytes,
        IReadOnlyList<HtmlBrowserNat64Prefix>? nat64Prefixes) {
        if (address.IsIPv6LinkLocal || address.IsIPv6Multicast || address.IsIPv6SiteLocal) return false;
        if (IsWellKnownIpv4Ipv6Translation(bytes)) {
            return IsGloballyReachableIpv4(bytes, 12); // 64:ff9b::/96 embeds IPv4 in the final 32 bits.
        }
        if (nat64Prefixes != null) {
            foreach (HtmlBrowserNat64Prefix prefix in nat64Prefixes) {
                if (prefix.TryExtract(address, out byte[] embeddedIpv4)) {
                    return IsGloballyReachableIpv4(embeddedIpv4);
                }
            }
        }
        if ((bytes[0] & 0xE0) != 0x20) return false; // Global unicast allocation is 2000::/3.

        // IANA special-purpose ranges whose destination is not globally reachable.
        if (bytes[0] == 0x20 && bytes[1] == 0x01 && (bytes[2] & 0xFE) == 0) {
            return IsGloballyReachableIetfAssignment(bytes); // 2001::/23 and public exceptions
        }
        if (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8) return false;
        if (bytes[0] == 0x20 && bytes[1] == 0x02) return false;
        if (bytes[0] == 0x3F && bytes[1] == 0xFF && (bytes[2] & 0xF0) == 0) return false;
        if (bytes[0] == 0x5F && bytes[1] == 0x00) return false;
        return true;
    }

    private static bool IsWellKnownIpv4Ipv6Translation(byte[] bytes) {
        if (bytes[0] != 0 || bytes[1] != 0x64 || bytes[2] != 0xFF || bytes[3] != 0x9B) return false;
        for (int index = 4; index < 12; index++) {
            if (bytes[index] != 0) return false;
        }
        return true;
    }

    private static bool IsGloballyReachableIetfAssignment(byte[] bytes) {
        // The parent 2001::/23 allocation is not globally reachable. Only entries that
        // the IANA registry explicitly marks globally reachable are admitted here.
        if (bytes[2] == 0 && bytes[3] == 1 && IsProtocolAnycastAddress(bytes)) return true;
        if (bytes[2] == 0 && bytes[3] == 3) return true; // 2001:3::/32
        if (bytes[2] == 0 && bytes[3] == 4 && bytes[4] == 1 && bytes[5] == 0x12) return true; // 2001:4:112::/48
        if (bytes[2] == 0 && ((bytes[3] & 0xF0) == 0x20 || (bytes[3] & 0xF0) == 0x30)) return true; // 2001:20::/28, 2001:30::/28
        return false;
    }

    private static bool IsProtocolAnycastAddress(byte[] bytes) {
        for (int index = 4; index < 15; index++) {
            if (bytes[index] != 0) return false;
        }
        return bytes[15] >= 1 && bytes[15] <= 3;
    }
}

internal readonly struct HtmlBrowserNat64Prefix : IEquatable<HtmlBrowserNat64Prefix> {
    private static readonly int[] SupportedPrefixLengths = { 32, 40, 48, 56, 64, 96 };
    private readonly byte[] _networkBytes;

    private HtmlBrowserNat64Prefix(byte[] networkBytes, int prefixLength) {
        _networkBytes = networkBytes;
        PrefixLength = prefixLength;
    }

    internal int PrefixLength { get; }

    internal static HtmlBrowserNat64Prefix Parse(string value) {
        string[] parts = value.Trim().Split('/');
        if (parts.Length != 2
            || !IPAddress.TryParse(parts[0], out IPAddress? address)
            || address.AddressFamily != AddressFamily.InterNetworkV6
            || !int.TryParse(parts[1], out int prefixLength)
            || !SupportedPrefixLengths.Contains(prefixLength)) {
            throw new ArgumentException($"NAT64 prefix '{value}' must be an IPv6 RFC 6052 prefix with length 32, 40, 48, 56, 64, or 96.", nameof(value));
        }

        byte[] bytes = address.GetAddressBytes();
        int prefixBytes = prefixLength / 8;
        if (bytes.Skip(prefixBytes).Any(current => current != 0)) {
            throw new ArgumentException($"NAT64 prefix '{value}' contains host bits outside /{prefixLength}.", nameof(value));
        }
        return new HtmlBrowserNat64Prefix(bytes, prefixLength);
    }

    internal bool TryExtract(IPAddress address, out byte[] embeddedIpv4) {
        embeddedIpv4 = Array.Empty<byte>();
        if (address.AddressFamily != AddressFamily.InterNetworkV6) return false;
        byte[] bytes = address.GetAddressBytes();
        int prefixBytes = PrefixLength / 8;
        for (int index = 0; index < prefixBytes; index++) {
            if (bytes[index] != _networkBytes[index]) return false;
        }

        embeddedIpv4 = new byte[4];
        if (PrefixLength == 96) {
            Buffer.BlockCopy(bytes, 12, embeddedIpv4, 0, 4);
            return true;
        }

        if (bytes[8] != 0) return false; // RFC 6052 reserves the u octet.
        int leadingIpv4Bytes = (64 - PrefixLength) / 8;
        Buffer.BlockCopy(bytes, prefixBytes, embeddedIpv4, 0, leadingIpv4Bytes);
        Buffer.BlockCopy(bytes, 9, embeddedIpv4, leadingIpv4Bytes, 4 - leadingIpv4Bytes);
        return true;
    }

    public bool Equals(HtmlBrowserNat64Prefix other) =>
        PrefixLength == other.PrefixLength && _networkBytes.SequenceEqual(other._networkBytes);

    public override bool Equals(object? obj) => obj is HtmlBrowserNat64Prefix other && Equals(other);

    public override int GetHashCode() {
        unchecked {
            int hash = PrefixLength;
            for (int index = 0; index < _networkBytes.Length; index++) hash = (hash * 397) ^ _networkBytes[index];
            return hash;
        }
    }

    public override string ToString() => new IPAddress(_networkBytes) + "/" + PrefixLength;
}
