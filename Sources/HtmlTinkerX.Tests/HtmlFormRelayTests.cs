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
    public void TryParse_KeepsEmptyActionOnResponseUrlWhenDocumentBaseIsPresent() {
        string html = """
<html>
<head><base href="https://idp.example.org/sso/"></head>
<body>
<form method="POST" name="hiddenform">
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
        Assert.Equal("https://rp.example.org/start", request!.ActionUri.AbsoluteUri);
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
    public void TryParse_AcceptsTargetedRelayFormOnMultiFormPage() {
        string html = """
<html>
<body>
<form method="GET" action="/search"><input name="q"></form>
<form method="POST" name="hiddenform" action="/continue">
<input type="hidden" name="SAMLResponse" value="redacted">
<input type="hidden" name="RelayState" value="state">
</form>
<script>document.forms['hiddenform'].submit()</script>
</body>
</html>
""";

        bool parsed = HtmlFormRelayParser.TryParse(html, new Uri("https://example.org/start"), out HtmlFormRelayRequest? request);

        Assert.True(parsed);
        Assert.NotNull(request);
        Assert.Equal("https://example.org/continue", request!.ActionUri.AbsoluteUri);
        Assert.Equal(new[] { "SAMLResponse", "RelayState" }, request.FieldNames);
    }

    [Fact]
    public void TryParse_AcceptsOnloadDrivenRelayForm() {
        string html = """
<html>
<body onload="document.forms[0].submit()">
<form method="POST" name="hiddenform" action="/continue">
<input type="hidden" name="csrf" value="redacted">
</form>
</body>
</html>
""";

        bool parsed = HtmlFormRelayParser.TryParse(html, new Uri("https://example.org/start"), out HtmlFormRelayRequest? request);

        Assert.True(parsed);
        Assert.NotNull(request);
        Assert.Equal(HtmlFormRelayProtocolHint.Generic, request!.ProtocolHint);
        Assert.True(request.HasAutoSubmitMarker);
    }

    [Fact]
    public void TryParse_RejectsProtocolRelayWithoutAutoSubmitProof() {
        string html = """
<html>
<body>
<form method="POST" name="hiddenform" action="/continue">
<input type="hidden" name="SAMLResponse" value="redacted">
<input type="hidden" name="RelayState" value="state">
<noscript><button type="submit">Continue</button></noscript>
</form>
</body>
</html>
""";

        bool parsed = HtmlFormRelayParser.TryParse(html, new Uri("https://example.org/start"), out HtmlFormRelayRequest? request);

        Assert.False(parsed);
        Assert.Null(request);
    }

    [Fact]
    public void TryParse_RejectsClickDrivenRelayFallback() {
        string html = """
<html>
<body>
<form method="POST" name="hiddenform" action="/continue">
<input type="hidden" name="SAMLResponse" value="redacted">
<input type="hidden" name="RelayState" value="state">
</form>
<button onclick="document.forms[0].submit()">Continue</button>
</body>
</html>
""";

        bool parsed = HtmlFormRelayParser.TryParse(html, new Uri("https://example.org/start"), out HtmlFormRelayRequest? request);

        Assert.False(parsed);
        Assert.Null(request);
    }

    [Fact]
    public void TryParse_RejectsClickListenerDrivenRelayFallback() {
        string html = """
<html>
<body>
<form method="POST" name="hiddenform" action="/continue">
<input type="hidden" name="SAMLResponse" value="redacted">
<input type="hidden" name="RelayState" value="state">
</form>
<button id="continue">Continue</button>
<script>document.getElementById('continue').addEventListener('click', () => document.forms[0].submit())</script>
</body>
</html>
""";

        bool parsed = HtmlFormRelayParser.TryParse(html, new Uri("https://example.org/start"), out HtmlFormRelayRequest? request);

        Assert.False(parsed);
        Assert.Null(request);
    }

    [Fact]
    public void TryParse_RejectsHelperFunctionWithoutAutomaticInvocation() {
        string html = """
<html>
<body>
<form method="POST" name="hiddenform" action="/continue">
<input type="hidden" name="SAMLResponse" value="redacted">
<input type="hidden" name="RelayState" value="state">
</form>
<script>function continueRelay() { document.forms[0].submit(); }</script>
</body>
</html>
""";

        bool parsed = HtmlFormRelayParser.TryParse(html, new Uri("https://example.org/start"), out HtmlFormRelayRequest? request);

        Assert.False(parsed);
        Assert.Null(request);
    }

    [Fact]
    public void TryParse_IgnoresNonExecutableScriptMarkers() {
        string html = """
<html>
<body>
<form method="POST" name="hiddenform" action="/continue">
<input type="hidden" name="SAMLResponse" value="redacted">
<input type="hidden" name="RelayState" value="state">
</form>
<script type="application/json">{"x":"document.forms[0].submit()"}</script>
</body>
</html>
""";

        bool parsed = HtmlFormRelayParser.TryParse(html, new Uri("https://example.org/start"), out HtmlFormRelayRequest? request);

        Assert.False(parsed);
        Assert.Null(request);
    }

    [Fact]
    public void TryParse_AcceptsEcmaScriptMimeSubmitMarker() {
        string html = """
<html>
<body>
<form method="POST" name="hiddenform" action="/continue">
<input type="hidden" name="SAMLResponse" value="redacted">
<input type="hidden" name="RelayState" value="state">
</form>
<script type="application/ecmascript">document.forms[0].submit()</script>
</body>
</html>
""";

        bool parsed = HtmlFormRelayParser.TryParse(html, new Uri("https://example.org/start"), out HtmlFormRelayRequest? request);

        Assert.True(parsed);
        Assert.NotNull(request);
        Assert.Equal("https://example.org/continue", request!.ActionUri.AbsoluteUri);
    }

    [Fact]
    public void TryParse_RejectsNonHttpRelayActions() {
        string html = """
<html>
<body>
<form method="POST" name="hiddenform" action="javascript:document.forms[0].submit()">
<input type="hidden" name="SAMLResponse" value="redacted">
</form>
<script>document.forms[0].submit()</script>
</body>
</html>
""";

        bool parsed = HtmlFormRelayParser.TryParse(html, new Uri("https://example.org/start"), out HtmlFormRelayRequest? request);

        Assert.False(parsed);
        Assert.Null(request);
    }

    [Fact]
    public void TryParse_FiltersControlsThatBrowserSubmitOmits() {
        string html = """
<html>
<body>
<form method="POST" name="hiddenform" action="/continue">
<input type="hidden" name="SAMLResponse" value="redacted">
<input type="hidden" name="disabledToken" value="leak" disabled>
<input type="checkbox" name="remember" value="yes">
<input type="submit" name="submit" value="Continue">
<button name="fallback" value="noscript">Continue</button>
</form>
<script>document.forms['hiddenform'].submit()</script>
</body>
</html>
""";

        bool parsed = HtmlFormRelayParser.TryParse(html, new Uri("https://example.org/start"), out HtmlFormRelayRequest? request);

        Assert.True(parsed);
        Assert.NotNull(request);
        Assert.Equal(new[] { "SAMLResponse" }, request!.FieldNames);
        Assert.DoesNotContain(request.FieldValues, field => field.Key == "submit" || field.Key == "fallback" || field.Key == "disabledToken" || field.Key == "remember");
    }

    [Fact]
    public void TryParse_IncludesFormOwnedControlsOutsideFormElement() {
        string html = """
<html>
<body>
<form id="relay" method="POST" action="/continue">
</form>
<input type="hidden" form="relay" name="SAMLResponse" value="redacted">
<input type="hidden" form="relay" name="RelayState" value="state">
<script>document.getElementById('relay').submit()</script>
</body>
</html>
""";

        bool parsed = HtmlFormRelayParser.TryParse(html, new Uri("https://example.org/start"), out HtmlFormRelayRequest? request);

        Assert.True(parsed);
        Assert.NotNull(request);
        Assert.Equal(new[] { "SAMLResponse", "RelayState" }, request!.FieldNames);
        Assert.Equal("https://example.org/continue", request.ActionUri.AbsoluteUri);
    }

    [Fact]
    public void TryParse_ReturnsFalseForMalformedRelayAction() {
        string html = """
<html>
<body>
<form method="POST" name="hiddenform" action="http://[::1">
<input type="hidden" name="SAMLResponse" value="redacted">
</form>
<script>document.forms['hiddenform'].submit()</script>
</body>
</html>
""";
        bool parsed = false;

        Exception? exception = Record.Exception(() =>
            parsed = HtmlFormRelayParser.TryParse(html, new Uri("https://example.org/start"), out HtmlFormRelayRequest? _));

        Assert.Null(exception);
        Assert.False(parsed);
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
    public async Task FollowAsync_UsesMetaCharsetForRelayHopHtml() {
        const string expectedValue = "zażółć";
        string serverBase = string.Empty;
        using var server = TestServerCompat.CreateTestServer(async context => {
            if (context.Request.Path == "/signin") {
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                string relayHtml = $"""
<html>
<head><meta charset="iso-8859-2"></head>
<body>
<form method="POST" name="hiddenform" action="{serverBase}/complete">
<input type="hidden" name="SAMLResponse" value="{expectedValue}">
</form>
<script>document.forms[0].submit()</script>
</body>
</html>
""";
                byte[] bytes = System.Text.Encoding.GetEncoding("iso-8859-2").GetBytes(relayHtml);
                context.Response.ContentType = "text/html";
                await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
                return;
            }

            if (context.Request.Path == "/complete") {
                IFormCollection form = await context.Request.ReadFormAsync();
                Assert.Equal(expectedValue, form["SAMLResponse"]);
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
    }

    [Fact]
    public async Task FollowAsync_RedactsSensitiveDiagnosticUrls() {
        string serverBase = string.Empty;
        using var server = TestServerCompat.CreateTestServer(async context => {
            if (context.Request.Path == "/continue") {
                Assert.Equal("abc123", context.Request.Query["token"]);
                await context.Response.WriteAsync("<main>done</main>");
                return;
            }

            context.Response.StatusCode = 404;
        }, null, null);
        serverBase = server.BaseAddress.ToString().TrimEnd('/');
        string initialHtml = $"""
<form method="POST" name="hiddenform" action="{serverBase}/continue?token=abc123#access_token=frag456">
<input type="hidden" name="SAMLResponse" value="redacted">
</form>
<script>document.forms['hiddenform'].submit()</script>
""";
        using HttpClient client = CreateCookieAwareClient(server);

        HtmlFormRelayResult result = await HtmlFormRelayClient.FollowAsync(
            initialHtml,
            new Uri(serverBase + "/start?session=start123#access_token=start456"),
            client);

        HtmlFormRelayStep step = Assert.Single(result.Steps);
        Assert.Contains("token=<redacted>", step.ActionUrl);
        Assert.Contains("access_token=<redacted>", step.ActionUrl);
        Assert.Contains("token=<redacted>", step.ResponseUrl);
        Assert.Contains("token=<redacted>", result.FinalUrl);
        Assert.DoesNotContain("abc123", step.ActionUrl, StringComparison.Ordinal);
        Assert.DoesNotContain("frag456", step.ActionUrl, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", step.ResponseUrl, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", result.FinalUrl, StringComparison.Ordinal);
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

    [Fact]
    public async Task FollowAsync_BlocksUnallowedCrossOriginRedirectDiagnostics() {
        string html = """
<form method="POST" name="hiddenform" action="https://rp.example.org/relay">
<input type="hidden" name="SAMLResponse" value="redacted">
</form>
<script>document.forms['hiddenform'].submit()</script>
""";
        using HttpClient client = new(new RedirectedRelayHandler());

        HtmlFormRelayResult result = await HtmlFormRelayClient.FollowAsync(
            html,
            new Uri("https://rp.example.org/start"),
            client);

        Assert.True(result.SubmittedRelay);
        Assert.Equal(HtmlFormRelayStopReason.CrossHostBlocked, result.StopReason);
        HtmlFormRelayStep step = Assert.Single(result.Steps);
        Assert.False(step.Blocked);
        Assert.True(step.IsCrossHost);
        Assert.True(step.IsCrossOrigin);
        Assert.Equal("https://idp.example.net/complete", step.ResponseUrl);
    }

    [Fact]
    public async Task FollowAsync_PreservesExistingGetActionQueryBytes() {
        string html = """
<form method="GET" name="hiddenform" action="https://rp.example.org/continue?RelayState=a%20b&sig=raw+plus">
<input type="hidden" name="SAMLResponse" value="redacted value">
</form>
<script>document.forms['hiddenform'].submit()</script>
""";
        using QueryCaptureHandler handler = new();
        using HttpClient client = new(handler);

        HtmlFormRelayResult result = await HtmlFormRelayClient.FollowAsync(
            html,
            new Uri("https://rp.example.org/start"),
            client);

        Assert.True(result.SubmittedRelay);
        Assert.Equal(HtmlFormRelayStopReason.NoRelayForm, result.StopReason);
        Assert.Contains("RelayState=a%20b", handler.ObservedQuery);
        Assert.Contains("sig=raw+plus", handler.ObservedQuery);
        Assert.Contains("SAMLResponse=redacted+value", handler.ObservedQuery);
        Assert.DoesNotContain("RelayState=a+b", handler.ObservedQuery, StringComparison.Ordinal);
        Assert.DoesNotContain("sig=raw%2Bplus", handler.ObservedQuery, StringComparison.Ordinal);
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

    private sealed class RedirectedRelayHandler : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            HttpResponseMessage response = new(HttpStatusCode.OK) {
                RequestMessage = new HttpRequestMessage(request.Method, "https://idp.example.net/complete"),
                Content = new StringContent("<main>redirected</main>")
            };

            return Task.FromResult(response);
        }
    }

    private sealed class QueryCaptureHandler : HttpMessageHandler, IDisposable {
        public string ObservedQuery { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            ObservedQuery = request.RequestUri?.Query ?? string.Empty;
            HttpResponseMessage response = new(HttpStatusCode.OK) {
                RequestMessage = request,
                Content = new StringContent("<main>done</main>")
            };

            return Task.FromResult(response);
        }
    }
}
