using Microsoft.Playwright;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlBrowser {
    private static void ValidateSessionNetworkPolicy(HtmlBrowserLaunchOptions options) {
        if (options.NetworkPolicy == null) return;
        if (options.Browser != HtmlBrowserEngine.Chromium
            || !string.IsNullOrWhiteSpace(options.CdpEndpointUrl)
            || !string.IsNullOrWhiteSpace(options.UserDataDirectory)
            || !string.IsNullOrWhiteSpace(options.Proxy)
            || !string.IsNullOrWhiteSpace(options.ProxyUsername)
            || !string.IsNullOrWhiteSpace(options.ProxyPassword)
            || options.BrowserArguments.Count != 0) {
            throw new ArgumentException("NetworkPolicy requires an owned non-persistent Chromium session without CDP, an external proxy, or custom browser arguments.", nameof(options));
        }
    }

    private static async Task InstallSessionNetworkPolicyAsync(
        IBrowserContext context,
        HtmlBrowserNetworkPolicy policy,
        CancellationToken policyLifetimeToken) {
        HtmlBrowserNetworkPolicyEvaluator evaluator = new(policy);
        await context.RouteAsync("**/*", async route => {
            bool allowed;
            try {
                allowed = await evaluator.IsAllowedAsync(route.Request.Url, null, policyLifetimeToken).ConfigureAwait(false);
            } catch (OperationCanceledException) when (policyLifetimeToken.IsCancellationRequested) {
                allowed = false;
            }
            if (allowed) await route.ContinueAsync().ConfigureAwait(false);
            else await route.AbortAsync("blockedbyclient").ConfigureAwait(false);
        }).ConfigureAwait(false);

        // HTTP routing does not intercept WebSockets. Do not connect the server side:
        // this session mode deliberately supports document/HTTP acquisition only.
        await context.RouteWebSocketAsync("**/*", socket => {
            Task close = socket.CloseAsync(new WebSocketRouteCloseOptions { Code = 1008, Reason = "Restricted browser sessions do not support WebSockets." });
            _ = close.ContinueWith(static task => _ = task.Exception, TaskContinuationOptions.OnlyOnFaulted);
        }).ConfigureAwait(false);
    }

    private static void ApplySessionPolicyLaunchOptions(BrowserTypeLaunchOptions launchOptions, string proxyServer) {
        launchOptions.Proxy = new Proxy { Server = proxyServer };
        launchOptions.Args = new[] {
            "--proxy-bypass-list=<-loopback>",
            "--force-webrtc-ip-handling-policy=disable_non_proxied_udp",
            "--disable-quic"
        };
    }
}

// Setup is an operation; this owner survives it until the returned session is
// disposed. In particular, completed launch deadlines must not cancel later DNS.
internal sealed class HtmlBrowserSessionNetworkPolicyLifetime : IAsyncDisposable {
    private readonly CancellationTokenSource _lifetime = new();
    private int _cancelled;
    private int _disposed;

    internal HtmlBrowserSessionNetworkPolicyLifetime(HtmlBrowserNetworkPolicy policy) {
        try {
            Proxy = new HtmlBrowserPolicyProxy(policy);
        } catch {
            _lifetime.Dispose();
            throw;
        }
    }

    internal HtmlBrowserPolicyProxy Proxy { get; }
    internal CancellationToken Token => _lifetime.Token;

    internal void Cancel() {
        if (Interlocked.Exchange(ref _cancelled, 1) == 0) _lifetime.Cancel();
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try {
            Cancel();
            await Proxy.DisposeAsync().ConfigureAwait(false);
        } finally {
            _lifetime.Dispose();
        }
    }
}
