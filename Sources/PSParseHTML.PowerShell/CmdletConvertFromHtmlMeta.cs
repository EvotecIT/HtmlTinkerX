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
/// <code>ConvertFrom-HtmlMeta -Url https://example.com</code>
/// </example>
[Cmdlet(VerbsData.ConvertFrom, "HtmlMeta", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(PSObject))]
public sealed class CmdletConvertFromHtmlMeta : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetUrl = "Url";

    /// <summary>HTML content with meta tags.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>URL of a page with meta tags.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri? Url { get; set; }

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
            string url = (Url ?? throw new PSArgumentNullException(nameof(Url))).ToString();
            tags = await HtmlParser.ParseUrlMetaTagsAsync(url, client).ConfigureAwait(false);
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