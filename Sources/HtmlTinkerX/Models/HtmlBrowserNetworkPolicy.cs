namespace HtmlTinkerX;

using System;
using System.Collections.Generic;
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
            .Select(value => value.Trim().TrimEnd('.').ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray());
}
