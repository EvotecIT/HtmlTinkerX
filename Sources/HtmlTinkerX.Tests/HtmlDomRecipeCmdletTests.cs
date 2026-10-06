#if !NETFRAMEWORK
using System;
using System.Collections;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using PSParseHTML.PowerShell;

namespace HtmlTinkerX.Tests;

public class HtmlDomRecipeCmdletTests {
    [Fact]
    public void PublicCommands_SaveLoadAndEvaluateFreshDomWithQualityRules() {
        string recipePath = Path.Combine(Path.GetTempPath(), "dom-recipe-" + Guid.NewGuid().ToString("N") + ".json");
        using var runspace = CreateRunspace();
        using var command = PowerShell.Create();
        command.Runspace = runspace;
        try {
            command.AddCommand("Export-HtmlExtractionRecipe")
                .AddParameter("ItemSelector", "article")
                .AddParameter("Property", new Hashtable {
                    ["Name"] = new Hashtable { ["Selector"] = "span", ["Required"] = true },
                    ["Price"] = new Hashtable { ["Selector"] = "b", ["DataType"] = typeof(decimal), ["Culture"] = "pl-PL", ["MaximumValueCount"] = 1 }
                }).AddParameter("Path", recipePath).AddParameter("MinimumItemCount", 1).AddParameter("PassThru", true);
            Assert.Equal(recipePath, Assert.Single(command.Invoke()).BaseObject);
            Assert.Empty(command.Streams.Error);

            command.Commands.Clear();
            command.AddCommand("Import-HtmlExtractionRecipe").AddParameter("Path", recipePath);
            var recipe = Assert.IsType<HtmlBrowserlessExtractionRecipe>(Assert.Single(command.Invoke()).BaseObject);
            Assert.Equal(typeof(decimal), recipe.DomProperties!["Price"].DataType);
            Assert.Equal(1, recipe.DomProperties["Price"].MaximumValueCount);

            command.Commands.Clear();
            command.AddCommand("Invoke-HtmlExtractionRecipe").AddParameter("Recipe", recipe)
                .AddParameter("Content", "<article><span>Name</span><b>1234,50</b></article>");
            var initial = Assert.IsType<HtmlBrowserlessExtractionResult>(Assert.Single(command.Invoke()).BaseObject);
            Assert.True(initial.Success);
            Assert.Equal(1234.50m, initial.DomReport!.Records[0].Values["Price"]);

            command.Commands.Clear();
            command.AddCommand("Invoke-HtmlExtractionRecipe").AddParameter("Path", recipePath)
                .AddParameter("Content", "<main></main>");
            var changed = Assert.IsType<HtmlBrowserlessExtractionResult>(Assert.Single(command.Invoke()).BaseObject);
            Assert.False(changed.Success);
            Assert.False(changed.DomReport!.ItemCountIsValid);
            Assert.Empty(changed.Requests);
            Assert.Empty(command.Streams.Error);
        } finally {
            if (File.Exists(recipePath)) { File.Delete(recipePath); }
        }
    }

    [Fact]
    public void PublicCommands_SaveAcceptedBaselineAndExposeChangedOptionalField() {
        string recipePath = Path.Combine(Path.GetTempPath(), "baseline-recipe-" + Guid.NewGuid().ToString("N") + ".json");
        using var runspace = CreateRunspace();
        using var command = PowerShell.Create();
        command.Runspace = runspace;
        try {
            command.AddCommand("Export-HtmlExtractionRecipe")
                .AddParameter("ItemSelector", "article")
                .AddParameter("Property", new Hashtable {
                    ["Name"] = new Hashtable { ["Selector"] = "h2", ["Required"] = true },
                    ["Note"] = ".note"
                }).AddParameter("Path", recipePath)
                .AddParameter("BaselineContent", "<article><h2>Name</h2><p class='note'>private note</p></article>");
            Assert.Empty(command.Invoke());
            Assert.Empty(command.Streams.Error);
            Assert.DoesNotContain("private note", File.ReadAllText(recipePath));

            command.Commands.Clear();
            command.AddCommand("Invoke-HtmlExtractionRecipe").AddParameter("Path", recipePath)
                .AddParameter("Content", "<article><h2>Updated name</h2></article>");
            var changed = Assert.IsType<HtmlBrowserlessExtractionResult>(Assert.Single(command.Invoke()).BaseObject);
            Assert.Empty(command.Streams.Error);
            Assert.False(changed.Success);
            Assert.True(changed.DomReport!.IsValid);
            Assert.True(changed.DriftReport!.ShapeChanged);
            Assert.Empty(changed.Requests);
        } finally {
            if (File.Exists(recipePath)) File.Delete(recipePath);
        }
    }

    private static Runspace CreateRunspace() {
        var state = InitialSessionState.Create();
        state.Commands.Add(new SessionStateCmdletEntry("Export-HtmlExtractionRecipe", typeof(CmdletExportHtmlExtractionRecipe), null));
        state.Commands.Add(new SessionStateCmdletEntry("Import-HtmlExtractionRecipe", typeof(CmdletImportHtmlExtractionRecipe), null));
        state.Commands.Add(new SessionStateCmdletEntry("Invoke-HtmlExtractionRecipe", typeof(CmdletInvokeHtmlExtractionRecipe), null));
        var runspace = RunspaceFactory.CreateRunspace(state);
        runspace.Open();
        return runspace;
    }
}
#endif
