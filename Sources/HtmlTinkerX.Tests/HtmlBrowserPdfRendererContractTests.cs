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
using Microsoft.Playwright;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed class HtmlBrowserPdfRendererContractTests {
    [Fact]
    public void RendererOptionsRejectNonChromiumBeforeLaunch() {
        NotSupportedException exception = Assert.Throws<NotSupportedException>(
            () => new HtmlBrowserPdfRendererOptions(browser: HtmlBrowserEngine.Firefox));

        Assert.Contains("only by Chromium", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DirectPagePdfApiUsesTheOptionsContractOnly() {
        System.Reflection.MethodInfo[] methods = typeof(HtmlBrowser).GetMethods()
            .Where(method => method.Name == nameof(HtmlBrowser.GetPagePdfAsync) || method.Name == nameof(HtmlBrowser.SavePagePdfAsync))
            .ToArray();

        Assert.Equal(2, methods.Length);
        Assert.All(methods, method => {
            System.Reflection.ParameterInfo[] parameters = method.GetParameters();
            Assert.Equal(typeof(IPage), parameters[0].ParameterType);
            Assert.Contains(parameters, parameter => parameter.ParameterType == typeof(HtmlBrowserPdfOptions));
            Assert.True(parameters.Length <= 5);
        });
    }

    [Fact]
    public void PdfReadinessRejectsNegativeDelay() {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HtmlBrowserPdfReadiness(delayMilliseconds: -1));
    }

    [Fact]
    public void CustomPageDimensionsOverrideTheDefaultNamedFormat() {
        HtmlBrowserPdfOptions options = new(width: "210mm", height: "297mm");

        Assert.Null(options.Format);
        Assert.Null(HtmlBrowserPdfCapture.CreatePageOptions(options).Format);
    }

    [Fact]
    public void DomainCookieDefaultsToTheRootPath() {
        HtmlBrowserPdfCookie cookie = new("session", "value", domain: "example.com");

        Assert.Equal("/", cookie.Path);
    }

    [Fact]
    public void RequestSnapshotsMutableCollections() {
        Dictionary<string, string> headers = new() { ["X-Correlation-Id"] = "first" };
        HtmlBrowserPdfRequest request = new(
            HtmlBrowserPdfSource.FromHtml("<h1>Snapshot</h1>", new Uri("https://reports.example/invoice")),
            headers: headers);

        headers["X-Correlation-Id"] = "changed";

        Assert.Equal("first", request.Headers["X-Correlation-Id"]);
    }

    [Fact]
    public void WebStorageSnapshotsPreserveCaseSensitiveKeys() {
        HtmlBrowserPdfRequest request = new(
            HtmlBrowserPdfSource.FromHtml("<p>storage</p>", new Uri("https://example.com/report")),
            localStorage: new Dictionary<string, string> { ["token"] = "lower", ["Token"] = "upper" });

        Assert.Equal(2, request.LocalStorage.Count);
        Assert.Equal("lower", request.LocalStorage["token"]);
        Assert.Equal("upper", request.LocalStorage["Token"]);
    }

    [Fact]
    public void IdnSourceOriginUsesTheBrowserCanonicalHost() {
        HtmlBrowserPdfSource source = HtmlBrowserPdfSource.FromUrl("https://bücher.example/report");

        Assert.Equal("xn--bcher-kva.example", source.SecurityOrigin!.IdnHost);
    }

    [Fact]
    public void RequestRejectsCredentialsWithoutAnHttpOrigin() {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml("<h1>No origin</h1>"),
            localStorage: new Dictionary<string, string> { ["token"] = "secret" }));

        Assert.Contains("HTTP/HTTPS base URI", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RequestKeepsNavigationAndReadinessTimeoutsIndependent() {
        HtmlBrowserPdfRequest request = new(
            HtmlBrowserPdfSource.FromHtml("<p>timeouts</p>"),
            readiness: new HtmlBrowserPdfReadiness(skipLoadState: true, selector: "p", timeout: 50),
            navigationTimeout: 2000);

        Assert.Equal(50, request.Readiness.Timeout);
        Assert.Equal(2000, request.NavigationTimeout);
    }

    [Fact]
    public void HttpsErrorOptInAlsoConfiguresTheDedicatedChromiumProcess() {
        HtmlBrowserLaunchOptions strict = new HtmlBrowserPdfRendererOptions().CreateLaunchOptions();
        HtmlBrowserLaunchOptions trusted = new HtmlBrowserPdfRendererOptions(ignoreHttpsErrors: true).CreateLaunchOptions();

        Assert.DoesNotContain("--ignore-certificate-errors", strict.BrowserArguments);
        Assert.Contains("--ignore-certificate-errors", trusted.BrowserArguments);
    }

    [Fact]
    public void BrowserTestConvenienceMethodsExposeTheHttpsOptIn() {
        string[] names = {
            nameof(HtmlBrowserTester.TestCssResourceAsync),
            nameof(HtmlBrowserTester.TestConsoleErrorsAsync),
            nameof(HtmlBrowserTester.TestPerformanceAsync)
        };

        foreach (string name in names) {
            System.Reflection.MethodInfo method = Assert.Single(typeof(HtmlBrowserTester).GetMethods(), candidate => candidate.Name == name);
            System.Reflection.ParameterInfo parameter = Assert.Single(method.GetParameters(), candidate => candidate.Name == "ignoreHttpsErrors");
            Assert.Equal(typeof(bool), parameter.ParameterType);
            Assert.Equal(false, parameter.DefaultValue);
        }
    }

    [Fact]
    public async Task PublicNetworkPolicyBlocksPrivateTargetsUnlessExplicitlyAllowed() {
        HtmlBrowserNetworkPolicyEvaluator publicOnly = new(HtmlBrowserNetworkPolicy.PublicNetworkOnly);
        HtmlBrowserNetworkPolicyEvaluator allowListed = new(new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" }));

        Assert.False(await publicOnly.IsAllowedAsync("http://127.0.0.1/report", null, CancellationToken.None));
        Assert.True(await allowListed.IsAllowedAsync("http://127.0.0.1/report", null, CancellationToken.None));
    }

    [Fact]
    public async Task SelectedFileDirectoryAllowsSiblingResourcesButNotTraversal() {
        string root = Path.Combine(Path.GetTempPath(), "HtmlTinkerX-PdfPolicy-" + Guid.NewGuid().ToString("N"));
        string sibling = Path.Combine(root, "assets", "style.css");
        string outside = root + "-outside.css";
        Directory.CreateDirectory(Path.GetDirectoryName(sibling)!);
        File.WriteAllText(sibling, "body{}");
        File.WriteAllText(outside, "body{}");
        try {
            HtmlBrowserNetworkPolicyEvaluator evaluator = new(HtmlBrowserNetworkPolicy.PublicNetworkOnly);

            Assert.True(await evaluator.IsAllowedAsync(new Uri(sibling).AbsoluteUri, root, CancellationToken.None));
            Assert.False(await evaluator.IsAllowedAsync(new Uri(outside).AbsoluteUri, root, CancellationToken.None));
        } finally {
            Directory.Delete(root, recursive: true);
            File.Delete(outside);
        }
    }

    [Theory]
    [InlineData(@"\\server\share\asset.css")]
    [InlineData("//server/share/asset.css")]
    [InlineData(@"\\?\UNC\server\share\asset.css")]
    [InlineData(@"\\.\PhysicalDrive0")]
    [InlineData(@"\??\C:\asset.css")]
    [InlineData(@"\Device\HarddiskVolume1\asset.css")]
    [InlineData("file://server/share/asset.css")]
    public void FilePathResolutionRejectsNetworkAndDevicePathsBeforeNormalization(string path) {
        Assert.True(HtmlBrowserFileSystemPath.IsNetworkOrDevicePath(path));
        Assert.False(HtmlBrowserFileSystemPath.TryResolveExistingPath(path, out _));
    }

    [Fact]
    public async Task FilePolicyRejectsRemoteFileUrisBeforePathResolution() {
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(new HtmlBrowserNetworkPolicy(
            allowFileAccess: true,
            allowedFileDirectories: new[] { Path.GetPathRoot(Path.GetFullPath("."))! }));

        Assert.False(await evaluator.IsAllowedAsync("file://server/share/asset.css", null, CancellationToken.None));
    }

#if !NETFRAMEWORK
    [Fact]
    public async Task SelectedFileDirectoryRejectsSymlinkEscape() {
        string root = Path.Combine(Path.GetTempPath(), "HtmlTinkerX-PdfPolicy-" + Guid.NewGuid().ToString("N"));
        string outside = root + "-outside";
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        string secret = Path.Combine(outside, "secret.css");
        File.WriteAllText(secret, "body{background:red}");
        string link = Path.Combine(root, "linked-assets");
        try {
            Directory.CreateSymbolicLink(link, outside);
            HtmlBrowserNetworkPolicyEvaluator evaluator = new(HtmlBrowserNetworkPolicy.PublicNetworkOnly);

            Assert.False(await evaluator.IsAllowedAsync(new Uri(Path.Combine(link, "secret.css")).AbsoluteUri, root, CancellationToken.None));
        } finally {
            if (Directory.Exists(link)) Directory.Delete(link);
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }
#endif

    [Fact]
    public async Task PublicNetworkPolicyRejectsMixedPublicAndPrivateDnsAnswers() {
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(
            HtmlBrowserNetworkPolicy.PublicNetworkOnly,
            _ => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8"), IPAddress.Loopback }));

        Assert.False(await evaluator.IsAllowedAsync("https://mixed.example/report", null, CancellationToken.None));
    }

    [Fact]
    public async Task UnicodeDeniedHostIsCanonicalizedLikeTheRequestUri() {
        HtmlBrowserNetworkPolicy policy = new(deniedHosts: new[] { "bücher.example" });
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(
            policy,
            _ => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }));

        Assert.Equal("xn--bcher-kva.example", Assert.Single(policy.DeniedHosts));
        Assert.False(await evaluator.IsAllowedAsync("https://bücher.example/report", null, CancellationToken.None));
    }

    [Fact]
    public async Task FailedDnsLookupIsEvictedSoTheWarmPolicyCanRecover() {
        int calls = 0;
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(
            HtmlBrowserNetworkPolicy.PublicNetworkOnly,
            _ => Interlocked.Increment(ref calls) == 1
                ? Task.FromException<IPAddress[]>(new SocketException())
                : Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }));

        Assert.False(await evaluator.IsAllowedAsync("https://recover.example/report", null, CancellationToken.None));
        Assert.True(await evaluator.IsAllowedAsync("https://recover.example/report", null, CancellationToken.None));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task SuccessfulDnsLookupExpiresInWarmPolicyEvaluator() {
        int calls = 0;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(
            HtmlBrowserNetworkPolicy.PublicNetworkOnly,
            _ => Task.FromResult(new[] { IPAddress.Parse(Interlocked.Increment(ref calls) == 1 ? "8.8.8.8" : "1.1.1.1") }),
            TimeSpan.FromSeconds(30),
            () => now);

        Assert.True(await evaluator.IsAllowedAsync("https://refresh.example/report", null, CancellationToken.None));
        Assert.True(await evaluator.IsAllowedAsync("https://refresh.example/report", null, CancellationToken.None));
        now = now.AddSeconds(31);
        Assert.True(await evaluator.IsAllowedAsync("https://refresh.example/report", null, CancellationToken.None));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task DnsLookupHasAnInternalDeadlineWithoutCallerCancellation() {
        TaskCompletionSource<IPAddress[]> pendingLookup = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(
            HtmlBrowserNetworkPolicy.PublicNetworkOnly,
            _ => Interlocked.Increment(ref calls) == 1
                ? pendingLookup.Task
                : Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }),
            dnsLookupTimeout: TimeSpan.FromMilliseconds(50));

        Task<bool> allowed = evaluator.IsAllowedAsync("https://timeout.example/report", null, CancellationToken.None);

        Assert.Same(allowed, await Task.WhenAny(allowed, Task.Delay(TimeSpan.FromSeconds(2))));
        Assert.False(await allowed);
        Assert.True(await evaluator.IsAllowedAsync("https://timeout.example/report", null, CancellationToken.None));
        Assert.Equal(2, calls);
        pendingLookup.TrySetResult(new[] { IPAddress.Parse("8.8.8.8") });
    }

    [Fact]
    public async Task RejectedInitialSourceDoesNotLaunchOrChargeABrowserSlot() {
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(maximumBrowserInstances: 1));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => renderer.CaptureAsync(
            new HtmlBrowserPdfRequest(HtmlBrowserPdfSource.FromUrl("http://127.0.0.1/rejected"))));

        HtmlBrowserPdfRendererMetrics metrics = renderer.GetMetricsSnapshot();
        Assert.Equal(0, metrics.BrowsersCreated);
        Assert.Equal(0, metrics.BrowsersRecycled);
        Assert.Equal(1, metrics.FailedCaptures);
    }

    [Fact]
    public async Task TimedOutDnsLookupsAreGloballyBoundedWithoutCachingGateSaturation() {
        TaskCompletionSource<IPAddress[]> pendingLookup = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        ConcurrentDictionary<string, byte> startedHosts = new(StringComparer.OrdinalIgnoreCase);
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(
            HtmlBrowserNetworkPolicy.PublicNetworkOnly,
            host => {
                Interlocked.Increment(ref calls);
                startedHosts.TryAdd(host, 0);
                return pendingLookup.Task;
            },
            dnsLookupTimeout: TimeSpan.FromMilliseconds(50));
        Task<bool>[] lookups = Enumerable.Range(0, 64)
            .Select(index => evaluator.IsAllowedAsync($"https://bounded-{index}.example/report", null, CancellationToken.None))
            .ToArray();

        bool[] results = await Task.WhenAll(lookups);

        Assert.All(results, Assert.False);
        Assert.InRange(Volatile.Read(ref calls), 1, 32);
        string saturatedHost = Enumerable.Range(0, 64)
            .Select(index => $"bounded-{index}.example")
            .First(host => !startedHosts.ContainsKey(host));
        pendingLookup.TrySetResult(new[] { IPAddress.Parse("8.8.8.8") });

        bool recovered = false;
        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
        while (!recovered && DateTime.UtcNow < deadline) {
            recovered = await evaluator.IsAllowedAsync($"https://{saturatedHost}/report", null, CancellationToken.None);
            if (!recovered) await Task.Delay(10);
        }
        Assert.True(recovered);
    }

    [Theory]
    [InlineData("192.0.2.1")]
    [InlineData("198.51.100.1")]
    [InlineData("203.0.113.1")]
    [InlineData("2001:db8::1")]
    [InlineData("64:ff9b:1::1")]
    [InlineData("100::1")]
    [InlineData("100:0:0:1::1")]
    [InlineData("2001::1")]
    [InlineData("2001:1::4")]
    [InlineData("2001:2::1")]
    [InlineData("2001:10::1")]
    [InlineData("2001:100::1")]
    [InlineData("3fff::1")]
    [InlineData("3fff:fff::1")]
    [InlineData("4000::1")]
    [InlineData("5f00::1")]
    public async Task PublicNetworkPolicyRejectsNonGloballyReachableSpecialAddresses(string address) {
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(
            HtmlBrowserNetworkPolicy.PublicNetworkOnly,
            _ => Task.FromResult(new[] { IPAddress.Parse(address) }));

        Assert.False(await evaluator.IsAllowedAsync("https://reserved.example/report", null, CancellationToken.None));
    }

    [Theory]
    [InlineData("64:ff9b::1")]
    [InlineData("2001:1::1")]
    [InlineData("2001:1::2")]
    [InlineData("2001:1::3")]
    [InlineData("2001:3::1")]
    [InlineData("2001:4:112::1")]
    [InlineData("2001:20::1")]
    [InlineData("2001:30::1")]
    [InlineData("3fff:1000::1")]
    public async Task PublicNetworkPolicyAllowsGloballyReachableIpv6SpecialAssignments(string address) {
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(
            HtmlBrowserNetworkPolicy.PublicNetworkOnly,
            _ => Task.FromResult(new[] { IPAddress.Parse(address) }));

        Assert.True(await evaluator.IsAllowedAsync("https://public.example/report", null, CancellationToken.None));
    }

    [Fact]
    public async Task PolicyProxyConnectsToTheExactAddressApprovedByTheEvaluator() {
        TcpListener origin = new(IPAddress.Loopback, 0);
        origin.Start();
        try {
            int originPort = ((IPEndPoint)origin.LocalEndpoint).Port;
            Task originTask = Task.Run(async () => {
                using TcpClient accepted = await origin.AcceptTcpClientAsync();
                using NetworkStream stream = accepted.GetStream();
                byte[] request = new byte[4096];
                int read = await stream.ReadAsync(request, 0, request.Length);
                Assert.Contains("GET /bound HTTP/1.1", Encoding.ASCII.GetString(request, 0, read), StringComparison.Ordinal);
                byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nConnection: close\r\n\r\nbound");
                await stream.WriteAsync(response, 0, response.Length);
            });
            HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "render.invalid" });
            HtmlBrowserNetworkPolicyEvaluator evaluator = new(policy, _ => Task.FromResult(new[] { IPAddress.Loopback }));
            await using HtmlBrowserPolicyProxy proxy = new(evaluator);
            Uri proxyUri = new(proxy.Server);
            using TcpClient browser = new();
            await browser.ConnectAsync(IPAddress.Loopback, proxyUri.Port);
            using NetworkStream browserStream = browser.GetStream();
            byte[] payload = Encoding.ASCII.GetBytes($"GET http://render.invalid:{originPort}/bound HTTP/1.1\r\nHost: render.invalid:{originPort}\r\n\r\n");
            await browserStream.WriteAsync(payload, 0, payload.Length);
            using MemoryStream responseBytes = new();
            await browserStream.CopyToAsync(responseBytes);

            Assert.EndsWith("bound", Encoding.ASCII.GetString(responseBytes.ToArray()), StringComparison.Ordinal);
            await originTask;
        } finally {
            origin.Stop();
        }
    }

    [Fact]
    public async Task PolicyProxyPreservesTunnelBytesCoalescedWithConnectHeaders() {
        TcpListener origin = new(IPAddress.Loopback, 0);
        origin.Start();
        try {
            int originPort = ((IPEndPoint)origin.LocalEndpoint).Port;
            byte[] tunnelPayload = Encoding.ASCII.GetBytes("coalesced-client-hello");
            Task originTask = Task.Run(async () => {
                using TcpClient accepted = await origin.AcceptTcpClientAsync();
                using NetworkStream stream = accepted.GetStream();
                byte[] received = new byte[tunnelPayload.Length];
                int offset = 0;
                while (offset < received.Length) {
                    int read = await stream.ReadAsync(received, offset, received.Length - offset);
                    if (read == 0) break;
                    offset += read;
                }
                Assert.Equal(tunnelPayload, received);
                byte[] response = Encoding.ASCII.GetBytes("tunnel-response");
                await stream.WriteAsync(response, 0, response.Length);
            });
            HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "render.invalid" });
            HtmlBrowserNetworkPolicyEvaluator evaluator = new(policy, _ => Task.FromResult(new[] { IPAddress.Loopback }));
            await using HtmlBrowserPolicyProxy proxy = new(evaluator);
            Uri proxyUri = new(proxy.Server);
            using TcpClient browser = new();
            await browser.ConnectAsync(IPAddress.Loopback, proxyUri.Port);
            using NetworkStream browserStream = browser.GetStream();
            byte[] connect = Encoding.ASCII.GetBytes($"CONNECT render.invalid:{originPort} HTTP/1.1\r\nHost: render.invalid:{originPort}\r\n\r\n");
            byte[] request = connect.Concat(tunnelPayload).ToArray();
            await browserStream.WriteAsync(request, 0, request.Length);
            using MemoryStream responseBytes = new();
            await browserStream.CopyToAsync(responseBytes);

            string responseText = Encoding.ASCII.GetString(responseBytes.ToArray());
            Assert.Contains("200 Connection Established", responseText, StringComparison.Ordinal);
            Assert.EndsWith("tunnel-response", responseText, StringComparison.Ordinal);
            await originTask;
        } finally {
            origin.Stop();
        }
    }

    [Fact]
    public async Task PolicyProxyBoundsAReversePumpAfterTheBrowserHalfCloses() {
        TcpListener origin = new(IPAddress.Loopback, 0);
        origin.Start();
        using CancellationTokenSource originLifetime = new();
        Task originTask = Task.CompletedTask;
        try {
            int originPort = ((IPEndPoint)origin.LocalEndpoint).Port;
            originTask = Task.Run(async () => {
                try {
                    using TcpClient accepted = await origin.AcceptTcpClientAsync();
                    using NetworkStream stream = accepted.GetStream();
                    byte[] payload = new byte[1];
                    Assert.Equal(1, await stream.ReadAsync(payload, 0, payload.Length));
                    try { await Task.Delay(Timeout.Infinite, originLifetime.Token); } catch (OperationCanceledException) { }
                } catch (SocketException) when (originLifetime.IsCancellationRequested) {
                } catch (ObjectDisposedException) when (originLifetime.IsCancellationRequested) {
                }
            });
            HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "render.invalid" });
            HtmlBrowserNetworkPolicyEvaluator evaluator = new(policy, _ => Task.FromResult(new[] { IPAddress.Loopback }));
            await using HtmlBrowserPolicyProxy proxy = new(evaluator, relayDrainTimeout: TimeSpan.FromMilliseconds(50));
            Uri proxyUri = new(proxy.Server);
            using TcpClient browser = new();
            await browser.ConnectAsync(IPAddress.Loopback, proxyUri.Port);
            using NetworkStream browserStream = browser.GetStream();
            byte[] connect = Encoding.ASCII.GetBytes($"CONNECT render.invalid:{originPort} HTTP/1.1\r\nHost: render.invalid:{originPort}\r\n\r\nx");
            await browserStream.WriteAsync(connect, 0, connect.Length);
            browser.Client.Shutdown(SocketShutdown.Send);

            using MemoryStream responseBytes = new();
            Task readResponse = browserStream.CopyToAsync(responseBytes);
            Assert.Same(readResponse, await Task.WhenAny(readResponse, Task.Delay(TimeSpan.FromSeconds(2))));
            await readResponse;
            Assert.Contains("200 Connection Established", Encoding.ASCII.GetString(responseBytes.ToArray()), StringComparison.Ordinal);
        } finally {
            originLifetime.Cancel();
            origin.Stop();
            await originTask;
        }
    }

    [Fact]
    public async Task PolicyProxyBoundsOutboundConnectionAttempts() {
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "render.invalid" });
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(policy, _ => Task.FromResult(new[] { IPAddress.Loopback }));
        TaskCompletionSource<bool> neverConnects = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using HtmlBrowserPolicyProxy proxy = new(
            evaluator,
            TimeSpan.FromMilliseconds(50),
            (_, _, _) => neverConnects.Task);
        Uri proxyUri = new(proxy.Server);
        using TcpClient browser = new();
        await browser.ConnectAsync(IPAddress.Loopback, proxyUri.Port);
        using NetworkStream browserStream = browser.GetStream();
        byte[] payload = Encoding.ASCII.GetBytes("GET http://render.invalid:8080/bound HTTP/1.1\r\nHost: render.invalid:8080\r\n\r\n");
        await browserStream.WriteAsync(payload, 0, payload.Length);
        using MemoryStream responseBytes = new();
        await browserStream.CopyToAsync(responseBytes);

        Assert.Contains("403 Forbidden", Encoding.ASCII.GetString(responseBytes.ToArray()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PolicyProxyFallsBackAfterAnAddressAttemptTimesOut() {
        TcpListener origin = new(IPAddress.Loopback, 0);
        origin.Start();
        try {
            int originPort = ((IPEndPoint)origin.LocalEndpoint).Port;
            Task originTask = Task.Run(async () => {
                using TcpClient accepted = await origin.AcceptTcpClientAsync();
                using NetworkStream stream = accepted.GetStream();
                byte[] request = new byte[4096];
                int read = await stream.ReadAsync(request, 0, request.Length);
                Assert.Contains("GET /fallback HTTP/1.1", Encoding.ASCII.GetString(request, 0, read), StringComparison.Ordinal);
                byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 8\r\nConnection: close\r\n\r\nfallback");
                await stream.WriteAsync(response, 0, response.Length);
            });
            IPAddress stalledAddress = IPAddress.Parse("127.0.0.2");
            HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "render.invalid" });
            HtmlBrowserNetworkPolicyEvaluator evaluator = new(
                policy,
                _ => Task.FromResult(new[] { stalledAddress, IPAddress.Loopback }));
            TaskCompletionSource<bool> neverConnects = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await using HtmlBrowserPolicyProxy proxy = new(
                evaluator,
                TimeSpan.FromSeconds(5),
                (client, address, port) => address.Equals(stalledAddress)
                    ? neverConnects.Task
                    : client.ConnectAsync(address, port));
            Uri proxyUri = new(proxy.Server);
            using TcpClient browser = new();
            await browser.ConnectAsync(IPAddress.Loopback, proxyUri.Port);
            using NetworkStream browserStream = browser.GetStream();
            byte[] payload = Encoding.ASCII.GetBytes($"GET http://render.invalid:{originPort}/fallback HTTP/1.1\r\nHost: render.invalid:{originPort}\r\n\r\n");
            await browserStream.WriteAsync(payload, 0, payload.Length);
            using MemoryStream responseBytes = new();
            await browserStream.CopyToAsync(responseBytes);

            Assert.EndsWith("fallback", Encoding.ASCII.GetString(responseBytes.ToArray()), StringComparison.Ordinal);
            await originTask;
        } finally {
            origin.Stop();
        }
    }

    [Fact]
    public void PooledPdfContractsDoNotExposePlaywrightTypes() {
        Type[] contractTypes = {
            typeof(HtmlBrowserPdfRendererOptions), typeof(HtmlBrowserPdfRequest), typeof(HtmlBrowserPdfCookie),
            typeof(HtmlBrowserPdfOptions), typeof(HtmlBrowserPdfReadiness), typeof(HtmlBrowserPdfResult)
        };

        Assert.DoesNotContain(contractTypes.SelectMany(type => type.GetProperties()), property =>
            string.Equals(property.PropertyType.Namespace, "Microsoft.Playwright", StringComparison.Ordinal));
        Assert.Equal(HtmlBrowserCookieSameSite.Strict, new HtmlBrowserPdfCookie("session", "value", url: "https://example.com", sameSite: HtmlBrowserCookieSameSite.Strict).SameSite);
    }

    [Fact]
    public void PdfCookieRejectsMixedUrlAndDomainPathScope() {
        Assert.Throws<ArgumentException>(() => new HtmlBrowserPdfCookie(
            "session",
            "value",
            url: "https://example.com",
            domain: "example.com"));
        Assert.Throws<ArgumentException>(() => new HtmlBrowserPdfCookie(
            "session",
            "value",
            url: "https://example.com",
            path: "/reports"));
    }

    [Fact]
    public void PdfFileSourceRejectsNetworkAndDevicePathsBeforeNormalization() {
        Assert.Throws<ArgumentException>(() => HtmlBrowserPdfSource.FromFile(@"\\server\share\report.html"));
        Assert.Throws<ArgumentException>(() => HtmlBrowserPdfSource.FromFile("file://server/share/report.html"));
        Assert.Throws<ArgumentException>(() => HtmlBrowserPdfSource.FromFile(@"\\?\C:\reports\report.html"));
        Assert.Throws<ArgumentException>(() => HtmlBrowserPdfSource.FromFile(@"\??\C:\reports\report.html"));
    }

    [Fact]
    public void PublicNetworkEnforcementRejectsCallerProxyWhoseDnsCannotBeBound() {
        Assert.Throws<ArgumentException>(() => new HtmlBrowserPdfRenderer(new HtmlBrowserPdfRendererOptions(proxy: "http://proxy.example:8080")));
    }

    [Fact]
    public void HostRulesRejectCallerProxyBecauseWebSocketTunnelsCannotBeEnforced() {
        HtmlBrowserNetworkPolicy policy = new(
            allowPrivateNetworks: true,
            deniedHosts: new[] { "internal.example" });

        Assert.Throws<ArgumentException>(() => new HtmlBrowserPdfRenderer(new HtmlBrowserPdfRendererOptions(
            proxy: "http://proxy.example:8080",
            networkPolicy: policy)));
    }

    [Fact]
    public void BrowserSessionsAndPooledRendererValidateHttpsByDefault() {
        Assert.False(new HtmlBrowserLaunchOptions().IgnoreHTTPSErrors);
        Assert.False(new HtmlBrowserPdfRendererOptions().IgnoreHttpsErrors);
    }
}
