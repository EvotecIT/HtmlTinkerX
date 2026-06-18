using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Exports a browser recipe object or current session recording snapshot to JSON.
/// </summary>
/// <example>
///   <summary>Export a recipe and a runtime variable template</summary>
///   <code>
/// Export-HtmlBrowserRecipe -Recipe $recipe -Path .\browser.recipe.json -VariableTemplatePath .\browser.recipe.variables.json
///   </code>
/// </example>
/// <example>
///   <summary>Export the active recording snapshot with hardened selector alternates</summary>
///   <code>Export-HtmlBrowserRecipe -Session $session -Path .\browser.recipe.json -HardenSelectors</code>
/// </example>
[Cmdlet(VerbsData.Export, "HtmlBrowserRecipe", DefaultParameterSetName = ParameterSetRecipe)]
[OutputType(typeof(string), typeof(HtmlBrowserRecipe))]
public sealed class CmdletExportHtmlBrowserRecipe : AsyncPSCmdlet {
    private const string ParameterSetRecipe = "Recipe";
    private const string ParameterSetSession = "Session";

    /// <summary>Recipe object to export.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0, ParameterSetName = ParameterSetRecipe)]
    public HtmlBrowserRecipe? Recipe { get; set; }

    /// <summary>Session whose active or stopped recording should be exported.</summary>
    [Parameter(ValueFromPipeline = true, Position = 0, ParameterSetName = ParameterSetSession)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Output JSON path.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string Path { get; set; } = string.Empty;

    /// <summary>Return the recipe object instead of the output path.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    /// <summary>Optional JSON path where a runtime variable template should be written.</summary>
    [Parameter]
    public string? VariableTemplatePath { get; set; }

    /// <summary>Include optional ValueVariable entries that already have stored fallback values in the variable template.</summary>
    [Parameter]
    public SwitchParameter IncludeOptionalVariables { get; set; }

    /// <summary>Add safe selector alternates from the current page before saving a session recording snapshot.</summary>
    [Parameter(ParameterSetName = ParameterSetSession)]
    public SwitchParameter HardenSelectors { get; set; }

    /// <summary>Maximum selector alternates to keep per selector-based step when <see cref="HardenSelectors"/> is used.</summary>
    [Parameter(ParameterSetName = ParameterSetSession)]
    [ValidateRange(1, int.MaxValue)]
    public int SelectorAlternateLimit { get; set; } = 5;

    /// <summary>Replace existing selector alternates during hardening instead of appending missing alternates.</summary>
    [Parameter(ParameterSetName = ParameterSetSession)]
    public SwitchParameter ReplaceSelectorAlternates { get; set; }

    /// <summary>Optional JSON path where a redacted selector hardening report should be written.</summary>
    [Parameter(ParameterSetName = ParameterSetSession)]
    public string? HardeningReportPath { get; set; }

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        HtmlBrowserSession? session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession");
        HtmlBrowserRecipe recipe = ParameterSetName == ParameterSetRecipe
            ? Recipe!
            : HtmlBrowser.GetRecipeRecording(session
                ?? throw new PSInvalidOperationException("No session provided and no default session found."));

        if (HardenSelectors.IsPresent || !string.IsNullOrWhiteSpace(HardeningReportPath)) {
            HtmlBrowserRecipeHardeningResult hardening = await HtmlBrowser.HardenRecipeSelectorsAsync(
                session!,
                recipe,
                SelectorAlternateLimit,
                ReplaceSelectorAlternates.IsPresent,
                linkedCts.Token).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(HardeningReportPath)) {
                await HtmlBrowser.SaveRecipeHardeningReportAsync(hardening, HardeningReportPath!, linkedCts.Token).ConfigureAwait(false);
            }
        }

        await HtmlBrowser.SaveRecipeAsync(recipe, Path, linkedCts.Token).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(VariableTemplatePath)) {
            await HtmlBrowser.SaveRecipeVariableTemplateAsync(recipe, VariableTemplatePath!, !IncludeOptionalVariables.IsPresent, linkedCts.Token).ConfigureAwait(false);
        }

        WriteObject(PassThru.IsPresent ? recipe : Path.ToFullPath());
    }
}
