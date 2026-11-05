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
            ["/"] = "<main>Streamed content</main><script>cachedOutsideSelection</script>"
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
                .AddParameter("CacheResponses", true)
                .AddParameter("Selector", "main")
                .AddParameter("StreamPages", true)
                .AddParameter("ReleasePageContent", releaseContent);

            var output = command.Invoke();
            Assert.Empty(command.Streams.Error);
            HtmlCrawlPage page = Assert.IsType<HtmlCrawlPage>(Assert.Single(output).BaseObject);
            Assert.Contains("Streamed content", page.Html);
            Assert.Contains("Streamed content", page.Text);
            Assert.Contains("Streamed content", page.Markdown);
            Assert.DoesNotContain("cachedOutsideSelection", page.Html);
            if (releaseContent) Assert.Null(page.HttpCache);
            else Assert.NotNull(page.HttpCache);
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

    [Fact]
    public void InvokeHtmlCrawl_ProcessWritesScriptOutputAndFollowsItsRequests() {
        var requested = new List<string>();
        using HttpListener server = StartServer(new Dictionary<string, string> {
            ["/"] = "<main><h1>Home</h1><a href='/linked'>Linked</a></main>",
            ["/next"] = "<main><h1>Next</h1></main>"
        }, out string root, onRequest: path => { lock (requested) requested.Add(path); });
        InitialSessionState state = InitialSessionState.Create();
        state.LanguageMode = PSLanguageMode.FullLanguage;
        state.Commands.Add(new SessionStateCmdletEntry("Invoke-HtmlCrawl", typeof(CmdletInvokeHtmlCrawl), null));
        using Runspace runspace = RunspaceFactory.CreateRunspace(state);
        runspace.Open();
        using PowerShell command = PowerShell.Create();
        command.Runspace = runspace;
        command.AddCommand("Invoke-HtmlCrawl")
            .AddParameter("Url", root)
            .AddParameter("MaxPages", 5)
            .AddParameter("NoSitemaps", true)
            .AddParameter("IgnoreRobotsTxt", true)
            .AddParameter("Process", ScriptBlock.Create("""
                [pscustomobject]@{ Heading = $_.Document.QuerySelector('h1').TextContent; From = $_.Data['from'] }
                if ($_.Depth -eq 0) { $_.Follow('/next', @{ from = 'home' }) }
                $_.FollowLinks = $false
                """));

        var output = command.Invoke();

        Assert.Empty(command.Streams.Error);
        Assert.Equal(new[] { "Home", "Next" }, output.Select(item => (string)item.Properties["Heading"].Value));
        Assert.Null(output[0].Properties["From"].Value);
        Assert.Equal("home", output[1].Properties["From"].Value);
        lock (requested) Assert.DoesNotContain("/linked", requested);
    }
}
#endif
