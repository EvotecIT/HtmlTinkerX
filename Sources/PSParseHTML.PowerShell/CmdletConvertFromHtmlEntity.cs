using HtmlTinkerX;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Decodes HTML entities in text.
/// </summary>
/// <example>
/// <code>ConvertFrom-HtmlEntity -Text 'A&amp;amp;B'</code>
/// </example>
[Cmdlet(VerbsData.ConvertFrom, "HtmlEntity")]
[OutputType(typeof(string))]
public sealed class CmdletConvertFromHtmlEntity : AsyncPSCmdlet {
    /// <summary>Text containing HTML entities to decode.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [Alias("Content", "InputObject")]
    public string Text { get; set; } = string.Empty;

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        WriteObject(HtmlEntityUtility.DeEntitize(Text));
        return Task.CompletedTask;
    }
}
