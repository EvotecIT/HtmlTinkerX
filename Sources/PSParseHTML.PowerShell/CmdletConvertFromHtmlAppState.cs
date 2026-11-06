using HtmlAgilityPack;
using HtmlTinkerX;
using System;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Extracts common embedded application state objects from HTML.</summary>
/// <example>
///   <summary>Extract framework app-state payloads from HTML content</summary>
///   <code>ConvertFrom-HtmlAppState -Content $html</code>
/// </example>
/// <example>
///   <summary>Inspect only a selected script node for app-state payloads</summary>
///   <code>ConvertFrom-Html -Content $html | Select-HtmlNode -XPath '//script[@id="__NEXT_DATA__"]' | ConvertFrom-HtmlAppState</code>
/// </example>
[Cmdlet(VerbsData.ConvertFrom, "HtmlAppState", DefaultParameterSetName = ParameterSetNode)]
[OutputType(typeof(HtmlAppStateEntry))]
public sealed class CmdletConvertFromHtmlAppState : AsyncUrlCmdlet {
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

    /// <summary>HtmlAgilityPack node to inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetNode, ValueFromPipeline = true, Position = 0)]
    [Alias("Node", "InputObject")]
    public HtmlNode HtmlNode { get; set; } = null!;

    /// <summary>URL of an HTML page to download and inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateUrlParameters();
        string html = await ReadHtmlAsync().ConfigureAwait(false);
        WriteObject(HtmlAppStateParser.Parse(html).ToArray(), true);
    }

    private async Task<string> ReadHtmlAsync() {
        switch (ParameterSetName) {
            case ParameterSetFile:
                return await HtmlUtilities.ReadFileCheckedAsync(Path.ToFullPath()).ConfigureAwait(false);
            case ParameterSetUrl:
                using (HttpClient client = await CreateUrlClientAsync().ConfigureAwait(false)) {
                    return await HtmlUtilities.GetStringWithProperEncodingAsync(client, Url.ToString(), fetchOptions: null, cancellationToken: CancelToken).ConfigureAwait(false);
                }
            case ParameterSetNode:
                return HtmlPipelineInput.ToHtmlMarkup(HtmlNode);
            default:
                return Content;
        }
    }
}
