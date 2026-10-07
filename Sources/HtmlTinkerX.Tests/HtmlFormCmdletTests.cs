#if !NETFRAMEWORK
using System;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using HtmlTinkerX;
using PSParseHTML.PowerShell;

namespace HtmlTinkerX.Tests;

public class HtmlFormCmdletTests {
    [Fact]
    public void ContentCommandPreservesOwnedValuesAndResolvedActionMetadata() {
        using Runspace runspace = CreateRunspace();
        using PowerShell command = PowerShell.Create();
        command.Runspace = runspace;
        command.AddCommand("ConvertFrom-HtmlForm")
            .AddParameter("Content", """
                <input form="settings" name="before" value="one">
                <form id="settings" action="save" class="account">
                  <input type="hidden" name="tag" value="first">
                  <input type="hidden" name="tag" value="second">
                  <input name="disabled" value="omit" disabled>
                </form>
                """)
            .AddParameter("BaseUri", new Uri("https://example.test/account/"))
            .AddParameter("IncludeMetadata");

        PSObject form = Assert.Single(command.Invoke());

        Assert.Empty(command.Streams.Error);
        Assert.Equal("save", form.Properties["Action"].Value);
        Assert.Equal("https://example.test/account/save", form.Properties["ResolvedAction"].Value);
        Assert.Equal("https://example.test/account/", form.Properties["BaseUrl"].Value);
        Assert.Equal("settings", form.Properties["FormId"].Value);
        Assert.Equal("account", form.Properties["FormClasses"].Value);
        Assert.Equal(new[] { "before", "tag", "tag", "disabled" },
            Assert.IsType<PSObject[]>(form.Properties["Fields"].Value)
                .Select(field => field.Properties["Name"].Value));
        Assert.Equal(new[] { "before=one", "tag=first", "tag=second" },
            Assert.IsType<System.Collections.Generic.KeyValuePair<string, string>[]>(form.Properties["SuccessfulFields"].Value)
                .Select(field => field.Key + "=" + field.Value));
    }

    [Fact]
    public void ContentCommandKeepsMultipleFormsAsOneOrderedResult() {
        using Runspace runspace = CreateRunspace();
        using PowerShell command = PowerShell.Create();
        command.Runspace = runspace;
        command.AddCommand("ConvertFrom-HtmlForm")
            .AddParameter("Content", "<form id='first'><input name='one' value='1'></form><form id='second'><input name='two' value='2'></form>");

        PSObject[] forms = Assert.IsType<PSObject[]>(Assert.Single(command.Invoke()).BaseObject);

        Assert.Empty(command.Streams.Error);
        Assert.Single(command.Streams.Warning);
        Assert.Equal(new[] { "one", "two" }, forms.Select(form =>
            Assert.Single(Assert.IsType<PSObject[]>(form.Properties["Fields"].Value)).Properties["Name"].Value));
    }

    private static Runspace CreateRunspace() {
        InitialSessionState state = InitialSessionState.Create();
        state.Commands.Add(new SessionStateCmdletEntry("ConvertFrom-HtmlForm", typeof(CmdletConvertFromHtmlForm), null));
        Runspace runspace = RunspaceFactory.CreateRunspace(state);
        runspace.Open();
        return runspace;
    }
}
#endif
