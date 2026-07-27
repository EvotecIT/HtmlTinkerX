using AngleSharp.Dom;
using HtmlTinkerX;
using System;
using System.Collections;
using System.IO;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Selects elements from static HTML with a CSS selector.</summary>
/// <example>
///   <summary>Select product cards from downloaded HTML</summary>
///   <code>ConvertFrom-Html -Content $html | Select-HtmlElement -Selector '.product-card'</code>
/// </example>
/// <example>
///   <summary>Download a page and return its first heading</summary>
///   <code>Select-HtmlElement -Url https://example.org -Selector 'h1' -First</code>
/// </example>
[Cmdlet(VerbsCommon.Select, "HtmlElement", DefaultParameterSetName = ParameterSetInput)]
[Alias("Find-HtmlElement")]
[OutputType(typeof(IElement))]
public sealed class CmdletSelectHtmlElement : AsyncPSCmdlet {
    private const string ParameterSetInput = "Input";
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";
    private const string ParameterSetUrl = "Url";

    /// <summary>Parsed document, element, HtmlAgilityPack node, or raw markup to search.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetInput, ValueFromPipeline = true, Position = 0)]
    [Alias("HtmlDocument", "HtmlNode", "Node", "InputObject")]
    public object Input { get; set; } = null!;

    /// <summary>HTML content to search.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Path to an HTML file to search.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

    /// <summary>URL of an HTML page to download and search.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>CSS selector evaluated against the static document or input element.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    [ValidateNotNullOrEmpty]
    public string Selector { get; set; } = string.Empty;

    /// <summary>Return only the first matching element.</summary>
    [Parameter]
    [Alias("Single")]
    public SwitchParameter First { get; set; }

    /// <summary>Throw when the selector matches no elements.</summary>
    [Parameter]
    public SwitchParameter Required { get; set; }

    /// <summary>Proxy server address used when downloading by URL.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? Proxy { get; set; }

    /// <summary>Credentials used with the proxy server.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>User-Agent header used when downloading <see cref="Url"/>.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? UserAgent { get; set; }

    /// <summary>Additional or replacement HTTP headers used when downloading <see cref="Url"/>.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Alias("Headers")]
    public Hashtable? Header { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        string html = await ReadHtmlAsync().ConfigureAwait(false);
        IReadOnlyList<IElement> matches = HtmlDomExtraction.SelectElements(html, Selector, First.IsPresent);
        if (Required.IsPresent && matches.Count == 0) {
            ThrowTerminatingError(new ErrorRecord(
                new PSInvalidOperationException($"CSS selector '{Selector}' did not match any elements."),
                "HtmlSelectorNoMatch",
                ErrorCategory.ObjectNotFound,
                Selector));
            return;
        }

        WriteObject(matches, true);
    }

    private async Task<string> ReadHtmlAsync() {
        switch (ParameterSetName) {
            case ParameterSetContent:
                return Content;
            case ParameterSetFile:
                return await HtmlUtilities.ReadFileCheckedAsync(Path.ToFullPath()).ConfigureAwait(false);
            case ParameterSetUrl:
                using (HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential, UserAgent, Header)) {
                    return await HtmlUtilities.GetStringWithProperEncodingAsync(client, Url.ToString(), fetchOptions: null, cancellationToken: CancelToken).ConfigureAwait(false);
                }
            default:
                return HtmlPipelineInput.ToHtmlMarkup(Input);
        }
    }
}
