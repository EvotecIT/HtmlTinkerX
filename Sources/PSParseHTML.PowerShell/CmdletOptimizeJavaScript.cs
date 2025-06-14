using System.IO;
using System.Management.Automation;
using PSParseHTML;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that minifies JavaScript code.
/// </summary>
/// <example>
/// <code>Optimize-JavaScript -Content $js</code>
/// </example>
[Cmdlet(VerbsCommon.Optimize, "JavaScript", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(string))]
public sealed class CmdletOptimizeJavaScript : PSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";

    /// <summary>Path to a JavaScript file to optimize.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

    /// <summary>JavaScript content to optimize.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Optional file path to write optimized output.</summary>
    [Parameter]
    public string? OutputFile { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        string optimized = ParameterSetName == ParameterSetFile
            ? HtmlOptimizer.OptimizeJavaScriptFile(HtmlUtilities.ResolvePath(Path))
            : HtmlOptimizer.OptimizeJavaScript(Content);

        if (!string.IsNullOrEmpty(OutputFile)) {
            string outPath = HtmlUtilities.ResolvePath(OutputFile!);
            File.WriteAllText(outPath, optimized);
        } else {
            WriteObject(optimized);
        }
    }
}
