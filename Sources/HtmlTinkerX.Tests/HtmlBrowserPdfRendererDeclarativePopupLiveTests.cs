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
    public async Task PageMonkeypatchCannotHideADeclarativePopupTarget() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        const string script = @"const anchor = document.querySelector('a');
            const hasAttribute = Element.prototype.hasAttribute;
            const getAttribute = Element.prototype.getAttribute;
            const querySelector = Document.prototype.querySelector;
            Element.prototype.hasAttribute = () => false;
            Element.prototype.getAttribute = () => null;
            Document.prototype.querySelector = () => null;
            anchor.click();
            setTimeout(() => {
                Element.prototype.hasAttribute = hasAttribute;
                Element.prototype.getAttribute = getAttribute;
                Document.prototype.querySelector = querySelector;
            }, 0);
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.DeclarativeAnchorUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'popup authorized'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

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
                function: "() => document.querySelector('#result').textContent === 'popup authorized|approve|1'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: "const form = document.querySelector('form'); form.requestSubmit(form.querySelector('button')); true"));

        AssertPdfContains(result.PdfBytes, "popup authorized|approve|1");
    }

    [Fact]
    public async Task ImageSubmitPopupPreservesTheClickCoordinates() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        const string clickImage = "const input = document.querySelector('input[type=image]'); const rect = input.getBoundingClientRect(); input.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, button: 0, clientX: rect.left + 7, clientY: rect.top + 9 })); true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.DeclarativeImageSubmitUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'popup authorized|7,9'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: clickImage));

        AssertPdfContains(result.PdfBytes, "popup authorized|7,9");
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

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ExplicitEmptyDeclarativeTargetOverridesInheritedPopupTarget(int submissionMode) {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(submissionMode switch {
                0 => server.DeclarativeExplicitSelfAnchorUrl,
                2 => server.DeclarativeExplicitSelfNativeFormUrl,
                _ => server.DeclarativeExplicitSelfFormUrl
            }),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                selector: "#self-result",
                timeout: 5000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: submissionMode switch {
                1 => "const form = document.querySelector('form'); form.requestSubmit(form.querySelector('button')); true",
                2 => "document.querySelector('form').submit(); true",
                _ => "document.querySelector('a').click(); true"
            }));

        AssertPdfContains(result.PdfBytes, "self navigated");
    }

    [Fact]
    public async Task InheritedFormPopupTargetRemainsEffectiveAfterTemporarySubmissionTargetRestoration() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        const string script = @"const form = document.querySelector('form');
            form.removeAttribute('target');
            const base = document.createElement('base');
            base.target = '_blank';
            document.head.appendChild(base);
            const state = document.createElement('p');
            state.id = 'target-state';
            state.textContent = 'pending';
            document.body.appendChild(state);
            let submittedAgain = false;
            setInterval(() => fetch('/popup-count-status').then(response => response.text()).then(text => {
                const count = Number(text);
                if (count >= 1 && !submittedAgain && !form.hasAttribute('target')) {
                    submittedAgain = true;
                    form.requestSubmit();
                }
                if (count >= 2) state.textContent = form.hasAttribute('target') ? 'target leaked' : 'base target restored';
            }), 20);
            form.requestSubmit();
            true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.DeclarativeFormUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#target-state')?.textContent !== 'pending'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "base target restored");
        Assert.Equal(2, server.PopupRequestCount);
    }

    [Fact]
    public async Task DelegatedDocumentClickHandlerCanCancelDeclarativePopup() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.DeclarativeCancelledAnchorUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'navigation cancelled'",
                timeout: 5000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: "document.querySelector('a').click(); true"));

        AssertPdfContains(result.PdfBytes, "navigation cancelled");
        Assert.Null(server.LastPopupToken);
    }

    [Fact]
    public async Task LaterWindowClickHandlerCanCancelDeclarativePopup() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.DeclarativeWindowCancelledAnchorUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'window navigation cancelled'",
                timeout: 5000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: "document.querySelector('a').click(); true"));

        AssertPdfContains(result.PdfBytes, "window navigation cancelled");
        Assert.Null(server.LastPopupToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StoppedPropagationStillStagesTheNativePopupDefaultAction(bool form) {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));
        string script = form
            ? "const form = document.querySelector('form'); form.addEventListener('submit', event => event.stopImmediatePropagation()); form.requestSubmit(); true"
            : "const anchor = document.querySelector('a'); anchor.addEventListener('click', event => event.stopImmediatePropagation()); anchor.click(); true";

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(form ? server.DeclarativeFormUrl : server.DeclarativeAnchorUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'popup authorized'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: script));

        AssertPdfContains(result.PdfBytes, "popup authorized");
        Assert.Equal("popup-token", server.LastPopupToken);
        Assert.Equal("popup-token", server.LastProtectedToken);
    }

    [Fact]
    public async Task BlankNamedSiblingFrameUsesNativeTargetedNavigation() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.SiblingNamedContextUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'existing context authorized'",
                timeout: 5000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: "frames.sourceFrame.document.querySelector('a').click(); true"));

        AssertPdfContains(result.PdfBytes, "existing context authorized");
        Assert.Equal("popup-token", server.LastExistingContextToken);
    }

    [Fact]
    public async Task DeclarativeAnchorPreservesItsNoReferrerPolicy() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.DeclarativeReferrerPolicyUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'popup authorized'",
                timeout: 5000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: "document.querySelector('a').click(); true"));

        AssertPdfContains(result.PdfBytes, "popup authorized");
        Assert.Null(server.LastPopupReferer);
    }

    [Fact]
    public async Task CrossOriginPopupRedirectBackToCaptureOriginReceivesScopedHeaders() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1", "localhost" })));

        HtmlBrowserPdfResult result = await renderer.CaptureAsync(new HtmlBrowserPdfRequest(
            HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
            readiness: new HtmlBrowserPdfReadiness(
                skipLoadState: true,
                function: "() => document.querySelector('#result').textContent === 'popup authorized'",
                timeout: 10000),
            headers: new Dictionary<string, string> { ["X-Render-Token"] = "popup-token" },
            beforeCaptureScript: $"window.open('{server.CrossOriginRedirectUrl}', '_blank'); true"));

        AssertPdfContains(result.PdfBytes, "popup authorized");
        Assert.Equal("popup-token", server.LastPopupToken);
        Assert.Equal("popup-token", server.LastProtectedToken);
    }
}
