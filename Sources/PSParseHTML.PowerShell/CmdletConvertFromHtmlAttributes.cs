using AngleSharp.Dom;
using System.Management.Automation;
using System.Net.Http;

namespace PSParseHTML.PowerShell;

/// <summary>Extracts HTML elements by tag, class, id or name attributes.</summary>
/// <para>
/// Input can be raw HTML or a page retrieved from <c>-Url</c>. Use
/// <c>-Proxy</c> when the page must be downloaded through a proxy server.
/// </para>
/// <example>
///   <summary>Extract links from a page</summary>
///   <code>ConvertFrom-HtmlAttributes -Url https://example.com -Tag a</code>
/// </example>
/// <example>
///   <summary>Specify a proxy while downloading</summary>
///   <code>ConvertFrom-HtmlAttributes -Url https://example.com -Proxy http://proxy:8080 -Tag a</code>
/// </example>
[Cmdlet(VerbsData.ConvertFrom, "HtmlAttributes", DefaultParameterSetName = ParameterSetContent)]
[Alias("ConvertFrom-HTMLTag", "ConvertFrom-HTMLClass")]
[OutputType(typeof(string), typeof(IElement))]
public sealed class CmdletConvertFromHtmlAttributes : PSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetUrl = "Url";

    /// <summary>HTML content to parse.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>URL of HTML page to download.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Tag name to search for.</summary>
    [Parameter]
    public string? Tag { get; set; }

    /// <summary>Class name to search for.</summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>ID attribute to search for.</summary>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>Name attribute to search for.</summary>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>
    /// Proxy server address used when <see cref="Url"/> is specified.
    /// Include protocol and port if necessary.
    /// </summary>
    [Parameter]
    public string? Proxy { get; set; }

    /// <summary>
    /// Credentials used with the <see cref="Proxy"/> server.
    /// </summary>
    [Parameter]
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>Return matching <see cref="IElement"/> objects instead of text.</summary>
    [Parameter]
    public SwitchParameter ReturnObject { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        string html = ParameterSetName == ParameterSetUrl ? DownloadHtml() : Content;

        IEnumerable<IElement> elements = HtmlParserExtensions.GetElements(html, Tag, Class, Id, Name);

        if (ReturnObject.IsPresent) {
            foreach (var e in elements) {
                WriteObject(e);
            }
        } else {
            foreach (var e in elements) {
                WriteObject(e.TextContent);
            }
        }
    }

    private string DownloadHtml() {
        using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential);
        return HttpContentHelper.GetStringWithProperEncodingAsync(client, Url.ToString()).GetAwaiter().GetResult();
        }
}
