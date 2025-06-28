using System.Management.Automation;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Playwright;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that navigates an existing browser session to a new URL.
/// </summary>
[Cmdlet(VerbsLifecycle.Invoke, "HTMLNavigation", DefaultParameterSetName = ParameterSetUrl)]
[OutputType(typeof(HtmlBrowserSession))]
public sealed class CmdletInvokeHtmlNavigation : AsyncPSCmdlet {
    private const string ParameterSetUrl = "ByUrl";
    private const string ParameterSetText = "ByText";
    private const string ParameterSetSelector = "BySelector";

    /// <summary>Existing browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Destination URL.</summary>
    [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetUrl)]
    public string? Url { get; set; }

    /// <summary>Text of the element to click.</summary>
    [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetText)]
    public string? Text { get; set; }

    /// <summary>CSS selector of the element to click.</summary>
    [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetSelector)]
    public string? Selector { get; set; }

    /// <summary>Use exact text match.</summary>
    [Parameter(ParameterSetName = ParameterSetText)]
    public SwitchParameter Exact { get; set; }

    /// <summary>Regular expression for text match.</summary>
    [Parameter(ParameterSetName = ParameterSetText)]
    public string? Regex { get; set; }

    /// <summary>Wait for navigation event after clicking.</summary>
    [Parameter(ParameterSetName = ParameterSetText)]
    [Parameter(ParameterSetName = ParameterSetSelector)]
    public SwitchParameter WaitForNavigation { get; set; }

    /// <summary>Return the session object.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }
    /// <summary>Timeout in milliseconds for navigation and clicks.</summary>
    [Parameter]
    [ValidateRange(0,int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;

        try {
            switch (ParameterSetName) {
                case ParameterSetUrl:
                    await HtmlBrowser.NavigateAsync(session, Url!, Timeout, token).ConfigureAwait(false);
                    break;
                case ParameterSetSelector:
                    await HtmlBrowser.ClickSelectorAsync(session, Selector!, WaitForNavigation.IsPresent, Timeout, token).ConfigureAwait(false);
                    break;
                case ParameterSetText:
                    await HtmlBrowser.ClickTextAsync(session, Text!, Exact.IsPresent, Regex, WaitForNavigation.IsPresent, Timeout, token).ConfigureAwait(false);
                    break;
            }
        } catch (PlaywrightException ex) when (ex.Message.Contains("strict mode violation")) {
            string query = ParameterSetName switch {
                ParameterSetSelector => Selector!,
                _ => Text!
            };
            string message = HtmlBrowser.FormatStrictModeMessage(query, ex);
            WriteError(new ErrorRecord(new InvalidOperationException(message), "StrictModeViolation", ErrorCategory.InvalidOperation, query));
            return;
        }

        if (PassThru.IsPresent) {
            WriteObject(session);
        }
    }

}

