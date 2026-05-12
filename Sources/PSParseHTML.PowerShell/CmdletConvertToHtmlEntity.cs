using HtmlTinkerX;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Encodes text as HTML entities.
/// </summary>
/// <example>
/// <code>ConvertTo-HtmlEntity -Text 'A&amp;B'</code>
/// </example>
[Cmdlet(VerbsData.ConvertTo, "HtmlEntity")]
[OutputType(typeof(string))]
public sealed class CmdletConvertToHtmlEntity : AsyncPSCmdlet {
    /// <summary>Text to encode as HTML entities.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [Alias("Content", "InputObject")]
    public string Text { get; set; } = string.Empty;

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        WriteObject(HtmlEntityUtility.Entitize(Text));
        return Task.CompletedTask;
    }
}
