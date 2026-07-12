using HtmlAgilityPack;
using HtmlTinkerX;
using System;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Finds forms, hidden fields, tokens, inline endpoints, and optional linked-script endpoints in HTML.</summary>
/// <example>
///   <summary>Find static interaction surfaces in captured HTML</summary>
///   <code>Find-HtmlInteractionSurface -Content $html</code>
/// </example>
/// <example>
///   <summary>Download a page and inspect linked same-origin JavaScript endpoints</summary>
///   <code>Find-HtmlInteractionSurface -Url https://example.org/app -IncludeLinkedScripts</code>
/// </example>
/// <example>
///   <summary>Inspect only a selected form subtree</summary>
///   <code>Select-HtmlNode -Content $html -CssSelector 'form' | Find-HtmlInteractionSurface</code>
/// </example>
[Cmdlet(VerbsCommon.Find, "HtmlInteractionSurface", DefaultParameterSetName = ParameterSetNode)]
[OutputType(typeof(HtmlInteractionSurfaceItem))]
public sealed class CmdletFindHtmlInteractionSurface : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";
    private const string ParameterSetNode = "Node";
    private const string ParameterSetUrl = "Url";

    /// <summary>HTML content to inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [ValidateNotNullOrEmpty]
    public string Content { get; set; } = string.Empty;

    /// <summary>Path to an HTML file.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; } = string.Empty;

    /// <summary>HtmlAgilityPack node or document to inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetNode, ValueFromPipeline = true, Position = 0)]
    [Alias("Node", "InputObject")]
    public object HtmlNode { get; set; } = null!;

    /// <summary>URL of an HTML page to download and inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Base URL used to resolve linked script URLs. Defaults to Url when downloading, and can be supplied by an absolute document base element.</summary>
    [Parameter]
    public Uri? BaseUrl { get; set; }

    /// <summary>Downloads and inspects same-origin linked JavaScript files when BaseUrl, Url, or an absolute document base element is available.</summary>
    [Parameter]
    public SwitchParameter IncludeLinkedScripts { get; set; }

    /// <summary>Allows cross-origin linked JavaScript downloads when IncludeLinkedScripts is used.</summary>
    [Parameter]
    public SwitchParameter IncludeExternalLinkedScripts { get; set; }

    /// <summary>Maximum number of bytes accepted for the page and each linked JavaScript response.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaximumResponseBytes { get; set; } = HtmlHttpFetchOptions.DefaultMaximumResponseBytes;

    /// <summary>Proxy server address used when downloading by URL.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? Proxy { get; set; }

    /// <summary>Credentials used with the proxy server.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential);
        HtmlHttpFetchOptions fetchOptions = new() { MaximumResponseBytes = MaximumResponseBytes };
        string html = await ReadHtmlAsync(client, fetchOptions).ConfigureAwait(false);
        Uri? baseUri = BaseUrl ?? (ParameterSetName == ParameterSetUrl ? Url : null);
        WriteObject((await HtmlParsingToolbox.FindInteractionSurfaceAsync(html, baseUri, IncludeLinkedScripts.IsPresent, IncludeExternalLinkedScripts.IsPresent, client, fetchOptions, CancelToken).ConfigureAwait(false)).ToArray(), true);
    }

    private async Task<string> ReadHtmlAsync(HttpClient client, HtmlHttpFetchOptions fetchOptions) {
        switch (ParameterSetName) {
            case ParameterSetFile:
                return await HtmlUtilities.ReadFileCheckedAsync(Path.ToFullPath()).ConfigureAwait(false);
            case ParameterSetUrl:
                return await HtmlUtilities.GetStringWithProperEncodingAsync(client, Url.ToString(), fetchOptions: fetchOptions, cancellationToken: CancelToken).ConfigureAwait(false);
            case ParameterSetNode:
                return HtmlPipelineInput.ToHtmlMarkup(HtmlNode);
            default:
                return Content;
        }
    }
}
