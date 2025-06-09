using System.Management.Automation;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that navigates an existing browser session to a new URL.
/// </summary>
[Cmdlet(VerbsLifecycle.Invoke, "HTMLNavigation", DefaultParameterSetName = ParameterSetUrl)]
[OutputType(typeof(BrowserSession))]
public sealed class CmdletInvokeHtmlNavigation : AsyncPSCmdlet {
    private const string ParameterSetUrl = "ByUrl";
    private const string ParameterSetText = "ByText";
    private const string ParameterSetSelector = "BySelector";

    /// <summary>Existing browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public BrowserSession? Session { get; set; }

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

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        BrowserSession session = Session ?? (BrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");

        switch (ParameterSetName) {
            case ParameterSetUrl:
                await session.Page.GotoAsync(Url!).ConfigureAwait(false);
                await session.Page.WaitForLoadStateAsync(LoadState.NetworkIdle).ConfigureAwait(false);
                break;
            case ParameterSetSelector:
                try {
                    if (WaitForNavigation.IsPresent) {
                        await session.Page.RunAndWaitForNavigationAsync(() => session.Page.ClickAsync(Selector!)).ConfigureAwait(false);
                    } else {
                        await session.Page.ClickAsync(Selector!).ConfigureAwait(false);
                    }
                } catch (PlaywrightException ex) when (ex.Message.Contains("strict mode violation")) {
                    HandleStrictMode(ex, Selector!);
                    return;
                }
                break;
            case ParameterSetText:
                ILocator locator;
                if (!string.IsNullOrEmpty(Regex)) {
                    locator = session.Page.GetByText(new Regex(Regex));
                } else if (Exact.IsPresent) {
                    locator = session.Page.GetByText(Text!, new PageGetByTextOptions { Exact = true });
                } else {
                    locator = session.Page.GetByText(Text!);
                }

                try {
                    if (WaitForNavigation.IsPresent) {
                        await session.Page.RunAndWaitForNavigationAsync(() => locator.ClickAsync()).ConfigureAwait(false);
                    } else {
                        await locator.ClickAsync().ConfigureAwait(false);
                    }
                } catch (PlaywrightException ex) when (ex.Message.Contains("strict mode violation")) {
                    HandleStrictMode(ex, Text!);
                    return;
                }
                break;
        }

        if (PassThru.IsPresent) {
            WriteObject(session);
        }
    }

    private void HandleStrictMode(PlaywrightException ex, string query) {
        string text = ex.Message;
        int start = text.IndexOf("strict mode violation:", System.StringComparison.Ordinal);
        if (start >= 0) {
            text = text.Substring(start + "strict mode violation:".Length).Trim();
        }
        int idx = text.IndexOf("Call log:", System.StringComparison.Ordinal);
        if (idx > 0) {
            text = text.Substring(0, idx).TrimEnd();
        }
        string[] parts = text.Replace("  ", " ").Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        string message = $"Strict mode violation for '{query}':" + System.Environment.NewLine + string.Join(System.Environment.NewLine, parts);
        WriteError(new ErrorRecord(
            new InvalidOperationException(message),
            "StrictModeViolation",
            ErrorCategory.InvalidOperation,
            query));
    }
}

