using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Fact]
    public async Task FetchRenderer_ReturnsTheFinalAddressAndRejectsAPageTheBrowserWasRefusedToo() {
        using HttpListener server = StartFlexibleServer(async context => {
            switch (context.Request.Url!.AbsolutePath) {
                case "/moved":
                    context.Response.StatusCode = 302;
                    context.Response.Headers["Location"] = "/article";
                    break;
                case "/article":
                    await RespondAsync(context, "<main>Rendered article</main>");
                    break;
                default:
                    context.Response.StatusCode = 403;
                    await RespondAsync(context, "<html><head><title>Access Denied</title></head><body><h1>Access Denied</h1>"
                        + "You don't have permission to access this page on this server.<p>Reference #18.8f6d1402.1730812345.1a2b3c</p></body></html>");
                    break;
            }
        }, out string root);
        Func<Uri, CancellationToken, Task<HtmlRenderedPage>> render = HtmlBrowser.CreateFetchRenderer();

        HtmlRenderedPage page = await render(new Uri(root + "moved"), CancellationToken.None);
        HtmlHttpException refused = await Assert.ThrowsAsync<HtmlHttpException>(() => render(new Uri(root + "blocked"), CancellationToken.None));

        Assert.Contains("Rendered article", page.Html);
        Assert.Equal(new Uri(root + "article"), page.FinalUri);
        Assert.Equal(HtmlHttpFailureKind.BlockPage, refused.Kind);
        Assert.Equal("Akamai", refused.Provider);
        Assert.Equal(HttpStatusCode.Forbidden, refused.Failure.StatusCode);
    }
}
