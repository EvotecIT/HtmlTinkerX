using HtmlTinkerX;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Returns CSS custom property declarations.</summary>
/// <example>
///   <summary>Get theme tokens</summary>
///   <code>Get-CssVariable -Content ':root { --brand-color: #0369a1; }'</code>
/// </example>
[Cmdlet(VerbsCommon.Get, "CssVariable", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(HtmlCssVariableMatch))]
public sealed class CmdletGetCssVariable : AsyncPSCmdlet {
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

    /// <summary>Custom property name to match, such as --brand-color.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? Name { get; set; }

    /// <summary>Matches custom property names that contain the provided name.</summary>
    [Parameter]
    public SwitchParameter Contains { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string css = ParameterSetName == ParameterSetFile
            ? await HtmlUtilities.ReadFileCheckedAsync(Path.ToFullPath()).ConfigureAwait(false)
            : Content;

        WriteObject(HtmlCssQueryParser.SelectVariables(css, Name, Contains.IsPresent).ToArray(), true);
    }
}
