using HtmlTinkerX;
using System;
using System.Collections;
using System.IO;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Saves a browserless extraction recipe from a discovered data source or DOM field rules.
/// </summary>
/// <example>
///   <summary>Export a recipe for the first direct source</summary>
///   <code>Find-HtmlDataSource -Content $html -DirectOnly | Select-Object -First 1 | Export-HtmlExtractionRecipe -Path .\recipe.json</code>
/// </example>
/// <example>
///   <summary>Save DOM fields and expected counts for reuse on current HTML</summary>
///   <code>
/// Export-HtmlExtractionRecipe -ItemSelector '.product-card' -Property @{
///     Name = @{ Selector = '.product-title'; Required = $true }
///     Price = @{ Selector = '.product-price'; DataType = [decimal]; MaximumValueCount = 1 }
/// } -MinimumItemCount 1 -Path .\products.json
///   </code>
/// </example>
/// <example>
///   <summary>Accept a DOM structure and check later extractions for drift</summary>
///   <code>
/// Export-HtmlExtractionRecipe -ItemSelector '.product' -Property @{
///     Name = @{ Selector = 'h2'; Required = $true }
///     Note = '.note'
/// } -BaselineContent $acceptedHtml -Path .\products.json
/// $result = Invoke-HtmlExtractionRecipe -Path .\products.json -Content $currentHtml
/// $result.DriftReport
///   </code>
/// </example>
[Cmdlet(VerbsData.Export, "HtmlExtractionRecipe", DefaultParameterSetName = ParameterSetSource)]
[OutputType(typeof(string))]
public sealed class CmdletExportHtmlExtractionRecipe : AsyncPSCmdlet {
    private const string ParameterSetSource = "Source";
    private const string ParameterSetDom = "Dom";

    /// <summary>Browserless data source to save as a recipe.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSource, ValueFromPipeline = true, Position = 0)]
    public HtmlBrowserlessDataSource DataSource { get; set; } = null!;

    /// <summary>Destination JSON recipe path.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    [Alias("OutFile")]
    public string Path { get; set; } = string.Empty;

    /// <summary>Includes raw static payloads in the recipe. Review recipe files before sharing them.</summary>
    [Parameter(ParameterSetName = ParameterSetSource)]
    public SwitchParameter IncludeRawContent { get; set; }

    /// <summary>CSS selector matching repeated DOM items.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetDom)]
    [ValidateNotNullOrEmpty]
    public string? ItemSelector { get; set; }

    /// <summary>DOM property-to-selector map using the same field rules as Select-HtmlData.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetDom)]
    public IDictionary? Property { get; set; }

    /// <summary>Page URL used to resolve relative field URLs when the recipe is evaluated.</summary>
    [Parameter(ParameterSetName = ParameterSetDom)]
    public Uri? BaseUrl { get; set; }

    /// <summary>Minimum acceptable number of items when a DOM recipe is evaluated.</summary>
    [Parameter(ParameterSetName = ParameterSetDom)]
    [ValidateRange(0, int.MaxValue)]
    public int? MinimumItemCount { get; set; }

    /// <summary>Maximum acceptable number of items when a DOM recipe is evaluated.</summary>
    [Parameter(ParameterSetName = ParameterSetDom)]
    [ValidateRange(0, int.MaxValue)]
    public int? MaximumItemCount { get; set; }

    /// <summary>Accepted HTML used to capture DOM item structure and collection confidence in the saved recipe.</summary>
    [Parameter(ParameterSetName = ParameterSetDom)]
    [ValidateNotNull]
    public string? BaselineContent { get; set; }

    /// <summary>Successful structured-data extraction whose inspected output structure is accepted as a baseline.</summary>
    [Parameter(ParameterSetName = ParameterSetSource)]
    [ValidateNotNull]
    public HtmlBrowserlessExtractionResult? AcceptedResult { get; set; }

    /// <summary>Writes the recipe path to the pipeline.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserlessExtractionRecipe recipe = ParameterSetName == ParameterSetDom
            ? HtmlBrowserlessExtraction.CreateDomRecipe(ItemSelector!, HtmlDomPropertyMapConverter.Convert(Property!),
                new HtmlDomExtractionReportOptions {
                    MinimumItemCount = MinimumItemCount, MaximumItemCount = MaximumItemCount
                }, BaseUrl)
            : HtmlBrowserlessExtraction.CreateRecipe(DataSource, IncludeRawContent.IsPresent);
        if (BaselineContent != null) {
            recipe = HtmlBrowserlessExtraction.CaptureDomRecipeBaseline(recipe, BaselineContent);
        } else if (AcceptedResult != null) {
            recipe = HtmlBrowserlessExtraction.CaptureRecipeBaseline(recipe, AcceptedResult);
        }
        string json = HtmlBrowserlessExtraction.SerializeRecipe(recipe);
        string fullPath = Path.ToFullPath();
        string? directory = System.IO.Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }

        await Task.Run(() => File.WriteAllText(fullPath, json), CancelToken).ConfigureAwait(false);
        if (PassThru.IsPresent) {
            WriteObject(fullPath);
        }
    }
}
