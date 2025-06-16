using System.Management.Automation;
using System.Threading.Tasks;
using PSParseHTML;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that formats JavaScript code using JsBeautifier.
/// </summary>
[Cmdlet(VerbsCommon.Format, "JavaScript", DefaultParameterSetName = ParameterSetContent)]
[Alias("Format-JS")]
[OutputType(typeof(string))]
public sealed class CmdletFormatJavaScript : AsyncPSCmdlet {
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
    protected override async Task ProcessRecordAsync() {
        string formatted = ParameterSetName == ParameterSetFile
            ? await HtmlFormatter.FormatJavaScriptFileAsync(HtmlUtilities.ResolvePath(Path)).ConfigureAwait(false)
            : await HtmlFormatter.FormatJavaScriptAsync(Content).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(OutputFile)) {
            string outPath = HtmlUtilities.ResolvePath(OutputFile!);
#if NETSTANDARD2_0 || FRAMEWORK
            System.IO.File.WriteAllText(outPath, formatted);
#else
            await System.IO.File.WriteAllTextAsync(outPath, formatted, CancelToken).ConfigureAwait(false);
#endif
        } else {
            WriteObject(formatted);
        }
    }
}
