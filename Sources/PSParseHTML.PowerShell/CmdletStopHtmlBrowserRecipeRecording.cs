using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Stops a browser recipe recording and optionally saves the captured recipe to JSON.
/// </summary>
/// <example>
///   <summary>Stop recording and save a variable template beside the recipe</summary>
///   <code>
/// Stop-HtmlBrowserRecipeRecording -Session $session -Path .\browser.recipe.json -VariableTemplatePath .\browser.recipe.variables.json
///   </code>
/// </example>
/// <example>
///   <summary>Stop recording and harden selector alternates before saving</summary>
///   <code>Stop-HtmlBrowserRecipeRecording -Session $session -Path .\browser.recipe.json -HardenSelectors</code>
/// </example>
[Cmdlet(VerbsLifecycle.Stop, "HtmlBrowserRecipeRecording")]
[OutputType(typeof(HtmlBrowserRecipe), typeof(string))]
public sealed class CmdletStopHtmlBrowserRecipeRecording : AsyncPSCmdlet {
    /// <summary>Browser session being recorded. When omitted, the default PSParseHTML session is used.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Optional JSON path where the recorded recipe should be saved.</summary>
    [Parameter(Position = 1)]
    public string? Path { get; set; }

    /// <summary>Return the recipe object after saving. When no path is supplied, the recipe is always returned.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    /// <summary>Optional JSON path where a runtime variable template should be written.</summary>
    [Parameter]
    public string? VariableTemplatePath { get; set; }

    /// <summary>Include optional ValueVariable entries that already have stored fallback values in the variable template.</summary>
    [Parameter]
    public SwitchParameter IncludeOptionalVariables { get; set; }

    /// <summary>Add safe selector alternates from the current page before saving or returning the recipe.</summary>
    [Parameter]
    public SwitchParameter HardenSelectors { get; set; }

    /// <summary>Maximum selector alternates to keep per selector-based step when <see cref="HardenSelectors"/> is used.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int SelectorAlternateLimit { get; set; } = 5;

    /// <summary>Replace existing selector alternates during hardening instead of appending missing alternates.</summary>
    [Parameter]
    public SwitchParameter ReplaceSelectorAlternates { get; set; }

    /// <summary>Optional JSON path where a redacted selector hardening report should be written.</summary>
    [Parameter]
    public string? HardeningReportPath { get; set; }

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");

        HtmlBrowserRecipe recipe = HtmlBrowser.StopRecipeRecording(session);
        if (HardenSelectors.IsPresent || !string.IsNullOrWhiteSpace(HardeningReportPath)) {
            HtmlBrowserRecipeHardeningResult hardening = await HtmlBrowser.HardenRecipeSelectorsAsync(
                session,
                recipe,
                SelectorAlternateLimit,
                ReplaceSelectorAlternates.IsPresent,
                linkedCts.Token).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(HardeningReportPath)) {
                await HtmlBrowser.SaveRecipeHardeningReportAsync(hardening, HardeningReportPath!, linkedCts.Token).ConfigureAwait(false);
            }
        }

        if (!string.IsNullOrWhiteSpace(VariableTemplatePath)) {
            await HtmlBrowser.SaveRecipeVariableTemplateAsync(recipe, VariableTemplatePath!, !IncludeOptionalVariables.IsPresent, linkedCts.Token).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(Path)) {
            await HtmlBrowser.SaveRecipeAsync(recipe, Path!, linkedCts.Token).ConfigureAwait(false);
            if (PassThru.IsPresent) {
                WriteObject(recipe);
            } else {
                WriteObject(Path!.ToFullPath());
            }

            return;
        }

        WriteObject(recipe);
    }
}
