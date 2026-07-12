using HtmlAgilityPack;
using HtmlTinkerX;
using System;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Selects normalized structured data, links, assets, tokens, forms, and app state from HTML.</summary>
/// <example>
///   <summary>Extract every supported data family from a page</summary>
///   <code>Select-HtmlData -Url https://example.org -BaseUrl https://example.org</code>
/// </example>
/// <example>
///   <summary>Extract only SEO and schema data from static HTML</summary>
///   <code>Select-HtmlData -Content $html -Kind JsonLd,OpenGraph,Meta,Microdata</code>
/// </example>
/// <example>
///   <summary>Inspect a selected HtmlAgilityPack node</summary>
///   <code>Select-HtmlNode -Content $html -XPath '//head' | Select-HtmlData -Kind HeadLink,Meta</code>
/// </example>
[Cmdlet(VerbsCommon.Select, "HtmlData", DefaultParameterSetName = ParameterSetNode)]
[OutputType(typeof(HtmlDataItem))]
public sealed class CmdletSelectHtmlData : AsyncPSCmdlet {
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

    /// <summary>Data families to include. Supported values include JsonLd, Microdata, OpenGraph, Meta, HeadLink, AppState, ScriptData, Token, Form, Link, and Asset.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string[]? Kind { get; set; }

    /// <summary>Base URL used to resolve relative links and assets. Defaults to Url when downloading.</summary>
    [Parameter]
    public Uri? BaseUrl { get; set; }

    /// <summary>Proxy server address used when downloading by URL.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? Proxy { get; set; }

    /// <summary>Credentials used with the proxy server.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        string html = await ReadHtmlAsync().ConfigureAwait(false);
        Uri? baseUri = BaseUrl ?? (ParameterSetName == ParameterSetUrl ? Url : null);
        WriteObject(HtmlParsingToolbox.SelectData(html, Kind, baseUri).ToArray(), true);
    }

    private async Task<string> ReadHtmlAsync() {
        switch (ParameterSetName) {
            case ParameterSetFile:
                return await HtmlUtilities.ReadFileCheckedAsync(Path.ToFullPath()).ConfigureAwait(false);
            case ParameterSetUrl:
                using (HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential)) {
                    return await HtmlUtilities.GetStringWithProperEncodingAsync(client, Url.ToString(), CancelToken).ConfigureAwait(false);
                }
            case ParameterSetNode:
                return HtmlPipelineInput.ToHtmlMarkup(HtmlNode);
            default:
                return Content;
        }
    }
}
