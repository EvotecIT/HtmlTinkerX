using HtmlTinkerX;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that runs arbitrary JavaScript in an existing browser session.
/// </summary>
[Cmdlet(VerbsLifecycle.Invoke, "HtmlBrowserScript")]
[OutputType(typeof(object))]
[Alias("Invoke-HTMLScript")]
public sealed class CmdletInvokeHtmlBrowserScript : AsyncPSCmdlet {
    /// <summary>Browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>JavaScript code to execute.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string Script { get; set; } = string.Empty;

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");

        object? result = await HtmlBrowser.EvaluateAsync<object>(session, Script).ConfigureAwait(false);
        WriteObject(result);
    }
}