using HtmlTinkerX;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Base for cmdlets that download a page with <c>-Url</c>. It owns the proxy and fetch parameters (retries, browser
/// headers, a browser fallback, a headed resume and the development replay cache) so every such cmdlet reads the web
/// the same way. Without any fetch parameter the download is the plain client it always was.
/// </summary>
public abstract class AsyncUrlCmdlet : AsyncPSCmdlet {
    /// <summary>Parameter set name of the <c>-Url</c> input.</summary>
    protected const string UrlParameterSet = "Url";

    // Reading a browser's identity launches it, so one read serves the whole process.
    private static readonly ConcurrentDictionary<string, Task<HtmlBrowserHeaderProfile>> BrowserIdentities = new(StringComparer.Ordinal);

    /// <summary>Proxy server address used when downloading by URL. Give several to send each attempt through the next one.</summary>
    [Parameter]
    public string[]? Proxy { get; set; }

    /// <summary>Credentials used with the proxy server.</summary>
    [Parameter]
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>Retries after a transient status (408, 429, 500, 502, 503, 504) or a dropped connection, honouring Retry-After up to 30 seconds. Defaults to 1 with BrowserHeaders, RenderWhenBlocked, ResumeInBrowser or several proxies, otherwise 0.</summary>
    [Parameter(ParameterSetName = UrlParameterSet)]
    [ValidateRange(0, 10)]
    public int RetryCount { get; set; }

    /// <summary>Sends a consistent set of browser headers: User-Agent, Accept, Accept-Language and client hints. With RenderWhenBlocked or ResumeInBrowser they are read from that browser, so the HTTP requests and the browser present one identity; otherwise they describe an ordinary desktop Chrome. Cannot be combined with UserAgent, or with Header values for the headers it sets.</summary>
    [Parameter(ParameterSetName = UrlParameterSet)]
    public SwitchParameter BrowserHeaders { get; set; }

    /// <summary>Renders the page in a headless browser, through the first proxy, when a Cloudflare challenge or a recognised CDN block page is returned.</summary>
    [Parameter(ParameterSetName = UrlParameterSet)]
    public SwitchParameter RenderWhenBlocked { get; set; }

    /// <summary>When the site challenges or blocks the request, opens the page in a visible browser so you can complete what the site asks, then continues with that browser's cookies for the site. Nothing is solved automatically. Combine with BrowserHeaders so the cookies are used with the browser's own identity.</summary>
    [Parameter(ParameterSetName = UrlParameterSet)]
    public SwitchParameter ResumeInBrowser { get; set; }

    /// <summary>Seconds ResumeInBrowser waits for you before the request fails as blocked.</summary>
    [Parameter(ParameterSetName = UrlParameterSet)]
    [ValidateRange(1, 3600)]
    public int ResumeTimeoutSeconds { get; set; } = (int)HtmlBrowserResumeOptions.DefaultTimeout.TotalSeconds;

    /// <summary>Chrome DevTools Protocol endpoint of your own running Chrome, used by RenderWhenBlocked and ResumeInBrowser instead of a launched browser.</summary>
    [Parameter(ParameterSetName = UrlParameterSet)]
    [Alias("CdpEndpoint", "RemoteDebuggingUrl")]
    public string? CdpEndpointUrl { get; set; }

    /// <summary>Directory of a development cache that stores whole responses and replays them without fetching the site again. Requests with cookies, credentials or custom headers are not cached. Not for production reads: stored responses never expire.</summary>
    [Parameter(ParameterSetName = UrlParameterSet)]
    public string? ReplayCache { get; set; }

    /// <summary>The first proxy given, or null.</summary>
    protected string? FirstProxy => Proxy?.FirstOrDefault(static proxy => !string.IsNullOrWhiteSpace(proxy));

    /// <summary>Rejects parameter combinations that would send contradicting requests.</summary>
    /// <param name="userAgent">The cmdlet's User-Agent override, if it has one.</param>
    /// <param name="headers">The cmdlet's header overrides, if it has them.</param>
    protected void ValidateUrlParameters(string? userAgent = null, IDictionary? headers = null) {
        ValidateProxy(FirstProxy, ProxyCredential);
        if (BrowserHeaders.IsPresent && !string.IsNullOrWhiteSpace(userAgent)) {
            ThrowTerminatingError(new ErrorRecord(
                new PSArgumentException("BrowserHeaders sets its own consistent User-Agent; do not combine it with UserAgent."),
                "BrowserHeadersWithUserAgent", ErrorCategory.InvalidArgument, userAgent));
        }
        if (BrowserHeaders.IsPresent && headers?.Keys.Cast<object>().Select(static key => key?.ToString()).FirstOrDefault(IsBrowserProfileHeader) is { } header) {
            ThrowTerminatingError(new ErrorRecord(
                new PSArgumentException($"BrowserHeaders sets its own consistent {header} and client hints; do not override them with Header."),
                "BrowserHeadersWithHeader", ErrorCategory.InvalidArgument, header));
        }
        if (RenderWhenBlocked.IsPresent && ResumeInBrowser.IsPresent) {
            ThrowTerminatingError(new ErrorRecord(
                new PSArgumentException("Choose RenderWhenBlocked or ResumeInBrowser; both decide what happens when a site blocks the request."),
                "RenderWhenBlockedWithResumeInBrowser", ErrorCategory.InvalidArgument, null));
        }
    }

    /// <summary>Creates the client that downloads <c>-Url</c>.</summary>
    /// <param name="userAgent">The cmdlet's User-Agent override, if it has one.</param>
    /// <param name="headers">The cmdlet's header overrides, if it has them.</param>
    protected async Task<HttpClient> CreateUrlClientAsync(string? userAgent = null, IDictionary? headers = null) {
        bool retryBound = MyInvocation.BoundParameters.ContainsKey(nameof(RetryCount));
        bool browserDecision = RenderWhenBlocked.IsPresent || ResumeInBrowser.IsPresent;
        bool retryByDefault = BrowserHeaders.IsPresent || browserDecision || Proxy?.Length > 1;
        if (!retryBound && !retryByDefault && string.IsNullOrWhiteSpace(ReplayCache)) {
            return HttpClientHelper.Create(FirstProxy, ProxyCredential, userAgent, headers);
        }

        HtmlBrowserHeaderProfile? identity = !BrowserHeaders.IsPresent ? null
            : browserDecision ? await ReadBrowserIdentityAsync().ConfigureAwait(false)
            : HtmlBrowserHeaderProfile.Chrome();
        HtmlFetchPolicy policy = new() {
            RetryCount = retryBound ? RetryCount : retryByDefault ? 1 : 0,
            BrowserHeaders = identity,
            ReplayCache = string.IsNullOrWhiteSpace(ReplayCache) ? null : new HtmlReplayCache(ReplayCache!.ToFullPath()),
            OnRetry = (host, reason) => WriteVerbose($"Retrying {host}: {reason}")
        };
        CookieContainer cookies = new();
        if (RenderWhenBlocked.IsPresent) {
            policy.Renderer = HtmlBrowser.CreateFetchRenderer(CreateBrowserOptions(headless: true, identity));
            policy.OnBlocked = (_, failure, _) => {
                bool render = failure.Kind is HtmlHttpFailureKind.Challenge or HtmlHttpFailureKind.BlockPage;
                WriteVerbose(render ? $"Rendering in a browser after {failure.Provider ?? failure.Kind.ToString()} blocked the request." : failure.ToString());
                return Task.FromResult(render ? HtmlBlockedAction.Render : HtmlBlockedAction.Fail);
            };
        }
        if (ResumeInBrowser.IsPresent) {
            var resume = HtmlBrowser.CreateBlockedResume(cookies, new HtmlBrowserResumeOptions {
                Browser = CreateBrowserOptions(headless: false, identity),
                Timeout = TimeSpan.FromSeconds(ResumeTimeoutSeconds)
            });
            policy.OnBlocked = (uri, failure, token) => {
                WriteWarning($"{uri.Host} returned {failure.Provider ?? failure.Kind.ToString()}. Complete what the site asks in the browser window; "
                    + $"the download continues once the page loads normally, or fails after {ResumeTimeoutSeconds} seconds or when you close the page.");
                return resume(uri, failure, token);
            };
        }
        return HttpClientHelper.CreateFetchClient(policy, Proxy, ProxyCredential, userAgent, headers, cookies);
    }

    /// <summary>
    /// The browser for a fallback or a resume. It leaves through the same proxy as the request it replaces, never
    /// directly; your own Chrome over CDP keeps its own network settings.
    /// </summary>
    private HtmlBrowserLaunchOptions CreateBrowserOptions(bool headless, HtmlBrowserHeaderProfile? identity) {
        if (!string.IsNullOrWhiteSpace(CdpEndpointUrl)) {
            return new HtmlBrowserLaunchOptions { CdpEndpointUrl = CdpEndpointUrl, HeaderProfile = identity };
        }
        NetworkCredential? proxyCredential = FirstProxy != null
            ? ProxyCredential?.GetNetworkCredential()
            : HtmlHttpClientFactory.DefaultProxyCredential as NetworkCredential;
        return new HtmlBrowserLaunchOptions {
            Headless = headless,
            HeaderProfile = identity,
            Proxy = FirstProxy ?? HtmlHttpClientFactory.DefaultProxy,
            ProxyUsername = proxyCredential?.UserName,
            ProxyPassword = proxyCredential?.Password
        };
    }

    // A headless read of the launched browser gives the identity its headed window has, without flashing a window.
    private async Task<HtmlBrowserHeaderProfile> ReadBrowserIdentityAsync() {
        HtmlBrowserLaunchOptions options = CreateBrowserOptions(headless: true, identity: null);
        string key = options.CdpEndpointUrl ?? "chromium";
        Task<HtmlBrowserHeaderProfile> read = BrowserIdentities.GetOrAdd(key, _ => HtmlBrowser.GetHeaderProfileAsync(options, acceptLanguage: null));
        try {
            return await read.ConfigureAwait(false);
        } catch {
            // A failed read is not kept, so the next command tries again.
            BrowserIdentities.TryRemove(key, out _);
            throw;
        }
    }

    // Headers the browser profile keeps consistent with each other, including client hints it does not send.
    private static bool IsBrowserProfileHeader(string? name) => name != null
        && (HtmlBrowserHeaderProfile.Chrome().Headers.Any(header => header.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
            || name.StartsWith("sec-ch-ua", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Sec-Fetch-", StringComparison.OrdinalIgnoreCase));
}
