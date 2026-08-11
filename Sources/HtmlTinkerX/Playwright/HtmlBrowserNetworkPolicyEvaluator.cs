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
    private const int MaximumConcurrentDnsLookups = 32;
    private const int DefaultMaximumDnsCacheEntries = 1024;
    private static readonly TimeSpan DefaultDnsCacheDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultDnsLookupTimeout = TimeSpan.FromSeconds(5);
    private static readonly SemaphoreSlim DnsLookupGate = new(MaximumConcurrentDnsLookups, MaximumConcurrentDnsLookups);
    private readonly HtmlBrowserNetworkPolicy _policy;
    private readonly Func<string, Task<IPAddress[]>> _resolveHost;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly TimeSpan _dnsCacheDuration;
    private readonly TimeSpan _dnsLookupTimeout;
    private readonly int _maximumDnsCacheEntries;
    private readonly ConcurrentDictionary<string, DnsCacheEntry> _dns = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _dnsSync = new();

    internal HtmlBrowserNetworkPolicyEvaluator(
        HtmlBrowserNetworkPolicy policy,
        Func<string, Task<IPAddress[]>>? resolveHost = null,
        TimeSpan? dnsCacheDuration = null,
        Func<DateTimeOffset>? utcNow = null,
        TimeSpan? dnsLookupTimeout = null,
        int maximumDnsCacheEntries = DefaultMaximumDnsCacheEntries) {
        if (maximumDnsCacheEntries <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDnsCacheEntries));
        _policy = policy;
        _resolveHost = resolveHost ?? Dns.GetHostAddressesAsync;
        _dnsCacheDuration = dnsCacheDuration ?? DefaultDnsCacheDuration;
        _dnsLookupTimeout = dnsLookupTimeout ?? DefaultDnsLookupTimeout;
        _maximumDnsCacheEntries = maximumDnsCacheEntries;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    internal int DnsCacheEntryCount => _dns.Count;

    internal Task<bool> IsAllowedAsync(string url, string? selectedFileDirectory, CancellationToken cancellationToken) =>
        IsAllowedAsync(url, selectedFileDirectory, deferNetworkResolutionToProxy: false, cancellationToken);

    internal async Task<bool> IsAllowedAsync(
        string url,
        string? selectedFileDirectory,
        bool deferNetworkResolutionToProxy,
        CancellationToken cancellationToken) {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)) return false;

        switch (uri.Scheme.ToLowerInvariant()) {
            case "about":
            case "data":
            case "blob":
                return true;
            case "file":
                if (uri.IsUnc
                    || (!string.IsNullOrEmpty(uri.Host)
                        && !string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))) return false;
                return IsFileAllowed(uri.LocalPath, selectedFileDirectory);
            case "http":
            case "https":
            case "ws":
            case "wss":
                if (deferNetworkResolutionToProxy) return IsNetworkUriAllowedByTrustedProxy(uri);
                return await IsNetworkUriAllowedAsync(uri, cancellationToken).ConfigureAwait(false);
            default:
                return false;
        }
    }

    private bool IsNetworkUriAllowedByTrustedProxy(Uri uri) {
        if (!_policy.AllowPrivateNetworks
            || _policy.AllowedHosts.Count > 0
            || _policy.DeniedHosts.Count > 0) return false;
        return _policy.AllowUriCredentials || string.IsNullOrEmpty(uri.UserInfo);
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
            using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(_dnsLookupTimeout);
            try {
                entry = GetOrRefreshDnsEntry(host);
                addresses = await WaitAsync(entry.Lookup, deadline.Token).ConfigureAwait(false);
            } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested) {
                // Retain an unfinished lookup so later requests share the same globally bounded
                // work instead of consuming another permit while the resolver is still running.
                return Array.Empty<IPAddress>();
            } catch (SocketException) {
                if (entry != null) RemoveDnsEntry(host, entry);
                return Array.Empty<IPAddress>();
            } catch (DnsLookupCapacityException) {
                if (entry != null) RemoveDnsEntry(host, entry);
                return Array.Empty<IPAddress>();
            }
        }

        if (addresses.Length == 0) return Array.Empty<IPAddress>();
        if (MatchesHost(host, _policy.AllowedHosts) || _policy.AllowPrivateNetworks) return addresses;
        return addresses.All(address => HtmlBrowserNetworkAddressClassifier.IsGloballyReachable(address, _policy.ParsedNat64Prefixes))
            ? addresses
            : Array.Empty<IPAddress>();
    }

    private DnsCacheEntry GetOrRefreshDnsEntry(string host) {
        lock (_dnsSync) {
            DateTimeOffset now = _utcNow();
            if (_dns.TryGetValue(host, out DnsCacheEntry? current)
                && (!current.IsCompleted || current.ExpiresAt > now)) return current;

            DnsCacheEntry replacement = new(() => ResolveHostBoundedAsync(host), now.Add(_dnsCacheDuration));
            if (current != null) {
                _dns[host] = replacement;
                return replacement;
            }

            EvictCompletedDnsEntries(now, onlyExpired: true);
            if (_dns.Count >= _maximumDnsCacheEntries) EvictCompletedDnsEntries(now, onlyExpired: false, maximumToRemove: 1);
            if (_dns.Count >= _maximumDnsCacheEntries) throw new DnsLookupCapacityException();
            _dns[host] = replacement;
            return replacement;
        }
    }

    private void EvictCompletedDnsEntries(DateTimeOffset now, bool onlyExpired, int maximumToRemove = int.MaxValue) {
        IEnumerable<KeyValuePair<string, DnsCacheEntry>> candidates = _dns
            .Where(pair => pair.Value.IsCompleted && (!onlyExpired || pair.Value.ExpiresAt <= now))
            .OrderBy(pair => pair.Value.ExpiresAt)
            .Take(maximumToRemove)
            .ToArray();
        foreach (KeyValuePair<string, DnsCacheEntry> candidate in candidates) {
            ((ICollection<KeyValuePair<string, DnsCacheEntry>>)_dns).Remove(candidate);
        }
    }

    private void RemoveDnsEntry(string host, DnsCacheEntry entry) {
        lock (_dnsSync) {
            ((ICollection<KeyValuePair<string, DnsCacheEntry>>)_dns).Remove(new KeyValuePair<string, DnsCacheEntry>(host, entry));
        }
    }

    private async Task<IPAddress[]> ResolveHostBoundedAsync(string host) {
        if (!DnsLookupGate.Wait(0)) throw new DnsLookupCapacityException();
        try {
            return await _resolveHost(host).ConfigureAwait(false);
        } finally {
            DnsLookupGate.Release();
        }
    }

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

    private static async Task<T> WaitAsync<T>(Task<T> task, CancellationToken cancellationToken) {
        if (!cancellationToken.CanBeCanceled || task.IsCompleted) return await task.ConfigureAwait(false);
        TaskCompletionSource<bool> cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = cancellationToken.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), cancelled);
        if (await Task.WhenAny(task, cancelled.Task).ConfigureAwait(false) != task) {
            _ = task.ContinueWith(static completed => _ = completed.Exception, TaskContinuationOptions.OnlyOnFaulted);
            cancellationToken.ThrowIfCancellationRequested();
        }
        return await task.ConfigureAwait(false);
    }

    private sealed class DnsCacheEntry {
        private readonly Lazy<Task<IPAddress[]>> _lookup;

        internal DnsCacheEntry(Func<Task<IPAddress[]>> lookup, DateTimeOffset expiresAt) {
            _lookup = new Lazy<Task<IPAddress[]>>(lookup, LazyThreadSafetyMode.ExecutionAndPublication);
            ExpiresAt = expiresAt;
        }

        internal Task<IPAddress[]> Lookup => _lookup.Value;
        internal bool IsCompleted => _lookup.IsValueCreated && _lookup.Value.IsCompleted;
        internal DateTimeOffset ExpiresAt { get; }
    }

    private sealed class DnsLookupCapacityException : Exception { }
}
