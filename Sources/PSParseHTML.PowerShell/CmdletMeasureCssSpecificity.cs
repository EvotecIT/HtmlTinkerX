using HtmlTinkerX;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Measures CSS selector specificity.</summary>
/// <example>
///   <summary>Measure selectors</summary>
///   <code>'.btn', '#app .btn:hover' | Measure-CssSpecificity</code>
/// </example>
[Cmdlet(VerbsDiagnostic.Measure, "CssSpecificity")]
[OutputType(typeof(HtmlCssSpecificity))]
public sealed class CmdletMeasureCssSpecificity : AsyncPSCmdlet {
    /// <summary>Selectors to measure.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    public string[] Selector { get; set; } = Array.Empty<string>();

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        WriteObject(HtmlCssQueryParser.MeasureSpecificity(Selector).ToArray(), true);
        return Task.CompletedTask;
    }
}
