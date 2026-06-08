using HtmlTinkerX;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Selects CSS declarations by property and optional selector.</summary>
/// <example>
///   <summary>Select color declarations</summary>
///   <code>Select-CssDeclaration -Content '.btn { color: red; margin: 0; }' -Property color</code>
/// </example>
[Cmdlet(VerbsCommon.Select, "CssDeclaration", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(HtmlCssDeclarationMatch))]
public sealed class CmdletSelectCssDeclaration : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";

    /// <summary>CSS content to inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [ValidateNotNullOrEmpty]
    public string Content { get; set; } = string.Empty;

    /// <summary>Path to a CSS file.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; } = string.Empty;

    /// <summary>CSS property to match.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? Property { get; set; }

    /// <summary>Optional selector to match.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? Selector { get; set; }

    /// <summary>Matches property or selector text that contains the provided value.</summary>
    [Parameter]
    public SwitchParameter Contains { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string css = ParameterSetName == ParameterSetFile
            ? await HtmlUtilities.ReadFileCheckedAsync(Path.ToFullPath()).ConfigureAwait(false)
            : Content;

        WriteObject(HtmlCssQueryParser.SelectDeclarations(css, Property, Selector, Contains.IsPresent).ToArray(), true);
    }
}
