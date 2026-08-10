using System;
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
    public void RequestSnapshotsMutableCollections() {
        Dictionary<string, string> headers = new() { ["X-Correlation-Id"] = "first" };
        HtmlBrowserPdfRequest request = new(
            HtmlBrowserPdfSource.FromHtml("<h1>Snapshot</h1>", new Uri("https://reports.example/invoice")),
            headers: headers);

        headers["X-Correlation-Id"] = "changed";

        Assert.Equal("first", request.Headers["X-Correlation-Id"]);
    }

    [Fact]
    public void RequestRejectsCredentialsWithoutAnHttpOrigin() {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromHtml("<h1>No origin</h1>"),
            localStorage: new Dictionary<string, string> { ["token"] = "secret" }));

        Assert.Contains("HTTP/HTTPS base URI", exception.Message, StringComparison.Ordinal);
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

    [Theory]
    [InlineData("192.0.2.1")]
    [InlineData("198.51.100.1")]
    [InlineData("203.0.113.1")]
    [InlineData("2001:db8::1")]
    public async Task PublicNetworkPolicyRejectsNonRoutableDocumentationAddresses(string address) {
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(
            HtmlBrowserNetworkPolicy.PublicNetworkOnly,
            _ => Task.FromResult(new[] { IPAddress.Parse(address) }));

        Assert.False(await evaluator.IsAllowedAsync("https://reserved.example/report", null, CancellationToken.None));
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
    public void PublicNetworkEnforcementRejectsCallerProxyWhoseDnsCannotBeBound() {
        Assert.Throws<ArgumentException>(() => new HtmlBrowserPdfRenderer(new HtmlBrowserPdfRendererOptions(proxy: "http://proxy.example:8080")));
    }

    [Fact]
    public void BrowserSessionsAndPooledRendererValidateHttpsByDefault() {
        Assert.False(new HtmlBrowserLaunchOptions().IgnoreHTTPSErrors);
        Assert.False(new HtmlBrowserPdfRendererOptions().IgnoreHttpsErrors);
    }
}
