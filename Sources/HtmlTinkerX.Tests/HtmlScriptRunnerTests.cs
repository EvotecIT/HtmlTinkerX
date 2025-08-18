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
    public async Task RunAsync_NullHtml_Throws() {
        var method = typeof(HtmlScriptRunner).GetMethod(nameof(HtmlScriptRunner.RunAsync))
            ?.MakeGenericMethod(typeof(int)) ?? throw new MissingMethodException();
        await Assert.ThrowsAsync<ArgumentNullException>(async () => {
            object? taskObj = method.Invoke(null, new object?[] { null, "1" });
            await (taskObj as Task ?? throw new InvalidOperationException());
        });
    }

    [Fact]
    public async Task RunAsync_NullScript_Throws() {
        var method = typeof(HtmlScriptRunner).GetMethod(nameof(HtmlScriptRunner.RunAsync))
            ?.MakeGenericMethod(typeof(int)) ?? throw new MissingMethodException();
        await Assert.ThrowsAsync<ArgumentNullException>(async () => {
            object? taskObj = method.Invoke(null, new object?[] { "<html></html>", null });
            await (taskObj as Task ?? throw new InvalidOperationException());
        });
    }
}