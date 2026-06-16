using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Sets or removes a localStorage or sessionStorage item in an active browser session.
/// </summary>
/// <example>
///   <summary>Set a localStorage value before navigating an app</summary>
///   <code>
/// $session = Start-HtmlSession -Url https://example.org/app -Session
/// Set-HtmlBrowserStorage -Session $session -Scope Local -Key featureFlag -Value enabled
///   </code>
/// </example>
[Cmdlet(VerbsCommon.Set, "HtmlBrowserStorage")]
[OutputType(typeof(HtmlBrowserSession))]
[Alias("Set-HtmlStorage")]
public sealed class CmdletSetHtmlBrowserStorage : AsyncPSCmdlet {
    /// <summary>Existing browser session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Storage scope to update.</summary>
    [Parameter(Mandatory = true)]
    [ValidateSet("Local", "Session")]
    public string Scope { get; set; } = "Local";

    /// <summary>Storage key to set or remove.</summary>
    [Parameter(Mandatory = true)]
    public string Key { get; set; } = string.Empty;

    /// <summary>Storage value to set.</summary>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>Remove the storage key instead of setting it.</summary>
    [Parameter]
    public SwitchParameter Remove { get; set; }

    /// <summary>Return the session object.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        await HtmlBrowser.SetStorageAsync(session, Scope, Key, Value, Remove.IsPresent, linkedCts.Token).ConfigureAwait(false);
        if (PassThru.IsPresent) {
            WriteObject(session);
        }
    }
}
