using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HtmlTinkerX.Benchmarks;

// A finite, immutable input corpus. It serves no external requests or filesystem paths.
internal sealed class LoopbackSite : IDisposable {
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource lifetime = new();
    private readonly IReadOnlyDictionary<string, byte[]> pages;
    private readonly Task acceptLoop;

    internal LoopbackSite(IReadOnlyDictionary<string, string> pages) {
        this.pages = pages.ToDictionary(pair => pair.Key, pair => Encoding.UTF8.GetBytes(pair.Value));
        listener.Start();
        Root = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/");
        acceptLoop = ServeAsync();
    }

    internal Uri Root { get; }

    private async Task ServeAsync() {
        try {
            while (!lifetime.IsCancellationRequested) {
                using TcpClient client = await listener.AcceptTcpClientAsync(lifetime.Token);
                using NetworkStream stream = client.GetStream();
                using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                requestTimeout.CancelAfter(TimeSpan.FromSeconds(10));
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
                string? firstLine = await reader.ReadLineAsync(requestTimeout.Token);
                string? line;
                do { line = await reader.ReadLineAsync(requestTimeout.Token); }
                while (!string.IsNullOrEmpty(line));
                string path = firstLine?.Split(' ').ElementAtOrDefault(1) ?? string.Empty;
                bool found = pages.TryGetValue(path, out byte[]? body);
                body ??= Encoding.UTF8.GetBytes("Not found");
                byte[] header = Encoding.ASCII.GetBytes($"HTTP/1.1 {(found ? "200 OK" : "404 Not Found")}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(header, requestTimeout.Token);
                await stream.WriteAsync(body, requestTimeout.Token);
            }
        } catch (OperationCanceledException) when (lifetime.IsCancellationRequested) {
        } catch (SocketException) when (lifetime.IsCancellationRequested) {
        }
    }

    public void Dispose() {
        lifetime.Cancel();
        listener.Stop();
        try { acceptLoop.GetAwaiter().GetResult(); } finally { lifetime.Dispose(); }
    }
}