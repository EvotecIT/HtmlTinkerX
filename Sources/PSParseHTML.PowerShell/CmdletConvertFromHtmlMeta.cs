using HtmlAgilityPack;
using HtmlTinkerX;
using System.Collections.Generic;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Parses &lt;meta&gt; tags from HTML content or a URL.
/// </summary>
/// <example>
///   <summary>Extract meta tags from a page</summary>
///   <code>ConvertFrom-HtmlMeta -Url https://example.com</code>
/// </example>
/// <example>
///   <summary>Inspect only the selected head node</summary>
///   <code>ConvertFrom-Html -Content $html | Select-HtmlNode -XPath '//head' | ConvertFrom-HtmlMeta</code>
/// </example>
[Cmdlet(VerbsData.ConvertFrom, "HtmlMeta", DefaultParameterSetName = ParameterSetNode)]
[OutputType(typeof(PSObject))]
public sealed class CmdletConvertFromHtmlMeta : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetNode = "Node";
    private const string ParameterSetUrl = "Url";

    /// <summary>HTML content with meta tags.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>HtmlAgilityPack node to inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetNode, ValueFromPipeline = true, Position = 0)]
    [Alias("Node", "InputObject")]
    public HtmlNode HtmlNode { get; set; } = null!;

    /// <summary>URL of a page with meta tags.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Proxy server address to use when downloading by URL.</summary>
    [Parameter]
    public string? Proxy { get; set; }

    /// <summary>Credentials for the specified proxy server.</summary>
    [Parameter]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        List<HtmlMetaTag> tags;
        if (ParameterSetName == ParameterSetUrl) {
            using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential);
            tags = await HtmlParser.ParseUrlMetaTagsAsync(Url.ToString(), client, cancellationToken: CancelToken).ConfigureAwait(false);
        } else if (ParameterSetName == ParameterSetNode) {
            tags = HtmlParser.ParseMetaTags(HtmlPipelineInput.ToHtmlMarkup(HtmlNode));
        } else {
            tags = HtmlParser.ParseMetaTags(Content);
        }

        foreach (var tag in tags) {
            PSObject obj = new();
            obj.Properties.Add(new PSNoteProperty("Name", tag.Name));
            obj.Properties.Add(new PSNoteProperty("Content", tag.Content));
            WriteObject(obj);
        }
    }
}
