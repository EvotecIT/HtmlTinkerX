using System;
using System.IO;
using System.Management.Automation;

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
    [Alias("Path")]
    public string File { get; set; } = string.Empty;

    /// <summary>CSS content to format.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Optional path to write the formatted CSS.</summary>
    [Parameter]
    public string? OutputFile { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        string css = GetCssContent();
        string formatted = HtmlFormatter.FormatCss(css);

        if (!string.IsNullOrEmpty(OutputFile)) {
            System.IO.File.WriteAllText(OutputFile, formatted);
        } else {
            WriteObject(formatted);
        }
    }

    private string GetCssContent() {
        switch (ParameterSetName) {
            case ParameterSetFile:
                if (!System.IO.File.Exists(File)) {
                    ThrowTerminatingError(new ErrorRecord(new FileNotFoundException($"CSS file not found: {File}", File), "FileNotFound", ErrorCategory.InvalidArgument, File));
                }
                return System.IO.File.ReadAllText(File);
            case ParameterSetContent:
            default:
                return Content;
        }
    }
}
