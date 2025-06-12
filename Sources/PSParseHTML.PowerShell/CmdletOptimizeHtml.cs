using System;
using System.Management.Automation;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that minifies HTML content using NUglify.
/// </summary>
/// <example>
/// <code>Optimize-HTML -Content $html</code>
/// </example>
[Cmdlet(VerbsCommon.Optimize, "HTML", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(string))]
public sealed class CmdletOptimizeHtml : PSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";

    /// <summary>HTML content to optimize.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Path to a HTML file.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("Path")]
    public string File { get; set; } = string.Empty;

    /// <summary>Optional path to write the optimized HTML.</summary>
    [Parameter]
    public string? OutputFile { get; set; }

    /// <summary>Decode CSS escape sequences while minifying.</summary>
    [Parameter]
    public SwitchParameter CSSDecodeEscapes { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        string result = ParameterSetName == ParameterSetFile
            ? HtmlOptimizer.OptimizeHtmlFile(File, CSSDecodeEscapes.IsPresent)
            : HtmlOptimizer.OptimizeHtml(Content, CSSDecodeEscapes.IsPresent);
        if (!string.IsNullOrEmpty(OutputFile)) {
            System.IO.File.WriteAllText(OutputFile, result);
        } else {
            WriteObject(result);
        }
    }
}
