using Microsoft.Playwright;
using Moq;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Playwright collection")]
public sealed class HtmlBrowserSessionNetworkPolicyLifetimeTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task FailedOrCancelledSetupClosesOwnedBrowserContextAndPolicyProxy(bool duringNavigation, bool cancel) {
        Mock<IPage> page = new();
        Mock<IBrowserContext> context = new();
        Mock<IBrowser> browser = new();
        Mock<IBrowserType> browserType = new();
        Mock<IPlaywright> playwright = new();
        TaskCompletionSource<bool> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Uri? proxy = null;
        context.Setup(value => value.RouteAsync("**/*", It.IsAny<Func<IRoute, Task>>(), null))
            .Returns(async () => {
                if (!duringNavigation) {
                    entered.TrySetResult(true);
                    await pending.Task;
                }
                return Mock.Of<IAsyncDisposable>();
            });
        context.Setup(value => value.RouteWebSocketAsync("**/*", It.IsAny<Action<IWebSocketRoute>>())).Returns(Task.CompletedTask);
        page.Setup(value => value.GotoAsync(It.IsAny<string>(), It.IsAny<PageGotoOptions>()))
            .Returns(async () => {
                entered.TrySetResult(true);
                await pending.Task;
                return (IResponse?)null;
            });
        context.Setup(value => value.NewPageAsync()).ReturnsAsync(page.Object);
        context.Setup(value => value.CloseAsync(It.IsAny<BrowserContextCloseOptions>())).Returns(Task.CompletedTask);
        browser.SetupGet(value => value.IsConnected).Returns(true);
        browser.Setup(value => value.CloseAsync(It.IsAny<BrowserCloseOptions>())).Returns(Task.CompletedTask);
        browser.Setup(value => value.NewContextAsync(It.IsAny<BrowserNewContextOptions>())).ReturnsAsync(context.Object);
        browserType.Setup(value => value.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>()))
            .Callback<BrowserTypeLaunchOptions>(options => proxy = new Uri(options.Proxy!.Server))
            .ReturnsAsync(browser.Object);
        playwright.SetupGet(value => value.Chromium).Returns(browserType.Object);
        Func<Task<IPlaywright>>? originalFactory = HtmlBrowser.PlaywrightFactory;
        HtmlBrowser.PlaywrightFactory = () => Task.FromResult(playwright.Object);
        using CancellationTokenSource setup = new();
        try {
            Task<HtmlBrowserSession> opening = HtmlBrowser.OpenSessionAsync("about:blank", new HtmlBrowserLaunchOptions {
                NetworkPolicy = HtmlBrowserNetworkPolicy.PublicNetworkOnly
            }, setup.Token);
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, Task.Delay(TimeSpan.FromSeconds(10))));
            if (cancel) {
                setup.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening);
                pending.TrySetException(new InvalidOperationException("late cancelled setup failure"));
            } else {
                pending.TrySetException(new InvalidOperationException("setup fixture failure"));
                await Assert.ThrowsAsync<InvalidOperationException>(() => opening);
            }

            context.Verify(value => value.CloseAsync(It.IsAny<BrowserContextCloseOptions>()), Times.AtLeastOnce);
            browser.Verify(value => value.CloseAsync(It.IsAny<BrowserCloseOptions>()), Times.AtLeastOnce);
            playwright.Verify(value => value.Dispose(), Times.Once);
            Assert.NotNull(proxy);
            using TcpClient connection = new();
            await Assert.ThrowsAsync<SocketException>(() => connection.ConnectAsync(proxy!.Host, proxy.Port));
        } finally {
            pending.TrySetResult(true);
            HtmlBrowser.PlaywrightFactory = originalFactory;
        }
    }
}
