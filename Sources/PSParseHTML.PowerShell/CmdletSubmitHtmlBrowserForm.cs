using HtmlTinkerX;
using Microsoft.Playwright;
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
[Alias("Submit-HtmlForm")]
public sealed class CmdletSubmitHtmlBrowserForm : AsyncPSCmdlet {
    private const string ParameterSetSession = "Session";
    private const string ParameterSetHttp = "Http";

    /// <summary>Form object created by ConvertFrom-HtmlForm.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    public PSObject Form { get; set; } = null!;

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

    /// <summary>Export screenshots, HTML, text, Markdown, network summary, locator suggestions, and failure context if browser form submission fails.</summary>
    [Parameter(ParameterSetName = ParameterSetSession)]
    public SwitchParameter OnFailureEvidence { get; set; }

    /// <summary>Root folder where failure evidence is written when <see cref="OnFailureEvidence"/> is used.</summary>
    [Parameter(ParameterSetName = ParameterSetSession)]
    public string? FailureEvidenceFolder { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        string action = Form.Properties["Action"]?.Value as string ?? string.Empty;
        FormMethod method = FormMethod.Get;
        object? methodValue = Form.Properties["Method"]?.Value;
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
            string? id = Form.Properties["FormId"]?.Value as string;
            if (!string.IsNullOrEmpty(id)) {
                selector = $"form#{id}";
            } else if (Form.Properties["FormIndex"]?.Value is int idx) {
                selector = $"form:nth-of-type({idx + 1})";
            } else {
                selector = "form";
            }

            try {
                await HtmlFormSubmitter.SubmitAsync(session.Page, selector, fields, Timeout, CancelToken).ConfigureAwait(false);
            } catch (Exception ex) when (ex is PlaywrightException || ex is TimeoutException || ex is InvalidOperationException) {
                await ExportFailureEvidenceIfRequestedAsync(session, OnFailureEvidence.IsPresent, "SubmitForm", ex, FailureEvidenceFolder, CancelToken).ConfigureAwait(false);
                throw;
            }

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
