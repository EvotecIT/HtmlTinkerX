using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrawlAsync_FailedCheckpointPublicationRemovesUnpublishedRecordsAndPreservesResume(bool unsupported) {
        string output = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string manifest = Path.Combine(output, "crawl-result.json");
        string? published = null;
        bool injectFailure = true;
        using HttpListener server = StartFlexibleServer(async context => {
            string path = context.Request.Url!.AbsolutePath;
            if (path == "/next" && injectFailure) {
                published = File.ReadAllText(manifest);
                File.Delete(manifest);
                Directory.CreateDirectory(manifest); // Simulate a filesystem failure at the manifest commit boundary.
            }
            await RespondAsync(context, path == "/" ? "<main>Root<a href='/next'>Next</a></main>"
                : path == "/next" ? unsupported ? "PDF data" : "<main>Next<img src='/asset.png'></main>" : "asset",
                path == "/asset.png" ? "image/png" : unsupported && path == "/next" ? "application/pdf" : "text/html");
        }, out string root);
        try {
            HtmlCrawlOptions options = StaticOptions(2); options.OutputPath = output; options.DownloadAssets = true;
            await Assert.ThrowsAnyAsync<IOException>(() => HtmlCrawler.CrawlAsync(root, options));
            Assert.NotNull(published);
            Directory.Delete(manifest);
            File.WriteAllText(manifest, published);
            HtmlCrawlResult previous = await HtmlCrawler.LoadResultAsync(output);
            Assert.Single(previous.Pages); Assert.Single(previous.PendingPages);
            string generation = Assert.Single(Directory.GetDirectories(manifest + ".state"));
            Assert.Equal("page-00000000.json", Path.GetFileName(Assert.Single(Directory.GetFiles(generation))));
            injectFailure = false;
            options.ResumePath = output;
            HtmlCrawlResult resumed = await HtmlCrawler.CrawlAsync(root, options);
            if (unsupported) {
                Assert.Single(resumed.Pages); Assert.Single(resumed.SkippedPages);
            } else {
                Assert.Equal(2, resumed.Pages.Count); Assert.Single(resumed.Assets);
            }
            Assert.False(Directory.Exists(manifest + ".state"));
        } finally { if (Directory.Exists(output)) Directory.Delete(output, true); }
    }

    [Fact]
    public async Task CrawlAsync_BasicAuthenticationWithoutCustomHeadersBlocksServiceWorkerBypass() {
        ConcurrentQueue<string?> protectedAuthorization = new();
        const string expectedAuthorization = "Basic dXNlcjpwYXNzd29yZA==";
        using HttpListener server = StartFlexibleServer(async context => {
            string path = context.Request.Url!.AbsolutePath;
            if (path == "/worker.js") {
                await RespondAsync(context, "self.addEventListener('install', e => e.waitUntil(self.skipWaiting()));"
                    + "self.addEventListener('activate', e => e.waitUntil(self.clients.claim()));"
                    + "self.addEventListener('message', e => e.waitUntil(fetch('/protected').then(r=>r.text()).then(t=>e.ports[0].postMessage(t))));", "application/javascript");
            } else if (path == "/protected") {
                string? authorization = context.Request.Headers["Authorization"];
                protectedAuthorization.Enqueue(authorization);
                await RespondAsync(context, authorization == expectedAuthorization ? "authenticated" : "unauthenticated", "text/plain");
            } else {
                await RespondAsync(context, "<main>Worker boundary</main><script>(async()=>{"
                    + "navigator.serviceWorker.register('/worker.js').then(async()=>{let registration=await navigator.serviceWorker.ready;"
                    + "let channel=new MessageChannel();channel.port1.onmessage=e=>document.body.dataset.workerResult=e.data;"
                    + "registration.active.postMessage('fetch',[channel.port2]);}).catch(()=>{});"
                    + "document.body.dataset.result=await fetch('/protected').then(r=>r.text());})();</script>");
            }
        }, out string root, "127.0.0.1");
        HtmlCrawlOptions options = StaticOptions(1); options.Render = true;
        options.Username = "user"; options.Password = "password";
        options.WaitForSelector = "body[data-result]";
        WorkerBoundaryObserver observer = new(); options.RenderedPageObserver = observer;
        HtmlCrawlPage page = Assert.Single((await HtmlCrawler.CrawlAsync(root, options)).Pages);
        Assert.True(page.Status == HtmlCrawlPageStatus.Success, page.Error);
        Assert.Equal(0, observer.Registrations);
        Assert.Equal("authenticated", observer.Result);
        Assert.NotEmpty(protectedAuthorization);
        Assert.All(protectedAuthorization, value => Assert.Equal(expectedAuthorization, value));
    }

    private sealed class WorkerBoundaryObserver : IHtmlCrawlRenderedPageObserver {
        public int Registrations { get; private set; }
        public string? Result { get; private set; }
        public async Task ObserveAsync(HtmlCrawlRenderedPageContext context, CancellationToken cancellationToken = default) {
            Registrations = await context.Session.Page.EvaluateAsync<int>("async()=> (await navigator.serviceWorker.getRegistrations()).length");
            Result = await context.Session.Page.GetAttributeAsync("body", "data-result");
        }
    }
}
