using System.IO;
using System.Management.Automation;
using PSParseHTML;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that minifies CSS content.
/// </summary>
/// <example>
/// <code>Optimize-CSS -Css $css</code>
/// </example>
[Cmdlet(VerbsCommon.Optimize, "CSS", DefaultParameterSetName = ParameterSetCss)]
[OutputType(typeof(string))]
public sealed class CmdletOptimizeCss : PSCmdlet {
    private const string ParameterSetCss = "Css";
    private const string ParameterSetPath = "Path";

    /// <summary>CSS content to optimize.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetCss, ValueFromPipeline = true)]
    public string Css { get; set; } = string.Empty;

    /// <summary>Path to a CSS file.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetPath)]
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

    /// <summary>Optional output file for the optimized CSS.</summary>
    [Parameter]
    public string? OutputFile { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        string result = ParameterSetName == ParameterSetPath
            ? HtmlOptimizer.OptimizeCssFile(FileUtilities.ResolvePath(Path))
            : HtmlOptimizer.OptimizeCss(Css);

        if (!string.IsNullOrEmpty(OutputFile)) {
            string outPath = FileUtilities.ResolvePath(OutputFile);
            File.WriteAllText(outPath, result);
        } else {
            WriteObject(result);
        }
    }
}
