namespace HtmlTinkerX;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

internal sealed class HtmlBrowserNetworkPolicyEvaluator {
    private static readonly TimeSpan DefaultDnsCacheDuration = TimeSpan.FromSeconds(30);
    private readonly HtmlBrowserNetworkPolicy _policy;
    private readonly Func<string, Task<IPAddress[]>> _resolveHost;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly TimeSpan _dnsCacheDuration;
    private readonly ConcurrentDictionary<string, DnsCacheEntry> _dns = new(StringComparer.OrdinalIgnoreCase);

    internal HtmlBrowserNetworkPolicyEvaluator(
        HtmlBrowserNetworkPolicy policy,
        Func<string, Task<IPAddress[]>>? resolveHost = null,
        TimeSpan? dnsCacheDuration = null,
        Func<DateTimeOffset>? utcNow = null) {
        _policy = policy;
        _resolveHost = resolveHost ?? Dns.GetHostAddressesAsync;
        _dnsCacheDuration = dnsCacheDuration ?? DefaultDnsCacheDuration;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    internal async Task<bool> IsAllowedAsync(string url, string? selectedFileDirectory, CancellationToken cancellationToken) {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)) return false;

        switch (uri.Scheme.ToLowerInvariant()) {
            case "about":
            case "data":
            case "blob":
                return true;
            case "file":
                return IsFileAllowed(uri.LocalPath, selectedFileDirectory);
            case "http":
            case "https":
            case "ws":
            case "wss":
                return await IsNetworkUriAllowedAsync(uri, cancellationToken).ConfigureAwait(false);
            default:
                return false;
        }
    }

    private async Task<bool> IsNetworkUriAllowedAsync(Uri uri, CancellationToken cancellationToken) =>
        (await ResolveAllowedAddressesAsync(uri, cancellationToken).ConfigureAwait(false)).Length > 0;

    internal async Task<IPAddress[]> ResolveAllowedAddressesAsync(Uri uri, CancellationToken cancellationToken) {
        if (!_policy.AllowUriCredentials && !string.IsNullOrEmpty(uri.UserInfo)) return Array.Empty<IPAddress>();

        string host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
        if (MatchesHost(host, _policy.DeniedHosts)) return Array.Empty<IPAddress>();
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)) {
            if (!MatchesHost(host, _policy.AllowedHosts) && !_policy.AllowPrivateNetworks) return Array.Empty<IPAddress>();
        }

        IPAddress[] addresses;
        DnsCacheEntry? entry = null;
        if (IPAddress.TryParse(host, out IPAddress? literal)) {
            addresses = new[] { literal };
        } else {
            try {
                entry = GetOrRefreshDnsEntry(host);
                addresses = await WaitAsync(entry.Lookup, cancellationToken).ConfigureAwait(false);
            } catch (SocketException) {
                if (entry != null) RemoveDnsEntry(host, entry);
                return Array.Empty<IPAddress>();
            }
        }

        if (addresses.Length == 0) return Array.Empty<IPAddress>();
        if (MatchesHost(host, _policy.AllowedHosts) || _policy.AllowPrivateNetworks) return addresses;
        return addresses.All(IsPublicAddress) ? addresses : Array.Empty<IPAddress>();
    }

    private DnsCacheEntry GetOrRefreshDnsEntry(string host) {
        while (true) {
            DateTimeOffset now = _utcNow();
            if (_dns.TryGetValue(host, out DnsCacheEntry? current) && current.ExpiresAt > now) return current;

            DnsCacheEntry replacement = new(() => _resolveHost(host), now.Add(_dnsCacheDuration));
            if (current == null) {
                if (_dns.TryAdd(host, replacement)) return replacement;
            } else if (_dns.TryUpdate(host, replacement, current)) {
                return replacement;
            }
        }
    }

    private void RemoveDnsEntry(string host, DnsCacheEntry entry) =>
        ((ICollection<KeyValuePair<string, DnsCacheEntry>>)_dns).Remove(new KeyValuePair<string, DnsCacheEntry>(host, entry));

    private bool IsFileAllowed(string path, string? selectedFileDirectory) {
        if (!HtmlBrowserFileSystemPath.TryResolveExistingPath(path, out string fullPath)) return false;

        if (!string.IsNullOrWhiteSpace(selectedFileDirectory)
            && HtmlBrowserFileSystemPath.TryResolveExistingPath(selectedFileDirectory!, out string selectedRoot)
            && IsWithinDirectory(fullPath, selectedRoot)) return true;
        if (!_policy.AllowFileAccess) return false;
        return _policy.AllowedFileDirectories.Count == 0
            || _policy.AllowedFileDirectories.Any(directory => HtmlBrowserFileSystemPath.TryResolveExistingPath(directory, out string allowedRoot) && IsWithinDirectory(fullPath, allowedRoot));
    }

    private static bool MatchesHost(string host, IReadOnlyList<string> patterns) {
        foreach (string pattern in patterns) {
            if (pattern.StartsWith("*.", StringComparison.Ordinal)) {
                string suffix = pattern.Substring(1);
                if (host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && host.Length > suffix.Length) return true;
            } else if (string.Equals(host, pattern, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }
        return false;
    }

    private static bool IsWithinDirectory(string path, string directory) {
        string fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string fullPath = Path.GetFullPath(path);
        if (HtmlBrowserNetworkPolicy.PathComparer.Equals(fullPath, fullDirectory)) return true;
        string prefix = fullDirectory + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(prefix, HtmlBrowserNetworkPolicy.PathComparer == StringComparer.OrdinalIgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static bool IsPublicAddress(IPAddress address) {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.None) || address.Equals(IPAddress.IPv6None)) return false;

        byte[] bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork) {
            byte first = bytes[0];
            byte second = bytes[1];
            if (first == 0 || first == 10 || first == 127 || first >= 224) return false;
            if (first == 100 && second >= 64 && second <= 127) return false;
            if (first == 169 && second == 254) return false;
            if (first == 172 && second >= 16 && second <= 31) return false;
            if (first == 192 && second == 168) return false;
            if (first == 192 && second == 0 && bytes[2] == 0) return false;
            if (first == 192 && second == 0 && bytes[2] == 2) return false;
            if (first == 192 && second == 88 && bytes[2] == 99) return false;
            if (first == 198 && (second == 18 || second == 19)) return false;
            if (first == 198 && second == 51 && bytes[2] == 100) return false;
            if (first == 203 && second == 0 && bytes[2] == 113) return false;
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6) {
            if (address.IsIPv6LinkLocal || address.IsIPv6Multicast || address.IsIPv6SiteLocal) return false;
            if ((bytes[0] & 0xFE) == 0xFC) return false;
            if (bytes[0] == 0x01 && bytes.Take(8).Skip(1).All(value => value == 0)) return false;
            if (bytes[0] == 0x20 && bytes[1] == 0x01) {
                if (bytes[2] == 0x00 && bytes[3] == 0x00) return false;
                if (bytes[2] == 0x00 && bytes[3] == 0x02) return false;
                if (bytes[2] == 0x0D && bytes[3] == 0xB8) return false;
                if (bytes[2] == 0 && ((bytes[3] & 0xF0) == 0x10 || (bytes[3] & 0xF0) == 0x20)) return false;
            }
            if (bytes[0] == 0x20 && bytes[1] == 0x02) return false;
            if (bytes[0] == 0x00 && bytes[1] == 0x64 && bytes[2] == 0xFF && bytes[3] == 0x9B && bytes[4] == 0 && bytes[5] == 1) return false;
            return true;
        }

        return false;
    }

    private static async Task<T> WaitAsync<T>(Task<T> task, CancellationToken cancellationToken) {
        if (!cancellationToken.CanBeCanceled || task.IsCompleted) return await task.ConfigureAwait(false);
        TaskCompletionSource<bool> cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = cancellationToken.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), cancelled);
        if (await Task.WhenAny(task, cancelled.Task).ConfigureAwait(false) != task) cancellationToken.ThrowIfCancellationRequested();
        return await task.ConfigureAwait(false);
    }

    private sealed class DnsCacheEntry {
        private readonly Lazy<Task<IPAddress[]>> _lookup;

        internal DnsCacheEntry(Func<Task<IPAddress[]>> lookup, DateTimeOffset expiresAt) {
            _lookup = new Lazy<Task<IPAddress[]>>(lookup, LazyThreadSafetyMode.ExecutionAndPublication);
            ExpiresAt = expiresAt;
        }

        internal Task<IPAddress[]> Lookup => _lookup.Value;
        internal DateTimeOffset ExpiresAt { get; }
    }
}
