using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Fact]
    public async Task CrawlAsync_PageSnapshotAfterReleaseSavesItsCurrentContentWithoutTheSourceDataset() {
        using var server = StartServer(new Dictionary<string, string> {
            ["/"] = "<main>Previously released content</main>"
        }, out string root);
        string outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string snapshotPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        HtmlCrawlOptions options = StaticOptions(1);
        options.OutputPath = outputPath;
        options.RetainPageContent = false;
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root, options);
            HtmlCrawlPage snapshot = Assert.Single(result.Pages).CreateSnapshot();
            Assert.Empty(snapshot.Html);
            Assert.Empty(snapshot.Text);
            Directory.Delete(outputPath, true);
            result.Pages.Clear();
            result.Pages.Add(snapshot);
            await HtmlCrawler.SaveResultAsync(result, snapshotPath);
            HtmlCrawlPage saved = Assert.Single((await HtmlCrawler.LoadResultAsync(snapshotPath)).Pages);
            Assert.Empty(saved.Html);
            Assert.Empty(saved.Text);
        } finally {
            if (Directory.Exists(outputPath)) Directory.Delete(outputPath, true);
            if (Directory.Exists(snapshotPath)) Directory.Delete(snapshotPath, true);
        }
    }

    [Fact]
    public async Task CrawlAsync_PageObserverCanKeepContentWhileResultReleasesIt() {
        using var server = StartServer(new Dictionary<string, string> {
            ["/"] = "<main>Observed content</main>"
        }, out string root);
        string outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var observed = new List<HtmlCrawlPage>();
        HtmlCrawlOptions options = StaticOptions(1);
        options.OutputPath = outputPath;
        options.RetainPageContent = false;
        options.IncludeMarkdown = true;
        options.PageObserver = new DelegatePageObserver((page, token) => {
            observed.Add(page.CreateSnapshot());
            return Task.CompletedTask;
        });
        try {
            HtmlCrawlPage original = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);
            HtmlCrawlPage snapshot = Assert.Single(observed);
            Assert.NotSame(original, snapshot);
            Assert.Empty(original.Html);
            Assert.Empty(original.Text);
            Assert.Empty(original.Markdown);
            Assert.Equal(original.ContentFingerprint, snapshot.ContentFingerprint);
            Assert.Contains("Observed content", snapshot.Html);
            Assert.Contains("Observed content", snapshot.Text);
            Assert.Contains("Observed content", snapshot.Markdown);
        } finally {
            if (Directory.Exists(outputPath)) Directory.Delete(outputPath, true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrawlAsync_PageObserverReceivesExtractedStaticAndRenderedPages(bool render) {
        using var server = StartServer(new Dictionary<string, string> { ["/"] = "<main>Observed content</main>" }, out string root);
        HtmlCrawlOptions options = StaticOptions(1);
        options.Render = render;
        var observed = new List<HtmlCrawlPage>();
        options.PageObserver = new DelegatePageObserver((page, token) => {
            Assert.NotEqual(default, page.Finished);
            Assert.Contains("Observed content", page.Text);
            observed.Add(page);
            return Task.CompletedTask;
        });
        Assert.Same(options.PageObserver, options.Clone().PageObserver);
        Assert.DoesNotContain("PageObserver", JsonSerializer.Serialize(options));
        HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root, options);
        HtmlCrawlPage page = Assert.Single(result.Pages);
        Assert.Same(page, Assert.Single(observed));
        Assert.Equal(render, page.Rendered);
    }

    [Fact]
    public async Task CrawlAsync_PageObserverReceivesFetchFailuresInResultOrder() {
        using var server = StartServer(new Dictionary<string, string> { ["/"] = "<main>Home<a href='/missing'>Missing</a></main>" }, out string root);
        HtmlCrawlOptions options = StaticOptions(2);
        var observed = new List<HtmlCrawlPage>();
        options.PageObserver = new DelegatePageObserver((page, token) => { observed.Add(page); return Task.CompletedTask; });
        HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(root, options);
        Assert.Equal(2, observed.Count);
        Assert.Equal(result.Pages, observed);
        Assert.Equal(HtmlCrawlPageStatus.Success, observed[0].Status);
        Assert.Equal(HtmlCrawlPageStatus.Failed, observed[1].Status);
        Assert.Equal(404, observed[1].StatusCode);
    }

    [Fact]
    public async Task CrawlAsync_ObserverFailurePreservesCheckpointAndResumeRetriesOnlyUncommittedPages() {
        string output = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        using var server = StartServer(new Dictionary<string, string> {
            ["/"] = "<main>Home<a href='/next'>Next</a></main>", ["/next"] = "<main>Second page</main>"
        }, out string root);
        HtmlCrawlOptions options = StaticOptions(2);
        options.OutputPath = output;
        var delivered = new List<string>();
        options.PageObserver = new DelegatePageObserver(async (page, token) => {
            await Task.Yield();
            delivered.Add(page.Url);
            if (delivered.Count == 2) throw new InvalidOperationException("sink failed");
        });
        try {
            InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(() => HtmlCrawler.CrawlAsync(root, options));
            Assert.Equal("sink failed", failure.Message);
            HtmlCrawlResult checkpoint = await HtmlCrawler.LoadResultAsync(output);
            Assert.Equal(root, Assert.Single(checkpoint.Pages).Url);
            Assert.EndsWith("/next", Assert.Single(checkpoint.PendingPages).Url);
            delivered.Clear();
            options.ResumePath = output;
            options.PageObserver = new DelegatePageObserver((page, token) => { delivered.Add(page.Url); return Task.CompletedTask; });
            HtmlCrawlResult resumed = await HtmlCrawler.CrawlAsync(root, options);
            Assert.Equal(2, resumed.Pages.Count);
            Assert.Equal(resumed.Pages[1].Url, Assert.Single(delivered));
        } finally { if (Directory.Exists(output)) Directory.Delete(output, true); }
    }

    [Fact]
    public async Task CrawlAsync_CancellationReachesAnAwaitedPageObserverBeforeTheNextFetch() {
        int secondRequests = 0;
        using var server = StartServer(new Dictionary<string, string> {
            ["/"] = "<main>Home<a href='/next'>Next</a></main>", ["/next"] = "<main>Second page</main>"
        }, out string root, onRequest: path => { if (path == "/next") Interlocked.Increment(ref secondRequests); });
        using CancellationTokenSource stopping = new();
        TaskCompletionSource<bool> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        HtmlCrawlOptions options = StaticOptions(2);
        options.PageObserver = new DelegatePageObserver(async (page, token) => {
            Assert.Equal(stopping.Token, token);
            entered.TrySetResult(true);
            await Task.Delay(Timeout.Infinite, token);
        });
        Task<HtmlCrawlResult> pending = HtmlCrawler.CrawlAsync(root, options, stopping.Token);
        try {
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, Task.Delay(5000)));
            Assert.False(pending.IsCompleted);
            stopping.Cancel();
            OperationCanceledException failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.Equal(stopping.Token, failure.CancellationToken);
            Assert.Equal(0, secondRequests);
        } finally { stopping.Cancel(); }
    }

    private sealed class DelegatePageObserver : IHtmlCrawlPageObserver {
        private readonly Func<HtmlCrawlPage, CancellationToken, Task> _observe;
        public DelegatePageObserver(Func<HtmlCrawlPage, CancellationToken, Task> observe) => _observe = observe;
        public Task ObserveAsync(HtmlCrawlPage page, CancellationToken cancellationToken = default) => _observe(page, cancellationToken);
    }
}
