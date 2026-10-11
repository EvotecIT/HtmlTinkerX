#if !FRAMEWORK
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
using PSParseHTML.PowerShell;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Fact]
    public async System.Threading.Tasks.Task CrawlAsync_ConcurrentDownloadsAreAvailableThroughThePublicPowerShellCommand() {
        var bothEntered = CrawlSignal();
        int children = 0;
        await using ConcurrentCrawlServer server = new(async (context, token) => {
            if (context.Request.Url!.AbsolutePath == "/") await RespondAsync(context, "<a href='/one'>One</a><a href='/two'>Two</a>");
            else {
                if (Interlocked.Increment(ref children) == 2) bothEntered.TrySetResult(true);
                await AwaitCrawlSignalAsync(bothEntered.Task, token);
                await RespondAsync(context, "<main>Concurrent command page</main>");
            }
        });
        InitialSessionState state = InitialSessionState.Create();
        state.Commands.Add(new SessionStateCmdletEntry("Invoke-HtmlCrawl", typeof(CmdletInvokeHtmlCrawl), null));
        using Runspace runspace = RunspaceFactory.CreateRunspace(state);
        runspace.Open();
        using PowerShell command = PowerShell.Create();
        command.Runspace = runspace;
        command.AddCommand("Invoke-HtmlCrawl").AddParameter("Url", server.Url).AddParameter("MaxPages", 3)
            .AddParameter("IgnoreRobotsTxt").AddParameter("NoSitemaps")
            .AddParameter("MaxConcurrentPageRequests", 2).AddParameter("MaxConcurrentPageRequestsPerOrigin", 2);
        HtmlCrawlResult result = Assert.IsType<HtmlCrawlResult>(Assert.Single(command.Invoke()).BaseObject);
        Assert.Empty(command.Streams.Error);
        Assert.Equal(3, result.Pages.Count);
        Assert.All(result.Pages, page => Assert.Equal(HtmlCrawlPageStatus.Success, page.Status));
        Assert.Equal(2, Volatile.Read(ref children));
        Assert.All(result.Pages.Skip(1), page => Assert.Contains("Concurrent command page", page.Text));
    }
}
#endif
