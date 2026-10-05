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
/// <example>
///   <summary>Extract typed prices and require non-empty product names</summary>
///   <code>
/// Select-HtmlData -Content $html -ItemSelector '.product-card' -Property @{
///     Name = @{ Selector = '.product-title'; Required = $true; TreatEmptyAsMissing = $true }
///     Price = @{ Selector = '.product-price'; DataType = [decimal]; Culture = 'pl-PL' }
/// }
///   </code>
///   <para>Price values such as 1234,50 become decimal values. Invalid prices and missing names raise an error identifying the field and item.</para>
/// </example>
/// <example>
///   <summary>Inspect extraction quality and require a non-empty dataset</summary>
///   <code>
/// $report = Select-HtmlData -Content $html -ItemSelector '.product-card' -Property @{
///     Name = @{ Selector = '.product-title'; Required = $true }
///     Price = @{ Selector = '.product-price'; DataType = [decimal]; MaximumValueCount = 1 }
/// } -AsExtractionReport -MinimumItemCount 1
/// $report.IsValid
/// $report.Fields
///   </code>
///   <para>Missing required fields, invalid values, and count changes are reported. Inspect IsValid before accepting Records; fields with data errors contain null.</para>
/// </example>
[Cmdlet(VerbsCommon.Select, "HtmlData", DefaultParameterSetName = ParameterSetNode)]
[OutputType(typeof(HtmlDataItem), typeof(PSObject), typeof(HtmlDomExtractionReport))]
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
    /// ValueKind, All, Required, DefaultValue, ResolveUrl, DataType, Culture, TreatEmptyAsMissing,
    /// MinimumValueCount, or MaximumValueCount.
    /// DataType accepts [string], [int], [long], [decimal], [bool], [DateTimeOffset], or an enum type.
    /// Conversion uses invariant culture by default; Culture can specify a name such as pl-PL.
    /// Required fields throw when missing. Invalid typed values report the property and item index.
    /// </summary>
    [Parameter]
    [Alias("Properties", "Field", "Fields")]
    public IDictionary? Property { get; set; }

    /// <summary>Return a single extraction report with records, selector provenance, and field quality checks. Requires ItemSelector and Property.</summary>
    [Parameter]
    public SwitchParameter AsExtractionReport { get; set; }

    /// <summary>Minimum acceptable item count in an extraction report. Requires AsExtractionReport.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int? MinimumItemCount { get; set; }

    /// <summary>Maximum acceptable item count in an extraction report. Requires AsExtractionReport.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int? MaximumItemCount { get; set; }

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
        if (!AsExtractionReport && (MinimumItemCount.HasValue || MaximumItemCount.HasValue)) {
            throw new PSArgumentException("Item-count bounds require AsExtractionReport.");
        }
        if (AsExtractionReport && (string.IsNullOrWhiteSpace(ItemSelector) || Property == null)) {
            throw new PSArgumentException("AsExtractionReport requires ItemSelector and Property.");
        }
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
            if (AsExtractionReport) {
                WriteObject(HtmlDomExtraction.ExtractReport(html, ItemSelector!, definitions,
                    new HtmlDomExtractionReportOptions {
                        MinimumItemCount = MinimumItemCount, MaximumItemCount = MaximumItemCount
                    }, baseUri));
                return;
            }
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