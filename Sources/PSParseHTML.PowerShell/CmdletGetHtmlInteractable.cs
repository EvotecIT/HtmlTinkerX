using System.Collections.Generic;
using System.Management.Automation;
using System.Threading.Tasks;
using PSParseHTML;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that lists clickable or interactable elements from a browser session.
/// </summary>
[Cmdlet(VerbsCommon.Get, "HTMLInteractable", DefaultParameterSetName = ParameterSetSession)]
[OutputType(typeof(InteractableElement))]
public sealed class CmdletGetHtmlInteractable : AsyncPSCmdlet {
    private const string ParameterSetSession = "Session";
    private const string ParameterSetUrl = "Url";
    private const string ParameterSetFile = "File";

    /// <summary>Existing browser session.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetSession, ValueFromPipeline = true)]
    public BrowserSession Session { get; set; } = null!;

    /// <summary>URL of the page to inspect.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetUrl)]
    public string Url { get; set; } = string.Empty;

    /// <summary>Path to a local HTML file.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetFile)]
    [Alias("Path")]
    public string File { get; set; } = string.Empty;

    /// <summary>Browser engine to use when loading <see cref="Url"/> or <see cref="File"/>.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public BrowserEngine Browser { get; set; } = BrowserEngine.Chromium;

    /// <summary>Reinstall browser runtimes when using <see cref="Url"/> or <see cref="File"/>.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public SwitchParameter Clean { get; set; }

    /// <summary>Include elements hidden from view.</summary>
    [Parameter]
    public SwitchParameter IncludeHidden { get; set; }

    /// <summary>Maximum number of elements to return.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int Limit { get; set; } = 100;

    /// <summary>Credentials for pages requiring authentication.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public PSCredential? Credential { get; set; }

    /// <summary>Basic authentication username.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? Username { get; set; }

    /// <summary>Basic authentication password.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? Password { get; set; }

    /// <summary>URL of a login form.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? LoginUrl { get; set; }

    /// <summary>CSS selector for the username field.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? UsernameSelector { get; set; }

    /// <summary>CSS selector for the password field.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? PasswordSelector { get; set; }

    /// <summary>CSS selector for the submit element.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? SubmitSelector { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        List<InteractableElement> list;
        switch (ParameterSetName) {
            case ParameterSetUrl:
                string? user = Credential?.UserName ?? Username;
                string? pass = Credential?.GetNetworkCredential().Password ?? Password;
                FormLoginOptions? form = null;
                if (!string.IsNullOrEmpty(LoginUrl) &&
                    !string.IsNullOrEmpty(UsernameSelector) &&
                    !string.IsNullOrEmpty(PasswordSelector) &&
                    !string.IsNullOrEmpty(SubmitSelector)) {
                    form = new FormLoginOptions {
                        LoginUrl = LoginUrl!,
                        UsernameSelector = UsernameSelector!,
                        PasswordSelector = PasswordSelector!,
                        SubmitSelector = SubmitSelector!
                    };
                }

                list = await HtmlBrowserRenderer.GetInteractableElementsAsync(
                    Url,
                    Browser,
                    Clean.IsPresent,
                    IncludeHidden.IsPresent,
                    Limit,
                    user,
                    pass,
                    form).ConfigureAwait(false);
                break;
            case ParameterSetFile:
                list = await HtmlBrowserRenderer.GetInteractableElementsFromFileAsync(
                    File,
                    Browser,
                    Clean.IsPresent,
                    IncludeHidden.IsPresent,
                    Limit).ConfigureAwait(false);
                break;
            default:
                list = await HtmlBrowserRenderer.GetInteractableElementsAsync(
                    Session.Page,
                    IncludeHidden.IsPresent,
                    Limit).ConfigureAwait(false);
                break;
        }

        foreach (InteractableElement element in list) {
            WriteObject(element);
        }
    }
}
