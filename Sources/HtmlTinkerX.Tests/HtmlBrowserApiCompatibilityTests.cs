using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlBrowserApiCompatibilityTests {
    [Fact]
    public void OpenSessionAsync_PreservesPreResourceBlockingSignature() {
        Type[] parameterTypes = {
            typeof(string),
            typeof(HtmlBrowserEngine),
            typeof(bool),
            typeof(string),
            typeof(string),
            typeof(HtmlFormLogin),
            typeof(bool),
            typeof(int),
            typeof(string),
            typeof(int),
            typeof(int),
            typeof(string),
            typeof(string),
            typeof(int?),
            typeof(int?),
            typeof(float?),
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(double?),
            typeof(double?),
            typeof(string),
            typeof(int),
            typeof(CancellationToken)
        };

        MethodInfo? method = typeof(HtmlBrowser).GetMethod(
            nameof(HtmlBrowser.OpenSessionAsync),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: parameterTypes,
            modifiers: null);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<HtmlBrowserSession>), method!.ReturnType);
    }

    [Fact]
    public void CaptureResponseBodiesAsync_PreservesPreRedactionSignature() {
        Type[] parameterTypes = {
            typeof(HtmlBrowserSession),
            typeof(int),
            typeof(IEnumerable<HtmlNetworkResourceType>),
            typeof(CancellationToken)
        };

        MethodInfo? method = typeof(HtmlBrowser).GetMethod(
            nameof(HtmlBrowser.CaptureResponseBodiesAsync),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: parameterTypes,
            modifiers: null);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method!.ReturnType);
    }

    [Fact]
    public void HtmlRenderProfile_PreservesOriginalHeavyDynamicPageValue() {
        Assert.Equal(0, (int)HtmlRenderProfile.Custom);
        Assert.Equal(1, (int)HtmlRenderProfile.HeavyDynamicPage);
    }

    [Fact]
    public void ClickSelectorAsync_PreservesPreNthSignature() {
        Type[] parameterTypes = {
            typeof(HtmlBrowserSession),
            typeof(string),
            typeof(bool),
            typeof(int),
            typeof(CancellationToken)
        };

        MethodInfo? method = typeof(HtmlBrowser).GetMethod(
            nameof(HtmlBrowser.ClickSelectorAsync),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: parameterTypes,
            modifiers: null);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method!.ReturnType);
    }

    [Fact]
    public void ClickTextAsync_PreservesPreNthSignature() {
        Type[] parameterTypes = {
            typeof(HtmlBrowserSession),
            typeof(string),
            typeof(bool),
            typeof(string),
            typeof(bool),
            typeof(int),
            typeof(CancellationToken)
        };

        MethodInfo? method = typeof(HtmlBrowser).GetMethod(
            nameof(HtmlBrowser.ClickTextAsync),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: parameterTypes,
            modifiers: null);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method!.ReturnType);
    }

    [Fact]
    public void TryClickTextAsync_PreservesPreNthSignature() {
        Type[] parameterTypes = {
            typeof(HtmlBrowserSession),
            typeof(string),
            typeof(bool),
            typeof(string),
            typeof(int),
            typeof(CancellationToken)
        };

        MethodInfo? method = typeof(HtmlBrowser).GetMethod(
            nameof(HtmlBrowser.TryClickTextAsync),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: parameterTypes,
            modifiers: null);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<bool>), method!.ReturnType);
    }
}
