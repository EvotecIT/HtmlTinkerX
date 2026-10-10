using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

[CollectionDefinition("Process diagnostics", DisableParallelization = true)]
public sealed class ProcessDiagnosticsCollection { }

[Collection("Process diagnostics")]
public sealed class HtmlBrowserPolicyProxyDiagnosticTests {
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
