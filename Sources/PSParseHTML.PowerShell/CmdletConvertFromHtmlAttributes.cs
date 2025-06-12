using AngleSharp.Dom;
using System.Management.Automation;
using System.Net.Http;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that extracts elements from HTML by tag, class, id or name attributes.
/// </summary>
[Cmdlet(VerbsData.ConvertFrom, "HtmlAttributes", DefaultParameterSetName = ParameterSetContent)]
[Alias("ConvertFrom-HTMLTag", "ConvertFrom-HTMLClass")]
[OutputType(typeof(string), typeof(IElement))]
public sealed class CmdletConvertFromHtmlAttributes : PSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetUrl = "Url";
    private const string ParameterSetFile = "File";

    /// <summary>HTML content to parse.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Path to an HTML file.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

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

    [Parameter]
    public string? Proxy { get; set; }

    [Parameter]
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>Return matching <see cref="IElement"/> objects instead of text.</summary>
    [Parameter]
    public SwitchParameter ReturnObject { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        IEnumerable<IElement> elements = ParameterSetName switch {
            ParameterSetUrl => HtmlParserExtensions.GetElements(
                DownloadHtml(),
                Tag,
                Class,
                Id,
                Name),
            ParameterSetFile => HtmlParserExtensions.GetElementsFromFile(
                Path,
                Tag,
                Class,
                Id,
                Name),
            _ => HtmlParserExtensions.GetElements(Content, Tag, Class, Id, Name)
        };

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
