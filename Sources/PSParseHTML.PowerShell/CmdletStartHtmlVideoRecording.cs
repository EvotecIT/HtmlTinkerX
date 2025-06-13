using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

[Cmdlet(VerbsLifecycle.Start, "HTMLVideoRecording", DefaultParameterSetName = ParameterSetSession)]
[OutputType(typeof(HtmlBrowserSession))]
public sealed class CmdletStartHtmlVideoRecording : AsyncPSCmdlet {
    private const string ParameterSetUrl = "Url";
    private const string ParameterSetFile = "File";
    private const string ParameterSetSession = "Session";

    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetUrl)]
    public string Url { get; set; } = string.Empty;

    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string? Path { get; set; }

    [Parameter(Position = 0, ParameterSetName = ParameterSetSession, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    [Parameter(Mandatory = true)]
    [ValidateScript({
        if ([System.IO.Path]::GetExtension($_) -ne '.webm') {
            throw [System.ArgumentException] 'Only .webm files are supported.'
        }
        $true
    })]
    public string OutFile { get; set; } = string.Empty;

    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public HtmlBrowserEngine Browser { get; set; } = HtmlBrowserEngine.Chromium;

    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public SwitchParameter Clean { get; set; }

    [Parameter]
    public SwitchParameter Visible { get; set; }

    [Parameter]
    [ValidateRange(0,int.MaxValue)]
    public int SlowMo { get; set; } = 0;

    [Parameter]
    [ValidateRange(1,int.MaxValue)]
    public int Width { get; set; } = 800;

    [Parameter]
    [ValidateRange(1,int.MaxValue)]
    public int Height { get; set; } = 600;

    [Parameter]
    public SwitchParameter NoDefault { get; set; }

    protected override async Task ProcessRecordAsync() {
        string target;
        HtmlBrowserEngine engine = Browser;
        bool clean = Clean.IsPresent;
        bool headless = !Visible.IsPresent;

        switch (ParameterSetName) {
            case ParameterSetSession:
                Session ??= (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
                    ?? throw new PSInvalidOperationException("No session provided and no default session found.");
                target = Session.Page.Url;
                engine = Session.Browser.BrowserType.Name switch {
                    "firefox" => HtmlBrowserEngine.Firefox,
                    "webkit" => HtmlBrowserEngine.Webkit,
                    _ => HtmlBrowserEngine.Chromium
                };
                break;
            case ParameterSetFile:
                target = new System.Uri(HtmlUtilities.ResolvePath(Path!)).AbsoluteUri;
                break;
            default:
                target = Url;
                break;
        }

        HtmlBrowserSession sess = await HtmlBrowser.StartVideoRecordingAsync(
            target,
            OutFile,
            engine,
            clean,
            null,
            null,
            null,
            headless,
            SlowMo,
            Width,
            Height).ConfigureAwait(false);

        if (!NoDefault.IsPresent) {
            SessionState.PSVariable.Set("PSParseHTML_DefaultSession", sess);
        }

        WriteObject(sess);
    }
}
