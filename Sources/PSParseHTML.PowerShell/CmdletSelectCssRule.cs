using HtmlTinkerX;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Selects CSS style rules by selector.</summary>
/// <example>
///   <summary>Select a button rule</summary>
///   <code>Select-CssRule -Content '.btn { color: red; }' -Selector '.btn'</code>
/// </example>
[Cmdlet(VerbsCommon.Select, "CssRule", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(HtmlCssRuleMatch))]
public sealed class CmdletSelectCssRule : AsyncPSCmdlet {
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

    /// <summary>Selector to match.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? Selector { get; set; }

    /// <summary>Matches selector text that contains the provided selector text.</summary>
    [Parameter]
    public SwitchParameter Contains { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string css = ParameterSetName == ParameterSetFile
            ? await HtmlUtilities.ReadFileCheckedAsync(Path.ToFullPath()).ConfigureAwait(false)
            : Content;

        WriteObject(HtmlCssQueryParser.SelectRules(css, Selector, Contains.IsPresent).ToArray(), true);
    }
}
