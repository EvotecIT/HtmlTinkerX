using HtmlTinkerX;
using System;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Extracts inline React Server Component / React Flight payloads from HTML.
/// </summary>
/// <example>
/// <code>ConvertFrom-HtmlRscPayload -Content $html</code>
/// </example>
[Cmdlet(VerbsData.ConvertFrom, "HtmlRscPayload", DefaultParameterSetName = ParameterSetContent)]
[Alias("ConvertFrom-HtmlReactFlight", "ConvertFrom-HtmlReactFlightPayload")]
[OutputType(typeof(HtmlReactFlightRow))]
[OutputType(typeof(HtmlReactFlightPayload))]
[OutputType(typeof(HtmlReactFlightDocument))]
public sealed class CmdletConvertFromHtmlRscPayload : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";
    private const string ParameterSetUrl = "Url";

    /// <summary>HTML content to inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [Alias("Html")]
    [ValidateNotNullOrEmpty]
    public string Content { get; set; } = string.Empty;

    /// <summary>Path to an HTML file.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; } = string.Empty;

    /// <summary>URL of an HTML page to download and inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Proxy server address used when downloading by URL.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? Proxy { get; set; }

    /// <summary>Credentials used with the proxy server.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>Returns raw Next.js inline payload instructions instead of decoded rows.</summary>
    [Parameter]
    public SwitchParameter RawPayload { get; set; }

    /// <summary>Returns the full document object with both payloads and rows.</summary>
    [Parameter]
    public SwitchParameter AsDocument { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        HtmlReactFlightDocument document = await ReadDocumentAsync().ConfigureAwait(false);

        if (AsDocument.IsPresent) {
            WriteObject(document);
            return;
        }

        if (RawPayload.IsPresent) {
            WriteObject(document.Payloads.ToArray(), true);
            return;
        }

        WriteObject(document.Rows.ToArray(), true);
    }

    private async Task<HtmlReactFlightDocument> ReadDocumentAsync() {
        switch (ParameterSetName) {
            case ParameterSetFile:
                string html = await HtmlUtilities.ReadFileCheckedAsync(Path.ToFullPath()).ConfigureAwait(false);
                return HtmlReactFlightParser.Parse(html);
            case ParameterSetUrl:
                using (HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential)) {
                    return await HtmlReactFlightParser.ParseUrlAsync(Url.ToString(), client).ConfigureAwait(false);
                }
            default:
                return HtmlReactFlightParser.Parse(Content);
        }
    }
}
