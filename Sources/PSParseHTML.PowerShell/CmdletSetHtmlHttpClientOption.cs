using System.Collections;
using System.Management.Automation;
using System.Net;
using System.Threading.Tasks;
using PSParseHTML;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that configures default <see cref="HttpClient"/> options used by the module.
/// </summary>
[Cmdlet(VerbsCommon.Set, "HTMLHttpClientOption")]
public sealed class CmdletSetHtmlHttpClientOption : AsyncPSCmdlet {
    /// <summary>Timeout in seconds for created clients.</summary>
    [Parameter]
    public int TimeoutSeconds { get; set; } = -1;

    /// <summary>Headers to apply to created clients.</summary>
    [Parameter]
    public Hashtable? Header { get; set; }

    /// <summary>Clear previously configured headers.</summary>
    [Parameter]
    public SwitchParameter ClearHeader { get; set; }

    /// <summary>Proxy server address.</summary>
    [Parameter]
    public string? Proxy { get; set; }

    /// <summary>Credentials for the proxy.</summary>
    [Parameter]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        if (TimeoutSeconds < -1) {
            ThrowTerminatingError(new ErrorRecord(
                new PSArgumentOutOfRangeException(nameof(TimeoutSeconds), TimeoutSeconds, "TimeoutSeconds cannot be less than -1."),
                "TimeoutSecondsOutOfRange",
                ErrorCategory.InvalidArgument,
                TimeoutSeconds));
            return Task.CompletedTask;
        }
        if (TimeoutSeconds >= 0) {
            HtmlHttpClientFactory.DefaultTimeout = TimeSpan.FromSeconds(TimeoutSeconds);
            HtmlHttpClientFactory.ResetShared();
        }

        if (ClearHeader.IsPresent) {
            HtmlHttpClientFactory.DefaultHeaders.Clear();
            HtmlHttpClientFactory.ResetShared();
        }

        if (Header != null) {
            foreach (DictionaryEntry entry in Header) {
                HtmlHttpClientFactory.DefaultHeaders[entry.Key.ToString()!] = entry.Value.ToString()!;
            }
            HtmlHttpClientFactory.ResetShared();
        }

        if (Proxy != null) {
            HtmlHttpClientFactory.DefaultProxy = Proxy;
            HtmlHttpClientFactory.ResetShared();
        }
        if (ProxyCredential != null) {
            HtmlHttpClientFactory.DefaultProxyCredential = ProxyCredential.GetNetworkCredential();
            HtmlHttpClientFactory.ResetShared();
        }

        return Task.CompletedTask;
    }
}
