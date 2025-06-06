using System.Management.Automation;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that formats HTML markup using NUglify.
/// </summary>
/// <example>
/// <code>Format-HTML -Content '&lt;div&gt;test&lt;/div&gt;'</code>
/// </example>
[Cmdlet(VerbsCommon.Format, "HTML", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(string))]
public sealed class CmdletFormatHtml : PSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";

    /// <summary>Path to an HTML file.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("Path")]
    public string File { get; set; } = string.Empty;

    /// <summary>HTML content to format.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Optional path to write the formatted HTML.</summary>
    [Parameter]
    public string? OutputFile { get; set; }

    /// <summary>Indentation string.</summary>
    [Parameter]
    public string Indent { get; set; } = "    ";

    /// <summary>Determines how blocks start.</summary>
    [Parameter]
    public NUglify.BlockStart BlockStartLine { get; set; } = NUglify.BlockStart.SameLine;

    /// <summary>Remove HTML comments.</summary>
    [Parameter]
    public SwitchParameter RemoveHTMLComments { get; set; }

    /// <summary>Remove optional tags.</summary>
    [Parameter]
    public SwitchParameter RemoveOptionalTags { get; set; }

    /// <summary>Output text nodes on new line.</summary>
    [Parameter]
    public SwitchParameter OutputTextNodesOnNewLine { get; set; }

    /// <summary>Remove empty attributes.</summary>
    [Parameter]
    public SwitchParameter RemoveEmptyAttributes { get; set; }

    /// <summary>Alphabetically order attributes.</summary>
    [Parameter]
    public SwitchParameter AlphabeticallyOrderAttributes { get; set; }

    /// <summary>Remove empty CSS blocks.</summary>
    [Parameter]
    public SwitchParameter RemoveEmptyBlocks { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        string html = ParameterSetName == ParameterSetFile ? System.IO.File.ReadAllText(File) : Content;
        string result = HtmlFormatter.FormatHtml(
            html,
            Indent,
            BlockStartLine,
            RemoveHTMLComments.IsPresent,
            RemoveOptionalTags.IsPresent,
            OutputTextNodesOnNewLine.IsPresent,
            RemoveEmptyAttributes.IsPresent,
            AlphabeticallyOrderAttributes.IsPresent,
            RemoveEmptyBlocks.IsPresent);

        if (!string.IsNullOrEmpty(OutputFile)) {
            System.IO.File.WriteAllText(OutputFile, result);
        } else {
            WriteObject(result);
        }
    }
}

