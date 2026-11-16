#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Creates an HTTP transport that only connects to globally reachable addresses, for fetching URLs that come from
/// untrusted metadata (a package's declared license page, a feed's link). Loopback, private, link-local, carrier-grade
/// NAT, multicast and other special-purpose addresses are refused, including cloud metadata endpoints. .NET 8 and later only.
/// </summary>
/// <remarks>
/// The host is resolved once per connection and every resolved address must be globally reachable; a name that also
/// resolves to a private address is refused rather than tried on its public address. The socket connects to the address
/// that was checked, so a different DNS answer between the check and the connection cannot redirect it. The transport
/// connects directly: it uses no proxy, sends no cookies and never follows redirects itself, so each redirect target is
/// checked by this transport when <see cref="HtmlFetchHandler"/> sends it. Requests stay on HTTP/2 or below: HTTP/3
/// (QUIC) does not use the connect callback, so a request that requires it fails and one that allows it is capped,
/// which also stops a server's Alt-Svc advertisement from moving later requests to an unchecked endpoint.
/// </remarks>
public static class HtmlPublicNetworkTransport {
    private const int MaximumAddressesPerFamily = 4;

    /// <summary>Creates the transport. The caller owns and disposes it.</summary>
    /// <param name="connectTimeoutSeconds">Maximum seconds per candidate address, 1 to 30.</param>
    /// <param name="nat64Prefixes">
    /// The network's NAT64 prefixes, such as <c>2001:db8:64::/96</c>, when it translates IPv6 to IPv4 with a
    /// network-specific prefix. An address inside one of them is checked by the IPv4 address it embeds. The well-known
    /// prefix <c>64:ff9b::/96</c> is always checked that way.
    /// </param>
    public static HttpMessageHandler Create(int connectTimeoutSeconds = 5, IEnumerable<string>? nat64Prefixes = null) {
        if (connectTimeoutSeconds is < 1 or > 30) throw new ArgumentOutOfRangeException(nameof(connectTimeoutSeconds));
        HtmlBrowserNat64Prefix[] prefixes = (nat64Prefixes ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(HtmlBrowserNat64Prefix.Parse)
            .Distinct()
            .OrderByDescending(prefix => prefix.PrefixLength)
            .ToArray();
        var sockets = new SocketsHttpHandler {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectCallback = (context, token) => ConnectAsync(context.DnsEndPoint, connectTimeoutSeconds, prefixes, Dns.GetHostAddressesAsync, token)
        };
        return new TcpOnlyHandler(sockets);
    }

    /// <summary>Keeps every request on a TCP connection made by the checked connect callback.</summary>
    private sealed class TcpOnlyHandler(HttpMessageHandler inner) : DelegatingHandler(inner) {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            HtmlHttpNetworkHandler.KeepOnTcp(request);
            return base.SendAsync(request, cancellationToken);
        }
    }

    internal static async ValueTask<Stream> ConnectAsync(DnsEndPoint endpoint, int connectTimeoutSeconds, IReadOnlyList<HtmlBrowserNat64Prefix> nat64Prefixes,
        Func<string, CancellationToken, Task<IPAddress[]>> resolve, CancellationToken token) {
        IReadOnlyList<IPAddress> addresses = await PublicAddressesAsync(endpoint.Host, nat64Prefixes, resolve, token).ConfigureAwait(false);
        Exception? failure = null;
        foreach (var address in addresses) {
            token.ThrowIfCancellationRequested();
            Socket socket = new(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                deadline.CancelAfter(TimeSpan.FromSeconds(connectTimeoutSeconds));
                await socket.ConnectAsync(new IPEndPoint(address, endpoint.Port), deadline.Token).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            } catch (Exception error) when (error is SocketException || error is OperationCanceledException && !token.IsCancellationRequested) {
                failure = error;
                socket.Dispose();
            } catch { socket.Dispose(); throw; }
        }
        throw new HttpRequestException("No public address of the host could be connected.", failure);
    }

    /// <summary>The addresses to try, or an exception when the host is local or any resolved address is not public.</summary>
    internal static async Task<IReadOnlyList<IPAddress>> PublicAddressesAsync(string host, IReadOnlyList<HtmlBrowserNat64Prefix> nat64Prefixes,
        Func<string, CancellationToken, Task<IPAddress[]>> resolve, CancellationToken token) {
        string name = host.Trim('[', ']').TrimEnd('.');
        if (name.Equals("localhost", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
            throw new HttpRequestException("Local host names are not public network destinations.");
        IPAddress[] resolved = IPAddress.TryParse(name, out var literal) ? [literal] : await resolve(name, token).ConfigureAwait(false);
        IPAddress[] usable = resolved.Distinct().Where(address => address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6).ToArray();
        if (usable.Length == 0) throw new HttpRequestException("The host has no IPv4 or IPv6 address.");
        if (!usable.All(address => HtmlBrowserNetworkAddressClassifier.IsGloballyReachable(address, nat64Prefixes)))
            throw new HttpRequestException("The host resolves to a private or special-purpose address; only public addresses are allowed.");
        return usable.GroupBy(address => address.AddressFamily).SelectMany(group => group.Take(MaximumAddressesPerFamily)).ToArray();
    }
}
#endif
