using HtmlTinkerX;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that submits an HTML form using Playwright or HTTP requests.
/// </summary>
[Cmdlet(VerbsLifecycle.Submit, "HtmlBrowserForm", DefaultParameterSetName = ParameterSetHttp)]
[OutputType(typeof(string))]
[Alias("Submit-HTMLForm")]
public sealed class CmdletSubmitHtmlBrowserForm : AsyncPSCmdlet {
    private const string ParameterSetSession = "Session";
    private const string ParameterSetHttp = "Http";

    /// <summary>Form object created by ConvertFrom-HtmlForm.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    public PSObject? Form { get; set; }

    /// <summary>Hashtable of field values keyed by name.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public Hashtable FieldValue { get; set; } = new();

    /// <summary>Existing browser session for Playwright submission.</summary>
    [Parameter(ParameterSetName = ParameterSetSession)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Proxy server address for HTTP submission.</summary>
    [Parameter(ParameterSetName = ParameterSetHttp)]
    public string? Proxy { get; set; }

    /// <summary>Proxy credentials for HTTP submission.</summary>
    [Parameter(ParameterSetName = ParameterSetHttp)]
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>Timeout for Playwright operations.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    /// <summary>Return session object when using Playwright.</summary>
    [Parameter(ParameterSetName = ParameterSetSession)]
    public SwitchParameter PassThru { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        PSObject form = Form ?? throw new PSArgumentNullException(nameof(Form));
        string action = form.Properties["Action"]?.Value as string ?? string.Empty;
        FormMethod method = FormMethod.Get;
        object? methodValue = form.Properties["Method"]?.Value;
        if (methodValue is FormMethod m) {
            method = m;
        } else if (methodValue is string ms && ms.Equals("POST", StringComparison.OrdinalIgnoreCase)) {
            method = FormMethod.Post;
        }

        Dictionary<string, string> fields = FieldValue.Cast<DictionaryEntry>()
            .ToDictionary(d => (string)d.Key, d => d.Value?.ToString() ?? string.Empty);

        if (ParameterSetName == ParameterSetSession) {
            HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
                ?? throw new PSInvalidOperationException("No session provided and no default session found.");

            string selector;
            string? id = form.Properties["FormId"]?.Value as string;
            if (!string.IsNullOrEmpty(id)) {
                selector = $"form#{id}";
            } else if (form.Properties["FormIndex"]?.Value is int idx) {
                selector = $"form:nth-of-type({idx + 1})";
            } else {
                selector = "form";
            }

            await HtmlFormSubmitter.SubmitAsync(session.Page, selector, fields, Timeout).ConfigureAwait(false);
            if (PassThru.IsPresent) {
                WriteObject(session);
            }
        } else {
            using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential);
            string result = await HtmlFormSubmitter.SubmitAsync(action, method, fields, client).ConfigureAwait(false);
            WriteObject(result);
        }
    }
}