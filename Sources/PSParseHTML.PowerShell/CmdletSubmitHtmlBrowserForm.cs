using HtmlTinkerX;
using Microsoft.Playwright;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that submits an HTML form using Playwright or HTTP requests.
/// </summary>
/// <example>
/// <code>Submit-HtmlBrowserForm -Form $form -FieldValue @{ tag = @('first', 'second') }</code>
/// <para>Retains successful defaults and replaces all values named tag with the two supplied values.</para>
/// </example>
/// <example>
/// <code>Submit-HtmlBrowserForm -Form $form -HttpClient $client -FieldValue @{ displayName = 'Ada' }</code>
/// <para>Reuses the downloading client's cookies without changing or disposing that client.</para>
/// </example>
[Cmdlet(VerbsLifecycle.Submit, "HtmlBrowserForm", DefaultParameterSetName = ParameterSetHttp)]
[OutputType(typeof(string))]
[Alias("Submit-HtmlForm")]
public sealed class CmdletSubmitHtmlBrowserForm : AsyncPSCmdlet {
    private const string ParameterSetSession = "Session";
    private const string ParameterSetHttp = "Http";
    private const string ParameterSetClient = "HttpClient";

    /// <summary>Form object created by ConvertFrom-HtmlForm.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    public PSObject Form { get; set; } = null!;

    /// <summary>Field overrides by name. HTTP submission retains other successful values; arrays supply repeated values.</summary>
    [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetSession)]
    [Parameter(Position = 1, ParameterSetName = ParameterSetHttp)]
    [Parameter(Position = 1, ParameterSetName = ParameterSetClient)]
    public Hashtable FieldValue { get; set; } = new();

    /// <summary>Reusable HTTP client for submitting the form. The caller retains ownership, cookies, and configuration.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetClient)]
    public HttpClient? HttpClient { get; set; }

    /// <summary>Existing browser session for Playwright submission.</summary>
    [Parameter(ParameterSetName = ParameterSetSession)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Proxy server address for HTTP submission.</summary>
    [Parameter(ParameterSetName = ParameterSetHttp)]
    public string? Proxy { get; set; }

    /// <summary>Proxy credentials for HTTP submission.</summary>
    [Parameter(ParameterSetName = ParameterSetHttp)]
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>Timeout in milliseconds for browser operations or the complete HTTP submission. Zero disables this timeout; a supplied HTTP client retains its own timeout.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    /// <summary>Maximum HTTP response body bytes. Default: 16 MiB. Raise explicitly for trusted large responses.</summary>
    [Parameter(ParameterSetName = ParameterSetHttp)]
    [Parameter(ParameterSetName = ParameterSetClient)]
    [ValidateRange(1, int.MaxValue)]
    public int MaximumResponseBytes { get; set; } = HtmlHttpFetchOptions.DefaultMaximumResponseBytes;

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

        if (ParameterSetName == ParameterSetSession) {
            Dictionary<string, string> fields = FieldValue.Cast<DictionaryEntry>()
                .ToDictionary(d => (string)d.Key, d => d.Value?.ToString() ?? string.Empty);
            HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
                ?? throw new PSInvalidOperationException("No session provided and no default session found.");

            string? id = Form.Properties["FormId"]?.Value as string;
            try {
                if (!string.IsNullOrEmpty(id) || Form.Properties["FormIndex"]?.Value is int) {
                    HtmlFormResult browserForm = new() { Metadata = new() {
                        Id = id, FormIndex = Form.Properties["FormIndex"]?.Value is int index ? index : 0
                    } };
                    await HtmlFormSubmitter.SubmitAsync(session.Page, browserForm, fields, Timeout, CancelToken).ConfigureAwait(false);
                } else {
                    await HtmlFormSubmitter.SubmitAsync(session.Page, "form", fields, Timeout, CancelToken).ConfigureAwait(false);
                }
            } catch (Exception ex) when (ex is PlaywrightException || ex is TimeoutException || ex is InvalidOperationException) {
                await ExportFailureEvidenceIfRequestedAsync(session, OnFailureEvidence.IsPresent, "SubmitForm", ex, FailureEvidenceFolder, CancelToken).ConfigureAwait(false);
                throw;
            }

            if (PassThru.IsPresent) {
                WriteObject(session);
            }
        } else {
            HtmlFormResult form = new() { Metadata = new() { Action = action, Method = method } };
            string? resolvedAction = Form.Properties["ResolvedAction"]?.Value as string;
            if (!string.IsNullOrEmpty(resolvedAction)) {
                form.Metadata.ResolvedActionUri = new Uri(resolvedAction, UriKind.Absolute);
            }
            if (Form.Properties["SuccessfulFields"]?.Value is IEnumerable successfulFields) {
                foreach (object value in successfulFields) {
                    PSObject field = PSObject.AsPSObject(value);
                    form.SuccessfulFields.Add(new KeyValuePair<string, string>(
                        LanguagePrimitives.ConvertTo<string>(field.Properties["Key"]?.Value),
                        LanguagePrimitives.ConvertTo<string>(field.Properties["Value"]?.Value) ?? string.Empty));
                }
            }
            List<KeyValuePair<string, string>> overrides = new();
            foreach (DictionaryEntry value in FieldValue) {
                string name = LanguagePrimitives.ConvertTo<string>(value.Key);
                IEnumerator? values = LanguagePrimitives.GetEnumerator(value.Value);
                if (values == null) overrides.Add(new(name, LanguagePrimitives.ConvertTo<string>(value.Value) ?? string.Empty));
                else {
                    using IDisposable? enumeratorOwner = values as IDisposable;
                    if (!values.MoveNext()) throw new PSArgumentException($"FieldValue '{name}' needs at least one value. Use an empty string to submit an empty value.");
                    do overrides.Add(new(name, LanguagePrimitives.ConvertTo<string>(values.Current) ?? string.Empty));
                    while (values.MoveNext());
                }
            }
            using HttpClient? ownedClient = HttpClient == null ? HttpClientHelper.Create(Proxy, ProxyCredential) : null;
            if (ownedClient != null) ownedClient.Timeout = Timeout == 0 ? System.Threading.Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(Timeout);
            using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(CancelToken);
            if (Timeout > 0) deadline.CancelAfter(Timeout);
            string result = await HtmlFormSubmitter.SubmitAsync(form, overrides, HttpClient ?? ownedClient!,
                new HtmlHttpFetchOptions { MaximumResponseBytes = MaximumResponseBytes }, deadline.Token).ConfigureAwait(false);
            WriteObject(result);
        }
    }
}
