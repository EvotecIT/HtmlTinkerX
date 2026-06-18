using HtmlTinkerX;
using System.IO;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Hardens browser recipe selectors against the current browser page by adding safe selector alternates.
/// </summary>
/// <example>
///   <summary>Harden a recorded recipe and save the updated JSON</summary>
///   <code>
/// $session = Start-HtmlBrowserSession -Url https://example.org/app
/// Optimize-HtmlBrowserRecipe -Session $session -Path .\browser.recipe.json -OutPath .\browser.hardened.recipe.json
///   </code>
/// </example>
[Cmdlet(VerbsCommon.Optimize, "HtmlBrowserRecipe", DefaultParameterSetName = ParameterSetRecipe)]
[OutputType(typeof(HtmlBrowserRecipeHardeningResult))]
public sealed class CmdletOptimizeHtmlBrowserRecipe : AsyncPSCmdlet {
    private const string ParameterSetRecipe = "Recipe";
    private const string ParameterSetPath = "Path";

    /// <summary>Browser session whose current page matches the recipe state to harden.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Recipe object to harden.</summary>
    [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetRecipe)]
    public HtmlBrowserRecipe? Recipe { get; set; }

    /// <summary>Recipe JSON path to load and harden.</summary>
    [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetPath)]
    [Alias("File")]
    public string? Path { get; set; }

    /// <summary>Optional path where the hardened recipe should be saved. When omitted, the input path is overwritten for Path input.</summary>
    [Parameter]
    public string? OutPath { get; set; }

    /// <summary>Maximum selector alternates to keep per selector-based step.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int SelectorAlternateLimit { get; set; } = 5;

    /// <summary>Replace existing selector alternates instead of appending missing alternates.</summary>
    [Parameter]
    public SwitchParameter ReplaceSelectorAlternates { get; set; }

    /// <summary>Optional JSON path where a redacted selector hardening report should be written.</summary>
    [Parameter]
    public string? ReportPath { get; set; }

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");

        HtmlBrowserRecipe recipe = await GetRecipeAsync(linkedCts.Token).ConfigureAwait(false);
        HtmlBrowserRecipeHardeningResult result = await HtmlBrowser.HardenRecipeSelectorsAsync(
            session,
            recipe,
            SelectorAlternateLimit,
            ReplaceSelectorAlternates.IsPresent,
            linkedCts.Token).ConfigureAwait(false);

        string? outputPath = GetOutputPath();
        if (!string.IsNullOrWhiteSpace(outputPath)) {
            await HtmlBrowser.SaveRecipeAsync(result.Recipe, outputPath!, linkedCts.Token).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(ReportPath)) {
            await HtmlBrowser.SaveRecipeHardeningReportAsync(result, ReportPath!, linkedCts.Token).ConfigureAwait(false);
        }

        WriteObject(result);
    }

    private async Task<HtmlBrowserRecipe> GetRecipeAsync(CancellationToken cancellationToken) {
        if (ParameterSetName == ParameterSetRecipe) {
            return Recipe!;
        }

        string fullPath = Path!.ToFullPath();
#if NETSTANDARD2_0 || NETFRAMEWORK
        string json = File.ReadAllText(fullPath);
        await Task.CompletedTask.ConfigureAwait(false);
#else
        string json = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
#endif
        return HtmlBrowser.DeserializeRecipe(json);
    }

    private string? GetOutputPath() {
        if (!string.IsNullOrWhiteSpace(OutPath)) {
            return OutPath;
        }

        return ParameterSetName == ParameterSetPath ? Path : null;
    }
}
