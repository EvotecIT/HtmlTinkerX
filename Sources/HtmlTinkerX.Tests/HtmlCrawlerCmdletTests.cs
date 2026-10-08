#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Net;
using PSParseHTML.PowerShell;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvokeHtmlCrawl_StreamPagesReturnsPageObjectsAndDefaultReturnsTheResult(bool streamPages) {
        using HttpListener server = StartServer(new Dictionary<string, string> {
            ["/"] = "<main>Home<a href='/missing'>Next</a></main>"
        }, out string root);
        InitialSessionState state = InitialSessionState.Create();
        state.Commands.Add(new SessionStateCmdletEntry("Invoke-HtmlCrawl", typeof(CmdletInvokeHtmlCrawl), null));
        using Runspace runspace = RunspaceFactory.CreateRunspace(state);
        runspace.Open();
        using PowerShell command = PowerShell.Create();
        command.Runspace = runspace;
        command.AddCommand("Invoke-HtmlCrawl")
            .AddParameter("Url", root)
            .AddParameter("MaxPages", 2)
            .AddParameter("NoSitemaps", true)
            .AddParameter("IgnoreRobotsTxt", true)
            .AddParameter("StreamPages", streamPages);

        var output = command.Invoke();
        Assert.Empty(command.Streams.Error);
        HtmlCrawlPage[] pages;
        if (streamPages) {
            Assert.Equal(2, output.Count);
            pages = new[] {
                Assert.IsType<HtmlCrawlPage>(output[0].BaseObject),
                Assert.IsType<HtmlCrawlPage>(output[1].BaseObject)
            };
        } else {
            HtmlCrawlResult result = Assert.IsType<HtmlCrawlResult>(Assert.Single(output).BaseObject);
            Assert.Equal(2, result.Pages.Count);
            pages = result.Pages.ToArray();
        }
        Assert.Equal(HtmlCrawlPageStatus.Success, pages[0].Status);
        Assert.Contains("Home", pages[0].Text);
        Assert.Equal(HtmlCrawlPageStatus.Failed, pages[1].Status);
        Assert.Equal(404, pages[1].StatusCode);
    }
}
#endif
