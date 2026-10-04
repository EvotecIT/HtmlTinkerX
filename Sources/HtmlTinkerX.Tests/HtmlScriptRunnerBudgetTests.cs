using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Js;
using Jint.Runtime;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlScriptRunnerBudgetTests {
    [Theory]
    [InlineData(true, "page|object|undefined|undefined|undefined")]
    [InlineData(false, "undefined|object|undefined|undefined|undefined")]
    public async Task RunAsync_PagePolicyRetainsExplicitDomAndOfflineServices(bool executePageScripts, string expected) {
        const string html = "<div id='value'>before</div><script>window.pageValue = 'page';</script>";
        const string script = "document.getElementById('value').textContent = 'after'; [typeof pageValue === 'undefined' ? 'undefined' : pageValue, typeof navigator, typeof XMLHttpRequest, typeof fetch, typeof WebSocket].join('|')";
        var options = new HtmlScriptRunOptions { ExecutePageScripts = executePageScripts };

        Assert.Equal(expected, await HtmlScriptRunner.RunAsync<string>(html, script, options, CancellationToken.None));
    }

    [Theory]
    [InlineData(true, "ran")]
    [InlineData(false, "undefined")]
    public async Task RunAsync_PagePolicyAlsoControlsInlineEventHandlers(bool executePageScripts, string expected) {
        var options = new HtmlScriptRunOptions { ExecutePageScripts = executePageScripts };
        const string html = "<button onclick=\"window.clicked = 'ran'\">Go</button>";
        const string script = "document.querySelector('button').click(); typeof clicked === 'undefined' ? 'undefined' : clicked";
        Assert.Equal(expected, await HtmlScriptRunner.RunAsync<string>(html, script, options, CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_DefaultLiteralStillSelectsCallerContextOverload() {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            HtmlScriptRunner.RunAsync<int>("<html></html>", "1", default!));
        Assert.Equal("context", exception.ParamName);
    }

    [Fact]
    public async Task RunAsync_ClosesOwnedDocumentButRetainsCallerContext() {
        IDocument? owned = await HtmlScriptRunner.RunAsync<IDocument>("<p>Owned</p>", "document");
        Assert.NotNull(owned);
        Assert.Null(owned!.Context.Active);

        using var context = BrowsingContext.New(Configuration.Default.WithJs());
        IDocument? callerOwned = await HtmlScriptRunner.RunAsync<IDocument>("<p>Caller</p>", "document", context);
        Assert.Same(callerOwned, context.Active);
        Assert.Equal("Caller", callerOwned!.QuerySelector("p")!.TextContent);
        Assert.Equal(2, Convert.ToInt32(callerOwned.ExecuteScript("1 + 1")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_StatementBudgetAppliesBeforeInlineAndExplicitExecution(bool inline) {
        const string expensive = "for (var i = 0; i < 1000; i++) { document.title = 'work'; } 'completed'";
        string html = inline ? "<script>" + expensive + "</script>" : "<html></html>";
        var options = new HtmlScriptRunOptions { MaximumStatements = 64 };

        await Assert.ThrowsAsync<StatementsCountOverflowException>(() => HtmlScriptRunner.RunAsync<string>(
            html, inline ? "'explicit'" : expensive, options, CancellationToken.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_TimeoutInterruptsInlineAndExplicitLoops(bool inline) {
        const string loop = "while (true) {}";
        var options = new HtmlScriptRunOptions {
            ExecutionTimeout = TimeSpan.FromMilliseconds(100), MaximumStatements = int.MaxValue
        };

        await Assert.ThrowsAsync<TimeoutException>(() => HtmlScriptRunner.RunAsync<object>(
            inline ? "<script>" + loop + "</script>" : "<html></html>",
            inline ? "1" : loop, options, CancellationToken.None));
        Assert.Equal(3, await HtmlScriptRunner.RunAsync<int>("<html></html>", "1 + 2"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_CallerCancellationInterruptsInlineAndExplicitLoops(bool inline) {
        const string loop = "while (true) {}";
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var options = new HtmlScriptRunOptions {
            ExecutionTimeout = Timeout.InfiniteTimeSpan, MaximumStatements = int.MaxValue
        };

        OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            HtmlScriptRunner.RunAsync<object>(inline ? "<script>" + loop + "</script>" : "<html></html>",
                inline ? "1" : loop, options, cancellation.Token));
        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task RunAsync_OperationBudgetInterruptsNativeRegex(bool inline, bool cancel) {
        const string regex = "var subject = 'a'.repeat(40) + '!'; /^(a+)+$/.test(subject)";
        await HtmlScriptRunner.RunAsync<int>("<html></html>", "1");
        using var cancellation = new CancellationTokenSource();
        var options = new HtmlScriptRunOptions {
            ExecutionTimeout = cancel ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(100),
            MaximumStatements = int.MaxValue
        };
        if (cancel) cancellation.CancelAfter(100);
        var elapsed = Stopwatch.StartNew();
        Func<Task<object?>> run = () => HtmlScriptRunner.RunAsync<object>(
            inline ? "<script>" + regex + "</script>" : "<html></html>",
            inline ? "1" : regex, options, cancellation.Token);

        if (cancel) {
            OperationCanceledException failure = await Assert.ThrowsAsync<OperationCanceledException>(run);
            Assert.Equal(cancellation.Token, failure.CancellationToken);
        } else {
            await Assert.ThrowsAsync<TimeoutException>(run);
        }
        // The old native matcher waited for its own ten-second timeout instead.
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(5), $"Operation took {elapsed.Elapsed}.");
    }

    [Fact]
    public async Task RunAsync_InlineAndRequestedScriptShareOneOperationDeadline() {
        const string work = "var until = Date.now() + 100; while (Date.now() < until) {}";
        var options = new HtmlScriptRunOptions {
            ExecutionTimeout = TimeSpan.FromMilliseconds(150), MaximumStatements = int.MaxValue
        };

        await Assert.ThrowsAsync<TimeoutException>(() => HtmlScriptRunner.RunAsync<object>(
            "<script>" + work + "</script>", work + "; 1", options, CancellationToken.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_AllocationBudgetRejectsGrowingScriptData(bool inline) {
        const string script = "var data = []; for (var i = 0; i < 10000; i++) { data.push('payload-' + i); } data.length";
        var options = new HtmlScriptRunOptions { MaximumMemoryBytes = 64 * 1024 };

        await Assert.ThrowsAsync<MemoryLimitExceededException>(() => HtmlScriptRunner.RunAsync<int>(
            inline ? "<script>" + script + "</script>" : "<html></html>", inline ? "1" : script,
            options, CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_CharacterBoundsAcceptExactInputAndRejectAdditionalCharacters() {
        const string html = "<p>bounded</p>";
        const string script = "1 + 2";
        var options = new HtmlScriptRunOptions { MaximumHtmlCharacters = html.Length, MaximumScriptCharacters = script.Length };
        Assert.Equal(3, await HtmlScriptRunner.RunAsync<int>(html, script, options, CancellationToken.None));

        ArgumentException htmlFailure = await Assert.ThrowsAsync<ArgumentException>(() =>
            HtmlScriptRunner.RunAsync<int>(html + " ", script, options, CancellationToken.None));
        Assert.Equal("html", htmlFailure.ParamName);
        ArgumentException scriptFailure = await Assert.ThrowsAsync<ArgumentException>(() =>
            HtmlScriptRunner.RunAsync<int>(html, script + " ", options, CancellationToken.None));
        Assert.Equal("script", scriptFailure.ParamName);
    }
}
