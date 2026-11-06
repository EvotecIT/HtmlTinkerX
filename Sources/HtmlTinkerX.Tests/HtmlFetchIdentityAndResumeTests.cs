using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Fact]
    public async Task BrowserFallbackSendsTheSameIdentityAsTheHttpRequestItReplaces() {
        ConcurrentQueue<(string UserAgent, string? Language, string? ClientHints)> pageRequests = new();
        string? browserLocale = null;
        using HttpListener server = StartFlexibleServer(async context => {
            HttpListenerRequest request = context.Request;
            switch (request.Url!.AbsolutePath) {
                case "/page":
                    pageRequests.Enqueue((request.UserAgent ?? string.Empty, request.Headers["Accept-Language"], request.Headers["sec-ch-ua"]));
                    if (pageRequests.Count == 1) {
                        context.Response.StatusCode = 403;
                        context.Response.Headers["cf-mitigated"] = "challenge";
                        return;
                    }
                    await RespondAsync(context, "<main>Article</main><script>fetch('/locale?value=' + navigator.language)</script>");
                    return;
                case "/locale":
                    browserLocale = request.QueryString["value"];
                    return;
            }
        }, out string root);
        HtmlBrowserHeaderProfile identity = await HtmlBrowser.GetHeaderProfileAsync(acceptLanguage: "pl-PL,pl;q=0.9,en;q=0.8");
        HtmlFetchPolicy policy = new() {
            RetryCount = 0,
            BrowserHeaders = identity,
            OnBlocked = (_, _, _) => Task.FromResult(HtmlBlockedAction.Render),
            Renderer = HtmlBrowser.CreateFetchRenderer(new HtmlBrowserLaunchOptions { HeaderProfile = identity })
        };
        using HttpClient client = HtmlHttpClientFactory.Create(policy);

        string html = await client.GetStringAsync(root + "page");

        Assert.Contains("Article", html);
        Assert.DoesNotContain("Headless", identity.UserAgent);
        Assert.Equal(2, pageRequests.Count);
        Assert.All(pageRequests, sent => {
            Assert.Equal(identity.UserAgent, sent.UserAgent);
            Assert.Equal("pl-PL,pl;q=0.9,en;q=0.8", sent.Language);
            Assert.Equal(identity.Headers.Single(header => header.Key == "sec-ch-ua").Value, sent.ClientHints);
        });
        Assert.Equal("pl-PL", browserLocale);
    }

    [Fact]
    public async Task BlockedResumeHandsTheBrowserCookiesToTheNextRequestAfterThePersonFinishes() {
        ConcurrentQueue<string?> httpCookies = new();
        using HttpListener server = StartFlexibleServer(async context => {
            if (context.Request.Url!.AbsolutePath != "/page") return;
            string? cookie = context.Request.Headers["Cookie"];
            bool fromBrowser = context.Request.Headers["Sec-Fetch-Mode"] != null;
            if (!fromBrowser) httpCookies.Enqueue(cookie);
            if (cookie?.Contains("clearance=ok") == true) {
                await RespondAsync(context, "<main>Article</main>");
                return;
            }
            // Stands in for a site's check: the browser that shows the page earns a cookie, the HTTP client gets the challenge.
            context.Response.StatusCode = 403;
            context.Response.Headers["cf-mitigated"] = "challenge";
            await RespondAsync(context, "<script>document.cookie = 'clearance=ok; path=/'</script>");
        }, out string root);
        CookieContainer cookies = new();
        int waits = 0;
        HtmlFetchPolicy policy = new() {
            OnBlocked = HtmlBrowser.CreateBlockedResume(cookies, new HtmlBrowserResumeOptions {
                Browser = new HtmlBrowserLaunchOptions { Headless = true },
                WaitForPerson = (_, _) => Task.FromResult(++waits > 0)
            })
        };
        using HttpClient client = HtmlHttpClientFactory.Create(policy, new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = cookies });

        string html = await client.GetStringAsync(root + "page");

        Assert.Contains("Article", html);
        Assert.Equal(1, waits);
        Assert.Equal(new string?[] { null, "clearance=ok" }, httpCookies.ToArray());
    }

    [Fact]
    public async Task ReplayCacheReplaysAStoredResponseWithoutTheNetworkAndKeepsItsCookiesOffDisk() {
        int hits = 0;
        string directory = Path.Combine(Path.GetTempPath(), "htmltinkerx-replay-" + Guid.NewGuid().ToString("N"));
        HttpListener server = StartFlexibleServer(async context => {
            if (context.Request.Url!.AbsolutePath == "/start") {
                context.Response.StatusCode = 302;
                context.Response.Headers["Location"] = "/doc";
                return;
            }
            int hit = Interlocked.Increment(ref hits);
            context.Response.Headers["X-Version"] = hit.ToString();
            context.Response.Headers["Set-Cookie"] = "session=secret; path=/";
            await RespondAsync(context, $"<p>v{hit}</p>");
        }, out string root);
        try {
            using (HttpClient first = HtmlHttpClientFactory.Create(new HtmlFetchPolicy { RetryCount = 0, ReplayCache = new HtmlReplayCache(directory) })) {
                Assert.Equal("<p>v1</p>", await first.GetStringAsync(root + "start"));
            }
            server.Close();

            using HttpClient replay = HtmlHttpClientFactory.Create(new HtmlFetchPolicy { RetryCount = 0, ReplayCache = new HtmlReplayCache(directory) });
            using HttpResponseMessage response = await replay.GetAsync(root + "start");

            Assert.Equal("<p>v1</p>", await response.Content.ReadAsStringAsync());
            Assert.Equal(new Uri(root + "doc"), response.RequestMessage!.RequestUri);
            Assert.Equal("1", response.Headers.GetValues("X-Version").Single());
            Assert.False(response.Headers.Contains("Set-Cookie"));
            Assert.DoesNotContain("secret", File.ReadAllText(Directory.GetFiles(directory).Single()));
            Assert.Equal(1, hits);
        } finally {
            server.Close();
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("header")]
    [InlineData("transport")]
    public async Task ReplayCacheSkipsRequestsCarryingRequesterStateUnlessAskedTo(string identity) {
        int hits = 0;
        string directory = Path.Combine(Path.GetTempPath(), "htmltinkerx-replay-" + Guid.NewGuid().ToString("N"));
        using HttpListener server = StartFlexibleServer(context => RespondAsync(context, $"<p>v{Interlocked.Increment(ref hits)}</p>"), out string root);
        async Task<string> ReadAsync(bool includeRequesterState) {
            CookieContainer cookies = new();
            if (identity == "transport") cookies.Add(new Uri(root), new Cookie("session", "1"));
            HtmlFetchPolicy policy = new() { RetryCount = 0, ReplayCache = new HtmlReplayCache(directory) { IncludeRequesterState = includeRequesterState } };
            using HttpClient client = HtmlHttpClientFactory.Create(policy, new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = cookies });
            using HttpRequestMessage request = new(HttpMethod.Get, root + "account");
            if (identity == "header") request.Headers.TryAddWithoutValidation("Authorization", "Bearer token");
            using HttpResponseMessage response = await client.SendAsync(request);
            return await response.Content.ReadAsStringAsync();
        }
        try {
            Assert.Equal("<p>v1</p>", await ReadAsync(includeRequesterState: false));
            Assert.Equal("<p>v2</p>", await ReadAsync(includeRequesterState: false));
            Assert.False(Directory.Exists(directory) && Directory.GetFiles(directory).Length > 0);

            Assert.Equal("<p>v3</p>", await ReadAsync(includeRequesterState: true));
            Assert.Equal("<p>v3</p>", await ReadAsync(includeRequesterState: true));
            Assert.Equal(3, hits);
        } finally {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
