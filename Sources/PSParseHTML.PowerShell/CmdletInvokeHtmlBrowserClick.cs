using HtmlTinkerX;
using Microsoft.Playwright;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that performs a mouse click on an element.
/// </summary>
[Cmdlet(VerbsLifecycle.Invoke, "HtmlBrowserClick")]
[OutputType(typeof(HtmlBrowserSession))]
[Alias("Invoke-HtmlClick")]
public sealed class CmdletInvokeHtmlBrowserClick : AsyncPSCmdlet {
    /// <summary>Existing browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>CSS selector of the element to click.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string Selector { get; set; } = string.Empty;

    /// <summary>Mouse button to use.</summary>
    [Parameter]
    public MouseButton Button { get; set; } = MouseButton.Left;

    /// <summary>Number of clicks.</summary>
    [Parameter]
    public int ClickCount { get; set; } = 1;

    /// <summary>Keyboard modifiers.</summary>
    [Parameter]
    public KeyboardModifier[]? Modifier { get; set; }

    /// <summary>Timeout in milliseconds.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    /// <summary>Return the session object.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        if (ClickCount < 1) {
            ThrowTerminatingError(new ErrorRecord(
                new PSArgumentOutOfRangeException(nameof(ClickCount), ClickCount, "ClickCount must be positive."),
                "ClickCountOutOfRange",
                ErrorCategory.InvalidArgument,
                ClickCount));
            return;
        }
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");

        await HtmlBrowser.MouseClickAsync(session, Selector, Button, ClickCount, Modifier, Timeout).ConfigureAwait(false);

        if (PassThru.IsPresent) {
            WriteObject(session);
        }
    }
}
