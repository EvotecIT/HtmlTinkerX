#if !NETFRAMEWORK
using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using PSParseHTML.PowerShell;

namespace HtmlTinkerX.Tests;

public class HtmlDomExtractionReportCmdletTests {
    [Fact]
    public void PublicCommand_ReturnsOneReportAndCarriesFieldAndItemBounds() {
        using var runspace = CreateRunspace();
        using var command = PowerShell.Create();
        command.Runspace = runspace;
        command.AddCommand("Select-HtmlData")
            .AddParameter("Content", "<article><b>12</b><b>24</b></article>")
            .AddParameter("ItemSelector", "article")
            .AddParameter("Property", new Hashtable {
                ["Price"] = new Hashtable { ["Selector"] = "b", ["DataType"] = typeof(decimal), ["MaximumValueCount"] = 1 },
                ["Name"] = new Hashtable { ["Selector"] = ".absent", ["Required"] = true }
            })
            .AddParameter("AsExtractionReport", true)
            .AddParameter("MinimumItemCount", 2)
            .AddParameter("MaximumItemCount", 3);

        var report = Assert.IsType<HtmlDomExtractionReport>(Assert.Single(command.Invoke()).BaseObject);

        Assert.Empty(command.Streams.Error);
        Assert.False(report.IsValid);
        Assert.False(report.ItemCountIsValid);
        Assert.Equal(2, report.MinimumItemCount);
        Assert.Equal(3, report.MaximumItemCount);
        Assert.Equal(2, report.InvalidFieldCount);
        var price = Assert.Single(report.Fields, diagnostic => diagnostic.PropertyName == "Price");
        Assert.Equal(HtmlDomFieldStatus.TooManyValues, price.Status);
        Assert.Equal(1, price.MaximumValueCount);
        Assert.Equal(HtmlDomFieldStatus.RequiredMissing,
            Assert.Single(report.Fields, diagnostic => diagnostic.PropertyName == "Name").Status);
    }

    [Fact]
    public void PublicCommand_RejectsCountBoundsWithoutReportMode() {
        using var runspace = CreateRunspace();
        using var command = PowerShell.Create();
        command.Runspace = runspace;
        command.AddCommand("Select-HtmlData").AddParameter("Content", "<article>One</article>")
            .AddParameter("ItemSelector", "article")
            .AddParameter("Property", new Hashtable { ["Name"] = new Hashtable { ["Self"] = true } })
            .AddParameter("MinimumItemCount", 1);

        var error = Assert.Throws<CmdletInvocationException>(() => command.Invoke());
        Assert.Contains("AsExtractionReport", error.Message);
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