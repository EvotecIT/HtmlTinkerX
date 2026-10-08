using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FlexibleFixtureAcceptsRequestsWhileAnotherResponseIsPending(bool flushRedirectHeaders) {
        TaskCompletionSource<bool> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using HttpListener server = StartFlexibleServer(async context => {
            if (context.Request.Url!.AbsolutePath == "/pending") {
                if (flushRedirectHeaders) {
                    context.Response.StatusCode = 302;
                    context.Response.RedirectLocation = "/final";
                    context.Response.ContentLength64 = 1;
                    await context.Response.OutputStream.FlushAsync();
                }
                entered.TrySetResult(true);
                await release.Task;
                await RespondAsync(context, "x");
            } else await RespondAsync(context, "final");
        }, out string root);
        using HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false }) {
            Timeout = TimeSpan.FromSeconds(2)
        };
        Task<HttpResponseMessage> pending = client.GetAsync(root + "pending");
        try {
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, Task.Delay(5000)));
            Assert.Equal("final", await client.GetStringAsync(root + "final"));
        } finally {
            release.TrySetResult(true);
            try { using HttpResponseMessage response = await pending; } catch (TaskCanceledException) { }
        }
    }
}
