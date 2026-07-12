using HtmlTinkerX;
using OfficeIMO.Markdown;
using OfficeIMO.Markdown.Html;
using System;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Converts HTML content to Markdown.</summary>
/// <para>
/// You can provide raw HTML, a local file, or download a page with <c>-Url</c>.
/// When downloading, optional <c>-Proxy</c> and <c>-ProxyCredential</c> can be used.
/// </para>
/// <example>
///   <summary>Convert a simple HTML string</summary>
///   <code>Convert-HtmlToMarkdown -Content "&lt;h1&gt;Hello&lt;/h1&gt;"</code>
/// </example>
/// <example>
///   <summary>Download through a proxy</summary>
///   <code>Convert-HtmlToMarkdown -Url https://example.com -Proxy http://proxy:8080</code>
/// </example>
[Cmdlet(VerbsData.Convert, "HtmlToMarkdown", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(string))]
public sealed class CmdletConvertHtmlToMarkdown : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";
    private const string ParameterSetUrl = "Url";

    /// <summary>HTML content to convert.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Path to a HTML file.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

    /// <summary>URL of a HTML page to download and convert.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Optional absolute page URL used to resolve relative links and images.</summary>
    [Parameter]
    public string? PageUrl { get; set; }

    /// <summary>Optional path to write the resulting Markdown.</summary>
    [Parameter]
    public string? OutputFile { get; set; }

    /// <summary>Controls which Markdown dialect profile is used.</summary>
    [Parameter]
    public HtmlMarkdownProfile MarkdownProfile { get; set; } = HtmlMarkdownProfile.Portable;

    /// <summary>Controls how images are emitted in Markdown.</summary>
    [Parameter]
    public MarkdownImageRenderingMode MarkdownImageMode { get; set; } = MarkdownImageRenderingMode.PortableMarkdown;

    /// <summary>Controls whether low-value metadata inside repeated listing cards should be preserved or suppressed.</summary>
    [Parameter]
    public HtmlListingCardMetadataMode ListingCardMetadataMode { get; set; } = HtmlListingCardMetadataMode.SuppressInRepeatedCards;

    /// <summary>
    /// Proxy server address used when downloading from <see cref="Url"/>.
    /// Include the protocol and port if necessary.
    /// </summary>
    [Parameter]
    public string? Proxy { get; set; }

    /// <summary>Credentials used for the <see cref="Proxy"/> server.</summary>
    [Parameter]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        string html = ParameterSetName switch {
            ParameterSetFile => HtmlUtilities.ReadFileChecked(Path.ToFullPath()),
            ParameterSetUrl => await GetUrlContentAsync().ConfigureAwait(false),
            _ => Content
        };
        string? pageUrl = !string.IsNullOrWhiteSpace(PageUrl)
            ? PageUrl
            : ParameterSetName == ParameterSetUrl ? Url.ToString() : null;

        string markdown = HtmlParserToMarkdown.ConvertToMarkdown(
            html,
            pageUrl,
            MarkdownImageMode,
            ListingCardMetadataMode,
            MarkdownProfile);
        await WriteOutputAsync(markdown).ConfigureAwait(false);
    }

    private async Task<string> GetUrlContentAsync() {
        using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential);
        return await HtmlUtilities.GetStringWithProperEncodingAsync(client, Url.ToString(), CancelToken).ConfigureAwait(false);
    }

    private async Task WriteOutputAsync(string markdown) {
        if (!string.IsNullOrEmpty(OutputFile)) {
            string outPath = OutputFile!.ToFullPath();
#if NETSTANDARD2_0 || NETFRAMEWORK
            System.IO.File.WriteAllText(outPath, markdown);
#else
            await System.IO.File.WriteAllTextAsync(outPath, markdown, CancelToken).ConfigureAwait(false);
#endif
        } else {
            WriteObject(markdown);
        }
    }
}
