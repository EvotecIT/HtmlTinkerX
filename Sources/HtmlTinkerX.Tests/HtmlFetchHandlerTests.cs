using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed class HtmlFetchHandlerTests {
    private const string AkamaiBlockPage = "<HTML><HEAD><TITLE>Access Denied</TITLE></HEAD><BODY><H1>Access Denied</H1>"
        + "You don't have permission to access \"http&#58;&#47;&#47;www&#46;example&#46;com&#47;kb&#63;session&#61;abc\" on this server.<P>"
        + "Your address is 203.0.113.7 or 2001:db8::7.<P>Reference&#32;&#35;18&#46;8f6d1402&#46;1730812345&#46;1a2b3c</BODY></HTML>";

    [Theory]
    [InlineData("azure", HtmlHttpFailureKind.BlockPage, "Azure Front Door")]
    [InlineData("akamai", HtmlHttpFailureKind.BlockPage, "Akamai")]
    [InlineData("plain", HtmlHttpFailureKind.AccessDenied, null)]
    public async Task GetText_ReportsNamedBlockPagesAndKeepsAPlainRefusalAsAccessDenied(string page, HtmlHttpFailureKind kind, string? provider) {
        using HttpClient client = new(new Callback(_ => {
            HttpResponseMessage response = new(HttpStatusCode.Forbidden) {
                Content = new StringContent(page switch {
                    "azure" => File.ReadAllText(Path.Combine(TestHelpers.ProjectRoot, "Fixtures", "microsoft-blocked-article.html")),
                    "akamai" => AkamaiBlockPage,
                    _ => "<html><head><title>Forbidden</title></head><body><h1>Access Denied</h1><p>Sign in first.</p></body></html>"
                }, Encoding.UTF8, "text/html")
            };
            // Azure Front Door tags every response with its reference; the captured fixture omits the body reference.
            if (page == "azure") response.Headers.TryAddWithoutValidation("X-Azure-Ref", "20261025T101010Z-r1abcdef");
            return response;
        }));

        HtmlHttpException failure = await Assert.ThrowsAsync<HtmlHttpException>(
            () => HtmlUtilities.GetTextWithProperEncodingAsync(client, "https://support.example/kb/1", null));

        Assert.IsAssignableFrom<HttpRequestException>(failure);
        Assert.Equal(kind, failure.Kind);
        Assert.Equal(provider, failure.Provider);
        Assert.Equal(HttpStatusCode.Forbidden, failure.Failure.StatusCode);
        if (page == "azure") {
            Assert.Equal("Service unavailable", failure.Failure.Title);
            Assert.Equal("The request is blocked.", failure.Failure.Excerpt);
        } else if (page == "akamai") {
            Assert.Equal("Access Denied", failure.Failure.Title);
            Assert.Contains("Reference #18.8f6d1402", failure.Failure.Excerpt);
            Assert.DoesNotContain("example", failure.Failure.Excerpt);
            Assert.DoesNotContain("203.0.113.7", failure.Failure.Excerpt);
            Assert.DoesNotContain("2001:db8", failure.Failure.Excerpt);
        } else {
            Assert.Null(failure.Failure.Excerpt);
        }
        Assert.DoesNotContain("support.example", failure.Message);
    }

    [Theory]
    [InlineData("GET", HttpCompletionOption.ResponseContentRead)]
    [InlineData("GET", HttpCompletionOption.ResponseHeadersRead)]
    [InlineData("POST", HttpCompletionOption.ResponseContentRead)]
    public async Task ChallengeHeadersRejectWithoutReadingTheBodyRetryingOrLeakingTheGate(string method, HttpCompletionOption completion) {
        UnreadableContent challenge = new();
        int calls = 0;
        List<string> retries = new();
        using HttpClient client = Client(new Callback(_ => ++calls == 1 ? Challenged(challenge) : new HttpResponseMessage(HttpStatusCode.OK)),
            new HtmlFetchPolicy { Gate = new HtmlHostRequestGate(1, 1), RetryDelay = TimeSpan.FromMilliseconds(10), OnRetry = (_, reason) => retries.Add(reason) });
        using HttpRequestMessage request = new(new HttpMethod(method), "https://publisher.example/source");
        if (method == "POST") request.Content = new StringContent("{}");

        HtmlHttpException failure = await Assert.ThrowsAsync<HtmlHttpException>(() => client.SendAsync(request, completion));

        Assert.Equal(HtmlHttpFailureKind.Challenge, failure.Kind);
        Assert.Equal("Cloudflare", failure.Provider);
        Assert.Equal(1, calls);
        Assert.Empty(retries);
        Assert.Equal(0, challenge.Reads);
        Assert.Equal(1, challenge.Disposals);
        // The single host and aggregate slot is reusable immediately after the typed rejection.
        using HttpResponseMessage next = await WithTimeout(client.GetAsync("https://publisher.example/next"));
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(600, 1)]
    public async Task RetryAfterIsHonouredUpToTheCap(int retryAfterSeconds, int expectedCalls) {
        int calls = 0;
        using HttpClient client = Client(new Callback(_ => ++calls == 1
            ? new HttpResponseMessage((HttpStatusCode)429) { Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(retryAfterSeconds)) } }
            : new HttpResponseMessage(HttpStatusCode.OK)), new HtmlFetchPolicy { RetryDelay = TimeSpan.Zero, MaximumRetryDelay = TimeSpan.FromSeconds(5) });
        Stopwatch elapsed = Stopwatch.StartNew();

        using HttpResponseMessage response = await client.GetAsync("https://publisher.example/feed");

        Assert.Equal(expectedCalls, calls);
        Assert.Equal(expectedCalls == 2 ? HttpStatusCode.OK : (HttpStatusCode)429, response.StatusCode);
        if (expectedCalls == 2) Assert.True(elapsed.Elapsed >= TimeSpan.FromMilliseconds(950), $"Retried after {elapsed.Elapsed}.");
    }

    [Fact]
    public async Task RedirectPolicyReturnsARefusedRedirectAndKeepsPrivateHeadersOnTheirOrigin() {
        Dictionary<string, string> redirects = new() {
            ["https://aka.example/short"] = "https://www.bing.example/?q=short",
            ["https://aka.example/known"] = "https://download.example/file"
        };
        List<string> seen = new();
        bool privateHeaderLeft = false;
        using HttpClient client = Client(new Callback(request => {
            string url = request.RequestUri!.AbsoluteUri;
            seen.Add(url);
            privateHeaderLeft |= request.RequestUri.Host != "aka.example" && request.Headers.Contains("X-Api-Key");
            return redirects.TryGetValue(url, out string? target)
                ? new HttpResponseMessage(HttpStatusCode.Found) { RequestMessage = request, Headers = { Location = new Uri(target) } }
                : new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request };
        }), new HtmlFetchPolicy { RedirectPolicy = HtmlFetchPolicy.AllowHosts(new[] { "aka.example", "download.example" }) });
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", "private-value");

        using HttpResponseMessage refused = await client.GetAsync("https://aka.example/short");
        using HttpResponseMessage followed = await client.GetAsync("https://aka.example/known");

        Assert.Equal(HttpStatusCode.Found, refused.StatusCode);
        Assert.Equal(new Uri("https://www.bing.example/?q=short"), refused.Headers.Location);
        Assert.DoesNotContain(seen, static url => url.Contains("bing"));
        Assert.Equal(HttpStatusCode.OK, followed.StatusCode);
        Assert.Equal(new Uri("https://download.example/file"), followed.RequestMessage!.RequestUri);
        Assert.False(privateHeaderLeft);
    }

#if NET5_0_OR_GREATER
    [Fact]
    public async Task StalledBodyReturnsItsHeadersAndACancelledReadReleasesTheHostSlot() {
        using HttpClient client = Client(new Callback(request => request.RequestUri!.AbsolutePath == "/slow"
            ? new HttpResponseMessage(HttpStatusCode.OK) { Headers = { ETag = new EntityTagHeaderValue("\"v1\"") }, Content = new StreamContent(new StalledStream()) }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ready") }),
            new HtmlFetchPolicy { Gate = new HtmlHostRequestGate(1, 1), ValidatorCache = new HtmlMemoryValidatorCache() });

        // A cacheable body is stored as the caller reads it, so its headers arrive before the stalled body ends.
        using (HttpResponseMessage slow = await WithTimeout(client.GetAsync("https://publisher.example/slow", HttpCompletionOption.ResponseHeadersRead))) {
            using CancellationTokenSource deadline = new(TimeSpan.FromMilliseconds(100));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WithTimeout(slow.Content.ReadAsStringAsync(deadline.Token)));
        }
        using HttpResponseMessage next = await WithTimeout(client.GetAsync("https://publisher.example/next"));

        Assert.Equal("ready", await next.Content.ReadAsStringAsync());
    }
#endif

    [Fact]
    public async Task ValidatorCacheRevalidatesAndReusesAnUnchangedBody() {
        List<string?> conditions = new();
        using HttpClient client = Client(new Callback(request => {
            conditions.Add(request.Headers.IfNoneMatch.FirstOrDefault()?.Tag);
            if (request.Headers.IfNoneMatch.Count > 0) return new HttpResponseMessage(HttpStatusCode.NotModified) { Headers = { ETag = new EntityTagHeaderValue("\"v1\"") } };
            return new HttpResponseMessage(HttpStatusCode.OK) {
                Headers = { ETag = new EntityTagHeaderValue("\"v1\"") },
                Content = new StringContent("<p>first body</p>", Encoding.UTF8, "text/html")
            };
        }), new HtmlFetchPolicy { ValidatorCache = new HtmlMemoryValidatorCache() });

        string first = await client.GetStringAsync("https://publisher.example/page");
        using HttpResponseMessage second = await client.GetAsync("https://publisher.example/page");

        Assert.Equal(new string?[] { null, "\"v1\"" }, conditions);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(first, await second.Content.ReadAsStringAsync());
        Assert.Equal("text/html", second.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("Authorization")]
    [InlineData("Vary: Accept-Language")]
    public async Task ValidatorCacheNeitherStoresNorReplaysRequesterSpecificBodies(string specific) {
        List<bool> conditional = new();
        using HttpClient client = Client(new Callback(request => {
            conditional.Add(request.Headers.IfNoneMatch.Count > 0);
            HttpResponseMessage response = new(HttpStatusCode.OK) {
                Headers = { ETag = new EntityTagHeaderValue("\"v1\"") },
                Content = new StringContent("<p>body</p>", Encoding.UTF8, "text/html")
            };
            if (specific.StartsWith("Vary")) response.Headers.Vary.Add("Accept-Language");
            return response;
        }), new HtmlFetchPolicy { ValidatorCache = new HtmlMemoryValidatorCache() });
        if (specific == "Authorization") client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "private-token");

        await client.GetStringAsync("https://publisher.example/page");
        await client.GetStringAsync("https://publisher.example/page");

        Assert.Equal(new[] { false, false }, conditional);
    }

    [Theory]
    [InlineData("cookie")]
    [InlineData("credentials")]
    [InlineData("default credentials")]
    public async Task ValidatorCacheIsNotConsultedWhenTheTransportIdentifiesTheRequester(string identity) {
        List<bool> conditional = new();
        int calls = 0;
        HttpClientHandler transport = new() { AllowAutoRedirect = false };
        if (identity == "cookie") transport.CookieContainer.Add(new Uri("https://publisher.example/"), new Cookie("session", "first-user"));
        if (identity == "credentials") transport.Credentials = new NetworkCredential("first-user", "secret");
        if (identity == "default credentials") transport.UseDefaultCredentials = true;
        using HttpClient client = Client(new Responder(request => {
            conditional.Add(request.Headers.IfNoneMatch.Count > 0);
            if (request.Headers.IfNoneMatch.Count > 0) return new HttpResponseMessage(HttpStatusCode.NotModified) { Headers = { ETag = new EntityTagHeaderValue("\"v1\"") } };
            return new HttpResponseMessage(HttpStatusCode.OK) {
                Headers = { ETag = new EntityTagHeaderValue("\"v1\"") },
                Content = new StringContent(++calls == 1 ? "first user's page" : "second user's page")
            };
        }, transport), new HtmlFetchPolicy { ValidatorCache = new HtmlMemoryValidatorCache() });

        await client.GetStringAsync("https://publisher.example/account");
        string second = await client.GetStringAsync("https://publisher.example/account");

        Assert.Equal(new[] { false, false }, conditional);
        Assert.Equal("second user's page", second);
    }

    [Theory]
    [InlineData("https://elsewhere.example/article")]
    [InlineData("http://publisher.example/article")]
    public async Task OnBlockedRenderRejectsAFinalAddressARedirectCouldNotReach(string final) {
        using HttpClient client = Client(new Callback(_ => new HttpResponseMessage(HttpStatusCode.Forbidden) {
            Content = new StringContent(AkamaiBlockPage, Encoding.UTF8, "text/html")
        }), new HtmlFetchPolicy {
            RedirectPolicy = HtmlFetchPolicy.AllowHosts(new[] { "publisher.example" }),
            OnBlocked = (_, _, _) => Task.FromResult(HtmlBlockedAction.Render),
            Renderer = (_, _) => Task.FromResult(new HtmlRenderedPage("<main>Elsewhere</main>", new Uri(final)))
        });

        HttpRequestException refused = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("https://publisher.example/article"));

        Assert.IsNotType<HtmlHttpException>(refused);
    }

    [Fact]
    public async Task NotModifiedWithAnEntityTagCannotConfirmABodyStoredWithoutOne() {
        int calls = 0;
        using HttpClient client = Client(new Callback(request => {
            if (request.Headers.IfModifiedSince != null) return new HttpResponseMessage(HttpStatusCode.NotModified) { Headers = { ETag = new EntityTagHeaderValue("\"v2\"") } };
            HttpResponseMessage response = new(HttpStatusCode.OK) { Content = new StringContent(++calls == 1 ? "old" : "new") };
            response.Content.Headers.LastModified = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
            return response;
        }), new HtmlFetchPolicy { ValidatorCache = new HtmlMemoryValidatorCache() });

        await client.GetStringAsync("https://publisher.example/page");
        string second = await client.GetStringAsync("https://publisher.example/page");

        Assert.Equal("new", second);
    }

    [Fact]
    public async Task ClassifyKeepsTheStatusFailureWhenTheBodyBreaksOff() {
        using HttpResponseMessage response = new(HttpStatusCode.Forbidden) { Content = new StreamContent(new BrokenStream()) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");

        HtmlHttpFailure? failure = await HtmlResponseClassifier.ClassifyAsync(response);

        Assert.Equal(HtmlHttpFailureKind.AccessDenied, failure?.Kind);
    }

    [Fact]
    public async Task ProxyRotatorSendsEachAttemptThroughTheNextHandlerOrTheSelectedOne() {
        List<int> used = new();
        HttpMessageHandler Proxy(int index) => new Callback(_ => {
            used.Add(index);
            return new HttpResponseMessage(used.Count == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK);
        });
        using (HttpClient client = Client(new HtmlProxyRotator(new[] { Proxy(0), Proxy(1), Proxy(2) }), new HtmlFetchPolicy { RetryDelay = TimeSpan.Zero })) {
            using HttpResponseMessage retried = await client.GetAsync("https://publisher.example/a");
            using HttpResponseMessage next = await client.GetAsync("https://publisher.example/b");
            Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        }
        Assert.Equal(new[] { 0, 1, 2 }, used);

        used.Clear();
        using HttpClient selected = Client(new HtmlProxyRotator(new[] { Proxy(0), Proxy(1) }, (request, count) => request.RequestUri!.AbsolutePath == "/b" ? 1 : 0),
            new HtmlFetchPolicy { RetryCount = 0 });
        (await selected.GetAsync("https://publisher.example/b")).Dispose();
        (await selected.GetAsync("https://publisher.example/a")).Dispose();
        Assert.Equal(new[] { 1, 0 }, used);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task OnBlockedRenderReturnsTheRenderedPageAndItsAddressInsteadOfTheBlockPage(HttpStatusCode status) {
        List<HtmlHttpFailure> blocked = new();
        List<Uri> rendered = new();
        Uri moved = new("https://publisher.example/articles/1");
        using HttpClient client = Client(new Callback(_ => new HttpResponseMessage(status) {
            Content = new StringContent(AkamaiBlockPage, Encoding.UTF8, "text/html")
        }), new HtmlFetchPolicy {
            RetryDelay = TimeSpan.Zero,
            OnBlocked = (_, failure, _) => { blocked.Add(failure); return Task.FromResult(HtmlBlockedAction.Render); },
            Renderer = (uri, _) => { rendered.Add(uri); return Task.FromResult(new HtmlRenderedPage("<main>Rendered article</main>", moved)); }
        });

        HtmlHttpTextResult result = await HtmlUtilities.GetTextWithProperEncodingAsync(client, "https://publisher.example/article", null);

        Assert.Equal("<main>Rendered article</main>", result.Content);
        Assert.Equal(moved, result.FinalUri);
        HtmlHttpFailure failure = Assert.Single(blocked);
        Assert.Equal(HtmlHttpFailureKind.BlockPage, failure.Kind);
        Assert.Equal("Akamai", failure.Provider);
        Assert.Equal(new Uri("https://publisher.example/article"), Assert.Single(rendered));
    }

    [Fact]
    public async Task AdaptiveGateDefersAThrottledHostBacksOffFurtherAndDecaysAfterSuccesses() {
        using HtmlHostRequestGate gate = new(perHost: 1, maxConcurrentRequests: 4);
        Uri host = new("https://publisher.example/page");
        using HttpResponseMessage throttled = new((HttpStatusCode)429) { Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(300)) } };
        using HttpResponseMessage unavailable = new(HttpStatusCode.ServiceUnavailable);
        using HttpResponseMessage ok = new(HttpStatusCode.OK);

        gate.ReportResponse(host, throttled);
        TimeSpan deferred = await TimeEnterAsync(gate, host);
        gate.ReportResponse(host, unavailable);
        Stopwatch backingOff = Stopwatch.StartNew();
        Task<IDisposable> backedOffEntry = gate.EnterAsync(host);
        // Another origin is admitted while the throttled one is still held back.
        using (await WithTimeout(gate.EnterAsync(new Uri("https://other.example/")))) Assert.False(backedOffEntry.IsCompleted);
        (await WithTimeout(backedOffEntry)).Dispose();
        TimeSpan backedOff = backingOff.Elapsed;
        for (int success = 0; success < 6; success++) {
            gate.ReportResponse(host, ok);
            await TimeEnterAsync(gate, host);
        }
        TimeSpan decayed = await TimeEnterAsync(gate, host);

        Assert.True(deferred >= TimeSpan.FromMilliseconds(280), $"Deferred {deferred}.");
        Assert.True(backedOff >= TimeSpan.FromMilliseconds(560), $"Backed off {backedOff}.");
        Assert.True(decayed < TimeSpan.FromMilliseconds(100), $"Decayed delay {decayed}.");
    }

    [Fact]
    public async Task SameOriginSpacingIsMeasuredFromActualStartsWhenTheProcessBudgetWasFull() {
        using HtmlHostRequestGate gate = new(perHost: 2, maxConcurrentRequests: 1) { MinimumDelay = TimeSpan.FromMilliseconds(300) };
        Uri host = new("https://publisher.example/page");
        IDisposable busy = await gate.EnterAsync(new Uri("https://other.example/"));
        Task<IDisposable> first = gate.EnterAsync(host);
        Task<IDisposable> second = gate.EnterAsync(host);
        // Long enough for both starts to have been due while every slot was taken.
        await Task.Delay(500);

        busy.Dispose();
        (await WithTimeout(first)).Dispose();
        Stopwatch gap = Stopwatch.StartNew();
        (await WithTimeout(second)).Dispose();

        Assert.True(gap.Elapsed >= TimeSpan.FromMilliseconds(280), $"Second start followed after {gap.Elapsed}.");
    }

    private static async Task<TimeSpan> TimeEnterAsync(HtmlHostRequestGate gate, Uri uri) {
        Stopwatch elapsed = Stopwatch.StartNew();
        using (await gate.EnterAsync(uri)) return elapsed.Elapsed;
    }

    private static HttpClient Client(HttpMessageHandler inner, HtmlFetchPolicy policy) =>
        new(new HtmlFetchHandler(policy, inner));

    private static async Task<T> WithTimeout<T>(Task<T> task) {
        Assert.Same(task, await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5))));
        return await task;
    }

    private static HttpResponseMessage Challenged(HttpContent body) {
        HttpResponseMessage response = new(HttpStatusCode.Forbidden) { Content = body };
        response.Headers.Add("cf-mitigated", "challenge");
        return response;
    }

    private sealed class Callback(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    /// <summary>Answers in place of the network while a real primary handler, with its cookie container, sits below it.</summary>
    private sealed class Responder(Func<HttpRequestMessage, HttpResponseMessage> respond, HttpMessageHandler transport) : DelegatingHandler(transport) {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private sealed class StalledStream : MemoryStream {
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
#if NET5_0_OR_GREATER
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
#endif
    }

    private sealed class BrokenStream : MemoryStream {
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("The connection was reset.");
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            throw new IOException("The connection was reset.");
#if NET5_0_OR_GREATER
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new IOException("The connection was reset.");
#endif
    }

    private sealed class UnreadableContent : HttpContent {
        internal int Reads, Disposals;
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) {
            Reads++;
            throw new InvalidOperationException("Challenge body must not be read");
        }
        protected override Task<Stream> CreateContentReadStreamAsync() {
            Reads++;
            throw new InvalidOperationException("Challenge body must not be read");
        }
        protected override void Dispose(bool disposing) {
            if (disposing) Disposals++;
            base.Dispose(disposing);
        }
    }
}
