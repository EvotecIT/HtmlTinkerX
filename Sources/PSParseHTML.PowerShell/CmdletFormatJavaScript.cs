using System.Management.Automation;
using PSParseHTML;

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
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Optional path to write the formatted JavaScript.
    /// </summary>
    [Parameter]
    public string? OutputFile { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        string formatted = ParameterSetName == ParameterSetFile
            ? HtmlFormatter.FormatJavaScriptFile(FileUtilities.ResolvePath(Path))
            : HtmlFormatter.FormatJavaScript(Content);

        if (!string.IsNullOrEmpty(OutputFile)) {
            string outPath = FileUtilities.ResolvePath(OutputFile);
            System.IO.File.WriteAllText(outPath, formatted);
        } else {
            WriteObject(formatted);
        }
    }
}
