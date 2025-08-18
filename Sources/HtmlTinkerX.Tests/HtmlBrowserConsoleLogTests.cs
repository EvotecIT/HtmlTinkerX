using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using Xunit;
using System;
using System.Linq;

namespace HtmlTinkerX.Tests;

public class HtmlBrowserConsoleLogTests {
    [Fact]
    public async Task GetConsoleLog_ReturnsCapturedEntries() {
        var playwright = new Mock<IPlaywright>();
        var browser = new Mock<IBrowser>();
        var context = new Mock<IBrowserContext>();
        var page = new Mock<IPage>();

        var message = new Mock<IConsoleMessage>();
        message.SetupGet(m => m.Text).Returns("hello");
        message.SetupGet(m => m.Type).Returns("log");
        message.SetupGet(m => m.Location).Returns("file.js:1:2");

        var session = new HtmlBrowserSession(playwright.Object, browser.Object, context.Object, page.Object);

        page.Raise(p => p.Console += (_, _) => { }, page.Object, message.Object);

        HtmlConsoleEntry entry = Assert.Single(HtmlBrowser.GetConsoleLog(session));
        Assert.Equal("hello", entry.Text);
        Assert.Equal(HtmlConsoleMessageType.Log, entry.Type);
        Assert.Equal("file.js:1:2", entry.Location);
        await session.DisposeAsync();
    }

    [Fact]
    public void GetConsoleLog_NullSession_Throws() {
        var method = typeof(HtmlBrowser).GetMethod(
            nameof(HtmlBrowser.GetConsoleLog),
            new[] { typeof(HtmlBrowserSession), typeof(HtmlConsoleSeverity) })
            ?? throw new MissingMethodException();
        Assert.Throws<ArgumentNullException>(() => method.Invoke(null, new object?[] { null, HtmlConsoleSeverity.Info }));
    }

    [Fact]
    public async Task GetConsoleLog_FiltersBySeverity() {
        var playwright = new Mock<IPlaywright>();
        var browser = new Mock<IBrowser>();
        var context = new Mock<IBrowserContext>();
        var page = new Mock<IPage>();

        var err = new Mock<IConsoleMessage>();
        err.SetupGet(m => m.Text).Returns("boom");
        err.SetupGet(m => m.Type).Returns("error");

        var info = new Mock<IConsoleMessage>();
        info.SetupGet(m => m.Text).Returns("hi");
        info.SetupGet(m => m.Type).Returns("log");

        var session = new HtmlBrowserSession(playwright.Object, browser.Object, context.Object, page.Object);

        page.Raise(p => p.Console += (_, _) => { }, page.Object, err.Object);
        page.Raise(p => p.Console += (_, _) => { }, page.Object, info.Object);

        var entries = HtmlBrowser.GetConsoleLog(session, HtmlConsoleSeverity.Error).ToList();
        Assert.Single(entries);
        Assert.Equal(HtmlConsoleMessageType.Error, entries[0].Type);
        await session.DisposeAsync();
    }
}