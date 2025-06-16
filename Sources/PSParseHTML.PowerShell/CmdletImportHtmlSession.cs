using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that restores a browser session from a previously exported state file.
/// </summary>
[Cmdlet(VerbsData.Import, "HTMLSession")]
[OutputType(typeof(HtmlBrowserSession))]
public sealed class CmdletImportHtmlSession : AsyncPSCmdlet {
    /// <summary>Path to the saved session state.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    public string Path { get; set; } = string.Empty;

    /// <summary>URL to navigate to.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string Url { get; set; } = string.Empty;

    [Parameter]
    public HtmlBrowserEngine Browser { get; set; } = HtmlBrowserEngine.Chromium;

    [Parameter]
    public SwitchParameter Clean { get; set; }

    [Parameter]
    public SwitchParameter Visible { get; set; }

    [Parameter]
    [ValidateRange(0,int.MaxValue)]
    public int SlowMo { get; set; } = 0;

    [Parameter]
    public string? UserAgent { get; set; }

    [Parameter]
    [ValidateRange(1,int.MaxValue)]
    public int? ViewportWidth { get; set; }

    [Parameter]
    [ValidateRange(1,int.MaxValue)]
    public int? ViewportHeight { get; set; }

    [Parameter]
    public double? DeviceScaleFactor { get; set; }

    [Parameter]
    public double? GeoLatitude { get; set; }

    [Parameter]
    public double? GeoLongitude { get; set; }

    [Parameter]
    public string? Timezone { get; set; }

    [Parameter]
    public SwitchParameter NoDefault { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
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
            timezone: Timezone).ConfigureAwait(false);

        if (!NoDefault.IsPresent) {
            SessionState.PSVariable.Set("PSParseHTML_DefaultSession", session);
        }
        WriteObject(session);
    }
}
