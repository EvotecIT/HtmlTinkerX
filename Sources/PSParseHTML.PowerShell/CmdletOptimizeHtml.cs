using HtmlTinkerX;
using System;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that minifies HTML content using NUglify.
/// </summary>
/// <example>
/// <code>Optimize-HTML -Content $html</code>
/// </example>
[Cmdlet(VerbsCommon.Optimize, "HTML", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(string))]
public sealed class CmdletOptimizeHtml : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";

    /// <summary>HTML content to optimize.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Path to a HTML file.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

    /// <summary>Optional path to write the optimized HTML.</summary>
    [Parameter]
    public string? OutputFile { get; set; }

    /// <summary>Decode CSS escape sequences while minifying.</summary>
    [Parameter]
    public SwitchParameter CSSDecodeEscapes { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string result = ParameterSetName == ParameterSetFile
            ? await HtmlOptimizer.OptimizeHtmlFileAsync(HtmlUtilities.ResolvePath(Path), CSSDecodeEscapes.IsPresent).ConfigureAwait(false)
            : await HtmlOptimizer.OptimizeHtmlAsync(Content, CSSDecodeEscapes.IsPresent).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(OutputFile)) {
            string outPath = HtmlUtilities.ResolvePath(OutputFile!);
#if NETSTANDARD2_0 || NETFRAMEWORK
            System.IO.File.WriteAllText(outPath, result);
#else
            await System.IO.File.WriteAllTextAsync(outPath, result, CancelToken).ConfigureAwait(false);
#endif
        } else {
            WriteObject(result);
        }
    }
}