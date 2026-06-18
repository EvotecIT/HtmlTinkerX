using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Creates a reusable browser profile for rendered web automation sessions.
/// </summary>
[Cmdlet(VerbsCommon.New, "HtmlBrowserProfile")]
[OutputType(typeof(HtmlBrowserProfile))]
public sealed class CmdletNewHtmlBrowserProfile : AsyncPSCmdlet {
    /// <summary>Friendly profile name.</summary>
    [Parameter(Position = 0)]
    public string? Name { get; set; }

    /// <summary>Optional path where the profile JSON should be saved.</summary>
    [Parameter]
    public string? Path { get; set; }

    /// <summary>Browser engine to use.</summary>
    [Parameter]
    public HtmlBrowserEngine Browser { get; set; } = HtmlBrowserEngine.Chromium;

    /// <summary>Intent-focused browser automation defaults to apply before explicit profile values.</summary>
    [Parameter]
    public HtmlBrowserScenario Scenario { get; set; } = HtmlBrowserScenario.Custom;

    /// <summary>Persistent user-data directory for cookies, storage, cache, and permissions.</summary>
    [Parameter]
    public string? UserDataDirectory { get; set; }

    /// <summary>Browser distribution channel, such as chrome, msedge, chromium, chrome-beta, or msedge-dev.</summary>
    [Parameter]
    public string? BrowserChannel { get; set; }

    /// <summary>Path to a browser executable.</summary>
    [Parameter]
    public string? BrowserExecutablePath { get; set; }

    /// <summary>Chrome DevTools Protocol endpoint URL for attaching to an already-running Chromium browser.</summary>
    [Parameter]
    [Alias("CdpEndpoint", "RemoteDebuggingUrl")]
    public string? CdpEndpointUrl { get; set; }

    /// <summary>Locale used by the browser context.</summary>
    [Parameter]
    public string? Locale { get; set; }

    /// <summary>Timezone identifier used by the browser JavaScript runtime.</summary>
    [Parameter]
    public string? Timezone { get; set; }

    /// <summary>Viewport width in pixels.</summary>
    [Parameter]
    public int? ViewportWidth { get; set; }

    /// <summary>Viewport height in pixels.</summary>
    [Parameter]
    public int? ViewportHeight { get; set; }

    /// <summary>Screen width in pixels.</summary>
    [Parameter]
    public int? ScreenWidth { get; set; }

    /// <summary>Screen height in pixels.</summary>
    [Parameter]
    public int? ScreenHeight { get; set; }

    /// <summary>User agent string used by the browser context.</summary>
    [Parameter]
    public string? UserAgent { get; set; }

    /// <summary>Initial browser navigation readiness state.</summary>
    [Parameter]
    public HtmlBrowserLoadState LoadState { get; set; } = HtmlBrowserLoadState.NetworkIdle;

    /// <summary>Navigation and selector timeout in milliseconds.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int? Timeout { get; set; }

    /// <summary>Prevent recognized SSO handoff forms from auto-submitting so their fields can be inspected.</summary>
    [Parameter]
    public SwitchParameter PreventSsoAutoSubmit { get; set; }

    /// <summary>Additional browser command-line arguments.</summary>
    [Parameter]
    public string[] BrowserArgument { get; set; } = System.Array.Empty<string>();

    /// <summary>Browser permissions granted to pages in the context.</summary>
    [Parameter]
    public string[] Permission { get; set; } = System.Array.Empty<string>();

    /// <summary>Browser resource types to abort before navigation, such as Image, Media, Font, or Stylesheet.</summary>
    [Parameter]
    public HtmlNetworkResourceType[] BlockResourceType { get; set; } = System.Array.Empty<HtmlNetworkResourceType>();

    /// <summary>Playwright URL glob patterns to abort before navigation, such as **/analytics/**.</summary>
    [Parameter]
    public string[] BlockResourcePattern { get; set; } = System.Array.Empty<string>();

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        if (System.Array.IndexOf(BlockResourceType, HtmlNetworkResourceType.Document) >= 0) {
            throw new PSArgumentException("BlockResourceType Document would abort page navigation. Block subresources such as Image, Media, Font, Stylesheet, Script, XHR, or Fetch instead.");
        }

        HtmlBrowserProfile profile = new() {
            Name = Name,
            Browser = MyInvocation.BoundParameters.ContainsKey(nameof(Browser)) ? Browser : null,
            Scenario = MyInvocation.BoundParameters.ContainsKey(nameof(Scenario)) ? Scenario : null,
            UserDataDirectory = UserDataDirectory?.ToFullPath(),
            BrowserChannel = BrowserChannel,
            BrowserExecutablePath = BrowserExecutablePath?.ToFullPath(),
            CdpEndpointUrl = CdpEndpointUrl,
            Locale = Locale,
            Timezone = Timezone,
            ViewportWidth = ViewportWidth,
            ViewportHeight = ViewportHeight,
            ScreenWidth = ScreenWidth,
            ScreenHeight = ScreenHeight,
            UserAgent = UserAgent,
            LoadState = MyInvocation.BoundParameters.ContainsKey(nameof(LoadState)) ? LoadState : null,
            Timeout = Timeout,
            PreventSsoAutoSubmit = MyInvocation.BoundParameters.ContainsKey(nameof(PreventSsoAutoSubmit)) ? PreventSsoAutoSubmit.IsPresent : null
        };

        profile.BrowserArguments.AddRange(BrowserArgument);
        profile.Permissions.AddRange(Permission);
        profile.BlockResourceTypes.AddRange(BlockResourceType);
        profile.BlockResourcePatterns.AddRange(BlockResourcePattern);

        if (!string.IsNullOrWhiteSpace(Path)) {
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
            await profile.SaveAsync(Path!, linkedCts.Token).ConfigureAwait(false);
        }

        WriteObject(profile);
    }
}
