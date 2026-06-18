using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Finds browserless extraction candidates from observed browser network traffic.
/// </summary>
/// <example>
///   <summary>Find observed API calls that can become browserless extraction recipes</summary>
///   <code>$session | Find-HtmlBrowserDataSource -IncludeResponseBody | Export-HtmlExtractionRecipe -Path .\recipe.json</code>
/// </example>
[Cmdlet(VerbsCommon.Find, "HtmlBrowserDataSource")]
[OutputType(typeof(HtmlBrowserlessDataSource))]
public sealed class CmdletFindHtmlBrowserDataSource : AsyncPSCmdlet {
    /// <summary>Browser session containing network traffic. When omitted, the default PSParseHTML session is used.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Override the page URL used for same-origin checks.</summary>
    [Parameter]
    public string? PageUrl { get; set; }

    /// <summary>Browser resource types considered as data-source candidates. Defaults to XHR and Fetch.</summary>
    [Parameter]
    public HtmlNetworkResourceType[] ResourceType { get; set; } = System.Array.Empty<HtmlNetworkResourceType>();

    /// <summary>Also include document requests.</summary>
    [Parameter]
    public SwitchParameter IncludeDocument { get; set; }

    /// <summary>Include failed or non-successful requests in the output.</summary>
    [Parameter]
    public SwitchParameter IncludeFailed { get; set; }

    /// <summary>Include non-GET requests. They are classified as higher risk and are not fetched automatically.</summary>
    [Parameter]
    public SwitchParameter IncludeNonGet { get; set; }

    /// <summary>Include endpoints outside the page origin.</summary>
    [Parameter]
    public SwitchParameter IncludeExternal { get; set; }

    /// <summary>Copy captured response bodies into output sources when available.</summary>
    [Parameter]
    public SwitchParameter IncludeResponseBody { get; set; }

    /// <summary>Redact common tokens, passwords, and secrets before copied response bodies are exposed as data-source content.</summary>
    [Parameter]
    public SwitchParameter RedactResponseBody { get; set; }

    /// <summary>Maximum UTF-8 bytes captured per response body when <see cref="IncludeResponseBody"/> is used.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int ResponseBodyMaxBytes { get; set; } = 65536;

    /// <summary>Resource types whose response bodies should be captured. Defaults to XHR and Fetch.</summary>
    [Parameter]
    public HtmlNetworkResourceType[] ResponseBodyResourceType { get; set; } = System.Array.Empty<HtmlNetworkResourceType>();

    /// <summary>Maximum number of data-source candidates returned. Zero means no limit.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int MaxSource { get; set; }

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        CancellationToken.ThrowIfCancellationRequested();
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");

        if (RedactResponseBody.IsPresent && !IncludeResponseBody.IsPresent) {
            throw new PSArgumentException("RedactResponseBody requires -IncludeResponseBody.");
        }

        HtmlBrowserNetworkDataSourceOptions options = new() {
            PageUrl = PageUrl,
            IncludeFailed = IncludeFailed.IsPresent,
            IncludeNonGet = IncludeNonGet.IsPresent,
            IncludeExternal = IncludeExternal.IsPresent,
            IncludeResponseBody = IncludeResponseBody.IsPresent,
            MaxSources = MaxSource
        };

        foreach (HtmlNetworkResourceType resourceType in ResourceType) {
            options.ResourceTypes.Add(resourceType);
        }

        if (IncludeDocument.IsPresent && !options.ResourceTypes.Contains(HtmlNetworkResourceType.Document)) {
            options.ResourceTypes.Add(HtmlNetworkResourceType.Document);
        }

        if (IncludeResponseBody.IsPresent) {
            HtmlNetworkResourceType[]? responseBodyResourceTypes = MyInvocation.BoundParameters.ContainsKey(nameof(ResponseBodyResourceType))
                ? ResponseBodyResourceType
                : null;
            await HtmlBrowser.CaptureResponseBodiesAsync(
                session,
                ResponseBodyMaxBytes,
                responseBodyResourceTypes,
                CancellationToken,
                RedactResponseBody.IsPresent).ConfigureAwait(false);
        }

        WriteObject(HtmlBrowser.FindNetworkDataSources(session, options), true);
    }
}
