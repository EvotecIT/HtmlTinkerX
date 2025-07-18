using HtmlTinkerX;
using System.IO;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that minifies JavaScript code.
/// </summary>
/// <example>
/// <code>Optimize-JavaScript -Content $js</code>
/// </example>
[Cmdlet(VerbsCommon.Optimize, "JavaScript", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(string))]
public sealed class CmdletOptimizeJavaScript : AsyncPSCmdlet {
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
    protected override async Task ProcessRecordAsync() {
        string optimized = ParameterSetName == ParameterSetFile
            ? await HtmlOptimizer.OptimizeJavaScriptFileAsync(HtmlUtilities.ResolvePath(Path)).ConfigureAwait(false)
            : await HtmlOptimizer.OptimizeJavaScriptAsync(Content).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(OutputFile)) {
            string outPath = HtmlUtilities.ResolvePath(OutputFile!);
#if NETSTANDARD2_0 || NETFRAMEWORK
            File.WriteAllText(outPath, optimized);
#else
            await File.WriteAllTextAsync(outPath, optimized, CancelToken).ConfigureAwait(false);
#endif
        } else {
            WriteObject(optimized);
        }
    }
}