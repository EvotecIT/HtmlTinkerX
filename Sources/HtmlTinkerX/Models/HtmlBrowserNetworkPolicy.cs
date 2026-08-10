namespace HtmlTinkerX;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

/// <summary>
/// Immutable network and local-resource policy enforced for every request in a PDF capture context.
/// </summary>
public sealed class HtmlBrowserNetworkPolicy {
    /// <summary>Initializes a browser resource policy.</summary>
    public HtmlBrowserNetworkPolicy(
        bool allowPrivateNetworks = false,
        bool allowFileAccess = false,
        bool allowUriCredentials = false,
        IEnumerable<string>? allowedHosts = null,
        IEnumerable<string>? deniedHosts = null,
        IEnumerable<string>? allowedFileDirectories = null,
        int blockedRequestDiagnosticLimit = 32) {
        if (blockedRequestDiagnosticLimit < 0) {
            throw new ArgumentOutOfRangeException(nameof(blockedRequestDiagnosticLimit));
        }

        AllowPrivateNetworks = allowPrivateNetworks;
        AllowFileAccess = allowFileAccess;
        AllowUriCredentials = allowUriCredentials;
        AllowedHosts = SnapshotHosts(allowedHosts);
        DeniedHosts = SnapshotHosts(deniedHosts);
        AllowedFileDirectories = Array.AsReadOnly((allowedFileDirectories ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(Path.GetFullPath)
            .Distinct(PathComparer)
            .ToArray());
        BlockedRequestDiagnosticLimit = blockedRequestDiagnosticLimit;
    }

    /// <summary>Gets a public-network-only policy suitable for service boundaries.</summary>
    public static HtmlBrowserNetworkPolicy PublicNetworkOnly { get; } = new();

    /// <summary>Creates a policy that permits private network targets while retaining URI/file checks.</summary>
    public static HtmlBrowserNetworkPolicy CreatePrivateNetworkAllowed() => new(allowPrivateNetworks: true);

    /// <summary>Gets whether private, loopback, link-local, and otherwise non-public IP addresses are allowed.</summary>
    public bool AllowPrivateNetworks { get; }
    /// <summary>Gets whether local file resources outside the explicitly selected input file directory are allowed.</summary>
    public bool AllowFileAccess { get; }
    /// <summary>Gets whether user information embedded in a URI is allowed.</summary>
    public bool AllowUriCredentials { get; }
    /// <summary>Gets hosts explicitly allowed, including private hosts.</summary>
    public IReadOnlyList<string> AllowedHosts { get; }
    /// <summary>Gets hosts explicitly denied.</summary>
    public IReadOnlyList<string> DeniedHosts { get; }
    /// <summary>Gets local directories from which file resources may be loaded.</summary>
    public IReadOnlyList<string> AllowedFileDirectories { get; }
    /// <summary>Gets the maximum number of blocked resource URLs retained in diagnostics.</summary>
    public int BlockedRequestDiagnosticLimit { get; }

    internal static StringComparer PathComparer =>
        Environment.OSVersion.Platform == PlatformID.Win32NT ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static IReadOnlyList<string> SnapshotHosts(IEnumerable<string>? hosts) =>
        Array.AsReadOnly((hosts ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(NormalizeHostPattern)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray());

    private static string NormalizeHostPattern(string value) {
        string pattern = value.Trim().TrimEnd('.');
        bool wildcard = pattern.StartsWith("*.", StringComparison.Ordinal);
        string host = wildcard ? pattern.Substring(2) : pattern;
        if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host patterns cannot be empty.", nameof(value));
        string normalized;
        if (System.Net.IPAddress.TryParse(host, out System.Net.IPAddress? address)) {
            normalized = address.ToString();
        } else {
            try {
                normalized = new IdnMapping().GetAscii(host).ToLowerInvariant();
            } catch (ArgumentException exception) {
                throw new ArgumentException($"Host pattern '{value}' is not a valid DNS name.", nameof(value), exception);
            }
        }
        return wildcard ? "*." + normalized : normalized;
    }
}
