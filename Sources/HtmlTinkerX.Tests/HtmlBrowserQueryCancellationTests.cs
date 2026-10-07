using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Cancellation timing")]
public class HtmlBrowserQueryCancellationTests {
    [Theory]
    [InlineData("Cookies")]
    [InlineData("CookieOverload")]
    [InlineData("ElementsCount")]
    [InlineData("ElementsMetadata")]
    [InlineData("ActiveElement")]
    [InlineData("InteractablesEnumeration")]
    [InlineData("InteractablesText")]
    [InlineData("Storage")]
    [InlineData("LoginPage")]
    [InlineData("LoginSession")]
    [InlineData("Locators")]
    [InlineData("LocatorAlternates")]
    [InlineData("ElementCountWait")]
    [InlineData("RecipePreflightCount")]
    [InlineData("StabilityDom")]
    public async Task BrowserQuery_CancellationReturnsWhileDriverReadIsPending(string scenario) {
        Mock<IPage> page = new();
        Mock<IBrowser> browser = new();
        Mock<IBrowserContext> context = new();
        Mock<ILocator> locator = new();
        Mock<IElementHandle> element = new();
        TaskCompletionSource<bool> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<string> text = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<int> count = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<string[]> selectors = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<IReadOnlyList<IElementHandle>> elements = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<IReadOnlyList<BrowserContextCookiesResult>> cookies = new(TaskCreationOptions.RunContinuationsAsynchronously);
        page.SetupGet(value => value.Url).Returns("https://example.test/page");
        page.Setup(value => value.Locator("main", It.IsAny<PageLocatorOptions?>())).Returns(locator.Object);
        locator.SetupGet(value => value.First).Returns(locator.Object);
        locator.Setup(value => value.Nth(It.IsAny<int>())).Returns(locator.Object);
        locator.Setup(value => value.CountAsync()).ReturnsAsync(1);
        page.Setup(value => value.QuerySelectorAllAsync(It.IsAny<string>())).ReturnsAsync(new[] { element.Object });

        Task pendingRead;
        if (scenario == "Cookies" || scenario == "CookieOverload") {
            context.Setup(value => value.CookiesAsync()).Callback(() => entered.TrySetResult(true)).Returns(cookies.Task);
            pendingRead = cookies.Task;
        } else if (scenario == "ElementsCount" || scenario == "ElementCountWait" || scenario == "RecipePreflightCount") {
            locator.Setup(value => value.CountAsync()).Callback(() => entered.TrySetResult(true)).Returns(count.Task);
            pendingRead = count.Task;
        } else if (scenario == "ElementsMetadata") {
            locator.Setup(value => value.EvaluateAsync<string>(It.IsAny<string>(), It.IsAny<object?>())).Callback(() => entered.TrySetResult(true)).Returns(text.Task);
            pendingRead = text.Task;
        } else if (scenario == "InteractablesEnumeration") {
            page.Setup(value => value.QuerySelectorAllAsync(It.IsAny<string>())).Callback(() => entered.TrySetResult(true)).Returns(elements.Task);
            pendingRead = elements.Task;
        } else if (scenario == "InteractablesText") {
            element.Setup(value => value.InnerTextAsync()).Callback(() => entered.TrySetResult(true)).Returns(text.Task);
            pendingRead = text.Task;
        } else if (scenario == "LoginPage" || scenario == "LoginSession") {
            page.Setup(value => value.ContentAsync()).Callback(() => entered.TrySetResult(true)).Returns(text.Task);
            pendingRead = text.Task;
        } else if (scenario == "LocatorAlternates") {
            page.Setup(value => value.EvaluateAsync<string[]>(It.IsAny<string>(), It.IsAny<object?>())).Callback(() => entered.TrySetResult(true)).Returns(selectors.Task);
            pendingRead = selectors.Task;
        } else {
            page.Setup(value => value.EvaluateAsync<string>(It.IsAny<string>(), It.IsAny<object?>())).Callback(() => entered.TrySetResult(true)).Returns(text.Task);
            pendingRead = text.Task;
        }

        await using HtmlBrowserSession session = new(new Mock<IPlaywright>().Object, browser.Object, context.Object, page.Object);
        using CancellationTokenSource cancellation = new();
        Task operation = scenario switch {
            "Cookies" => HtmlBrowser.GetCookiesAsync(session, domains: null, cancellationToken: cancellation.Token),
            "CookieOverload" => HtmlBrowser.GetCookiesAsync(session, cancellation.Token),
            "ElementsCount" or "ElementsMetadata" => HtmlBrowser.GetElementsAsync(session, "main", cancellationToken: cancellation.Token),
            "ActiveElement" => HtmlBrowser.GetActiveElementAsync(session, cancellationToken: cancellation.Token),
            "InteractablesEnumeration" or "InteractablesText" => HtmlBrowser.GetInteractablesAsync(page.Object, cancellation.Token),
            "Storage" => HtmlBrowser.GetStorageAsync(session, cancellationToken: cancellation.Token),
            "LoginPage" => HtmlBrowser.DetectLoginFormAsync(page.Object, cancellation.Token),
            "LoginSession" => HtmlBrowser.DetectLoginFormAsync(session, cancellation.Token),
            "Locators" => HtmlBrowser.FindLocatorCandidatesAsync(session, cancellationToken: cancellation.Token),
            "LocatorAlternates" => HtmlBrowser.FindSelectorAlternatesAsync(session, "main", cancellationToken: cancellation.Token),
            "ElementCountWait" => HtmlBrowser.WaitForElementCountAsync(session, "main", 1, cancellationToken: cancellation.Token),
            "RecipePreflightCount" => HtmlBrowser.ExecuteRecipeAsync(new HtmlBrowserRecipe {
                Steps = new List<HtmlBrowserRecipeStep> { new() { Action = HtmlBrowserRecipeAction.Click, Selector = "main" } }
            }, session, cancellation.Token),
            "StabilityDom" => HtmlBrowser.WaitUntilStableAsync(session, cancellationToken: cancellation.Token),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        try {
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, Task.Delay(TimeSpan.FromSeconds(2))));
            cancellation.Cancel();
            Assert.Same(operation, await Task.WhenAny(operation, Task.Delay(TimeSpan.FromSeconds(2))));
            OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
            Assert.Equal(cancellation.Token, exception.CancellationToken);
            Assert.False(pendingRead.IsCompleted);
            page.Verify(value => value.CloseAsync(It.IsAny<PageCloseOptions?>()), Times.Never);
            context.Verify(value => value.CloseAsync(It.IsAny<BrowserContextCloseOptions?>()), Times.Never);
            browser.Verify(value => value.CloseAsync(It.IsAny<BrowserCloseOptions?>()), Times.Never);
            Assert.False(session.SuppressRecipeRecording);
            if (scenario == "RecipePreflightCount") {
                locator.Verify(value => value.ClickAsync(It.IsAny<LocatorClickOptions?>()), Times.Never);
            }
        } finally {
            text.TrySetResult("[]");
            count.TrySetResult(0);
            selectors.TrySetResult(Array.Empty<string>());
            elements.TrySetResult(Array.Empty<IElementHandle>());
            cookies.TrySetResult(Array.Empty<BrowserContextCookiesResult>());
            try { await operation; } catch (Exception) { }
        }
    }

    [Fact]
    public async Task GetStorageAsync_PreCancelledRequestDoesNotReadDriver() {
        Mock<IPage> page = new();
        page.Setup(value => value.EvaluateAsync<string>(It.IsAny<string>(), It.IsAny<object?>())).ReturnsAsync("[]");
        await using HtmlBrowserSession session = new(new Mock<IPlaywright>().Object, new Mock<IBrowser>().Object, new Mock<IBrowserContext>().Object, page.Object);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => HtmlBrowser.GetStorageAsync(session, cancellationToken: cancellation.Token));
        page.Verify(value => value.EvaluateAsync<string>(It.IsAny<string>(), It.IsAny<object?>()), Times.Never);
    }
}
