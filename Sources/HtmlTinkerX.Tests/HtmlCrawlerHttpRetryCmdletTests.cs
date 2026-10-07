#if !FRAMEWORK
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using PSParseHTML.PowerShell;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Fact]
    public void CrawlAsync_HttpRetryIsAvailableThroughThePublicPowerShellCommand() {
        int requests = 0;
        using HttpListener server = StartFlexibleServer(async context => {
            if (Interlocked.Increment(ref requests) == 1) {
                context.Response.StatusCode = 503;
                context.Response.Headers["Retry-After"] = "0";
            } else {
                await RespondAsync(context, "<main>Command recovered</main>");
            }
        }, out string root);
        var state = InitialSessionState.Create();
        state.Commands.Add(new SessionStateCmdletEntry("Invoke-HtmlCrawl", typeof(CmdletInvokeHtmlCrawl), null));
        using var runspace = RunspaceFactory.CreateRunspace(state);
        runspace.Open();
        using var command = PowerShell.Create();
        command.Runspace = runspace;
        command.AddCommand("Invoke-HtmlCrawl").AddParameter("Url", root).AddParameter("MaxPages", 1)
            .AddParameter("IgnoreRobotsTxt").AddParameter("NoSitemaps").AddParameter("HttpRetryCount", 1);

        HtmlCrawlResult result = Assert.IsType<HtmlCrawlResult>(Assert.Single(command.Invoke()).BaseObject);

        Assert.Empty(command.Streams.Error);
        HtmlCrawlPage page = Assert.Single(result.Pages);
        Assert.Equal(HtmlCrawlPageStatus.Success, page.Status);
        Assert.Contains("Command recovered", page.Text);
        Assert.Equal(2, Volatile.Read(ref requests));
    }
}
#endif
