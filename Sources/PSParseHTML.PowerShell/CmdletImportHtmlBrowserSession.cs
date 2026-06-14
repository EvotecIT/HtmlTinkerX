using HtmlTinkerX;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that restores a browser session from a previously exported state file.
/// </summary>
[Cmdlet(VerbsData.Import, "HtmlBrowserSession")]
[OutputType(typeof(HtmlBrowserSession))]
[Alias("Import-HtmlSession")]
public sealed class CmdletImportHtmlBrowserSession : AsyncPSCmdlet {
    /// <summary>Path to the saved session state.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    public string Path { get; set; } = string.Empty;

    /// <summary>URL to navigate to.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string Url { get; set; } = string.Empty;

    /// <summary>Browser engine used for launching the session.</summary>
    [Parameter]
    public HtmlBrowserEngine Browser { get; set; } = HtmlBrowserEngine.Chromium;

    /// <summary>Reinstall browser runtimes before starting.</summary>
    [Parameter]
    public SwitchParameter Clean { get; set; }

    /// <summary>Show the browser instead of running headless.</summary>
    [Parameter]
    public SwitchParameter Visible { get; set; }

    /// <summary>Delay in milliseconds between actions.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int SlowMo { get; set; } = 0;

    /// <summary>Timeout in milliseconds for browser operations.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    /// <summary>User agent string used when launching the browser.</summary>
    [Parameter]
    public string? UserAgent { get; set; }

    /// <summary>Viewport width in pixels.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int? ViewportWidth { get; set; }

    /// <summary>Viewport height in pixels.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int? ViewportHeight { get; set; }

    /// <summary>Scaling factor for high DPI devices.</summary>
    [Parameter]
    public double? DeviceScaleFactor { get; set; }

    /// <summary>Latitude used for geolocation.</summary>
    [Parameter]
    public double? GeoLatitude { get; set; }

    /// <summary>Longitude used for geolocation.</summary>
    [Parameter]
    public double? GeoLongitude { get; set; }

    /// <summary>Timezone identifier.</summary>
    [Parameter]
    public string? Timezone { get; set; }

    /// <summary>Do not store this session as the default.</summary>
    [Parameter]
    public SwitchParameter NoDefault { get; set; }

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;
        HtmlBrowserSession session = await HtmlBrowser.ImportSessionAsync(
            Url,
            Path,
            Browser,
            Clean.IsPresent,
            !Visible.IsPresent,
            SlowMo,
            UserAgent,
            ViewportWidth,
            ViewportHeight,
            (float?)DeviceScaleFactor,
            proxy: null,
            proxyUsername: null,
            proxyPassword: null,
            geoLatitude: GeoLatitude,
            geoLongitude: GeoLongitude,
            timezone: Timezone,
            timeout: Timeout,
            cancellationToken: token).ConfigureAwait(false);

        if (!NoDefault.IsPresent) {
            SessionState.PSVariable.Set("PSParseHTML_DefaultSession", session);
        }
        WriteObject(session);
    }
}
