using HtmlTinkerX;
using System.Linq;
using System.Threading;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Fact]
    public async Task CrawlAsync_InvokesRenderedPageObserverWithoutSecondNavigation() {
        var responses = new System.Collections.Generic.Dictionary<string, string> {
            ["/"] = """
<html>
  <head><title>Rendered observer</title></head>
  <body><main>Prepared rendered content</main></body>
</html>
"""
        };

        int documentRequestCount = 0;
        using var server = StartServer(responses, out string rootUrl, onRequest: path => {
            if (path == "/") {
                Interlocked.Increment(ref documentRequestCount);
            }
        });
        RecordingRenderedPageObserver observer = new();
        HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
            MaxDepth = 0,
            MaxPages = 1,
            Render = true,
            RenderedPageObserver = observer
        });

        HtmlCrawlPage page = Assert.Single(result.Pages);
        Assert.Equal(1, observer.Count);
        Assert.Same(page, observer.Page);
        Assert.Equal(page.Url, observer.SessionUrl);
        Assert.Contains("Prepared rendered content", observer.BrowserHtml, System.StringComparison.Ordinal);
        Assert.Equal(1, documentRequestCount);
        Assert.Equal(HtmlCrawlRenderReasonCode.ExplicitRender, observer.RenderReasonCodeAtObservation);
        Assert.NotEqual(default, observer.FinishedAtObservation);
        Assert.Equal(page.Finished, observer.FinishedAtObservation);
        Assert.NotEmpty(observer.NetworkLog);
        Assert.Contains(observer.NetworkLog, entry => entry.Url == rootUrl);
        Assert.True(observer.Session!.Page.IsClosed);
    }

    [Fact]
    public async Task CrawlAsync_PropagatesRenderedPageObserverFailureAndCancellation() {
        var responses = new System.Collections.Generic.Dictionary<string, string> {
            ["/"] = "<html><body><main>Observer failures remain distinct.</main></body></html>"
        };

        using var server = StartServer(responses, out string rootUrl);
        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Render = true,
                RenderedPageObserver = new ThrowingRenderedPageObserver(cancel: false)
            }));
        Assert.Equal("observer failure", failure.Message);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Render = true,
                RenderedPageObserver = new ThrowingRenderedPageObserver(cancel: true)
            }));
    }

    [Fact]
    public async Task CrawlAsync_AutoRenderObserverReceivesOnlyCurrentPageNetworkEntries() {
        var responses = new System.Collections.Generic.Dictionary<string, string> {
            ["/"] = "<html><body><div id='app'></div><script>document.getElementById('app').innerHTML=\"<a href='/second'>Rendered home</a>\";</script></body></html>",
            ["/second"] = "<html><body><main>Second rendered page</main></body></html>"
        };

        using var server = StartServer(responses, out string rootUrl);
        RecordingRenderedPageObserver observer = new();
        HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
            MaxDepth = 1,
            MaxPages = 2,
            AutoRender = true,
            AutoRenderTextWordThreshold = 20,
            RenderedPageObserver = observer
        });

        Assert.Equal(2, result.PageCount);
        Assert.Equal(2, observer.Observations.Count);
        Assert.All(observer.Observations, observation => Assert.Equal(HtmlCrawlRenderMode.AutoRendered, observation.RenderMode));
        Assert.Contains(observer.Observations[0].NetworkUrls, url => url == rootUrl);
        string secondUrl = new Uri(new Uri(rootUrl), "/second").AbsoluteUri;
        Assert.Contains(observer.Observations[1].NetworkUrls, url => url == secondUrl);
        Assert.DoesNotContain(observer.Observations[1].NetworkUrls, url => url == rootUrl);
    }

    private sealed class RecordingRenderedPageObserver : IHtmlCrawlRenderedPageObserver {
        public int Count { get; private set; }

        public HtmlCrawlPage? Page { get; private set; }

        public string? SessionUrl { get; private set; }

        public string? BrowserHtml { get; private set; }

        public HtmlBrowserSession? Session { get; private set; }

        public HtmlCrawlRenderReasonCode RenderReasonCodeAtObservation { get; private set; }

        public System.DateTimeOffset FinishedAtObservation { get; private set; }

        public System.Collections.Generic.IReadOnlyList<HtmlNetworkEntry> NetworkLog { get; private set; } = System.Array.Empty<HtmlNetworkEntry>();

        public System.Collections.Generic.List<RenderedPageObservation> Observations { get; } = new();

        public async Task ObserveAsync(HtmlCrawlRenderedPageContext context, CancellationToken cancellationToken = default) {
            Count++;
            Page = context.Page;
            Session = context.Session;
            SessionUrl = context.Session.Page.Url;
            BrowserHtml = await context.Session.Page.ContentAsync();
            RenderReasonCodeAtObservation = context.Page.RenderReasonCode;
            FinishedAtObservation = context.Page.Finished;
            NetworkLog = context.NetworkLog;
            Observations.Add(new RenderedPageObservation(
                context.Page.Url,
                context.Page.RenderMode,
                context.NetworkLog.Select(entry => entry.Url).ToArray()));
        }
    }

    private sealed class ThrowingRenderedPageObserver : IHtmlCrawlRenderedPageObserver {
        private readonly bool _cancel;

        public ThrowingRenderedPageObserver(bool cancel) => _cancel = cancel;

        public Task ObserveAsync(HtmlCrawlRenderedPageContext context, CancellationToken cancellationToken = default) {
            if (_cancel) {
                throw new OperationCanceledException(cancellationToken);
            }

            throw new InvalidOperationException("observer failure");
        }
    }

    private sealed class RenderedPageObservation {
        public RenderedPageObservation(
            string url,
            HtmlCrawlRenderMode renderMode,
            System.Collections.Generic.IReadOnlyList<string> networkUrls) {
            Url = url;
            RenderMode = renderMode;
            NetworkUrls = networkUrls;
        }

        public string Url { get; }

        public HtmlCrawlRenderMode RenderMode { get; }

        public System.Collections.Generic.IReadOnlyList<string> NetworkUrls { get; }
    }
}
