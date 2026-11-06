using HtmlAgilityPack;
using HtmlTinkerX;
using System.Collections.Generic;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Extracts Open Graph metadata from HTML content or a URL.
/// </summary>
/// <example>
///   <summary>Extract Open Graph metadata from HTML content</summary>
///   <code>ConvertFrom-HtmlOpenGraph -Content $html</code>
/// </example>
/// <example>
///   <summary>Inspect only the selected head node</summary>
///   <code>ConvertFrom-Html -Content $html | Select-HtmlNode -XPath '//head' | ConvertFrom-HtmlOpenGraph</code>
/// </example>
[Cmdlet(VerbsData.ConvertFrom, "HtmlOpenGraph", DefaultParameterSetName = ParameterSetNode)]
[OutputType(typeof(PSObject))]
public sealed class CmdletConvertFromHtmlOpenGraph : AsyncUrlCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetNode = "Node";
    private const string ParameterSetUrl = "Url";

    /// <summary>HTML markup containing Open Graph meta tags.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>HtmlAgilityPack node to inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetNode, ValueFromPipeline = true, Position = 0)]
    [Alias("Node", "InputObject")]
    public HtmlNode HtmlNode { get; set; } = null!;

    /// <summary>URL of a page with Open Graph metadata.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateUrlParameters();
        HtmlOpenGraph graph;
        if (ParameterSetName == ParameterSetUrl) {
            using HttpClient client = await CreateUrlClientAsync().ConfigureAwait(false);
            graph = await HtmlParser.ParseUrlOpenGraphAsync(Url.ToString(), client, cancellationToken: CancelToken).ConfigureAwait(false);
        } else if (ParameterSetName == ParameterSetNode) {
            graph = HtmlParser.ParseOpenGraph(HtmlPipelineInput.ToHtmlMarkup(HtmlNode));
        } else {
            graph = HtmlParser.ParseOpenGraph(Content);
        }

        PSObject obj = new();
        foreach (OpenGraphProperty prop in graph.Properties) {
            object value = prop.Values.Count == 1 ? prop.Values[0] : prop.Values.ToArray();
            obj.Properties.Add(new PSNoteProperty(prop.Name, value));
        }
        WriteObject(obj);
    }
}
