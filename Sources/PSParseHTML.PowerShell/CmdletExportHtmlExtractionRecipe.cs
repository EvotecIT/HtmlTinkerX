using HtmlTinkerX;
using System.IO;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Saves a browserless extraction recipe created from a discovered data source.
/// </summary>
/// <example>
///   <summary>Export a recipe for the first direct source</summary>
///   <code>Find-HtmlDataSource -Content $html -DirectOnly | Select-Object -First 1 | Export-HtmlExtractionRecipe -Path .\recipe.json</code>
/// </example>
[Cmdlet(VerbsData.Export, "HtmlExtractionRecipe")]
[OutputType(typeof(string))]
public sealed class CmdletExportHtmlExtractionRecipe : AsyncPSCmdlet {
    /// <summary>Browserless data source to save as a recipe.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0)]
    public HtmlBrowserlessDataSource DataSource { get; set; } = null!;

    /// <summary>Destination JSON recipe path.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    [Alias("OutFile")]
    public string Path { get; set; } = string.Empty;

    /// <summary>Includes raw static payloads in the recipe. Review recipe files before sharing them.</summary>
    [Parameter]
    public SwitchParameter IncludeRawContent { get; set; }

    /// <summary>Writes the recipe path to the pipeline.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string fullPath = Path.ToFullPath();
        string? directory = System.IO.Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }

        HtmlBrowserlessExtractionRecipe recipe = HtmlBrowserlessExtraction.CreateRecipe(DataSource, IncludeRawContent.IsPresent);
        string json = HtmlBrowserlessExtraction.SerializeRecipe(recipe);
        await Task.Run(() => File.WriteAllText(fullPath, json), CancelToken).ConfigureAwait(false);
        if (PassThru.IsPresent) {
            WriteObject(fullPath);
        }
    }
}
