using System.Management.Automation;
using System.Threading.Tasks;
using PSParseHTML;
using Jsbeautifier;

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

    /// <summary>Number of spaces for indentation.</summary>
    [Parameter]
    public uint IndentSize { get; set; } = 4;

    /// <summary>Indentation character.</summary>
    [Parameter]
    public char IndentChar { get; set; } = ' ';

    /// <summary>Use tabs for indentation.</summary>
    [Parameter]
    public bool IndentWithTabs { get; set; }

    /// <summary>Preserve existing newlines.</summary>
    [Parameter]
    public bool PreserveNewlines { get; set; } = true;

    /// <summary>Maximum number of consecutive newlines to preserve.</summary>
    [Parameter]
    public float MaxPreserveNewlines { get; set; } = 10f;

    /// <summary>Enable jslint-happy formatting.</summary>
    [Parameter]
    public bool JslintHappy { get; set; }

    /// <summary>Brace formatting style.</summary>
    [Parameter]
    public BraceStyle BraceStyle { get; set; } = BraceStyle.Collapse;

    /// <summary>Keep array indentation.</summary>
    [Parameter]
    public bool KeepArrayIndentation { get; set; }

    /// <summary>Keep function indentation.</summary>
    [Parameter]
    public bool KeepFunctionIndentation { get; set; }

    /// <summary>Preserve eval code.</summary>
    [Parameter]
    public bool EvalCode { get; set; }


    /// <summary>Break chained methods.</summary>
    [Parameter]
    public bool BreakChainedMethods { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        BeautifierOptions opts = new() {
            IndentSize = IndentSize,
            IndentChar = IndentChar,
            IndentWithTabs = IndentWithTabs,
            PreserveNewlines = PreserveNewlines,
            MaxPreserveNewlines = MaxPreserveNewlines,
            JslintHappy = JslintHappy,
            BraceStyle = BraceStyle,
            KeepArrayIndentation = KeepArrayIndentation,
            KeepFunctionIndentation = KeepFunctionIndentation,
            EvalCode = EvalCode,
            BreakChainedMethods = BreakChainedMethods
        };

        string formatted = ParameterSetName == ParameterSetFile
            ? await HtmlFormatter.FormatJavaScriptFileAsync(HtmlUtilities.ResolvePath(Path), opts).ConfigureAwait(false)
            : await HtmlFormatter.FormatJavaScriptAsync(Content, opts).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(OutputFile)) {
            string outPath = HtmlUtilities.ResolvePath(OutputFile!);
#if NETSTANDARD2_0 || NETFRAMEWORK
            System.IO.File.WriteAllText(outPath, formatted);
#else
            await System.IO.File.WriteAllTextAsync(outPath, formatted, CancelToken).ConfigureAwait(false);
#endif
        } else {
            WriteObject(formatted);
        }
        await Task.CompletedTask;
    }
}
