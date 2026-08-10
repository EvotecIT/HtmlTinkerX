namespace HtmlTinkerX;

using System.Net;
using System.Net.Sockets;

/// <summary>
/// Classifies addresses for the public-network-only browser boundary.
/// </summary>
internal static class HtmlBrowserNetworkAddressClassifier {
    /// <summary>
    /// Returns whether an address is globally reachable rather than private or special-purpose.
    /// </summary>
    internal static bool IsGloballyReachable(IPAddress address) {
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
            return IsGloballyReachableIpv6(address, bytes);
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

    private static bool IsGloballyReachableIpv6(IPAddress address, byte[] bytes) {
        if (address.IsIPv6LinkLocal || address.IsIPv6Multicast || address.IsIPv6SiteLocal) return false;
        if (IsWellKnownIpv4Ipv6Translation(bytes)) {
            return IsGloballyReachableIpv4(bytes, 12); // 64:ff9b::/96 embeds IPv4 in the final 32 bits.
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
