using HtmlAgilityPack;
using HtmlTinkerX;
using System;
using System.Collections;
using System.Collections.Generic;
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
/// <example>
///   <summary>Convert repeated product cards into PowerShell objects with CSS selectors</summary>
///   <code>
/// Select-HtmlData -Url https://example.org/products -ItemSelector '.product-card' -Property @{
///     Name = '.product-title'
///     Price = '.product-price'
///     Link = @{ Selector = 'a'; Attribute = 'href' }
/// }
///   </code>
/// </example>
[Cmdlet(VerbsCommon.Select, "HtmlData", DefaultParameterSetName = ParameterSetNode)]
[OutputType(typeof(HtmlDataItem), typeof(PSObject))]
public sealed class CmdletSelectHtmlData : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";
    private const string ParameterSetNode = "Node";
    private const string ParameterSetUrl = "Url";
    private Uri? _effectiveUrl;

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

    /// <summary>CSS selector matching each repeated item to convert into a PowerShell object.</summary>
    [Parameter]
    public string? ItemSelector { get; set; }

    /// <summary>
    /// Property-to-selector map used with <see cref="ItemSelector"/>.
    /// String values read trimmed text. Hashtable values can specify Selector, Attribute,
    /// ValueKind, All, Required, DefaultValue, or ResolveUrl.
    /// </summary>
    [Parameter]
    [Alias("Properties", "Field", "Fields")]
    public IDictionary? Property { get; set; }

    /// <summary>Base URL used to resolve relative links and assets. Defaults to Url when downloading.</summary>
    [Parameter]
    public Uri? BaseUrl { get; set; }

    /// <summary>Proxy server address used when downloading by URL.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? Proxy { get; set; }

    /// <summary>Credentials used with the proxy server.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>User-Agent header used when downloading <see cref="Url"/>.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? UserAgent { get; set; }

    /// <summary>Additional or replacement HTTP headers used when downloading <see cref="Url"/>.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Alias("Headers")]
    public Hashtable? Header { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        string html = await ReadHtmlAsync().ConfigureAwait(false);
        Uri? baseUri = BaseUrl ?? (ParameterSetName == ParameterSetUrl ? _effectiveUrl ?? Url : null);
        bool extractionRequested = !string.IsNullOrWhiteSpace(ItemSelector) || Property != null;
        if (extractionRequested) {
            if (string.IsNullOrWhiteSpace(ItemSelector) || Property == null) {
                throw new PSArgumentException("ItemSelector and Property must be specified together.");
            }

            if (Kind != null && Kind.Length > 0) {
                throw new PSArgumentException("Kind cannot be combined with ItemSelector and Property.");
            }

            IReadOnlyDictionary<string, HtmlDomFieldDefinition> definitions =
                HtmlDomPropertyMapConverter.Convert(Property);
            foreach (HtmlDomExtractionRecord record in HtmlDomExtraction.Extract(
                html,
                ItemSelector!,
                definitions,
                baseUri)) {
                PSObject output = new();
                output.TypeNames.Insert(0, "HtmlTinkerX.HtmlDomRecord");
                foreach (KeyValuePair<string, object?> value in record.Values) {
                    output.Properties.Add(new PSNoteProperty(value.Key, value.Value));
                }

                WriteObject(output);
            }

            return;
        }

        WriteObject(HtmlParsingToolbox.SelectData(html, Kind, baseUri).ToArray(), true);
    }

    private async Task<string> ReadHtmlAsync() {
        switch (ParameterSetName) {
            case ParameterSetFile:
                return await HtmlUtilities.ReadFileCheckedAsync(Path.ToFullPath()).ConfigureAwait(false);
            case ParameterSetUrl:
                using (HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential, UserAgent, Header)) {
                    HtmlHttpTextResult result = await HtmlUtilities.GetTextWithProperEncodingAsync(
                        client,
                        Url.ToString(),
                        fetchOptions: null,
                        cancellationToken: CancelToken).ConfigureAwait(false);
                    _effectiveUrl = result.FinalUri ?? Url;
                    return result.Content;
                }
            case ParameterSetNode:
                return HtmlPipelineInput.ToHtmlMarkup(HtmlNode);
            default:
                return Content;
        }
    }
}
