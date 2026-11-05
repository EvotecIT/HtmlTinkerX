using HtmlTinkerX;
using System.IO;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Executes a browserless extraction recipe.
/// </summary>
/// <remarks>When a DOM recipe's baseline stores item fingerprints and the items no longer match, the result's
/// DriftReport.RelocationCandidates suggests where they moved and each candidate is written as a warning.
/// The recipe is not changed.</remarks>
/// <example>
///   <summary>Run a recipe from disk</summary>
///   <code>Invoke-HtmlExtractionRecipe -Path .\recipe.json -AllowHttpFetch</code>
/// </example>
/// <example>
///   <summary>Check fresh HTML against saved DOM fields and counts</summary>
///   <code>
/// $result = Invoke-HtmlExtractionRecipe -Path .\products.json -Content $html
/// $result.Success
/// $result.DomReport.Fields
/// $result.DriftReport
///   </code>
/// </example>
/// <example>
///   <summary>Review where items moved after a layout change broke the saved selector</summary>
///   <code>
/// $result = Invoke-HtmlExtractionRecipe -Path .\releases.json -Content $html
/// $result.DriftReport.RelocationCandidates | Format-Table Selector, Confidence, MatchCount, Reasons
///   </code>
/// </example>
[Cmdlet(VerbsLifecycle.Invoke, "HtmlExtractionRecipe", DefaultParameterSetName = ParameterSetRecipe)]
[OutputType(typeof(HtmlBrowserlessExtractionResult))]
public sealed class CmdletInvokeHtmlExtractionRecipe : AsyncPSCmdlet {
    private const string ParameterSetRecipe = "Recipe";
    private const string ParameterSetPath = "Path";

    /// <summary>Recipe object to execute.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetRecipe, ValueFromPipeline = true, Position = 0)]
    public HtmlBrowserlessExtractionRecipe? Recipe { get; set; }

    /// <summary>Recipe JSON path.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetPath, Position = 0)]
    [Alias("File")]
    public string? Path { get; set; }

    /// <summary>Current HTML to evaluate with a DOM recipe. DOM recipes require Content and perform no HTTP fetch.</summary>
    [Parameter]
    [AllowEmptyString]
    public string? Content { get; set; }

    /// <summary>Allows direct HTTP GET extraction for endpoint recipes.</summary>
    [Parameter]
    public SwitchParameter AllowHttpFetch { get; set; }

    /// <summary>Allows medium-risk endpoint recipes when HTTP fetch is enabled.</summary>
    [Parameter]
    public SwitchParameter AllowMediumRiskEndpoint { get; set; }

    /// <summary>Allows external endpoint recipes when HTTP fetch is enabled.</summary>
    [Parameter]
    public SwitchParameter AllowExternalEndpoint { get; set; }

    /// <summary>Includes raw payload or response content in the result.</summary>
    [Parameter]
    public SwitchParameter IncludeRawContent { get; set; }

    /// <summary>Proxy server address used when direct HTTP extraction is enabled.</summary>
    [Parameter]
    public string? Proxy { get; set; }

    /// <summary>Credentials used with the proxy server.</summary>
    [Parameter]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserlessExtractionRecipe recipe = await GetRecipeAsync().ConfigureAwait(false);
        if (Content != null) {
            HtmlBrowserlessExtractionResult domResult = HtmlBrowserlessExtraction.ExtractDomRecipe(recipe, Content, IncludeRawContent.IsPresent);
            foreach (HtmlSelectorRelocationCandidate candidate in domResult.DriftReport?.RelocationCandidates ?? []) {
                WriteWarning($"Items may have moved to '{candidate.Selector}' ({candidate.Confidence} confidence, "
                    + $"{candidate.MatchCount} match(es)): {string.Join("; ", candidate.Reasons)}.");
            }
            WriteObject(domResult);
            return;
        }
        ValidateProxy(Proxy, ProxyCredential);
        using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential);
        HtmlBrowserlessExtractionResult result = await HtmlBrowserlessExtraction.ExtractRecipeAsync(
            recipe,
            new HtmlBrowserlessExtractionOptions {
                AllowHttpFetch = AllowHttpFetch.IsPresent,
                AllowMediumRiskEndpoints = AllowMediumRiskEndpoint.IsPresent,
                AllowExternalEndpoints = AllowExternalEndpoint.IsPresent,
                IncludeRawContent = IncludeRawContent.IsPresent
            },
            client,
            CancelToken).ConfigureAwait(false);

        WriteObject(result);
    }

    private async Task<HtmlBrowserlessExtractionRecipe> GetRecipeAsync() {
        if (ParameterSetName == ParameterSetRecipe) {
            return Recipe!;
        }

        string fullPath = Path!.ToFullPath();
        string json = await Task.Run(() => File.ReadAllText(fullPath), CancelToken).ConfigureAwait(false);
        return HtmlBrowserlessExtraction.DeserializeRecipe(json);
    }
}
