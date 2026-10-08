#if !NETFRAMEWORK
using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;
using PSParseHTML.PowerShell;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Playwright collection")]
public sealed class HtmlFormBrowserCmdletTests {
    [Theory]
    [InlineData("id", false)]
    [InlineData("index", true)]
    [InlineData("selector", false)]
    public async Task BrowserCommandTargetsTheSelectedFormAndReturnsTheCallerSession(string identity, bool defaultSession) {
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync("about:blank");
        string first = identity == "selector" ? "target" : "other";
        string second = identity == "selector" ? "other" : "target";
        await session.Page.SetContentAsync($"""
            <form id="{first}" onsubmit="event.preventDefault(); window.submitted=this.id+':'+new FormData(this).get('q')">
              <input name="q" value="first">
            </form>
            <form id="{second}" onsubmit="event.preventDefault(); window.submitted=this.id+':'+new FormData(this).get('q')">
              <input name="q" value="second">
            </form>
            """);
        if (identity == "selector") await session.Page.Locator("#other").EvaluateAsync("form => form.remove()");
        InitialSessionState state = InitialSessionState.Create();
        state.Commands.Add(new SessionStateCmdletEntry("Submit-HtmlBrowserForm", typeof(CmdletSubmitHtmlBrowserForm), null));
        using Runspace runspace = RunspaceFactory.CreateRunspace(state);
        runspace.Open();
        if (defaultSession) runspace.SessionStateProxy.SetVariable("PSParseHTML_DefaultSession", session);
        PSObject form = new();
        if (identity == "id") form.Properties.Add(new PSNoteProperty("FormId", "target"));
        if (identity == "index") form.Properties.Add(new PSNoteProperty("FormIndex", 1));
        using PowerShell command = PowerShell.Create();
        command.Runspace = runspace;
        command.AddCommand("Submit-HtmlBrowserForm")
            .AddParameter("Form", form)
            .AddParameter("FieldValue", new Hashtable { ["q"] = "new value" })
            .AddParameter("PassThru")
            .AddParameter("Timeout", 2000);
        if (!defaultSession) command.AddParameter("Session", session);

        PSObject result = Assert.Single(command.Invoke());

        Assert.Empty(command.Streams.Error);
        Assert.Same(session, result.BaseObject);
        Assert.Equal("target:new value", await session.Page.EvaluateAsync<string>("() => window.submitted"));
        if (identity != "selector") Assert.Equal("first", await session.Page.Locator("#other input").InputValueAsync());
        await session.Page.EvaluateAsync("() => window.callerStillOwnsSession = true");
        Assert.True(await session.Page.EvaluateAsync<bool>("() => window.callerStillOwnsSession"));
    }
}
#endif
