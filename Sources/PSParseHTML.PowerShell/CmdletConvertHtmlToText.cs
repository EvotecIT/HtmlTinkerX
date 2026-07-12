using HtmlTinkerX;
using System;
using System.IO;
using System.Management.Automation;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Converts HTML content to plain text.</summary>
/// <para>
/// You can provide raw HTML, a local file, or download a page with <c>-Url</c>.
/// When downloading, optional <c>-Proxy</c> and <c>-ProxyCredential</c> can be used.
/// </para>
/// <example>
///   <summary>Convert a simple HTML string</summary>
///   <code>Convert-HtmlToText -Content "&lt;p&gt;Hello&lt;/p&gt;"</code>
/// </example>
/// <example>
///   <summary>Download through a proxy</summary>
///   <code>Convert-HtmlToText -Url https://example.com -Proxy http://proxy:8080</code>
/// </example>
[Cmdlet(VerbsData.Convert, "HtmlToText", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(string))]
public sealed class CmdletConvertHtmlToText : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";
    private const string ParameterSetUrl = "Url";

    /// <summary>
    /// HTML content to convert.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Path to a HTML file.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// URL of a HTML page to download and convert.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>
    /// Optional path to write the resulting text.
    /// </summary>
    [Parameter]
    public string? OutputFile { get; set; }

    /// <summary>Select the most readable article-like content region before converting to text.</summary>
    [Parameter]
    public SwitchParameter Readable { get; set; }

    /// <summary>Preferred CSS selector for the readable content container.</summary>
    [Parameter]
    public string? Selector { get; set; }

    /// <summary>Return readable extraction metadata instead of only the extracted text.</summary>
    [Parameter]
    public SwitchParameter IncludeMetadata { get; set; }

    /// <summary>
    /// Proxy server address used when downloading from <see cref="Url"/>.
    /// Include the protocol and port if necessary.
    /// </summary>
    [Parameter]
    public string? Proxy { get; set; }

    /// <summary>
    /// Credentials used for the <see cref="Proxy"/> server.
    /// </summary>
    [Parameter]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        string html = await ReadInputHtmlAsync().ConfigureAwait(false);

        if (Readable.IsPresent || IncludeMetadata.IsPresent || !string.IsNullOrWhiteSpace(Selector)) {
            HtmlReadableTextResult result = HtmlParserToText.ExtractReadableText(html, Selector);
            if (IncludeMetadata.IsPresent) {
                WriteObject(result);
                return;
            }

            await WriteOutputAsync(result.Text).ConfigureAwait(false);
            return;
        }

        string text = HtmlParserToText.ConvertToText(html);
        await WriteOutputAsync(text).ConfigureAwait(false);
    }

    private async Task<string> ReadInputHtmlAsync() {
        if (ParameterSetName == ParameterSetFile) {
            return HtmlUtilities.ReadFileChecked(Path.ToFullPath());
        }

        if (ParameterSetName == ParameterSetUrl) {
            using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential);
            return await HtmlUtilities.GetStringWithProperEncodingAsync(client, Url.ToString(), fetchOptions: null, cancellationToken: CancelToken).ConfigureAwait(false);
        }

        return Content;
    }

    private async Task WriteOutputAsync(string text) {
        if (!string.IsNullOrEmpty(OutputFile)) {
            string outPath = OutputFile!.ToFullPath();
#if NETSTANDARD2_0 || NETFRAMEWORK
            System.IO.File.WriteAllText(outPath, text);
#else
            await System.IO.File.WriteAllTextAsync(outPath, text, CancelToken).ConfigureAwait(false);
#endif
        } else {
            WriteObject(text);
        }
    }
}
