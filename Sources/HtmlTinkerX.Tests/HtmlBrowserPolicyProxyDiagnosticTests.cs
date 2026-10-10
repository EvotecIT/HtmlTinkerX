using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

[CollectionDefinition("Process diagnostics", DisableParallelization = true)]
public sealed class ProcessDiagnosticsCollection { }

[Collection("Process diagnostics")]
public sealed class HtmlBrowserPolicyProxyDiagnosticTests {
    [Fact]
    public async Task SuccessfulTunnelReportsTheEstablishedConnection() {
        string? previousDebug = Environment.GetEnvironmentVariable("DEBUG");
        TextWriter previousError = Console.Error;
        using StringWriter output = new();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        TcpListener origin = new(IPAddress.Loopback, 0);
        origin.Start();
        try {
            Environment.SetEnvironmentVariable("DEBUG", "pw:proxy");
            Console.SetError(output);
            int port = ((IPEndPoint)origin.LocalEndpoint).Port;
            HtmlBrowserNetworkPolicyEvaluator evaluator = new(
                new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "render.invalid" }),
                _ => Task.FromResult(new[] { IPAddress.Loopback }));
            await using HtmlBrowserPolicyProxy proxy = new(evaluator);
            using TcpClient browser = new();
            await browser.ConnectAsync(IPAddress.Loopback, new Uri(proxy.Server).Port)
                .WaitWithCancellationAsync(timeout.Token);
            using NetworkStream stream = browser.GetStream();
            byte[] request = Encoding.ASCII.GetBytes($"CONNECT render.invalid:{port} HTTP/1.1\r\nHost: render.invalid:{port}\r\n\r\n");
            await stream.WriteAsync(request, 0, request.Length, timeout.Token);
            using StreamReader response = new(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
            Assert.Equal("HTTP/1.1 200 Connection Established",
                await response.ReadLineAsync().WaitWithCancellationAsync(timeout.Token));
            using TcpClient connectedOrigin = await origin.AcceptTcpClientAsync().WaitWithCancellationAsync(timeout.Token);
        } finally {
            Console.SetError(previousError);
            Environment.SetEnvironmentVariable("DEBUG", previousDebug);
            origin.Stop();
        }

        Assert.Contains("accepted", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("connect-start", output.ToString(), StringComparison.Ordinal);
        Assert.Contains(" connected", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("connect-failed", output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConnectionFailureDiagnosticsRespectOptInAndRedactSensitiveDetails(bool enabled) {
        string? previousDebug = Environment.GetEnvironmentVariable("DEBUG");
        TextWriter previousError = Console.Error;
        using StringWriter output = new();
        try {
            Environment.SetEnvironmentVariable("DEBUG", enabled ? "pw:proxy" : null);
            Console.SetError(output);
            Assert.Contains("403 Forbidden", await FailedConnectionResponseAsync(), StringComparison.Ordinal);
        } finally {
            Console.SetError(previousError);
            Environment.SetEnvironmentVariable("DEBUG", previousDebug);
        }

        string diagnostic = output.ToString();
        if (!enabled) {
            Assert.Equal(string.Empty, diagnostic);
            return;
        }
        Assert.Contains("accepted", diagnostic, StringComparison.Ordinal);
        Assert.Contains("connect-start", diagnostic, StringComparison.Ordinal);
        Assert.Contains("connect-failed", diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain(" connected", diagnostic, StringComparison.Ordinal);
        Assert.Contains("socket=ConnectionRefused", diagnostic, StringComparison.Ordinal);
        Assert.Contains("disposing", diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("private-exception", diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("private-path", diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("private-query", diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("private-header", diagnostic, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosedDiagnosticStreamDoesNotChangeTheProxyResponse(bool disposed) {
        string? previousDebug = Environment.GetEnvironmentVariable("DEBUG");
        TextWriter previousError = Console.Error;
        try {
            Environment.SetEnvironmentVariable("DEBUG", "pw:proxy");
            Console.SetError(new ClosedWriter(disposed));
            Assert.Contains("403 Forbidden", await FailedConnectionResponseAsync(), StringComparison.Ordinal);
        } finally {
            Console.SetError(previousError);
            Environment.SetEnvironmentVariable("DEBUG", previousDebug);
        }
    }

    private static async Task<string> FailedConnectionResponseAsync() {
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "render.invalid" });
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(policy, _ => Task.FromResult(new[] { IPAddress.Loopback }));
        await using HtmlBrowserPolicyProxy proxy = new(evaluator, connect: (_, _, _) =>
            Task.FromException(new IOException("private-exception", new SocketException((int)SocketError.ConnectionRefused))));
        using TcpClient browser = new();
        await browser.ConnectAsync(IPAddress.Loopback, new Uri(proxy.Server).Port);
        using NetworkStream stream = browser.GetStream();
        byte[] request = Encoding.ASCII.GetBytes("GET http://render.invalid:8080/private-path?secret=private-query HTTP/1.1\r\nHost: render.invalid\r\nProxy-Authorization: private-header\r\n\r\n");
        await stream.WriteAsync(request, 0, request.Length);
        using MemoryStream response = new();
        await stream.CopyToAsync(response);
        return Encoding.ASCII.GetString(response.ToArray());
    }

    private sealed class ClosedWriter : TextWriter {
        private readonly bool _disposed;
        internal ClosedWriter(bool disposed) => _disposed = disposed;
        public override Encoding Encoding => Encoding.UTF8;
        public override void WriteLine(string? value) {
            if (_disposed) throw new ObjectDisposedException("diagnostic-stream");
            throw new IOException("diagnostic-stream closed");
        }
    }
}
