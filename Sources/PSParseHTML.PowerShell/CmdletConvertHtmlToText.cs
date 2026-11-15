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
///   <summary>Keep paragraph and line boundaries</summary>
///   <code>Convert-HtmlToText -Content "&lt;p&gt;Re&lt;b&gt;port&lt;/b&gt;&lt;/p&gt;&lt;p&gt;Line 1&lt;br&gt;Line 2&lt;/p&gt;" -PreserveLineBreaks</code>
///   <para>Returns three lines: Report, Line 1 and Line 2. Inline elements do not split words.</para>
/// </example>
/// <example>
///   <summary>Download through a proxy</summary>
///   <code>Convert-HtmlToText -Url https://example.com -Proxy http://proxy:8080</code>
/// </example>
[Cmdlet(VerbsData.Convert, "HtmlToText", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(string))]
public sealed class CmdletConvertHtmlToText : AsyncUrlCmdlet {
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
    /// Keep line and paragraph boundaries: br and block elements such as paragraphs, list items, headings and table rows
    /// end a line, newlines in text stay line breaks, and inline elements join their text. Scripts, styles, templates
    /// and noscript content are omitted. Lines are separated by a line feed. Cannot be combined with Readable,
    /// Selector or IncludeMetadata.
    /// </summary>
    [Parameter]
    public SwitchParameter PreserveLineBreaks { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateUrlParameters();
        bool readable = Readable.IsPresent || IncludeMetadata.IsPresent || !string.IsNullOrWhiteSpace(Selector);
        if (PreserveLineBreaks.IsPresent && readable) {
            ThrowTerminatingError(new ErrorRecord(
                new PSArgumentException("-PreserveLineBreaks cannot be combined with -Readable, -Selector or -IncludeMetadata."),
                "PreserveLineBreaksWithReadable", ErrorCategory.InvalidArgument, null));
        }

        string html = await ReadInputHtmlAsync().ConfigureAwait(false);

        if (PreserveLineBreaks.IsPresent) {
            await WriteOutputAsync(HtmlParserToText.ConvertFragmentToText(html, preserveLineBreaks: true)).ConfigureAwait(false);
            return;
        }

        if (readable) {
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
            using HttpClient client = await CreateUrlClientAsync().ConfigureAwait(false);
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
