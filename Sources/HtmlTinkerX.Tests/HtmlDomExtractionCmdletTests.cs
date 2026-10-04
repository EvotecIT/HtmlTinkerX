#if !NETFRAMEWORK
using System;
using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using PSParseHTML.PowerShell;

namespace HtmlTinkerX.Tests;

public class HtmlDomExtractionCmdletTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectHtmlData_ConvertsTypedMapsAndDefaultsThroughThePublicCommand(bool wrappedType) {
        using var runspace = CreateRunspace();
        using var command = PowerShell.Create();
        command.Runspace = runspace;
        var fields = new Hashtable {
            ["Price"] = new Hashtable {
                ["Self"] = true, ["Attribute"] = "data-price", ["DataType"] = typeof(decimal), ["Culture"] = "pl-PL"
            },
            ["Counts"] = new Hashtable {
                ["Selector"] = "span", ["DataType"] = wrappedType ? new PSObject(typeof(int)) : typeof(int), ["All"] = true
            },
            ["Fallback"] = new Hashtable {
                ["Selector"] = "b", ["DataType"] = typeof(decimal),
                ["TreatEmptyAsMissing"] = true, ["DefaultValue"] = new PSObject(7m)
            },
            ["Legacy"] = "span",
            ["NullType"] = new Hashtable { ["Selector"] = "span", ["DataType"] = null }
        };
        command.AddCommand("Select-HtmlData")
            .AddParameter("Content", "<article data-price='12,5'><span>12</span><span>24</span><b> </b></article>")
            .AddParameter("ItemSelector", "article")
            .AddParameter("Property", fields);

        var result = Assert.Single(command.Invoke());

        Assert.Empty(command.Streams.Error);
        Assert.Equal(12.5m, Assert.IsType<decimal>(result.Properties["Price"].Value));
        Assert.Equal(new object?[] { 12, 24 }, Assert.IsType<object?[]>(result.Properties["Counts"].Value));
        Assert.Equal(7m, Assert.IsType<decimal>(result.Properties["Fallback"].Value));
        Assert.Equal("12", result.Properties["Legacy"].Value);
        Assert.Equal("12", result.Properties["NullType"].Value);
    }

    [Fact]
    public void SelectHtmlData_RejectsATypeNameInsteadOfATypeInTheMap() {
        using var runspace = CreateRunspace();
        using var command = PowerShell.Create();
        command.Runspace = runspace;
        command.AddCommand("Select-HtmlData")
            .AddParameter("Content", "<article>12</article>")
            .AddParameter("ItemSelector", "article")
            .AddParameter("Property", new Hashtable {
                ["Price"] = new Hashtable { ["Self"] = true, ["DataType"] = "Decimal" }
            });

        var error = Assert.Throws<CmdletInvocationException>(() => command.Invoke());
        Assert.Contains("DataType", error.Message, StringComparison.Ordinal);
        Assert.Contains("Price", error.Message, StringComparison.Ordinal);
        Assert.Contains(".NET type", error.Message, StringComparison.Ordinal);
    }

    private static Runspace CreateRunspace() {
        var state = InitialSessionState.Create();
        state.Commands.Add(new SessionStateCmdletEntry("Select-HtmlData", typeof(CmdletSelectHtmlData), null));
        var runspace = RunspaceFactory.CreateRunspace(state);
        runspace.Open();
        return runspace;
    }
}
#endif