using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Cancellation timing")]
public class HtmlBrowserMetadataCancellationTests {
    [Theory]
    [InlineData("EvidenceTitle")]
    [InlineData("RenderedEvidenceTitle")]
    [InlineData("SnapshotTitle")]
    [InlineData("RecipeTitle")]
    [InlineData("RecipeStepTitle")]
    [InlineData("DiagnosticsTitle")]
    [InlineData("SsoTimeoutTitle")]
    [InlineData("ContentMarkup")]
    [InlineData("ContentText")]
    [InlineData("ContentOuterHtml")]
    [InlineData("ContentInnerHtml")]
    [InlineData("ContentSelectorText")]
    [InlineData("DiagnosticsCookies")]
    [InlineData("DiagnosticsScript")]
    [InlineData("DiagnosticsStorage")]
    [InlineData("SsoScript")]
    public async Task PageMetadata_CancellationReturnsWhileDriverReadIsPending(string scenario) {
        Mock<IPage> page = new();
        Mock<IBrowser> browser = new();
        Mock<IBrowserContext> context = new();
        Mock<ILocator> locator = new();
        TaskCompletionSource<bool> readStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<string> pendingText = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<IReadOnlyList<BrowserContextCookiesResult>> pendingCookies = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<string[]> pendingKeys = new(TaskCreationOptions.RunContinuationsAsynchronously);
        page.SetupGet(value => value.Url).Returns("https://example.test/page");
        page.Setup(value => value.TitleAsync()).ReturnsAsync("Page title");
        page.Setup(value => value.ContentAsync()).ReturnsAsync("<html><body><main>Page content</main></body></html>");
        page.Setup(value => value.InnerTextAsync("html", It.IsAny<PageInnerTextOptions?>())).ReturnsAsync("Page content");
        page.Setup(value => value.EvaluateAsync<string>(It.IsAny<string>(), It.IsAny<object?>())).ReturnsAsync("{}");
        page.Setup(value => value.EvaluateAsync<string[]>(It.IsAny<string>(), It.IsAny<object?>())).ReturnsAsync(Array.Empty<string>());
        page.Setup(value => value.Locator("main", It.IsAny<PageLocatorOptions?>())).Returns(locator.Object);
        context.Setup(value => value.CookiesAsync()).ReturnsAsync(Array.Empty<BrowserContextCookiesResult>());

        if (scenario.EndsWith("Title", StringComparison.Ordinal)) {
            page.Setup(value => value.TitleAsync()).Callback(() => readStarted.TrySetResult(true)).Returns(pendingText.Task);
        } else if (scenario == "ContentMarkup") {
            page.Setup(value => value.ContentAsync()).Callback(() => readStarted.TrySetResult(true)).Returns(pendingText.Task);
        } else if (scenario == "ContentText") {
            page.Setup(value => value.InnerTextAsync("html", It.IsAny<PageInnerTextOptions?>())).Callback(() => readStarted.TrySetResult(true)).Returns(pendingText.Task);
        } else if (scenario == "ContentOuterHtml") {
            locator.Setup(value => value.EvaluateAsync<string>("el => el.outerHTML", It.IsAny<object?>())).Callback(() => readStarted.TrySetResult(true)).Returns(pendingText.Task);
        } else if (scenario == "ContentInnerHtml") {
            locator.Setup(value => value.InnerHTMLAsync(It.IsAny<LocatorInnerHTMLOptions?>())).Callback(() => readStarted.TrySetResult(true)).Returns(pendingText.Task);
        } else if (scenario == "ContentSelectorText") {
            locator.Setup(value => value.InnerTextAsync(It.IsAny<LocatorInnerTextOptions?>())).Callback(() => readStarted.TrySetResult(true)).Returns(pendingText.Task);
        } else if (scenario == "DiagnosticsCookies") {
            context.Setup(value => value.CookiesAsync()).Callback(() => readStarted.TrySetResult(true)).Returns(pendingCookies.Task);
        } else if (scenario == "DiagnosticsStorage") {
            page.Setup(value => value.EvaluateAsync<string[]>(It.IsAny<string>(), It.IsAny<object?>())).Callback(() => readStarted.TrySetResult(true)).Returns(pendingKeys.Task);
        } else {
            page.Setup(value => value.EvaluateAsync<string>(It.IsAny<string>(), It.IsAny<object?>())).Callback(() => readStarted.TrySetResult(true)).Returns(pendingText.Task);
        }

        await using HtmlBrowserSession session = new(new Mock<IPlaywright>().Object, browser.Object, context.Object, page.Object);
        using CancellationTokenSource cancellation = new();
        string outputPath = Path.Combine(Path.GetTempPath(), "HtmlTinkerXTests", Guid.NewGuid().ToString("N"));
        Task operation = StartRead(scenario, session, outputPath, cancellation.Token);
        try {
            Assert.Same(readStarted.Task, await Task.WhenAny(readStarted.Task, Task.Delay(TimeSpan.FromSeconds(2))));
            cancellation.Cancel();
            Assert.Same(operation, await Task.WhenAny(operation, Task.Delay(TimeSpan.FromSeconds(2))));
            OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
            Assert.Equal(cancellation.Token, exception.CancellationToken);
            Task pendingRead = scenario == "DiagnosticsCookies" ? pendingCookies.Task
                : scenario == "DiagnosticsStorage" ? pendingKeys.Task : pendingText.Task;
            Assert.False(pendingRead.IsCompleted);
            page.Verify(value => value.CloseAsync(It.IsAny<PageCloseOptions?>()), Times.Never);
            context.Verify(value => value.CloseAsync(It.IsAny<BrowserContextCloseOptions?>()), Times.Never);
            browser.Verify(value => value.CloseAsync(It.IsAny<BrowserCloseOptions?>()), Times.Never);
            Assert.False(session.SuppressRecipeRecording);
        } finally {
            pendingText.TrySetResult("{}");
            pendingCookies.TrySetResult(Array.Empty<BrowserContextCookiesResult>());
            pendingKeys.TrySetResult(Array.Empty<string>());
            try { await operation; } catch (Exception) { }
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Evidence_PreCancelledRequestDoesNotReadDriverOrCreateOutput(bool rendered) {
        Mock<IPage> page = new();
        await using HtmlBrowserSession session = new(new Mock<IPlaywright>().Object, new Mock<IBrowser>().Object, new Mock<IBrowserContext>().Object, page.Object);
        string outputPath = Path.Combine(Path.GetTempPath(), "HtmlTinkerXTests", Guid.NewGuid().ToString("N"));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        try {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => StartRead(rendered ? "RenderedEvidenceTitle" : "EvidenceTitle", session, outputPath, cancellation.Token));
            Assert.False(Directory.Exists(outputPath));
            page.Verify(value => value.TitleAsync(), Times.Never);
        } finally {
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    private static Task StartRead(string scenario, HtmlBrowserSession session, string outputPath, CancellationToken cancellationToken) {
        HtmlBrowserEvidenceOptions evidenceOptions = new() {
            Screenshot = false, FullPageScreenshot = false, Pdf = false, Html = false,
            VisibleText = false, Markdown = false, NetworkSummary = false,
            SsoHandoffSummary = false, Manifest = false
        };
        return scenario switch {
            "EvidenceTitle" => HtmlBrowser.ExportEvidenceAsync(session, outputPath, evidenceOptions, cancellationToken),
            "RenderedEvidenceTitle" => HtmlBrowser.ExportRenderedPageEvidenceAsync(
                new HtmlCrawlRenderedPageContext(session, new HtmlCrawlPage { Url = session.Page.Url }, Array.Empty<HtmlNetworkEntry>()),
                outputPath, evidenceOptions, cancellationToken),
            "SnapshotTitle" => HtmlBrowser.CreateSnapshotAsync(session, session.Page.Url, cancellationToken: cancellationToken),
            "RecipeTitle" => HtmlBrowser.ExecuteRecipeAsync(new HtmlBrowserRecipe(), session, cancellationToken),
            "RecipeStepTitle" => HtmlBrowser.ExecuteRecipeAsync(new HtmlBrowserRecipe {
                Steps = new List<HtmlBrowserRecipeStep> { new() { Action = HtmlBrowserRecipeAction.WaitMilliseconds, Milliseconds = 0 } }
            }, session, cancellationToken),
            "DiagnosticsTitle" or "DiagnosticsCookies" or "DiagnosticsScript" or "DiagnosticsStorage" => HtmlBrowser.GetDiagnosticsAsync(session, cancellationToken),
            "SsoTimeoutTitle" => HtmlBrowser.GetSsoHandoffsAsync(session, new HtmlBrowserSsoHandoffOptions { Wait = true, Timeout = 1, PollMilliseconds = 1 }, cancellationToken),
            "SsoScript" => HtmlBrowser.GetSsoHandoffsAsync(session, cancellationToken: cancellationToken),
            "ContentMarkup" => HtmlBrowser.GetContentAsync(session.Page, cancellationToken: cancellationToken),
            "ContentText" => HtmlBrowser.GetContentAsync(session.Page, asText: true, cancellationToken: cancellationToken),
            "ContentOuterHtml" => HtmlBrowser.GetContentAsync(session.Page, "main", cancellationToken: cancellationToken),
            "ContentInnerHtml" => HtmlBrowser.GetContentAsync(session.Page, "main", innerHtml: true, cancellationToken: cancellationToken),
            "ContentSelectorText" => HtmlBrowser.GetContentAsync(session.Page, "main", asText: true, cancellationToken: cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
    }
}
