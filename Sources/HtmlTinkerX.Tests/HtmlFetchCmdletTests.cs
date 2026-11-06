#if !FRAMEWORK
using System.Collections.Concurrent;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Net;
using System.Text;
using PSParseHTML.PowerShell;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerTests {
    [Fact]
    public void GetHtmlPage_ABlockedPageIsATypedErrorWhoseTargetIsTheFailure() {
        using HttpListener server = StartFlexibleServer(async context => {
            context.Response.StatusCode = 403;
            byte[] body = Encoding.UTF8.GetBytes("<html><head><title>Access Denied</title></head><body><h1>Access Denied</h1>"
                + "You don't have permission to access this page on this server.<p>Reference #18.8f6d1402.1730812345.1a2b3c</p></body></html>");
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.OutputStream.WriteAsync(body, 0, body.Length);
        }, out string root);
        var state = InitialSessionState.CreateDefault2();
        state.Commands.Add(new SessionStateCmdletEntry("Get-HtmlPage", typeof(CmdletGetHtmlPage), null));
        using var runspace = RunspaceFactory.CreateRunspace(state);
        runspace.Open();
        using var command = PowerShell.Create();
        command.Runspace = runspace;
        command.AddCommand("Get-HtmlPage").AddParameter("Url", root).AddParameter("RetryCount", 0);

        RuntimeException error = Assert.ThrowsAny<RuntimeException>(() => command.Invoke());

        Assert.Equal("HtmlHttpBlockPage,PSParseHTML.PowerShell.CmdletGetHtmlPage", error.ErrorRecord.FullyQualifiedErrorId);
        Assert.Equal(ErrorCategory.PermissionDenied, error.ErrorRecord.CategoryInfo.Category);
        HtmlHttpFailure failure = Assert.IsType<HtmlHttpFailure>(error.ErrorRecord.TargetObject);
        Assert.Equal("Akamai", failure.Provider);
        Assert.IsType<HtmlHttpException>(error.ErrorRecord.Exception);
    }

    [Fact]
    public void UrlCmdletsTakeTheFetchParametersAndReplayFromTheCacheOnceTheSiteIsGone() {
        ConcurrentQueue<string?> userAgents = new();
        HttpListener server = StartFlexibleServer(context => {
            userAgents.Enqueue(context.Request.UserAgent);
            return RespondAsync(context, "<html><head><meta name=\"description\" content=\"Stored\"></head></html>");
        }, out string root);
        string directory = Path.Combine(Path.GetTempPath(), "htmltinkerx-replay-" + Guid.NewGuid().ToString("N"));
        var state = InitialSessionState.CreateDefault2();
        state.Commands.Add(new SessionStateCmdletEntry("ConvertFrom-HtmlMeta", typeof(CmdletConvertFromHtmlMeta), null));
        using var runspace = RunspaceFactory.CreateRunspace(state);
        runspace.Open();
        string? Describe() {
            using var command = PowerShell.Create();
            command.Runspace = runspace;
            command.AddCommand("ConvertFrom-HtmlMeta").AddParameter("Url", root).AddParameter("BrowserHeaders").AddParameter("RetryCount", 0)
                .AddParameter("ReplayCache", directory);
            return command.Invoke().Single(meta => (string)meta.Properties["Name"].Value == "description").Properties["Content"].Value as string;
        }
        try {
            Assert.Equal("Stored", Describe());
            server.Close();

            Assert.Equal("Stored", Describe());
            Assert.Equal(HtmlBrowserHeaderProfile.Chrome().UserAgent, Assert.Single(userAgents));
        } finally {
            server.Close();
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
#endif
