using HtmlTinkerX;
using System;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Extracts normalized items from RSS or Atom feed XML.
/// </summary>
[Cmdlet(VerbsData.ConvertFrom, "HtmlSyndication", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(HtmlSyndicationItem))]
public sealed class CmdletConvertFromHtmlSyndication : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";
    private const string ParameterSetUrl = "Url";

    /// <summary>RSS or Atom XML content.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Path to an RSS or Atom XML file.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

    /// <summary>URL of an RSS or Atom XML document.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Base URL used to resolve relative item URLs. Defaults to <see cref="Url"/> when downloading.</summary>
    [Parameter]
    public Uri? BaseUrl { get; set; }

    /// <summary>URL recorded as the source feed for returned items. Defaults to <see cref="Url"/> when downloading.</summary>
    [Parameter]
    public string? SourceFeedUrl { get; set; }

    /// <summary>Proxy server address used when downloading by URL.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? Proxy { get; set; }

    /// <summary>Credentials used with the proxy server.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        string xml = await ReadContentAsync().ConfigureAwait(false);
        Uri? baseUri = BaseUrl ?? (ParameterSetName == ParameterSetUrl ? Url : null);
        string? sourceFeedUrl = SourceFeedUrl ?? (ParameterSetName == ParameterSetUrl ? Url.ToString() : null);
        WriteObject(HtmlDiscoveryParser.ParseSyndicationItems(xml, baseUri, sourceFeedUrl).ToArray(), true);
    }

    private async Task<string> ReadContentAsync() {
        switch (ParameterSetName) {
            case ParameterSetFile:
                return await HtmlUtilities.ReadFileCheckedAsync(Path.ToFullPath()).ConfigureAwait(false);
            case ParameterSetUrl:
                using (HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential)) {
                    return await HtmlUtilities.GetStringWithProperEncodingAsync(client, Url.ToString(), CancelToken).ConfigureAwait(false);
                }
            default:
                return Content;
        }
    }
}
