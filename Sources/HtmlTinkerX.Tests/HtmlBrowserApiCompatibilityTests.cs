using System;
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
}
