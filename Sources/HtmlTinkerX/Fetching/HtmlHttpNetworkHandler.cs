#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>Direct connection address selection; preferences fall back only when a connection cannot be established.</summary>
public enum HtmlHttpAddressFamily {
    /// <summary>The operating system's ordinary address selection.</summary>
    Automatic,
    /// <summary>Connect over IPv4 only.</summary>
    IPv4Only,
    /// <summary>Connect over IPv6 only.</summary>
    IPv6Only,
    /// <summary>Try IPv4 first, then IPv6.</summary>
    PreferIPv4,
    /// <summary>Try IPv6 first, then IPv4.</summary>
    PreferIPv6
}

/// <summary>A host's direct HTTP connection policy. TLS, HTTP status and content validation remain with their existing owners.</summary>
public sealed record HtmlHttpNetworkPolicy {
    /// <summary>Which address family connections to the host use.</summary>
    public HtmlHttpAddressFamily AddressFamily { get; init; }

    /// <summary>Optional address already assigned to the desired local interface; never an interface name or remote address.</summary>
    public string? LocalAddress { get; init; }

    /// <summary>Maximum seconds per candidate endpoint, 1 to 30; the request's cancellation and deadline also apply.</summary>
    public int ConnectTimeoutSeconds { get; init; } = 5;

    /// <summary>Throws when the policy is out of range or its local address does not match a forced family.</summary>
    public void Validate() {
        if (!Enum.IsDefined(AddressFamily)) throw new ArgumentOutOfRangeException(nameof(AddressFamily));
        if (ConnectTimeoutSeconds is < 1 or > 30) throw new ArgumentOutOfRangeException(nameof(ConnectTimeoutSeconds));
        if (LocalAddress is not null && (!IPAddress.TryParse(LocalAddress, out var local)
            || AddressFamily == HtmlHttpAddressFamily.IPv4Only && local.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork
            || AddressFamily == HtmlHttpAddressFamily.IPv6Only && local.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6))
            throw new ArgumentException("The local address must be a valid address compatible with the forced family.", nameof(LocalAddress));
    }
}

/// <summary>Connection diagnostics without endpoint addresses or credentials. A pooled request may reuse this connection.</summary>
/// <param name="Host">The connected host name.</param>
/// <param name="SelectedFamily">The address family actually used.</param>
/// <param name="UsedFallback">Whether a preferred family failed and the other one was used.</param>
/// <param name="LocalAddressBound">Whether the socket was bound to a configured local address.</param>
public sealed record HtmlHttpNetworkConnection(string Host, HtmlHttpAddressFamily SelectedFamily, bool UsedFallback, bool LocalAddressBound);

/// <summary>
/// Applies explicit per-host direct connection routes (IPv4/IPv6 selection, local interface binding) while preserving
/// the caller's ordinary transport for other hosts. Each routed host keeps a separate connection pool. Use it as the
/// inner handler of <see cref="HtmlFetchHandler"/>, which still owns redirects, retries and pacing. .NET 8 and later only.
/// </summary>
/// <remarks>
/// Routed hosts connect directly. When a proxy (including the process default) applies to a routed host, the request
/// fails before connecting rather than silently bypassing that proxy. When the ordinary transport is a <see cref="SocketsHttpHandler"/>,
/// routed hosts reuse its TLS options (certificate validation, client certificates, protocols), credentials, cookie
/// container, decompression and pooling limits. Any other transport's settings cannot be read, so routed hosts then use
/// a default transport: system certificate validation, no credentials, client certificates or cookies. Routes apply to
/// TCP connections, so routed requests never use HTTP/3: a request that allows a lower version is sent at HTTP/2 or
/// below, and one that requires HTTP/3 fails with <see cref="NotSupportedException"/>.
/// </remarks>
public sealed class HtmlHttpNetworkHandler : HttpMessageHandler {
    private readonly HttpMessageInvoker _ordinary;
    private readonly IReadOnlyDictionary<string, HttpMessageInvoker> _routes;

    /// <summary>The transport for hosts without a route; routed transports share its cookie container or send no cookies.</summary>
    internal HttpMessageHandler OrdinaryTransport { get; }

    /// <summary>Owns <paramref name="ordinaryTransport"/> and the routed transports until disposal. Host keys are exact, without ports.</summary>
    /// <param name="ordinaryTransport">Transport for hosts without a route; it must not follow redirects automatically.</param>
    /// <param name="routes">Connection policies keyed by exact host name.</param>
    /// <param name="onConnected">Optional callback with connection diagnostics.</param>
    public HtmlHttpNetworkHandler(HttpMessageHandler ordinaryTransport, IReadOnlyDictionary<string, HtmlHttpNetworkPolicy> routes,
        Action<HtmlHttpNetworkConnection>? onConnected = null) {
        ArgumentNullException.ThrowIfNull(ordinaryTransport);
        ArgumentNullException.ThrowIfNull(routes);
        ValidateRoutes(routes);
        HtmlFetchHandler.RequireManualRedirects(ordinaryTransport);
        Dictionary<string, HtmlHttpNetworkPolicy> policies = new(StringComparer.OrdinalIgnoreCase);
        foreach (var (host, policy) in routes) {
            if (policy.AddressFamily != HtmlHttpAddressFamily.Automatic || policy.LocalAddress is not null)
                policies.Add(Normalize(host), policy);
        }
        OrdinaryTransport = ordinaryTransport;
        _ordinary = new(ordinaryTransport, disposeHandler: true);
        _routes = policies.ToDictionary(pair => pair.Key,
            pair => new HttpMessageInvoker(CreateTransport(pair.Value, ordinaryTransport as SocketsHttpHandler, onConnected), disposeHandler: true),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Checks host routes before work starts, without opening sockets or allocating transport pools.</summary>
    /// <param name="routes">Connection policies keyed by exact host name.</param>
    public static void ValidateRoutes(IReadOnlyDictionary<string, HtmlHttpNetworkPolicy> routes) {
        ArgumentNullException.ThrowIfNull(routes);
        if (routes.Count > 256) throw new ArgumentOutOfRangeException(nameof(routes));
        HashSet<string> hosts = new(StringComparer.OrdinalIgnoreCase);
        foreach (var (host, policy) in routes) {
            if (string.IsNullOrEmpty(host) || policy is null || Uri.CheckHostName(host.Trim('[', ']')) == UriHostNameType.Unknown)
                throw new ArgumentException("Network routes require valid exact hostnames and policies.", nameof(routes));
            policy.Validate();
            if (!hosts.Add(Normalize(host))) throw new ArgumentException("Duplicate network route host.", nameof(routes));
        }
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        if (!_routes.TryGetValue(Normalize(request.RequestUri!.IdnHost), out var route)) return _ordinary.SendAsync(request, cancellationToken);
        KeepOnTcp(request);
        return route.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// HTTP/3 runs over QUIC, which never calls the connect callback, so a routed request must not use it. A request
    /// allowing a higher version could also move to HTTP/3 after an Alt-Svc advertisement; it is capped at HTTP/2.
    /// </summary>
    private static void KeepOnTcp(HttpRequestMessage request) {
        if (request.Version.Major >= 3) {
            if (request.VersionPolicy != HttpVersionPolicy.RequestVersionOrLower)
                throw new NotSupportedException("Explicit network routes connect over TCP and cannot send a request that requires HTTP/3.");
            request.Version = HttpVersion.Version20;
        } else if (request.VersionPolicy == HttpVersionPolicy.RequestVersionOrHigher) {
            if (request.Version.Major == 2) {
                request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
            } else {
                request.Version = HttpVersion.Version20;
                request.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            }
        }
    }

    private static SocketsHttpHandler CreateTransport(HtmlHttpNetworkPolicy policy, SocketsHttpHandler? template,
        Action<HtmlHttpNetworkConnection>? onConnected) {
        // The connect callback opens the publisher's socket itself. A proxy that applies is left in place on purpose: the
        // callback then refuses the proxy endpoint, so a routed request fails instead of quietly going around the proxy.
        SocketsHttpHandler handler = new() { AllowAutoRedirect = false, UseCookies = false };
        if (template is not null) {
            handler.SslOptions = template.SslOptions;
            handler.Credentials = template.Credentials;
            handler.PreAuthenticate = template.PreAuthenticate;
            handler.UseCookies = template.UseCookies;
            if (template.UseCookies) handler.CookieContainer = template.CookieContainer;
            handler.AutomaticDecompression = template.AutomaticDecompression;
            handler.MaxConnectionsPerServer = template.MaxConnectionsPerServer;
            handler.MaxResponseHeadersLength = template.MaxResponseHeadersLength;
            handler.PooledConnectionLifetime = template.PooledConnectionLifetime;
            handler.PooledConnectionIdleTimeout = template.PooledConnectionIdleTimeout;
        }
        if (policy.AddressFamily == HtmlHttpAddressFamily.Automatic && policy.LocalAddress is null) return handler;
        handler.ConnectCallback = (context, token) => ConnectAsync(context, policy, onConnected, token);
        return handler;
    }

    private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, HtmlHttpNetworkPolicy policy,
        Action<HtmlHttpNetworkConnection>? onConnected, CancellationToken token) {
        // Binding a proxy socket does not select the publisher's address family. Fail explicitly rather than claiming it does.
        if (context.InitialRequestMessage.Method == HttpMethod.Connect
            || context.InitialRequestMessage.RequestUri is not { } uri || Normalize(uri.IdnHost) != Normalize(context.DnsEndPoint.Host))
            throw new NotSupportedException("Explicit network routes require a direct connection, not a forward proxy.");
        IPAddress? local = policy.LocalAddress is null ? null : IPAddress.Parse(policy.LocalAddress);
        IPAddress[] addresses = IPAddress.TryParse(context.DnsEndPoint.Host.Trim('[', ']'), out var literal) ? [literal]
            : await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, token).ConfigureAwait(false);
        bool v4 = policy.AddressFamily is HtmlHttpAddressFamily.IPv4Only or HtmlHttpAddressFamily.PreferIPv4;
        bool forced = policy.AddressFamily is HtmlHttpAddressFamily.IPv4Only or HtmlHttpAddressFamily.IPv6Only;
        var preferred = v4 ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6;
        var candidates = addresses.Distinct().Where(address => address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
            .Where(address => (!forced || address.AddressFamily == preferred) && (local is null || address.AddressFamily == local.AddressFamily))
            .GroupBy(address => address.AddressFamily).SelectMany(group => group.Take(4))
            .OrderBy(address => policy.AddressFamily == HtmlHttpAddressFamily.Automatic || address.AddressFamily == preferred ? 0 : 1);
        Exception? failure = null;
        foreach (var address in candidates) {
            token.ThrowIfCancellationRequested();
            Socket socket = new(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try {
                if (local is not null) socket.Bind(new IPEndPoint(local, 0));
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                deadline.CancelAfter(TimeSpan.FromSeconds(policy.ConnectTimeoutSeconds));
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), deadline.Token).ConfigureAwait(false);
                bool fallback = policy.AddressFamily is HtmlHttpAddressFamily.PreferIPv4 or HtmlHttpAddressFamily.PreferIPv6 && address.AddressFamily != preferred;
                onConnected?.Invoke(new(uri.IdnHost, address.AddressFamily == AddressFamily.InterNetwork ? HtmlHttpAddressFamily.IPv4Only : HtmlHttpAddressFamily.IPv6Only,
                    fallback, local is not null));
                return new NetworkStream(socket, ownsSocket: true);
            } catch (Exception error) when (error is SocketException || error is OperationCanceledException && !token.IsCancellationRequested) {
                failure = error;
                socket.Dispose();
            } catch { socket.Dispose(); throw; }
        }
        throw new HttpRequestException("No endpoint could be connected within the configured network policy.", failure);
    }

    private static string Normalize(string host) {
        host = host.Trim('[', ']').TrimEnd('.');
        return IPAddress.TryParse(host, out var address) ? address.ToString() : new IdnMapping().GetAscii(host).ToLowerInvariant();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing) {
        if (disposing) { _ordinary.Dispose(); foreach (var route in _routes.Values) route.Dispose(); }
        base.Dispose(disposing);
    }
}
#endif
