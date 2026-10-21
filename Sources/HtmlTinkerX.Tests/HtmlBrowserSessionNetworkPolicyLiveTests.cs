#if !NETFRAMEWORK
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Playwright collection")]
public sealed class HtmlBrowserSessionNetworkPolicyLiveTests {
    [Fact]
    public async Task CancellingCompletedSessionCreationDoesNotBlockLaterPermittedDnsRequests() {
        await using NetworkFixture fixture = await NetworkFixture.StartAsync();
        using CancellationTokenSource setup = new();
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionForNavigationAsync(new HtmlBrowserLaunchOptions {
            ChromiumSandbox = true,
            LoadState = HtmlBrowserLoadState.DomContentLoaded,
            NetworkPolicy = new HtmlBrowserNetworkPolicy(new[] { "localhost" }, requireHttps: false, allowPrivateNetworks: true)
        }, setup.Token);
        setup.Cancel();

        // The initial blank session did not resolve localhost. This request must
        // perform its own policy/DNS work after the setup operation has ended.
        await HtmlBrowser.NavigateAsync(session, fixture.Url.Replace("127.0.0.1", "localhost") + "/after-setup", HtmlBrowserLoadState.DomContentLoaded);

        Assert.Equal("restricted fixture", await session.Page.TitleAsync());
        Assert.Contains("localhost:/after-setup", fixture.Requests.Keys);
    }

    [Fact]
    public async Task RestrictedSessionEnforcesRealRedirectSubresourcePopupWorkerAndWebSocketBoundaries() {
        await using NetworkFixture fixture = await NetworkFixture.StartAsync();
        using (System.Net.Http.HttpClient control = new(new System.Net.Http.HttpClientHandler { UseProxy = false })) {
            Assert.Contains("restricted fixture", await control.GetStringAsync(fixture.Url + "/control"));
        }
        HtmlBrowserLaunchOptions options = new() {
            ChromiumSandbox = true,
            IgnoreHTTPSErrors = true, // Explicit HTTP fixture mode retains its legacy opt-in.
            LoadState = HtmlBrowserLoadState.DomContentLoaded,
            NetworkPolicy = new HtmlBrowserNetworkPolicy(new[] { "127.0.0.1" }, requireHttps: false, allowPrivateNetworks: true)
        };
        HtmlBrowserSession session = await HtmlBrowser.OpenSessionForNavigationAsync(options);
        IBrowser browser = session.Browser!;
        HtmlBrowserSessionNetworkPolicyLifetime policyLifetime = (HtmlBrowserSessionNetworkPolicyLifetime)session.NetworkPolicyLifetime!;
        Uri managedProxy = new(policyLifetime.Proxy.Server);
        CancellationToken policyToken = policyLifetime.Token;
        TaskCompletionSource<bool> popupBlocked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Context.RequestFailed += (_, request) => {
            if (request.Url.Contains("/forbidden-popup", StringComparison.Ordinal)) popupBlocked.TrySetResult(true);
        };
        try {
            await session.Page.GotoAsync(fixture.Url + "/start");
            Assert.Equal("restricted fixture", await session.Page.TitleAsync());
            Task<IResponse> selected = session.Page.WaitForResponseAsync(response => response.Url == fixture.Url + "/api");
            string result = await session.Page.EvaluateAsync<string>(@"async ({ good, bad }) => {
                const allowed = await (await fetch(good + '/api')).text();
                const reject = async url => { try { await fetch(url); return false; } catch { return true; } };
                const otherHost = await reject(bad + '/forbidden-fetch');
                const redirect = await reject(good + '/redirect');
                const script = await new Promise(resolve => { const s = document.createElement('script'); s.src = bad + '/forbidden-script'; s.onload = () => resolve(false); s.onerror = () => resolve(true); document.head.append(s); });
                const image = await new Promise(resolve => { const i = new Image(); i.onload = () => resolve(false); i.onerror = () => resolve(true); i.src = bad + '/forbidden-image'; });
                const socket = await new Promise(resolve => { const ws = new WebSocket(good.replace('http:', 'ws:') + '/forbidden-socket'); ws.onopen = () => { ws.close(); resolve(false); }; ws.onerror = () => resolve(true); ws.onclose = () => resolve(true); });
                let serviceWorker = false;
                try { const registration = await navigator.serviceWorker.register('/forbidden-worker.js'); serviceWorker = !registration; } catch { serviceWorker = true; }
                window.open(bad + '/forbidden-popup');
                return JSON.stringify({ allowed, otherHost, redirect, script, image, socket, serviceWorker });
            }", new { good = fixture.Url, bad = fixture.Url.Replace("127.0.0.1", "localhost") });

            using System.Text.Json.JsonDocument outcomes = System.Text.Json.JsonDocument.Parse(result);
            Assert.Equal("genuine response", outcomes.RootElement.GetProperty("allowed").GetString());
            foreach (string name in new[] { "otherHost", "redirect", "script", "image", "socket", "serviceWorker" }) {
                Assert.True(outcomes.RootElement.GetProperty(name).GetBoolean(), name);
            }
            await popupBlocked.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Contains("127.0.0.1:/api", fixture.Requests.Keys);
            Assert.Equal("genuine response", System.Text.Encoding.UTF8.GetString(await HtmlBrowser.ReadResponseBodyAsync(await selected, 128)));
            Assert.DoesNotContain(fixture.Requests.Keys, path => path.Contains("forbidden", StringComparison.Ordinal));

            IPage additional = await session.Context.NewPageAsync();
            await Assert.ThrowsAsync<PlaywrightException>(() => additional.GotoAsync(fixture.Url.Replace("127.0.0.1", "localhost") + "/forbidden-new-page"));
            Assert.DoesNotContain(fixture.Requests.Keys, path => path.Contains("forbidden", StringComparison.Ordinal));
        } finally {
            await session.DisposeAsync();
        }
        Assert.False(browser.IsConnected);
        Assert.True(policyToken.IsCancellationRequested);
        Assert.Null(session.NetworkPolicyLifetime);
        using (System.Net.Sockets.TcpClient connection = new()) {
            await Assert.ThrowsAsync<System.Net.Sockets.SocketException>(() => connection.ConnectAsync(managedProxy.Host, managedProxy.Port));
        }
        Assert.Null(options.Proxy); // Managed enforcement never rewrites caller options.
        Assert.Empty(options.BrowserArguments);
    }

    [Fact]
    public async Task PublicPolicyDoesNotPromoteStrictLoopbackHostsToPrivateExceptionsInARealBrowser() {
        await using NetworkFixture fixture = await NetworkFixture.StartAsync();
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionForNavigationAsync(new HtmlBrowserLaunchOptions {
            ChromiumSandbox = true,
            NetworkPolicy = new HtmlBrowserNetworkPolicy(new[] { "127.0.0.1" }, requireHttps: false)
        });

        await Assert.ThrowsAsync<PlaywrightException>(() => session.Page.GotoAsync(fixture.Url + "/forbidden-private"));

        Assert.Empty(fixture.Requests);
    }

    [Fact]
    public async Task HttpsRequirementBlocksAnOtherwiseAllowedPrivateFixtureBeforeItReceivesARequest() {
        await using NetworkFixture fixture = await NetworkFixture.StartAsync();
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionForNavigationAsync(new HtmlBrowserLaunchOptions {
            ChromiumSandbox = true,
            NetworkPolicy = new HtmlBrowserNetworkPolicy(new[] { "127.0.0.1" }, allowPrivateNetworks: true)
        });
        await Assert.ThrowsAsync<PlaywrightException>(() => session.Page.GotoAsync(fixture.Url + "/forbidden-http"));
        Assert.Empty(fixture.Requests);
    }

    private sealed class NetworkFixture : IAsyncDisposable {
        private readonly WebApplication _app;
        internal string Url { get; private set; } = string.Empty;
        internal ConcurrentDictionary<string, int> Requests { get; } = new();

        private NetworkFixture(WebApplication app) {
            _app = app;
        }

        internal static async Task<NetworkFixture> StartAsync() {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
            WebApplication app = builder.Build();
            NetworkFixture fixture = new(app);
            app.Run(async context => {
                string path = context.Request.Path.ToString();
                fixture.Requests.AddOrUpdate(context.Request.Host.Host + ":" + path, 1, (_, count) => count + 1);
                if (path == "/redirect") {
                    context.Response.Redirect(fixture.Url.Replace("127.0.0.1", "localhost") + "/forbidden-redirect");
                    return;
                }
                context.Response.ContentType = path == "/api" ? "text/plain" : "text/html";
                await context.Response.WriteAsync(path == "/api" ? "genuine response" : "<!doctype html><title>restricted fixture</title><main>ready</main>");
            });
            try {
                await app.StartAsync();
                fixture.Url = app.Urls.Single();
                return fixture;
            } catch {
                await fixture.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync() {
            await _app.DisposeAsync();
        }
    }
}
#endif
