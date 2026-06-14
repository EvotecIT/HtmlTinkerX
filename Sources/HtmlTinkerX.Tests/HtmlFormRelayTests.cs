using Microsoft.AspNetCore.Http;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace HtmlTinkerX.Tests;

public class HtmlFormRelayTests {
    [Fact]
    public void TryParse_DetectsWsFederationHiddenFormRelay() {
        string html = """
<html>
<body>
<form method="POST" name="hiddenform" action="/signinws">
<input type="hidden" name="wa" value="signin1.0">
<input type="hidden" name="wresult" value="redacted">
<input type="hidden" name="wctx" value="redacted">
</form>
<script>window.setTimeout('document.forms[0].submit()', 0);</script>
</body>
</html>
""";

        bool parsed = HtmlFormRelayParser.TryParse(html, new Uri("https://example.org/start"), out HtmlFormRelayRequest? request);

        Assert.True(parsed);
        Assert.NotNull(request);
        Assert.Equal("https://example.org/signinws", request!.ActionUri.AbsoluteUri);
        Assert.Equal(FormMethod.Post, request.Method);
        Assert.Equal(HtmlFormRelayProtocolHint.WsFederation, request.ProtocolHint);
        Assert.Contains("wresult", request.FieldNames);
    }

    [Fact]
    public void TryParse_ResolvesRelativeActionAgainstDocumentBase() {
        string html = """
<html>
<head><base href="https://idp.example.org/sso/"></head>
<body>
<form method="POST" name="hiddenform" action="continue">
<input type="hidden" name="SAMLResponse" value="redacted">
<input type="hidden" name="RelayState" value="state">
</form>
<script>document.forms[0].submit()</script>
</body>
</html>
""";

        bool parsed = HtmlFormRelayParser.TryParse(html, new Uri("https://rp.example.org/start"), out HtmlFormRelayRequest? request);

        Assert.True(parsed);
        Assert.NotNull(request);
        Assert.Equal("https://idp.example.org/sso/continue", request!.ActionUri.AbsoluteUri);
    }

    [Fact]
    public void TryParse_RejectsGenericHiddenFormWhenSubmitDoesNotTargetForm() {
        string html = """
<html>
<body>
<form method="POST" name="a" action="/continue">
<input type="hidden" name="csrf" value="redacted">
</form>
<script>const data = { submit: function() {} }; data.submit(); const marker = "a";</script>
</body>
</html>
""";

        bool parsed = HtmlFormRelayParser.TryParse(html, new Uri("https://example.org/start"), out HtmlFormRelayRequest? request);

        Assert.False(parsed);
        Assert.Null(request);
    }

    [Fact]
    public void TryParse_PreservesDuplicateFieldValuesInSourceOrder() {
        string html = """
<html>
<body>
<form method="POST" name="hiddenform" action="/continue">
<input type="hidden" name="SAMLResponse" value="redacted">
<input type="hidden" name="scope" value="openid">
<input type="hidden" name="scope" value="profile">
</form>
<script>document.forms['hiddenform'].submit()</script>
</body>
</html>
""";

        bool parsed = HtmlFormRelayParser.TryParse(html, new Uri("https://example.org/start"), out HtmlFormRelayRequest? request);

        Assert.True(parsed);
        Assert.NotNull(request);
        Assert.Equal(new[] { "SAMLResponse", "scope", "scope" }, request!.FieldNames);
        Assert.Equal(new[] { "openid", "profile" }, request.FieldValues.Where(field => field.Key == "scope").Select(field => field.Value).ToArray());
        Assert.Equal("profile", request.Fields["scope"]);
    }

    [Fact]
    public async Task FollowAsync_SubmitsDuplicateFieldValues() {
        string serverBase = string.Empty;
        using var server = TestServerCompat.CreateTestServer(async context => {
            if (context.Request.Path == "/continue") {
                IFormCollection form = await context.Request.ReadFormAsync();
                Assert.Equal(new[] { "openid", "profile" }, form["scope"].ToArray());
                await context.Response.WriteAsync("<main>done</main>");
                return;
            }

            context.Response.StatusCode = 404;
        }, null, null);
        serverBase = server.BaseAddress.ToString().TrimEnd('/');
        string initialHtml = $"""
<form method="POST" name="hiddenform" action="{serverBase}/continue">
<input type="hidden" name="SAMLResponse" value="redacted">
<input type="hidden" name="scope" value="openid">
<input type="hidden" name="scope" value="profile">
</form>
<script>document.forms['hiddenform'].submit()</script>
""";
        using HttpClient client = CreateCookieAwareClient(server);

        HtmlFormRelayResult result = await HtmlFormRelayClient.FollowAsync(
            initialHtml,
            new Uri(serverBase + "/start"),
            client);

        Assert.True(result.SubmittedRelay);
        Assert.Equal(HtmlFormRelayStopReason.NoRelayForm, result.StopReason);
    }

    [Fact]
    public async Task FollowAsync_SubmitsRelayHopsAndPreservesCookies() {
        string serverBase = string.Empty;
        using var server = TestServerCompat.CreateTestServer(async context => {
            if (context.Request.Path == "/signin") {
                IFormCollection form = await context.Request.ReadFormAsync();
                Assert.Equal("signin1.0", form["wa"]);
                context.Response.Cookies.Append("relay", "ok");
                await context.Response.WriteAsync($"""
<form method="POST" name="hiddenform" action="{serverBase}/complete">
<input type="hidden" name="SAMLResponse" value="redacted">
<input type="hidden" name="RelayState" value="state">
</form>
<script>document.forms[0].submit()</script>
""".Replace("{serverBase}", serverBase));
                return;
            }

            if (context.Request.Path == "/complete") {
                Assert.Equal("ok", context.Request.Cookies["relay"]);
                IFormCollection form = await context.Request.ReadFormAsync();
                Assert.Equal("redacted", form["SAMLResponse"]);
                await context.Response.WriteAsync("<main>done</main>");
                return;
            }

            context.Response.StatusCode = 404;
        }, null, null);
        serverBase = server.BaseAddress.ToString().TrimEnd('/');
        string initialHtml = $"""
<form method="POST" name="hiddenform" action="{serverBase}/signin">
<input type="hidden" name="wa" value="signin1.0">
<input type="hidden" name="wresult" value="redacted">
<input type="hidden" name="wctx" value="state">
</form>
<script>document.forms[0].submit()</script>
""";
        using HttpClient client = CreateCookieAwareClient(server);

        HtmlFormRelayResult result = await HtmlFormRelayClient.FollowAsync(
            initialHtml,
            new Uri(serverBase + "/start"),
            client);

        Assert.True(result.SubmittedRelay);
        Assert.Equal(HtmlFormRelayStopReason.NoRelayForm, result.StopReason);
        Assert.Contains("done", result.FinalContent);
        Assert.Equal(2, result.Steps.Count);
        Assert.Equal(HtmlFormRelayProtocolHint.WsFederation, result.Steps[0].ProtocolHint);
        Assert.Equal(HtmlFormRelayProtocolHint.Saml, result.Steps[1].ProtocolHint);
        Assert.DoesNotContain("redacted", string.Join(",", result.Steps.SelectMany(step => step.FieldNames)));
    }

    [Fact]
    public async Task FollowAsync_BlocksCrossHostRelayByDefault() {
        string html = """
<form method="POST" name="hiddenform" action="https://idp.example.net/signin">
<input type="hidden" name="wa" value="signin1.0">
<input type="hidden" name="wresult" value="redacted">
<input type="hidden" name="wctx" value="state">
</form>
<script>document.forms[0].submit()</script>
""";

        HtmlFormRelayResult result = await HtmlFormRelayClient.FollowAsync(
            html,
            new Uri("https://rp.example.org/start"),
            HtmlHttpClientFactory.Shared);

        Assert.False(result.SubmittedRelay);
        Assert.Equal(HtmlFormRelayStopReason.CrossHostBlocked, result.StopReason);
        HtmlFormRelayStep step = Assert.Single(result.Steps);
        Assert.True(step.Blocked);
        Assert.True(step.IsCrossHost);
        Assert.True(step.IsCrossOrigin);
    }

    [Fact]
    public async Task FollowAsync_BlocksSameHostDifferentOriginByDefault() {
        string html = """
<form method="POST" name="hiddenform" action="https://rp.example.org:9443/signin">
<input type="hidden" name="wa" value="signin1.0">
<input type="hidden" name="wresult" value="redacted">
<input type="hidden" name="wctx" value="state">
</form>
<script>document.forms[0].submit()</script>
""";

        HtmlFormRelayResult result = await HtmlFormRelayClient.FollowAsync(
            html,
            new Uri("https://rp.example.org/start"),
            HtmlHttpClientFactory.Shared);

        Assert.False(result.SubmittedRelay);
        Assert.Equal(HtmlFormRelayStopReason.CrossHostBlocked, result.StopReason);
        HtmlFormRelayStep step = Assert.Single(result.Steps);
        Assert.True(step.Blocked);
        Assert.False(step.IsCrossHost);
        Assert.True(step.IsCrossOrigin);
    }

    private static HttpClient CreateCookieAwareClient(TestServerFixture server) {
        TestServerCookieHandler handler = new() {
            InnerHandler = server.CreateHandler()
        };

        return new HttpClient(handler) {
            BaseAddress = server.BaseAddress
        };
    }

    private sealed class TestServerCookieHandler : DelegatingHandler {
        private readonly CookieContainer _cookies = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            Uri? requestUri = request.RequestUri;
            if (requestUri != null) {
                string cookieHeader = _cookies.GetCookieHeader(requestUri);
                if (!string.IsNullOrWhiteSpace(cookieHeader)) {
                    request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
                }
            }

            HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (requestUri != null && response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? setCookieHeaders)) {
                foreach (string setCookieHeader in setCookieHeaders) {
                    _cookies.SetCookies(requestUri, setCookieHeader);
                }
            }

            return response;
        }
    }
}
