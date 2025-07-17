using System;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp.Dom;
using HtmlAgilityPack;
using HtmlTinkerX;

namespace PSParseHTML.PowerShell;

/// <summary>Parses HTML content from a string or a remote page.</summary>
/// <para>
/// The cmdlet can read raw HTML or download a web page specified with
/// <c>-Url</c>. When downloading, optional <c>-Proxy</c> and
/// <c>-ProxyCredential</c> parameters control the web request.
/// </para>
/// <example>
///   <summary>Download a web page and parse it</summary>
///   <code>ConvertFrom-HTML -Url https://example.com</code>
/// </example>
/// <example>
///   <summary>Use a proxy server when downloading</summary>
///   <code>ConvertFrom-HTML -Url https://example.com -Proxy http://proxy:8080</code>
/// </example>
[Cmdlet(VerbsData.ConvertFrom, "HTML", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(object))]
public sealed class CmdletConvertFromHtml : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetUrl = "Url";

    /// <summary>HTML content to parse.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>URL of a HTML page.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Selects parsing engine.</summary>
    [Parameter]
    public HtmlParserEngine Engine { get; set; } = HtmlParserEngine.AgilityPack;

    /// <summary>
    /// Optional proxy server address used when fetching content from <see cref="Url"/>.
    /// Include the protocol and port number if required.
    /// </summary>
    [Parameter]
    public string? Proxy { get; set; }

    /// <summary>
    /// Credentials used to authenticate against the <see cref="Proxy"/> server.
    /// </summary>
    [Parameter]
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>Return raw document object.</summary>
    [Parameter]
    public SwitchParameter Raw { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        if (ParameterSetName == ParameterSetUrl) {
            using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential);
            if (Engine == HtmlParserEngine.AngleSharp) {
                IDocument doc = await HtmlParser.ParseUrlWithAngleSharpAsync(Url.ToString(), client).ConfigureAwait(false);
                WriteObject(Raw.IsPresent ? doc : doc.DocumentElement);
                return;
            }
            HtmlDocument doc2 = await HtmlParser.ParseUrlWithHtmlAgilityPackAsync(Url.ToString(), client).ConfigureAwait(false);
            WriteObject(Raw.IsPresent ? doc2 : doc2.DocumentNode);
            return;
        }

        if (Engine == HtmlParserEngine.AngleSharp) {
            IDocument doc = HtmlParser.ParseWithAngleSharp(Content);
            WriteObject(Raw.IsPresent ? doc : doc.DocumentElement);
        } else {
            HtmlDocument doc = HtmlParser.ParseWithHtmlAgilityPack(Content);
            WriteObject(Raw.IsPresent ? doc : doc.DocumentNode);
        }
    }
}
