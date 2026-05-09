using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

/// <summary>
/// Tests for <see cref="HtmlBrowser.RegisterRouteAsync"/> and related methods.
/// </summary>
public class HtmlBrowserRoutesTests {
    [Fact]
    public async Task RegisterRouteAsync_PageInvokesRouteAsync() {
        var page = new Mock<IPage>();
        Func<IRoute, Task> handler = _ => Task.CompletedTask;
        const string pattern = "**/data.json";
        page.Setup(p => p.RouteAsync(pattern, handler, null)).Returns(CompletedRouteRegistration()).Verifiable();

        await HtmlBrowser.RegisterRouteAsync(page.Object, pattern, handler);

        page.Verify();
    }

    [Fact]
    public async Task RegisterRouteAsync_SessionInvokesRouteAsync() {
        var page = new Mock<IPage>();
        Func<IRoute, Task> handler = _ => Task.CompletedTask;
        const string pattern = "**/data.json";
        page.Setup(p => p.RouteAsync(pattern, handler, null)).Returns(CompletedRouteRegistration()).Verifiable();
        var session = (HtmlBrowserSession)RuntimeHelpers.GetUninitializedObject(typeof(HtmlBrowserSession));
        typeof(HtmlBrowserSession)
            .GetField("<Page>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(session, page.Object);

        await HtmlBrowser.RegisterRouteAsync(session, pattern, handler);

        page.Verify();
    }

    [Fact]
    public async Task UnregisterRouteAsync_PageInvokesUnrouteAsyncWithoutHandler() {
        var page = new Mock<IPage>();
        const string pattern = "**/data.json";
        Action<IRoute>? captured = null;
        page.Setup(p => p.UnrouteAsync(pattern, It.IsAny<Action<IRoute>>()))
            .Callback<string, Action<IRoute>>((_, h) => captured = h)
            .Returns(Task.CompletedTask)
            .Verifiable();

        await HtmlBrowser.UnregisterRouteAsync(page.Object, pattern);

        page.Verify();
        Assert.Null(captured);
    }

    [Fact]
    public async Task UnregisterRouteAsync_PageInvokesUnrouteAsyncWithHandler() {
        var page = new Mock<IPage>();
        Func<IRoute, Task> handler = _ => Task.CompletedTask;
        const string pattern = "**/data.json";
        page.Setup(p => p.UnrouteAsync(pattern, handler)).Returns(Task.CompletedTask).Verifiable();

        await HtmlBrowser.UnregisterRouteAsync(page.Object, pattern, handler);

        page.Verify();
    }

    [Fact]
    public async Task UnregisterRouteAsync_SessionInvokesUnrouteAsyncWithoutHandler() {
        var page = new Mock<IPage>();
        const string pattern = "**/data.json";
        Action<IRoute>? captured = null;
        page.Setup(p => p.UnrouteAsync(pattern, It.IsAny<Action<IRoute>>()))
            .Callback<string, Action<IRoute>>((_, h) => captured = h)
            .Returns(Task.CompletedTask)
            .Verifiable();
        var session = (HtmlBrowserSession)RuntimeHelpers.GetUninitializedObject(typeof(HtmlBrowserSession));
        typeof(HtmlBrowserSession)
            .GetField("<Page>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(session, page.Object);

        await HtmlBrowser.UnregisterRouteAsync(session, pattern);

        page.Verify();
        Assert.Null(captured);
    }

    [Fact]
    public async Task UnregisterRouteAsync_SessionInvokesUnrouteAsyncWithHandler() {
        var page = new Mock<IPage>();
        Func<IRoute, Task> handler = _ => Task.CompletedTask;
        const string pattern = "**/data.json";
        page.Setup(p => p.UnrouteAsync(pattern, handler)).Returns(Task.CompletedTask).Verifiable();
        var session = (HtmlBrowserSession)RuntimeHelpers.GetUninitializedObject(typeof(HtmlBrowserSession));
        typeof(HtmlBrowserSession)
            .GetField("<Page>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(session, page.Object);

        await HtmlBrowser.UnregisterRouteAsync(session, pattern, handler);

        page.Verify();
    }

    private static Task<IAsyncDisposable> CompletedRouteRegistration() =>
        Task.FromResult(Mock.Of<IAsyncDisposable>());
}
