using HtmlAgilityPack;
using HtmlTinkerX;
using System;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Reports whether CSS selectors from inline or supplied CSS match elements in an HTML document.</summary>
/// <example>
///   <summary>Find unused inline style rules</summary>
///   <code>Select-HtmlStyleUsage -Content $html | Where-Object IsUsed -EQ $false</code>
/// </example>
/// <example>
///   <summary>Compare an external stylesheet with captured HTML</summary>
///   <code>Select-HtmlStyleUsage -Content $html -CssPath .\site.css -UsedOnly</code>
/// </example>
/// <example>
///   <summary>Inspect style usage inside selected markup</summary>
///   <code>Select-HtmlNode -Content $html -CssSelector 'main' | Select-HtmlStyleUsage -MaxMatchedElements 3</code>
/// </example>
[Cmdlet(VerbsCommon.Select, "HtmlStyleUsage", DefaultParameterSetName = ParameterSetNode)]
[OutputType(typeof(HtmlStyleUsageItem))]
public sealed class CmdletSelectHtmlStyleUsage : AsyncPSCmdlet {
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

    /// <summary>CSS content to compare against the HTML. When omitted, inline style elements are used.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? CssContent { get; set; }

    /// <summary>Path to a CSS file to compare against the HTML.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? CssPath { get; set; }

    /// <summary>Returns only selectors that matched at least one element or had a selector error.</summary>
    [Parameter]
    public SwitchParameter UsedOnly { get; set; }

    /// <summary>Maximum representative matched element selectors returned for each CSS rule.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int MaxMatchedElements { get; set; } = 10;

    /// <summary>Proxy server address used when downloading by URL.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? Proxy { get; set; }

    /// <summary>Credentials used with the proxy server.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        if (!string.IsNullOrEmpty(CssContent) && !string.IsNullOrEmpty(CssPath)) {
            throw new PSArgumentException("Use either -CssContent or -CssPath, not both.");
        }

        ValidateProxy(Proxy, ProxyCredential);
        string html = await ReadHtmlAsync().ConfigureAwait(false);
        string? css = string.IsNullOrEmpty(CssPath)
            ? CssContent
            : await HtmlUtilities.ReadFileCheckedAsync(CssPath!.ToFullPath()).ConfigureAwait(false);
        WriteObject(HtmlParsingToolbox.SelectStyleUsage(html, css, includeUnused: !UsedOnly.IsPresent, MaxMatchedElements).ToArray(), true);
    }

    private async Task<string> ReadHtmlAsync() {
        switch (ParameterSetName) {
            case ParameterSetFile:
                return await HtmlUtilities.ReadFileCheckedAsync(Path.ToFullPath()).ConfigureAwait(false);
            case ParameterSetUrl:
                using (HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential)) {
                    return await HtmlUtilities.GetStringWithProperEncodingAsync(client, Url.ToString()).ConfigureAwait(false);
                }
            case ParameterSetNode:
                return HtmlPipelineInput.ToHtmlMarkup(HtmlNode);
            default:
                return Content;
        }
    }
}
