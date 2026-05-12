using HtmlTinkerX;
using System;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Extracts URLs from sitemap urlset or sitemap index XML.
/// </summary>
[Cmdlet(VerbsData.ConvertFrom, "HtmlSitemap", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(string))]
public sealed class CmdletConvertFromHtmlSitemap : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";
    private const string ParameterSetUrl = "Url";

    /// <summary>Sitemap XML content.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Path to a sitemap XML file.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

    /// <summary>URL of a sitemap XML document.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Base URL used to resolve relative sitemap locations. Defaults to <see cref="Url"/> when downloading.</summary>
    [Parameter]
    public Uri? BaseUrl { get; set; }

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
        WriteObject(HtmlDiscoveryParser.ParseSitemapUrls(xml, baseUri).ToArray(), true);
    }

    private async Task<string> ReadContentAsync() {
        switch (ParameterSetName) {
            case ParameterSetFile:
                return await HtmlUtilities.ReadFileCheckedAsync(Path.ToFullPath()).ConfigureAwait(false);
            case ParameterSetUrl:
                using (HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential)) {
                    return await HtmlUtilities.GetStringWithProperEncodingAsync(client, Url.ToString()).ConfigureAwait(false);
                }
            default:
                return Content;
        }
    }
}
