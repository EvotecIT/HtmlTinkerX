using HtmlTinkerX;
using System;
using System.IO;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that formats CSS content using AngleSharp.
/// </summary>
/// <example>
/// <code>Format-CSS -Content "body{margin:0}"</code>
/// </example>
[Cmdlet(VerbsCommon.Format, "CSS", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(string))]
public sealed class CmdletFormatCss : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";

    /// <summary>Path to a CSS file to format.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

    /// <summary>CSS content to format.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Optional path to write the formatted CSS.</summary>
    [Parameter]
    public string? OutputFile { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string formatted = ParameterSetName == ParameterSetFile
            ? await HtmlFormatter.FormatCssFileAsync(Path.ToFullPath()).ConfigureAwait(false)
            : await HtmlFormatter.FormatCssAsync(Content).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(OutputFile)) {
            string outPath = OutputFile!.ToFullPath();
#if NETSTANDARD2_0 || NETFRAMEWORK
            System.IO.File.WriteAllText(outPath, formatted);
#else
            await System.IO.File.WriteAllTextAsync(outPath, formatted, CancelToken).ConfigureAwait(false);
#endif
        } else {
            WriteObject(formatted);
        }
    }
}