using HtmlTinkerX;
using System;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Selects JavaScript declarations and assignments from inline JavaScript script tags in HTML.</summary>
/// <example>
///   <summary>Read a JavaScript assignment value from an HTML document</summary>
///   <code>
/// $html = @'
/// <html>
/// <body>
/// <script type="text/javascript">
/// window.$Config = {
///     auth: {
///         sCtx: "expected-context"
///     }
/// };
/// </script>
/// </body>
/// </html>
/// '@
///
/// Select-HtmlJavaScriptVariable -Content $html -Name '$Config' -PropertyPath auth.sCtx
///   </code>
/// </example>
/// <example>
///   <summary>Read module scripts with ECMAScript module grammar</summary>
///   <code>
/// $html = @'
/// <script type="module">
/// import value from "./settings.js";
/// window.$Config = { sCtx: "from-module" };
/// </script>
/// <script>
/// window.$Config = { sCtx: "from-script" };
/// </script>
/// '@
///
/// Select-HtmlJavaScriptVariable -Content $html -Name '$Config' -PropertyPath sCtx |
///     Select-Object -First 1
///   </code>
/// </example>
/// <example>
///   <summary>Ignore non-JavaScript script tags and return each matching assignment</summary>
///   <code>
/// $html = @'
/// <script type="application/ld+json">{"name":"schema"}</script>
/// <script>
/// $Config = { sCtx: "first" };
/// $Config = { sCtx: "second" };
/// </script>
/// '@
///
/// Select-HtmlJavaScriptVariable -Content $html -Name '$Config' -PropertyPath sCtx
///   </code>
/// </example>
/// <example>
///   <summary>Return every matching HTML assignment so callers can choose first or last</summary>
///   <code>
/// $html = @'
/// <script>
/// $Config = { sCtx: "first" };
/// </script>
/// <script type="text/javascript">
/// $Config = { sCtx: "second" };
/// </script>
/// '@
///
/// $values = Select-HtmlJavaScriptVariable -Content $html -Name '$Config' -PropertyPath sCtx
/// $values | Select-Object -First 1
/// $values | Select-Object -Last 1
///   </code>
/// </example>
[Cmdlet(VerbsCommon.Select, "HtmlJavaScriptVariable", DefaultParameterSetName = ParameterSetContent)]
[Alias("Select-HtmlJSVariable")]
[OutputType(typeof(PSObject))]
public sealed class CmdletSelectHtmlJavaScriptVariable : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";
    private const string ParameterSetUrl = "Url";

    /// <summary>HTML content to inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    public string Content { get; set; } = string.Empty;

    /// <summary>Path to an HTML file.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; } = string.Empty;

    /// <summary>URL of an HTML page to download and inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Variable or assignment target names to return.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string[]? Name { get; set; }

    /// <summary>Matches variable names or full assignment paths that contain the provided Name values.</summary>
    [Parameter]
    public SwitchParameter Contains { get; set; }

    /// <summary>Matches variable names or full assignment paths that start with the provided Name values.</summary>
    [Parameter]
    public SwitchParameter StartsWith { get; set; }

    /// <summary>Returns only variable declarations and skips loose assignment expressions.</summary>
    [Parameter]
    public SwitchParameter DeclarationOnly { get; set; }

    /// <summary>Returns a value from a dotted property path inside the matched JavaScript object or array literal.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string[]? PropertyPath { get; set; }

    /// <summary>Enables Acornima tolerant parsing for inline script content.</summary>
    [Parameter]
    public SwitchParameter Tolerant { get; set; }

    /// <summary>Proxy server address used when downloading by URL.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? Proxy { get; set; }

    /// <summary>Credentials used with the proxy server.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        if (Contains.IsPresent && StartsWith.IsPresent) {
            throw new PSArgumentException("Use either -Contains or -StartsWith, not both.");
        }

        ValidateProxy(Proxy, ProxyCredential);
        string html = await ReadHtmlAsync().ConfigureAwait(false);
        foreach (HtmlJavaScriptVariableMatch match in HtmlJavaScriptVariableSelector.SelectHtml(
            html,
            Name,
            Contains.IsPresent,
            StartsWith.IsPresent,
            DeclarationOnly.IsPresent,
            PropertyPath,
            Tolerant.IsPresent)) {
            ThrowIfStopped();
            WriteObject(CmdletSelectJavaScriptVariable.ToPSObject(match));
        }
    }

    private async Task<string> ReadHtmlAsync() {
        switch (ParameterSetName) {
            case ParameterSetFile:
                return await HtmlUtilities.ReadFileCheckedAsync(Path.ToFullPath()).ConfigureAwait(false);
            case ParameterSetUrl:
                using (HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential)) {
                    return await HtmlUtilities.GetStringWithProperEncodingAsync(client, Url.ToString(), fetchOptions: null, cancellationToken: CancelToken).ConfigureAwait(false);
                }
            default:
                return Content;
        }
    }
}
