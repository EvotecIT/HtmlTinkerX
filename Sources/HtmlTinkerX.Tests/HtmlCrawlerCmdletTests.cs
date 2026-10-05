#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Net;
using System.Threading.Tasks;
using PSParseHTML.PowerShell;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvokeHtmlCrawl_StreamedPagesKeepTheirContentAfterPersistence(bool releaseContent) {
        using HttpListener server = StartServer(new Dictionary<string, string> {
            ["/"] = "<main>Streamed content</main>"
        }, out string root);
        string outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try {
            InitialSessionState state = InitialSessionState.Create();
            state.Commands.Add(new SessionStateCmdletEntry("Invoke-HtmlCrawl", typeof(CmdletInvokeHtmlCrawl), null));
            using Runspace runspace = RunspaceFactory.CreateRunspace(state);
            runspace.Open();
            using PowerShell command = PowerShell.Create();
            command.Runspace = runspace;
            command.AddCommand("Invoke-HtmlCrawl")
                .AddParameter("Url", root)
                .AddParameter("MaxPages", 1)
                .AddParameter("NoSitemaps", true)
                .AddParameter("IgnoreRobotsTxt", true)
                .AddParameter("OutPath", outputPath)
                .AddParameter("IncludeHtml", true)
                .AddParameter("IncludeMarkdown", true)
                .AddParameter("StreamPages", true)
                .AddParameter("ReleasePageContent", releaseContent);

            var output = command.Invoke();
            Assert.Empty(command.Streams.Error);
            HtmlCrawlPage page = Assert.IsType<HtmlCrawlPage>(Assert.Single(output).BaseObject);
            Assert.Contains("Streamed content", page.Html);
            Assert.Contains("Streamed content", page.Text);
            Assert.Contains("Streamed content", page.Markdown);
            HtmlCrawlPage savedPage = Assert.Single((await HtmlCrawler.LoadResultAsync(outputPath)).Pages);
            Assert.Contains("Streamed content", savedPage.Text);
        } finally {
            if (Directory.Exists(outputPath)) Directory.Delete(outputPath, true);
        }
    }

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
