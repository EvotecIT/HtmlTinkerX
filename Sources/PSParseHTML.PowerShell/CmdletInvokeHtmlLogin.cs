using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that detects a login form and fills it with provided credentials.
/// </summary>
[Cmdlet(VerbsLifecycle.Invoke, "HTMLLogin")]
[OutputType(typeof(HtmlBrowserSession))]
public sealed class CmdletInvokeHtmlLogin : AsyncPSCmdlet {
    /// <summary>Existing browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Username for the login form.</summary>
    [Parameter(Mandatory = true)]
    public string Username { get; set; } = string.Empty;

    /// <summary>Password for the login form.</summary>
    [Parameter(Mandatory = true)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Timeout in milliseconds when waiting for elements.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    /// <summary>Return the session object.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    /// <summary>Cancellation token.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");

        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;

        bool loggedIn = await HtmlBrowser.AutoLoginAsync(session, Username, Password, Timeout, token).ConfigureAwait(false);
        if (!loggedIn) {
            WriteWarning("Login form not detected on the current page.");
        }

        if (PassThru.IsPresent) {
            WriteObject(session);
        }
    }
}
