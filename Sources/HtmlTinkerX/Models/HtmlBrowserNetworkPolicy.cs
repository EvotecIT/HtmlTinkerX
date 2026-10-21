namespace HtmlTinkerX;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

/// <summary>
/// Immutable network and local-resource policy for PDF capture and explicitly restricted browser sessions.
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
        int blockedRequestDiagnosticLimit = 32,
        IEnumerable<string>? nat64Prefixes = null)
        : this(
            allowPrivateNetworks,
            allowFileAccess,
            allowUriCredentials,
            allowedHosts,
            deniedHosts,
            allowedFileDirectories,
            blockedRequestDiagnosticLimit,
            nat64Prefixes,
            allowNetworkAccess: true) { }

    /// <summary>Creates a host-restricted policy without treating those hosts as private-network exceptions.</summary>
    /// <param name="strictDestinationHosts">Complete destination host allowlist, including necessary subresource hosts.</param>
    /// <param name="requireHttps">Require HTTPS (or WSS where a caller supports WebSockets).</param>
    /// <param name="allowPrivateNetworks">Explicit private-network opt-in, intended for trusted intranet or isolated test fixtures.</param>
    public HtmlBrowserNetworkPolicy(
        IEnumerable<string> strictDestinationHosts,
        bool requireHttps = true,
        bool allowPrivateNetworks = false)
        : this(allowPrivateNetworks, false, false, null, null, null, 32, null, true) {
        if (strictDestinationHosts == null) throw new ArgumentNullException(nameof(strictDestinationHosts));
        StrictDestinationHosts = SnapshotHosts(strictDestinationHosts);
        if (StrictDestinationHosts.Count == 0) throw new ArgumentException("At least one destination host is required.", nameof(strictDestinationHosts));
        foreach (string pattern in StrictDestinationHosts) {
            string host = pattern.StartsWith("*.", StringComparison.Ordinal) ? pattern.Substring(2) : pattern;
            if (Uri.CheckHostName(host) == UriHostNameType.Unknown) {
                throw new ArgumentException("Destination hosts must be DNS names or IP addresses, without a scheme, port, or path.", nameof(strictDestinationHosts));
            }
        }
        RequireHttps = requireHttps;
    }

    private HtmlBrowserNetworkPolicy(
        bool allowPrivateNetworks,
        bool allowFileAccess,
        bool allowUriCredentials,
        IEnumerable<string>? allowedHosts,
        IEnumerable<string>? deniedHosts,
        IEnumerable<string>? allowedFileDirectories,
        int blockedRequestDiagnosticLimit,
        IEnumerable<string>? nat64Prefixes,
        bool allowNetworkAccess) {
        if (blockedRequestDiagnosticLimit < 0) {
            throw new ArgumentOutOfRangeException(nameof(blockedRequestDiagnosticLimit));
        }

        AllowPrivateNetworks = allowPrivateNetworks;
        AllowNetworkAccess = allowNetworkAccess;
        AllowFileAccess = allowFileAccess;
        AllowUriCredentials = allowUriCredentials;
        AllowedHosts = SnapshotHosts(allowedHosts);
        DeniedHosts = SnapshotHosts(deniedHosts);
        AllowedFileDirectories = Array.AsReadOnly((allowedFileDirectories ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(Path.GetFullPath)
            .Distinct(PathComparer)
            .ToArray());
        HtmlBrowserNat64Prefix[] parsedNat64Prefixes = (nat64Prefixes ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(HtmlBrowserNat64Prefix.Parse)
            .Distinct()
            .OrderByDescending(prefix => prefix.PrefixLength)
            .ToArray();
        Nat64Prefixes = Array.AsReadOnly(parsedNat64Prefixes.Select(prefix => prefix.ToString()).ToArray());
        ParsedNat64Prefixes = Array.AsReadOnly(parsedNat64Prefixes);
        BlockedRequestDiagnosticLimit = blockedRequestDiagnosticLimit;
    }

    /// <summary>Gets a public-network-only policy suitable for service boundaries.</summary>
    public static HtmlBrowserNetworkPolicy PublicNetworkOnly { get; } = new();

    /// <summary>Gets a policy that blocks every HTTP(S) and WebSocket request while retaining in-memory sources.</summary>
    public static HtmlBrowserNetworkPolicy Offline { get; } = new(
        allowPrivateNetworks: false,
        allowFileAccess: false,
        allowUriCredentials: false,
        allowedHosts: null,
        deniedHosts: null,
        allowedFileDirectories: null,
        blockedRequestDiagnosticLimit: 32,
        nat64Prefixes: null,
        allowNetworkAccess: false);

    /// <summary>Creates a policy that permits private network targets while retaining URI/file checks.</summary>
    public static HtmlBrowserNetworkPolicy CreatePrivateNetworkAllowed() => new(allowPrivateNetworks: true);

    /// <summary>Gets whether private, loopback, link-local, and otherwise non-public IP addresses are allowed.</summary>
    public bool AllowPrivateNetworks { get; }
    /// <summary>Gets whether HTTP(S) and WebSocket requests are allowed at all.</summary>
    public bool AllowNetworkAccess { get; }
    /// <summary>Gets whether local file resources outside the explicitly selected input file directory are allowed.</summary>
    public bool AllowFileAccess { get; }
    /// <summary>Gets whether user information embedded in a URI is allowed.</summary>
    public bool AllowUriCredentials { get; }
    /// <summary>Gets hosts explicitly allowed, including private hosts.</summary>
    public IReadOnlyList<string> AllowedHosts { get; }
    /// <summary>Gets the strict destination host allowlist, or <see langword="null"/> when no host restriction was selected. These hosts never grant private-network access.</summary>
    public IReadOnlyList<string>? StrictDestinationHosts { get; }
    /// <summary>Gets whether plain HTTP and WS destinations are denied.</summary>
    public bool RequireHttps { get; }
    /// <summary>Gets hosts explicitly denied.</summary>
    public IReadOnlyList<string> DeniedHosts { get; }
    /// <summary>Gets local directories from which file resources may be loaded.</summary>
    public IReadOnlyList<string> AllowedFileDirectories { get; }
    /// <summary>Gets configured RFC 6052 network-specific NAT64 prefixes used to classify embedded IPv4 destinations.</summary>
    public IReadOnlyList<string> Nat64Prefixes { get; }
    /// <summary>Gets the maximum number of blocked resource URLs retained in diagnostics.</summary>
    public int BlockedRequestDiagnosticLimit { get; }

    internal IReadOnlyList<HtmlBrowserNat64Prefix> ParsedNat64Prefixes { get; }

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
