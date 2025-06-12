using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Management.Automation;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that converts HTML content to plain text.
/// </summary>
/// <example>
/// <code>Convert-HTMLToText -Content "&lt;p&gt;Hello&lt;/p&gt;"</code>
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
    [Alias("Path")]
    public string File { get; set; } = string.Empty;

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

    [Parameter]
    public string? Proxy { get; set; }

    [Parameter]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        string text = ParameterSetName switch {
            ParameterSetFile => HtmlUtilities.ConvertFileToText(File),
            ParameterSetUrl => HtmlUtilities.ConvertToText(
                HttpContentHelper.GetStringWithProperEncodingAsync(
                    HttpClientHelper.Create(Proxy, ProxyCredential),
                    Url.ToString()).GetAwaiter().GetResult()),
            _ => HtmlUtilities.ConvertToText(Content)
        };

        if (!string.IsNullOrEmpty(OutputFile)) {
            System.IO.File.WriteAllText(OutputFile, text);
        } else {
            WriteObject(text);
        }
    }
}
