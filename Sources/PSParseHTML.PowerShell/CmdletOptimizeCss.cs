using System.IO;
using System.Management.Automation;
using System.Threading.Tasks;
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
public sealed class CmdletOptimizeCss : AsyncPSCmdlet {
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
    protected override async Task ProcessRecordAsync() {
        string result = ParameterSetName == ParameterSetPath
            ? await HtmlOptimizer.OptimizeCssFileAsync(HtmlUtilities.ResolvePath(Path)).ConfigureAwait(false)
            : await HtmlOptimizer.OptimizeCssAsync(Css).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(OutputFile)) {
            string outPath = HtmlUtilities.ResolvePath(OutputFile!);
#if NETSTANDARD2_0 || NETFRAMEWORK
            File.WriteAllText(outPath, result);
#else
            await File.WriteAllTextAsync(outPath, result, CancelToken).ConfigureAwait(false);
#endif
        } else {
            WriteObject(result);
        }
    }
}
