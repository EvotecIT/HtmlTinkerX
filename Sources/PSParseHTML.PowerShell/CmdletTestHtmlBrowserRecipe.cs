using HtmlTinkerX;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Validates a browser automation recipe before replaying it.
/// </summary>
/// <example>
///   <summary>Preflight a recipe JSON file before launching a browser</summary>
///   <code>
/// $validation = Test-HtmlBrowserRecipe -Path .\browser.recipe.json -VariablePath .\browser.recipe.variables.json
/// $validation.RequiredVariables
/// $validation.VariableTemplate
/// $validation.Issues | Format-Table Severity, StepIndex, Action, Property, Message
///   </code>
/// </example>
/// <example>
///   <summary>Fail a CI job when a recipe has errors or strict preflight warnings</summary>
///   <code>Test-HtmlBrowserRecipe -Path .\browser.recipe.json -StrictPreflight -ThrowOnFailure</code>
/// </example>
[Cmdlet(VerbsDiagnostic.Test, "HtmlBrowserRecipe", DefaultParameterSetName = ParameterSetRecipe)]
[OutputType(typeof(HtmlBrowserRecipeValidationResult))]
public sealed class CmdletTestHtmlBrowserRecipe : AsyncPSCmdlet {
    private const string ParameterSetRecipe = "Recipe";
    private const string ParameterSetPath = "Path";

    /// <summary>Recipe object to validate.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetRecipe, ValueFromPipeline = true, Position = 0)]
    public HtmlBrowserRecipe? Recipe { get; set; }

    /// <summary>Recipe JSON path.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetPath, Position = 0)]
    [Alias("File")]
    public string? Path { get; set; }

    /// <summary>Runtime variables that will be supplied during replay.</summary>
    [Parameter]
    [Alias("RecipeVariable")]
    public IDictionary? Variable { get; set; }

    /// <summary>JSON file containing runtime variables to use for validation. Placeholder values such as &lt;secret&gt; are treated as missing.</summary>
    [Parameter]
    public string? VariablePath { get; set; }

    /// <summary>Allow a missing StartUrl because replay will use an existing browser session.</summary>
    [Parameter]
    public SwitchParameter AssumeSession { get; set; }

    /// <summary>Treat warnings as blocking issues for validation summaries and CI gates.</summary>
    [Parameter]
    public SwitchParameter StrictPreflight { get; set; }

    /// <summary>Emit the validation result and then throw a terminating error when blocking issues are present.</summary>
    [Parameter]
    [Alias("FailOnFailure", "FailOnIssue")]
    public SwitchParameter ThrowOnFailure { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserRecipe recipe = await GetRecipeAsync().ConfigureAwait(false);
        string[] variables = await GetVariableNamesAsync().ConfigureAwait(false);
        HtmlBrowserRecipeValidationResult validation = HtmlBrowser.ValidateRecipe(recipe, variables, AssumeSession.IsPresent, StrictPreflight.IsPresent);
        WriteObject(validation);

        if (ThrowOnFailure.IsPresent && validation.HasBlockingIssues(StrictPreflight.IsPresent)) {
            ThrowTerminatingError(new ErrorRecord(
                new InvalidOperationException(validation.Summary),
                "HtmlBrowserRecipePreflightFailed",
                ErrorCategory.InvalidData,
                validation));
        }
    }

    private async Task<HtmlBrowserRecipe> GetRecipeAsync() {
        if (ParameterSetName == ParameterSetRecipe) {
            return Recipe!;
        }

        string fullPath = Path!.ToFullPath();
        string json = await Task.Run(() => File.ReadAllText(fullPath), CancelToken).ConfigureAwait(false);
        return HtmlBrowser.DeserializeRecipe(json);
    }

    private async Task<string[]> GetVariableNamesAsync() {
        System.Collections.Generic.HashSet<string> names = new(System.StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(VariablePath)) {
            System.Collections.Generic.Dictionary<string, string> fileVariables = await HtmlBrowser.LoadRecipeVariablesAsync(VariablePath!, CancelToken).ConfigureAwait(false);
            foreach (string name in fileVariables.Keys) {
                names.Add(name);
            }
        }

        if (Variable == null) {
            return names.ToArray();
        }

        foreach (DictionaryEntry entry in Variable) {
            if (entry.Key == null) {
                continue;
            }

            string name = entry.Key.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(name)) {
                names.Add(name);
            }
        }

        return names.ToArray();
    }
}
