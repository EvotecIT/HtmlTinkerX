using HtmlTinkerX;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Measures page load performance for a URL or local HTML file.
/// </summary>
[Cmdlet(VerbsDiagnostic.Measure, "HTMLPerformance", DefaultParameterSetName = ParameterSetUrl)]
[OutputType(typeof(HtmlPerformanceMetrics))]
public sealed class CmdletMeasureHtmlPerformance : AsyncPSCmdlet {
    private const string ParameterSetUrl = "Url";
    private const string ParameterSetFile = "File";

    /// <summary>URL to test.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetUrl, ValueFromPipeline = true)]
    [ValidateNotNullOrEmpty]
    public string Url { get; set; } = string.Empty;

    /// <summary>Path to local HTML file.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; } = string.Empty;

    /// <summary>Browser engine to use.</summary>
    [Parameter]
    public HtmlBrowserEngine Engine { get; set; } = HtmlBrowserEngine.Chromium;

    /// <summary>Enable headless mode.</summary>
    [Parameter]
    public SwitchParameter Headless { get; set; } = true;

    /// <summary>Timeout in milliseconds.</summary>
    [Parameter]
    [ValidateRange(1000, 300000)]
    public int Timeout { get; set; } = 30000;

    /// <summary>Proxy URL.</summary>
    [Parameter]
    public string? Proxy { get; set; }

    /// <summary>Proxy credentials.</summary>
    [Parameter]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string? proxyUsername = null;
        string? proxyPassword = null;
        if (!string.IsNullOrEmpty(Proxy) && ProxyCredential != null) {
            proxyUsername = ProxyCredential.UserName;
            proxyPassword = ProxyCredential.GetNetworkCredential()?.Password;
        }

        HtmlBrowserTestResult result;
        if (ParameterSetName == ParameterSetFile) {
            result = await HtmlBrowserTester.TestFileAsync(Path, Engine, Headless, Timeout).ConfigureAwait(false);
        } else {
            result = await HtmlBrowserTester.TestUrlAsync(Url, Engine, Headless, Timeout, Proxy, proxyUsername, proxyPassword).ConfigureAwait(false);
        }

        WriteObject(result.GetPerformanceMetrics());
    }
}