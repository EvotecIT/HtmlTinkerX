using System.Management.Automation;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that formats JavaScript code using JsBeautifier.
/// </summary>
[Cmdlet(VerbsCommon.Format, "JavaScript", DefaultParameterSetName = ParameterSetContent)]
[Alias("Format-JS")]
[OutputType(typeof(string))]
public sealed class CmdletFormatJavaScript : PSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";

    /// <summary>
    /// JavaScript code to format.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [Alias("FileContent")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Path to a JavaScript file to format.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("Path")]
    public string File { get; set; } = string.Empty;

    /// <summary>
    /// Optional path to write the formatted JavaScript.
    /// </summary>
    [Parameter]
    public string? OutputFile { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        string formatted = ParameterSetName == ParameterSetFile
            ? HtmlFormatter.FormatJavaScriptFile(File)
            : HtmlFormatter.FormatJavaScript(Content);

        if (!string.IsNullOrEmpty(OutputFile)) {
            System.IO.File.WriteAllText(OutputFile, formatted);
        } else {
            WriteObject(formatted);
        }
    }
}
