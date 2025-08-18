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
/// <code>ConvertFrom-HtmlOpenGraph -Content $html</code>
/// </example>
[Cmdlet(VerbsData.ConvertFrom, "HtmlOpenGraph", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(PSObject))]
public sealed class CmdletConvertFromHtmlOpenGraph : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetUrl = "Url";

    /// <summary>HTML markup containing Open Graph meta tags.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>URL of a page with Open Graph metadata.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri? Url { get; set; }

    /// <summary>Proxy server address when downloading by URL.</summary>
    [Parameter]
    public string? Proxy { get; set; }

    /// <summary>Credentials for the proxy server.</summary>
    [Parameter]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        HtmlOpenGraph graph;
        if (ParameterSetName == ParameterSetUrl) {
            using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential);
            string url = (Url ?? throw new PSArgumentNullException(nameof(Url))).ToString();
            graph = await HtmlParser.ParseUrlOpenGraphAsync(url, client).ConfigureAwait(false);
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