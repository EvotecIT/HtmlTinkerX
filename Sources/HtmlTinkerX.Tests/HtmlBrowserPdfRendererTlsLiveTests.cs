#if !NETFRAMEWORK
using Microsoft.Playwright;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererLiveTests {
    [Fact]
    public async Task HttpsCertificateErrorsRequireAnExplicitOptIn() {
        await using LoopbackHttpsServer server = new();
        HtmlBrowserNetworkPolicy policy = new(allowPrivateNetworks: true);
        string[] browserArguments = { "--host-resolver-rules=MAP localhost 127.0.0.1" };
        await using (HtmlBrowserPdfRenderer strict = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            browserArguments: browserArguments,
            networkPolicy: policy))) {
            await Assert.ThrowsAsync<PlaywrightException>(() => strict.CaptureAsync(
                new HtmlBrowserPdfRequest(HtmlBrowserPdfSource.FromUrl(server.Url))));
        }

        await using HtmlBrowserPdfRenderer trusted = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            ignoreHttpsErrors: true,
            browserArguments: browserArguments,
            networkPolicy: policy));
        HtmlBrowserPdfResult result = await trusted.CaptureAsync(
            new HtmlBrowserPdfRequest(HtmlBrowserPdfSource.FromUrl(server.Url)));

        AssertPdfContains(result.PdfBytes, "trusted TLS page");
    }

    private sealed class LoopbackHttpsServer : IAsyncDisposable {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly RSA _key = RSA.Create(2048);
        private readonly X509Certificate2 _certificate;
        private readonly ConcurrentDictionary<long, Task> _clients = new();
        private readonly Task _serverTask;
        private long _nextClient;

        internal LoopbackHttpsServer() {
            CertificateRequest request = new("CN=localhost", _key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            SubjectAlternativeNameBuilder names = new();
            names.AddDnsName("localhost");
            names.AddIpAddress(IPAddress.Loopback);
            names.AddIpAddress(IPAddress.IPv6Loopback);
            request.CertificateExtensions.Add(names.Build());
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                false));
            OidCollection usages = new();
            usages.Add(new Oid("1.3.6.1.5.5.7.3.1"));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usages, false));
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
            _certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Url = $"https://localhost:{port}/certificate";
            _serverTask = ServeAsync();
            WaitUntilReady(port);
        }

        internal string Url { get; }

        private async Task ServeAsync() {
            while (!_cancellation.IsCancellationRequested) {
                try {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    long id = Interlocked.Increment(ref _nextClient);
                    Task handling = HandleClientAsync(client);
                    _clients[id] = handling;
                    _ = handling.ContinueWith(
                        completed => {
                            _clients.TryRemove(id, out _);
                            _ = completed.Exception;
                        },
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                } catch (Exception ex) when (_cancellation.IsCancellationRequested
                    && (ex is OperationCanceledException || ex is ObjectDisposedException || ex is SocketException || ex is IOException)) {
                    return;
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client) {
            using (client)
            using (SslStream stream = new(client.GetStream(), leaveInnerStreamOpen: false)) {
                try {
                    await stream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions {
                        ServerCertificate = _certificate,
                        EnabledSslProtocols = SslProtocols.Tls12,
                        ApplicationProtocols = new System.Collections.Generic.List<SslApplicationProtocol> { SslApplicationProtocol.Http11 }
                    }, _cancellation.Token);
                    byte[] request = new byte[16384];
                    int totalRead = 0;
                    while (totalRead < request.Length) {
                        int read = await stream.ReadAsync(request, totalRead, request.Length - totalRead, _cancellation.Token);
                        if (read == 0) return;
                        totalRead += read;
                        if (Encoding.ASCII.GetString(request, 0, totalRead).Contains("\r\n\r\n", StringComparison.Ordinal)) break;
                    }
                    string body = "<html><body><p>trusted TLS page</p></body></html>";
                    byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
                    byte[] headerBytes = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n");
                    byte[] response = new byte[headerBytes.Length + bodyBytes.Length];
                    Buffer.BlockCopy(headerBytes, 0, response, 0, headerBytes.Length);
                    Buffer.BlockCopy(bodyBytes, 0, response, headerBytes.Length, bodyBytes.Length);
                    await stream.WriteAsync(response, 0, response.Length, _cancellation.Token);
                    await stream.FlushAsync(_cancellation.Token);
                    await stream.ShutdownAsync();
                } catch (Exception ex) when (_cancellation.IsCancellationRequested
                    && (ex is OperationCanceledException || ex is ObjectDisposedException || ex is SocketException || ex is IOException)) {
                    return;
                } catch (AuthenticationException) {
                    // The strict browser intentionally rejects this development certificate.
                } catch (IOException) {
                    // The strict browser can close immediately after certificate validation.
                }
            }
        }

        private static void WaitUntilReady(int port) {
            Exception? lastError = null;
            Stopwatch deadline = Stopwatch.StartNew();
            while (deadline.Elapsed < TimeSpan.FromSeconds(15)) {
                try {
                    using TcpClient client = new(AddressFamily.InterNetwork) {
                        ReceiveTimeout = 2000,
                        SendTimeout = 2000
                    };
                    client.Connect(IPAddress.Loopback, port);
                    using SslStream stream = new(client.GetStream(), false, (_, _, _, _) => true) {
                        ReadTimeout = 2000,
                        WriteTimeout = 2000
                    };
                    stream.AuthenticateAsClient(new SslClientAuthenticationOptions {
                        TargetHost = "localhost",
                        EnabledSslProtocols = SslProtocols.Tls12,
                        CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                        ApplicationProtocols = new System.Collections.Generic.List<SslApplicationProtocol> { SslApplicationProtocol.Http11 }
                    });
                    byte[] request = Encoding.ASCII.GetBytes("GET /certificate HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n");
                    stream.Write(request, 0, request.Length);
                    stream.Flush();
                    byte[] response = new byte[8192];
                    StringBuilder body = new();
                    while (body.Length < 65536) {
                        int read = stream.Read(response, 0, response.Length);
                        if (read == 0) break;
                        body.Append(Encoding.UTF8.GetString(response, 0, read));
                        if (body.ToString().Contains("200 OK", StringComparison.Ordinal)
                            && body.ToString().Contains("trusted TLS page", StringComparison.Ordinal)) return;
                    }
                    lastError = new InvalidOperationException("The loopback HTTPS fixture returned unexpected content.");
                } catch (Exception ex) when (ex is IOException || ex is SocketException || ex is AuthenticationException) {
                    lastError = ex;
                }
                Thread.Sleep(100);
            }
            throw new InvalidOperationException("The loopback HTTPS fixture did not complete a TLS request.", lastError);
        }

        public async ValueTask DisposeAsync() {
            _cancellation.Cancel();
            _listener.Stop();
            try { await _serverTask; } catch (ObjectDisposedException) { } catch (SocketException) { }
            Task[] clients = _clients.Values.ToArray();
            if (clients.Length > 0) await Task.WhenAll(clients);
            _certificate.Dispose();
            _key.Dispose();
            _cancellation.Dispose();
        }
    }
}
#endif
