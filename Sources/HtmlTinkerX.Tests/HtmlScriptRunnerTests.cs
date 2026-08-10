using HtmlTinkerX;
using System;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlScriptRunnerTests {
    [Fact]
    public async Task RunAsync_ReturnsResult() {
        const string html = "<html></html>";
        const string script = "1 + 2";
        int? result = await HtmlScriptRunner.RunAsync<int>(html, script);
        Assert.Equal(3, result);
    }

    [Fact]
    public async Task RunAsync_PreservesNavigatorWithoutExposingNetworkCapableBrowserApis() {
        const string script = "[typeof navigator, typeof XMLHttpRequest, typeof fetch, typeof WebSocket].join('|')";
        string? result = await HtmlScriptRunner.RunAsync<string>("<html></html>", script);
        Assert.Equal("object|undefined|undefined|undefined", result);
    }

    [Fact]
    public async Task RunAsync_NullHtml_Throws() {
        await Assert.ThrowsAsync<ArgumentNullException>(() => HtmlScriptRunner.RunAsync<int>(null!, "1"));
    }

    [Fact]
    public async Task RunAsync_NullScript_Throws() {
        await Assert.ThrowsAsync<ArgumentNullException>(() => HtmlScriptRunner.RunAsync<int>("<html></html>", null!));
    }
}
