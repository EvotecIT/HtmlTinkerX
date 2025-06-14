using System;
using System.IO;
using System.Management.Automation;
using PSParseHTML;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that formats CSS content using AngleSharp.
/// </summary>
/// <example>
/// <code>Format-CSS -Content "body{margin:0}"</code>
/// </example>
[Cmdlet(VerbsCommon.Format, "CSS", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(string))]
public sealed class CmdletFormatCss : PSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";

    /// <summary>Path to a CSS file to format.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

    /// <summary>CSS content to format.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Optional path to write the formatted CSS.</summary>
    [Parameter]
    public string? OutputFile { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        string formatted = ParameterSetName == ParameterSetFile
            ? HtmlFormatter.FormatCssFile(HtmlUtilities.ResolvePath(Path))
            : HtmlFormatter.FormatCss(Content);

        if (!string.IsNullOrEmpty(OutputFile)) {
            string outPath = HtmlUtilities.ResolvePath(OutputFile!);
            System.IO.File.WriteAllText(outPath, formatted);
        } else {
            WriteObject(formatted);
        }
    }
}
