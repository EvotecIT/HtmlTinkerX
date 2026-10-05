using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Theory]
    [InlineData("page", false)]
    [InlineData("asset", false)]
    [InlineData("robots", false)]
    [InlineData("sitemap", false)]
    [InlineData("page", true)]
    [InlineData("asset", true)]
    [InlineData("robots", true)]
    [InlineData("sitemap", true)]
    public async Task CrawlResponseRecovery_PreservesBudgetExhaustionAndCallerCancellation(string responseKind, bool cancelCaller) {
        using CancellationTokenSource caller = new();
        using HttpClient client = new(new CompletedReadAtDeadlineHandler()) {
            Timeout = cancelCaller ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(50)
        };
        HtmlCrawlOptions options = StaticOptions(1);
        HtmlCrawlResponseBudget budget = new(responseKind == "asset"
            ? nameof(HtmlCrawlOptions.MaximumTotalAssetResponseBytes)
            : nameof(HtmlCrawlOptions.MaximumTotalPageResponseBytes), 8);
        if (responseKind == "asset") options.AssetResponseBudget = budget;
        else options.PageResponseBudget = budget;
        if (cancelCaller) caller.CancelAfter(50);

        // Use the existing HTTP boundary without adding a client factory to the public crawl API.
        Task recovery = InvokeCrawlResponseRecovery(responseKind, client, options, caller.Token);
        if (cancelCaller) {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => recovery);
            Assert.True(caller.IsCancellationRequested);
        } else {
            HtmlCrawlBudgetExceededException error = await Assert.ThrowsAsync<HtmlCrawlBudgetExceededException>(() => recovery);
            Assert.Equal(8, error.LimitBytes);
            Assert.Equal(9, error.ResponseBytesRead);
        }
    }

    private static Task InvokeCrawlResponseRecovery(string responseKind, HttpClient client, HtmlCrawlOptions options, CancellationToken cancellationToken) {
        Type crawler = typeof(HtmlCrawler);
        const BindingFlags privateStatic = BindingFlags.NonPublic | BindingFlags.Static;
        Uri uri = new("http://localhost/final");
        string methodName;
        object?[] arguments;
        if (responseKind == "asset") {
            methodName = "DownloadAssetAsync";
            arguments = new object?[] { client, uri.AbsoluteUri, uri.AbsoluteUri, options, null, cancellationToken };
        } else if (responseKind == "page") {
            methodName = "FetchHttpPageAsync";
            Type requestType = crawler.GetNestedType("CrawlRequest", BindingFlags.NonPublic)!;
            object request = Activator.CreateInstance(requestType, nonPublic: true)!;
            requestType.GetProperty("Uri")!.SetValue(request, uri);
            arguments = new object?[] { client, request, options, new Dictionary<string, HtmlCrawlJsonSchemaField>(), cancellationToken, null };
        } else {
            Type robotsType = crawler.GetNestedType("RobotsDocument", BindingFlags.NonPublic)!;
            object robotsCache = Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), robotsType))!;
            if (responseKind == "robots") {
                methodName = "GetRobotsDocumentAsync";
                arguments = new object?[] { uri, client, options, robotsCache, cancellationToken };
            } else {
                methodName = "DiscoverSitemapCandidatesAsync";
                options.SitemapUrls.Add("http://localhost/sitemap.xml");
                Type requestType = crawler.GetNestedType("CrawlRequest", BindingFlags.NonPublic)!;
                object pending = Activator.CreateInstance(typeof(Queue<>).MakeGenericType(requestType))!;
                arguments = new object?[] { uri, client, options, robotsCache, new HtmlCrawlResult(), pending,
                    new HashSet<string>(), new HashSet<string>(), cancellationToken };
            }
        }
        return (Task)crawler.GetMethod(methodName, privateStatic)!.Invoke(null, arguments)!;
    }

    private sealed class CompletedReadAtDeadlineHandler : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            HttpResponseMessage response = new(HttpStatusCode.OK) {
                RequestMessage = request,
                Content = new StreamContent(new CompletedReadAtDeadlineStream())
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            return Task.FromResult(response);
        }
    }

    private sealed class CompletedReadAtDeadlineStream : MemoryStream {
        public CompletedReadAtDeadlineStream() : base(new byte[9]) { }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            int read = Read(buffer, offset, count);
            try {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                // A transport read can finish while cancellation becomes observable.
            }
            return read;
        }
    }
}
