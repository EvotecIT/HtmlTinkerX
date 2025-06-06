using System.IO;
using System.Management.Automation;

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
    public string Path { get; set; } = string.Empty;

    /// <summary>Optional output file for the optimized CSS.</summary>
    [Parameter]
    public string? OutputFile { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        string cssContent = ParameterSetName == ParameterSetPath
            ? File.ReadAllText(Path)
            : Css;

        string result = HtmlOptimizer.OptimizeCss(cssContent);

        if (!string.IsNullOrEmpty(OutputFile)) {
            File.WriteAllText(OutputFile, result);
        } else {
            WriteObject(result);
        }
    }
}
