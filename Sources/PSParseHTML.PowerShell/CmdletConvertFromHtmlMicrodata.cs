using HtmlAgilityPack;
using HtmlTinkerX;
using System.Collections.Generic;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Extracts microdata items from HTML content or a URL.
/// </summary>
/// <example>
/// <code>ConvertFrom-HtmlMicrodata -Content $html</code>
/// </example>
[Cmdlet(VerbsData.ConvertFrom, "HtmlMicrodata", DefaultParameterSetName = ParameterSetNode)]
[OutputType(typeof(PSObject))]
public sealed class CmdletConvertFromHtmlMicrodata : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetNode = "Node";
    private const string ParameterSetUrl = "Url";

    /// <summary>HTML markup containing microdata.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>HtmlAgilityPack node to inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetNode, ValueFromPipeline = true, Position = 0)]
    [Alias("Node", "InputObject")]
    public HtmlNode HtmlNode { get; set; } = null!;

    /// <summary>URL of a page with microdata.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Proxy server address when downloading by URL.</summary>
    [Parameter]
    public string? Proxy { get; set; }

    /// <summary>Credentials for the proxy server.</summary>
    [Parameter]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        List<HtmlMicrodataItem> items;
        if (ParameterSetName == ParameterSetUrl) {
            using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential);
            items = await HtmlParser.ParseUrlMicrodataItemsAsync(Url.ToString(), client).ConfigureAwait(false);
        } else if (ParameterSetName == ParameterSetNode) {
            items = HtmlParser.ParseMicrodataItems(HtmlPipelineInput.ToHtmlMarkup(HtmlNode));
        } else {
            items = HtmlParser.ParseMicrodataItems(Content);
        }

        foreach (var item in items) {
            PSObject obj = new();
            obj.Properties.Add(new PSNoteProperty("Type", item.Type));
            obj.Properties.Add(new PSNoteProperty("Id", item.Id));
            obj.Properties.Add(new PSNoteProperty("Properties", item.Properties));
            WriteObject(obj);
        }
    }
}
