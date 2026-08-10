namespace HtmlTinkerX;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Loopback HTTP CONNECT proxy that binds policy approval to the exact remote socket address.
/// Chromium retains the original host for HTTP Host headers and TLS SNI.
/// </summary>
internal sealed class HtmlBrowserPolicyProxy : IAsyncDisposable {
    private const int MaximumHeaderBytes = 64 * 1024;
    private static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DefaultRelayDrainTimeout = TimeSpan.FromSeconds(5);
    private readonly HtmlBrowserNetworkPolicyEvaluator _policy;
    private readonly TimeSpan _connectTimeout;
    private readonly TimeSpan _relayDrainTimeout;
    private readonly Func<TcpClient, IPAddress, int, Task> _connect;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly ConcurrentDictionary<long, Task> _clients = new();
    private readonly Task _acceptLoop;
    private long _nextClient;

    internal HtmlBrowserPolicyProxy(HtmlBrowserNetworkPolicy policy)
        : this(new HtmlBrowserNetworkPolicyEvaluator(policy)) { }

    internal HtmlBrowserPolicyProxy(
        HtmlBrowserNetworkPolicyEvaluator policy,
        TimeSpan? connectTimeout = null,
        Func<TcpClient, IPAddress, int, Task>? connect = null,
        TimeSpan? relayDrainTimeout = null) {
        _policy = policy;
        _connectTimeout = connectTimeout ?? DefaultConnectTimeout;
        _relayDrainTimeout = relayDrainTimeout ?? DefaultRelayDrainTimeout;
        _connect = connect ?? ((client, address, port) => client.ConnectAsync(address, port));
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        IPEndPoint endpoint = (IPEndPoint)_listener.LocalEndpoint;
        Server = $"http://127.0.0.1:{endpoint.Port}";
        _acceptLoop = AcceptLoopAsync();
    }

    internal string Server { get; }
    internal event Action<Uri>? RequestBlocked;

    private async Task AcceptLoopAsync() {
        while (!_lifetime.IsCancellationRequested) {
            TcpClient client;
            try {
                client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
            } catch (ObjectDisposedException) when (_lifetime.IsCancellationRequested) {
                break;
            } catch (SocketException) when (_lifetime.IsCancellationRequested) {
                break;
            }

            long id = Interlocked.Increment(ref _nextClient);
            Task handling = HandleClientAsync(client, _lifetime.Token);
            _clients[id] = handling;
            _ = handling.ContinueWith(
                completed => _clients.TryRemove(id, out _),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private async Task HandleClientAsync(TcpClient browserClient, CancellationToken cancellationToken) {
        using (browserClient) {
            try {
                NetworkStream browser = browserClient.GetStream();
                byte[] headerAndRemainder = await ReadHeadersAsync(browser, cancellationToken).ConfigureAwait(false);
                int headerEnd = FindHeaderEnd(headerAndRemainder);
                if (headerEnd < 0) {
                    await WriteStatusAsync(browser, 431, "Request Header Fields Too Large", cancellationToken).ConfigureAwait(false);
                    return;
                }

                string headerText = Encoding.ASCII.GetString(headerAndRemainder, 0, headerEnd);
                string[] lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
                if (lines.Length == 0) return;
                string[] requestParts = lines[0].Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                if (requestParts.Length != 3) {
                    await WriteStatusAsync(browser, 400, "Bad Request", cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (string.Equals(requestParts[0], "CONNECT", StringComparison.OrdinalIgnoreCase)) {
                    await HandleConnectAsync(
                        browserClient,
                        browser,
                        requestParts[1],
                        headerAndRemainder,
                        headerEnd + 4,
                        cancellationToken).ConfigureAwait(false);
                    return;
                }

                await HandleHttpAsync(browserClient, browser, requestParts, lines, headerAndRemainder, headerEnd + 4, cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                // Renderer disposal closes active proxy connections.
            } catch (IOException) {
                // Either endpoint closed the connection.
            } catch (SocketException) {
                // Either endpoint closed the connection.
            }
        }
    }

    private async Task HandleConnectAsync(
        TcpClient browserClient,
        NetworkStream browser,
        string authority,
        byte[] received,
        int tunnelOffset,
        CancellationToken cancellationToken) {
        if (!TryParseAuthority(authority, 443, out string host, out int port)) {
            await WriteStatusAsync(browser, 400, "Bad CONNECT Target", cancellationToken).ConfigureAwait(false);
            return;
        }

        Uri target = new UriBuilder(Uri.UriSchemeHttps, host, port).Uri;
        using TcpClient? remoteClient = await ConnectAllowedAsync(target, cancellationToken).ConfigureAwait(false);
        if (remoteClient == null) {
            await WriteStatusAsync(browser, 403, "Forbidden", cancellationToken).ConfigureAwait(false);
            return;
        }

        byte[] established = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
        await browser.WriteAsync(established, 0, established.Length, cancellationToken).ConfigureAwait(false);
        NetworkStream remote = remoteClient.GetStream();
        if (tunnelOffset < received.Length) {
            await remote.WriteAsync(received, tunnelOffset, received.Length - tunnelOffset, cancellationToken).ConfigureAwait(false);
        }
        await RelayAsync(browserClient, remoteClient, _relayDrainTimeout, cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleHttpAsync(
        TcpClient browserClient,
        NetworkStream browser,
        string[] requestParts,
        string[] lines,
        byte[] received,
        int bodyOffset,
        CancellationToken cancellationToken) {
        Uri? target = null;
        if (Uri.TryCreate(requestParts[1], UriKind.Absolute, out Uri? absolute)) {
            target = absolute;
        } else {
            string? hostHeader = lines.Skip(1).FirstOrDefault(line => line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase));
            if (hostHeader != null && TryParseAuthority(hostHeader.Substring(5).Trim(), 80, out string host, out int port)) {
                target = new UriBuilder(Uri.UriSchemeHttp, host, port, requestParts[1]).Uri;
            }
        }

        if (target == null || (target.Scheme != Uri.UriSchemeHttp
                               && target.Scheme != Uri.UriSchemeHttps
                               && target.Scheme != "ws"
                               && target.Scheme != "wss")) {
            await WriteStatusAsync(browser, 400, "Bad Proxy Target", cancellationToken).ConfigureAwait(false);
            return;
        }

        using TcpClient? remoteClient = await ConnectAllowedAsync(target, cancellationToken).ConfigureAwait(false);
        if (remoteClient == null) {
            await WriteStatusAsync(browser, 403, "Forbidden", cancellationToken).ConfigureAwait(false);
            return;
        }

        NetworkStream remote = remoteClient.GetStream();
        string pathAndQuery = string.IsNullOrEmpty(target.PathAndQuery) ? "/" : target.PathAndQuery;
        StringBuilder forwarded = new();
        forwarded.Append(requestParts[0]).Append(' ').Append(pathAndQuery).Append(' ').Append(requestParts[2]).Append("\r\n");
        bool hasHost = false;
        bool isUpgrade = lines.Skip(1).Any(line =>
            line.StartsWith("Connection:", StringComparison.OrdinalIgnoreCase)
            && line.Substring(line.IndexOf(':') + 1)
                .Split(',')
                .Any(token => string.Equals(token.Trim(), "upgrade", StringComparison.OrdinalIgnoreCase)))
            && lines.Skip(1).Any(line => line.StartsWith("Upgrade:", StringComparison.OrdinalIgnoreCase));
        foreach (string line in lines.Skip(1)) {
            if (line.Length == 0
                || line.StartsWith("Connection:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Proxy-Connection:", StringComparison.OrdinalIgnoreCase)) continue;
            if (line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase)) hasHost = true;
            forwarded.Append(line).Append("\r\n");
        }
        if (!hasHost) forwarded.Append("Host: ").Append(target.IsDefaultPort ? target.IdnHost : target.Authority).Append("\r\n");
        forwarded.Append(isUpgrade ? "Connection: Upgrade\r\n\r\n" : "Connection: close\r\n\r\n");

        byte[] forwardedHeader = Encoding.ASCII.GetBytes(forwarded.ToString());
        await remote.WriteAsync(forwardedHeader, 0, forwardedHeader.Length, cancellationToken).ConfigureAwait(false);
        if (bodyOffset < received.Length) {
            await remote.WriteAsync(received, bodyOffset, received.Length - bodyOffset, cancellationToken).ConfigureAwait(false);
        }
        await RelayAsync(browserClient, remoteClient, _relayDrainTimeout, cancellationToken).ConfigureAwait(false);
    }

    private async Task<TcpClient?> ConnectAllowedAsync(Uri target, CancellationToken cancellationToken) {
        IPAddress[] addresses = await _policy.ResolveAllowedAddressesAsync(target, cancellationToken).ConfigureAwait(false);
        if (addresses.Length == 0) RequestBlocked?.Invoke(target);
        int port = target.IsDefaultPort ? (target.Scheme == Uri.UriSchemeHttps || target.Scheme == "wss" ? 443 : 80) : target.Port;
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_connectTimeout);
        foreach (IPAddress address in addresses) {
            TcpClient client = new(address.AddressFamily);
            try {
                await WaitAsync(_connect(client, address, port), deadline.Token, client).ConfigureAwait(false);
                return client;
            } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested) {
                client.Dispose();
            } catch (Exception ex) when (ex is SocketException || ex is IOException) {
                client.Dispose();
            }
        }
        return null;
    }

    private static async Task RelayAsync(
        TcpClient firstClient,
        TcpClient secondClient,
        TimeSpan drainTimeout,
        CancellationToken cancellationToken) {
        Stream first = firstClient.GetStream();
        Stream second = secondClient.GetStream();
        Task firstToSecond = CopyAsync(first, second, cancellationToken);
        Task secondToFirst = CopyAsync(second, first, cancellationToken);
        Task completed = await Task.WhenAny(firstToSecond, secondToFirst).ConfigureAwait(false);
        if (completed.Status == TaskStatus.RanToCompletion) {
            TryShutdownSend(ReferenceEquals(completed, firstToSecond) ? secondClient : firstClient);
            Task remaining = ReferenceEquals(completed, firstToSecond) ? secondToFirst : firstToSecond;
            Task drainDeadline = Task.Delay(drainTimeout, cancellationToken);
            if (await Task.WhenAny(remaining, drainDeadline).ConfigureAwait(false) != remaining) {
                try { first.Dispose(); } catch (ObjectDisposedException) { }
                try { second.Dispose(); } catch (ObjectDisposedException) { }
            }
        } else {
            try { first.Dispose(); } catch (ObjectDisposedException) { }
            try { second.Dispose(); } catch (ObjectDisposedException) { }
        }
        try {
            await Task.WhenAll(firstToSecond, secondToFirst).ConfigureAwait(false);
        } catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is OperationCanceledException) {
        }
    }

    private static void TryShutdownSend(TcpClient client) {
        try {
            client.Client.Shutdown(SocketShutdown.Send);
        } catch (Exception ex) when (ex is ObjectDisposedException || ex is SocketException) {
            // The peer already completed the full close.
        }
    }

    private static async Task CopyAsync(Stream source, Stream destination, CancellationToken cancellationToken) {
        byte[] buffer = new byte[32 * 1024];
        while (true) {
            int read = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
            if (read == 0) return;
            await destination.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<byte[]> ReadHeadersAsync(Stream stream, CancellationToken cancellationToken) {
        using MemoryStream result = new();
        byte[] buffer = new byte[4096];
        while (result.Length < MaximumHeaderBytes) {
            int read = await stream.ReadAsync(buffer, 0, Math.Min(buffer.Length, MaximumHeaderBytes - (int)result.Length), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            result.Write(buffer, 0, read);
            byte[] current = result.ToArray();
            if (FindHeaderEnd(current) >= 0) return current;
        }
        return result.ToArray();
    }

    private static int FindHeaderEnd(byte[] bytes) {
        for (int i = 0; i <= bytes.Length - 4; i++) {
            if (bytes[i] == 13 && bytes[i + 1] == 10 && bytes[i + 2] == 13 && bytes[i + 3] == 10) return i;
        }
        return -1;
    }

    private static bool TryParseAuthority(string authority, int defaultPort, out string host, out int port) {
        host = string.Empty;
        port = defaultPort;
        if (!Uri.TryCreate("tcp://" + authority, UriKind.Absolute, out Uri? uri) || string.IsNullOrWhiteSpace(uri.Host)) return false;
        host = uri.Host;
        port = uri.IsDefaultPort ? defaultPort : uri.Port;
        return port > 0 && port <= 65535;
    }

    private static async Task WriteStatusAsync(Stream stream, int status, string reason, CancellationToken cancellationToken) {
        byte[] payload = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {reason}\r\nConnection: close\r\nContent-Length: 0\r\n\r\n");
        await stream.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WaitAsync(Task task, CancellationToken cancellationToken, TcpClient client) {
        if (!cancellationToken.CanBeCanceled || task.IsCompleted) {
            await task.ConfigureAwait(false);
            return;
        }
        TaskCompletionSource<bool> cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = cancellationToken.Register(static state => {
            Tuple<TaskCompletionSource<bool>, TcpClient> values = (Tuple<TaskCompletionSource<bool>, TcpClient>)state!;
            values.Item2.Dispose();
            values.Item1.TrySetResult(true);
        }, Tuple.Create(cancelled, client));
        if (await Task.WhenAny(task, cancelled.Task).ConfigureAwait(false) != task) {
            _ = task.ContinueWith(static completed => _ = completed.Exception, TaskContinuationOptions.OnlyOnFaulted);
            cancellationToken.ThrowIfCancellationRequested();
        }
        await task.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync() {
        if (_lifetime.IsCancellationRequested) return;
        _lifetime.Cancel();
        _listener.Stop();
        try { await _acceptLoop.ConfigureAwait(false); } catch (OperationCanceledException) { }
        Task[] clients = _clients.Values.ToArray();
        if (clients.Length > 0) {
            try { await Task.WhenAll(clients).ConfigureAwait(false); } catch (Exception ex) when (ex is IOException || ex is SocketException || ex is OperationCanceledException) { }
        }
        _lifetime.Dispose();
    }
}
