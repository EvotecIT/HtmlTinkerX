#if !NETFRAMEWORK
using HtmlAgilityPack;
using PSParseHTML.PowerShell;
using System;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace HtmlTinkerX.Tests;

public class HtmlSelectorCmdletTests {
    [Theory]
    [InlineData(false, "")]
    [InlineData(true, "fallback")]
    public void AttributeValueHandlesAnAttributeWithoutAValue(bool treatEmptyAsMissing, string expected) {
        var attribute = new HtmlDocument().CreateAttribute("disabled");
        Assert.Null(attribute.Value);

        using var runspace = CreateRunspace("Select-HtmlAttributeValue", typeof(CmdletSelectHtmlAttributeValue));
        using var command = PowerShell.Create();
        command.Runspace = runspace;
        command.AddCommand("Select-HtmlAttributeValue")
            .AddParameter("InputObject", attribute)
            .AddParameter("DefaultValue", "fallback")
            .AddParameter("TreatEmptyAsMissing", treatEmptyAsMissing);

        var result = command.Invoke();

        Assert.Empty(command.Streams.Error);
        Assert.Equal(expected, Assert.Single(result).BaseObject);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void InnerTextHandlesAnAttributeWithoutAValue(bool deEntitize, bool noTrim) {
        var attribute = new HtmlDocument().CreateAttribute("disabled");
        Assert.Null(attribute.Value);

        using var runspace = CreateRunspace("Select-HtmlInnerText", typeof(CmdletSelectHtmlInnerText));
        using var command = PowerShell.Create();
        command.Runspace = runspace;
        command.AddCommand("Select-HtmlInnerText")
            .AddParameter("InputObject", attribute)
            .AddParameter("DefaultValue", "fallback")
            .AddParameter("DeEntitize", deEntitize)
            .AddParameter("NoTrim", noTrim);

        var result = command.Invoke();

        Assert.Empty(command.Streams.Error);
        Assert.Equal("fallback", Assert.Single(result).BaseObject);
    }

    private static Runspace CreateRunspace(string commandName, Type commandType) {
        var state = InitialSessionState.Create();
        state.Commands.Add(new SessionStateCmdletEntry(commandName, commandType, null));
        var runspace = RunspaceFactory.CreateRunspace(state);
        runspace.Open();
        return runspace;
    }
}
#endif
