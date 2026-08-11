using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererLiveTests {
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task DeclarativeSameOriginPopupsWaitForOriginScopedHeaderInterception(int submissionMode) {
        await using LoopbackPopupServer server = new();
        HtmlBrowserNetworkPolicy policy = new(allowedHosts: new[] { "127.0.0.1" });
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: policy));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(submissionMode == 0 ? server.DeclarativeAnchorUrl : server.DeclarativeFormUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'popup authorized'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: submissionMode switch {
                1 => "document.querySelector('form').requestSubmit(); true",
                2 => "document.querySelector('form').submit(); true",
                _ => "document.querySelector('a').click(); true"
            }));

        AssertPdfContains(result.PdfBytes, "popup authorized");
        Assert.Equal("popup-token", server.LastPopupToken);
        Assert.Equal("popup-token", server.LastProtectedToken);
    }

    [Fact]
    public async Task DeclarativeFormRelOpenerPreservesTheExplicitOpenerRelationship() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.DeclarativeFormOpenerUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'popup authorized'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: "document.querySelector('form').submit(); true"));

        AssertPdfContains(result.PdfBytes, "popup authorized");
        Assert.Equal("popup-token", server.LastPopupToken);
        Assert.Equal("popup-token", server.LastProtectedToken);
    }

    [Fact]
    public async Task NewlyCreatedNamedDeclarativePopupWaitsForScopedHeaders() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.DeclarativeNamedUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'popup authorized'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: "document.querySelector('a').click(); true"));

        AssertPdfContains(result.PdfBytes, "popup authorized");
        Assert.Equal("popup-token", server.LastPopupToken);
        Assert.Equal("popup-token", server.LastProtectedToken);
    }

    [Fact]
    public async Task DeferredFormSubmissionDoesNotRedispatchTheSubmitEvent() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.DeclarativeSingleSubmitUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'popup authorized|1'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: "const form = document.querySelector('form'); form.requestSubmit(form.querySelector('button')); true"));

        AssertPdfContains(result.PdfBytes, "popup authorized|1");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeclarativeDefaultTargetRemainsInTheCurrentPage(bool form) {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(form ? server.DeclarativeSelfFormUrl : server.DeclarativeSelfAnchorUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                selector: "#self-result",
                timeout: 5000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: form
                ? "document.querySelector('form').requestSubmit(); true"
                : "document.querySelector('a').click(); true"));

        AssertPdfContains(result.PdfBytes, "self navigated");
    }
}
