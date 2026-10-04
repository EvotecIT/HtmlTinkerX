using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Playwright collection")]
public class HtmlBrowserSsoHandoffTests {
    private const string CallbackValues = "?code=query-secret&state=state-secret#id_token=fragment-secret";

    [Fact]
    public async Task FileCallbackPreservesBrowserQueryAndFragmentComponents() {
        string file = Path.Combine(Path.GetTempPath(), $"HtmlTinkerX-Sso-{Guid.NewGuid():N}.html");
        File.WriteAllText(file, "<!doctype html><html><body><main>Signed in</main></body></html>");
        try {
            string callback = HtmlBrowser.CreateLocalFileUri(file).AbsoluteUri;
            await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync("about:blank");
            await session.Page.GotoAsync(callback + CallbackValues, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

            await AssertSafeCallbackAsync(session, callback);
        } finally {
            File.Delete(file);
        }
    }

    [Theory]
    [InlineData("http")]
    [InlineData("https")]
    public async Task HttpCallbackPreservesBrowserQueryAndFragmentComponents(string scheme) {
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync("about:blank");
        await session.Page.RouteAsync("**/*", route => route.FulfillAsync(new RouteFulfillOptions {
            Status = 200,
            ContentType = "text/html",
            Body = "<!doctype html><html><body><main>Signed in</main></body></html>"
        }));
        string callback = $"{scheme}://callback.example.invalid/complete";
        await session.Page.GotoAsync(callback + CallbackValues, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

        await AssertSafeCallbackAsync(session, callback);
    }

    private static async Task AssertSafeCallbackAsync(HtmlBrowserSession session, string callback) {
        HtmlBrowserSsoHandoff handoff = Assert.Single(await HtmlBrowser.GetSsoHandoffsAsync(session));
        Assert.Equal(HtmlBrowserSsoHandoffKind.OpenIdConnect, handoff.Kind);
        Assert.Equal(callback, handoff.Action);
        Assert.Equal("location", handoff.FormSelector);
        Assert.Equal("<redacted>", handoff.FormData["code"]);
        Assert.Equal("<redacted>", handoff.FormData["state"]);
        Assert.Equal("<redacted>", handoff.FormData["id_token"]);
        Assert.Contains(handoff.Fields, field => field.Name == "code" && field.Type == "url-query");
        Assert.Contains(handoff.Fields, field => field.Name == "id_token" && field.Type == "url-fragment");
        string serialized = JsonSerializer.Serialize(handoff);
        Assert.DoesNotContain("query-secret", serialized);
        Assert.DoesNotContain("state-secret", serialized);
        Assert.DoesNotContain("fragment-secret", serialized);
    }
}
