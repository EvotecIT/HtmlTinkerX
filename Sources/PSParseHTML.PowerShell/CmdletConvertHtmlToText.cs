using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Management.Automation;
using PSParseHTML;

namespace PSParseHTML.PowerShell;

/// <summary>Converts HTML content to plain text.</summary>
/// <para>
/// You can provide raw HTML, a local file, or download a page with <c>-Url</c>.
/// When downloading, optional <c>-Proxy</c> and <c>-ProxyCredential</c> can be used.
/// </para>
/// <example>
///   <summary>Convert a simple HTML string</summary>
///   <code>Convert-HTMLToText -Content "&lt;p&gt;Hello&lt;/p&gt;"</code>
/// </example>
/// <example>
///   <summary>Download through a proxy</summary>
///   <code>Convert-HTMLToText -Url https://example.com -Proxy http://proxy:8080</code>
/// </example>
[Cmdlet(VerbsData.Convert, "HTMLToText", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(string))]
public sealed class CmdletConvertHtmlToText : PSCmdlet {
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
    protected override void ProcessRecord() {
        string text = ParameterSetName switch {
            ParameterSetFile => HtmlParserToText.ConvertFileToText(HtmlUtilities.ResolvePath(Path)),
            ParameterSetUrl => HtmlParserToText.ConvertToText(
                HtmlUtilities.GetStringWithProperEncodingAsync(
                    HttpClientHelper.Create(Proxy, ProxyCredential),
                    Url.ToString()).GetAwaiter().GetResult()),
            _ => HtmlParserToText.ConvertToText(Content)
        };

        if (!string.IsNullOrEmpty(OutputFile)) {
            string outPath = HtmlUtilities.ResolvePath(OutputFile);
            System.IO.File.WriteAllText(outPath, text);
        } else {
            WriteObject(text);
        }
    }
}
