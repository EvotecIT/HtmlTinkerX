using HtmlTinkerX;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Extracts a browserless data source discovered by Find-HtmlDataSource.
/// </summary>
/// <example>
///   <summary>Extract the first direct static or app-state source</summary>
///   <code>Find-HtmlDataSource -Content $html -DirectOnly | Select-Object -First 1 | Invoke-HtmlDataExtraction</code>
/// </example>
/// <example>
///   <summary>Fetch a low-risk GET endpoint directly</summary>
///   <code>Find-HtmlDataSource -Url https://example.org/products -DirectOnly | Invoke-HtmlDataExtraction -AllowHttpFetch</code>
/// </example>
[Cmdlet(VerbsLifecycle.Invoke, "HtmlDataExtraction")]
[OutputType(typeof(HtmlBrowserlessExtractionResult))]
public sealed class CmdletInvokeHtmlDataExtraction : AsyncPSCmdlet {
    /// <summary>Browserless data source to extract.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0)]
    public HtmlBrowserlessDataSource DataSource { get; set; } = null!;

    /// <summary>Allows direct HTTP GET extraction for endpoint sources.</summary>
    [Parameter]
    public SwitchParameter AllowHttpFetch { get; set; }

    /// <summary>Allows medium-risk endpoint sources when HTTP fetch is enabled.</summary>
    [Parameter]
    public SwitchParameter AllowMediumRiskEndpoint { get; set; }

    /// <summary>Allows external endpoint sources when HTTP fetch is enabled.</summary>
    [Parameter]
    public SwitchParameter AllowExternalEndpoint { get; set; }

    /// <summary>Includes raw payload or response content in the result.</summary>
    [Parameter]
    public SwitchParameter IncludeRawContent { get; set; }

    /// <summary>Maximum response body size to keep from direct HTTP endpoint extraction.</summary>
    [Parameter]
    public int MaxResponseBytes { get; set; } = 1024 * 1024;

    /// <summary>Proxy server address used when direct HTTP extraction is enabled.</summary>
    [Parameter]
    public string? Proxy { get; set; }

    /// <summary>Credentials used with the proxy server.</summary>
    [Parameter]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential);
        HtmlBrowserlessExtractionResult result = await HtmlBrowserlessExtraction.ExtractAsync(
            DataSource,
            new HtmlBrowserlessExtractionOptions {
                AllowHttpFetch = AllowHttpFetch.IsPresent,
                AllowMediumRiskEndpoints = AllowMediumRiskEndpoint.IsPresent,
                AllowExternalEndpoints = AllowExternalEndpoint.IsPresent,
                IncludeRawContent = IncludeRawContent.IsPresent,
                MaxResponseBytes = MaxResponseBytes
            },
            client,
            CancelToken).ConfigureAwait(false);

        WriteObject(result);
    }
}
