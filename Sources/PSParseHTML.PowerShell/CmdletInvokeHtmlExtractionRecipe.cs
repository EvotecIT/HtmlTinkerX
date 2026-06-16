using HtmlTinkerX;
using System.IO;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Executes a browserless extraction recipe.
/// </summary>
/// <example>
///   <summary>Run a recipe from disk</summary>
///   <code>Invoke-HtmlExtractionRecipe -Path .\recipe.json -AllowHttpFetch</code>
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
        ValidateProxy(Proxy, ProxyCredential);
        HtmlBrowserlessExtractionRecipe recipe = await GetRecipeAsync().ConfigureAwait(false);
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
