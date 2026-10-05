using HtmlTinkerX;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Extracts HTML form information into PowerShell objects.
/// </summary>
/// <example>
/// <code>ConvertFrom-HtmlForm -Url https://example.com</code>
/// </example>
/// <example>
/// <code>ConvertFrom-HtmlForm -Content '&lt;form action="save"&gt;&lt;input name="tag" value="one"&gt;&lt;/form&gt;' -BaseUri https://example.com/settings/ -IncludeMetadata</code>
/// <para>Returns the field inventory, ordered successful values, and resolved HTTP action.</para>
/// </example>
/// <example>
/// <code>$form = ConvertFrom-HtmlForm -Url https://example.com/settings/ -HttpClient $client</code>
/// <para>Downloads using a caller-owned, cookie-enabled client that can also submit the form.</para>
/// </example>
[Cmdlet(VerbsData.ConvertFrom, "HtmlForm", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(PSObject))]
public sealed class CmdletConvertFromHtmlForm : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetUrl = "Url";
    private const string ParameterSetClient = "HttpClient";

    /// <summary>HTML content containing forms.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>URL of a page with forms.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetClient)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Reusable HTTP client for downloading the form. The caller retains ownership, cookies, and configuration.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetClient)]
    public HttpClient? HttpClient { get; set; }

    /// <summary>Absolute document address used to resolve relative actions in supplied HTML.</summary>
    [Parameter(ParameterSetName = ParameterSetContent)]
    public Uri? BaseUri { get; set; }

    /// <summary>Include additional metadata like form index and CSS classes.</summary>
    [Parameter]
    public SwitchParameter IncludeMetadata { get; set; }

    /// <summary>Proxy server address for downloading when using <see cref="Url"/>.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetContent)]
    public string? Proxy { get; set; }

    /// <summary>Credentials for the proxy server.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Parameter(ParameterSetName = ParameterSetContent)]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        List<HtmlFormResult> forms;
        if (ParameterSetName == ParameterSetUrl || ParameterSetName == ParameterSetClient) {
            using HttpClient? ownedClient = HttpClient == null ? HttpClientHelper.Create(Proxy, ProxyCredential) : null;
            forms = await HtmlParser.ParseUrlFormsWithAngleSharpAsync(Url.ToString(), HttpClient ?? ownedClient!, cancellationToken: CancelToken).ConfigureAwait(false);
        } else {
            forms = HtmlParser.ParseFormsWithAngleSharp(Content, BaseUri);
        }

        var output = new List<PSObject>();
        foreach (var form in forms) {
            output.Add(CreateFormObject(form));
        }

        if (output.Count == 1) {
            WriteObject(output[0], false);
        } else {
            if (output.Count > 1) {
                WriteWarning($"{output.Count} forms found. Returning array of forms.");
            }
            WriteObject(output.ToArray(), false);
        }
    }

    private PSObject CreateFormObject(HtmlFormResult result) {
        PSObject obj = new();
        var fieldObjects = new List<PSObject>();
        foreach (var field in result.Fields) {
            PSObject f = new();
            f.Properties.Add(new PSNoteProperty("Name", field.Name));
            f.Properties.Add(new PSNoteProperty("Type", field.Type));
            f.Properties.Add(new PSNoteProperty("Value", field.Value));
            fieldObjects.Add(f);
        }
        obj.Properties.Add(new PSNoteProperty("Fields", fieldObjects.ToArray()));
        obj.Properties.Add(new PSNoteProperty("SuccessfulFields", result.SuccessfulFields.ToArray()));
        obj.Properties.Add(new PSNoteProperty("Action", result.Metadata.Action));
        obj.Properties.Add(new PSNoteProperty("ResolvedAction", result.Metadata.ResolvedActionUri?.AbsoluteUri ?? string.Empty));
        obj.Properties.Add(new PSNoteProperty("Method", result.Metadata.Method.ToString().ToUpperInvariant()));

        if (IncludeMetadata.IsPresent) {
            obj.Properties.Add(new PSNoteProperty("FormIndex", result.Metadata.FormIndex));
            obj.Properties.Add(new PSNoteProperty("FormId", result.Metadata.Id ?? string.Empty));
            obj.Properties.Add(new PSNoteProperty("FormClasses", result.Metadata.Classes ?? string.Empty));
            obj.Properties.Add(new PSNoteProperty("SourceUrl", result.Metadata.SourceUri?.AbsoluteUri ?? string.Empty));
            obj.Properties.Add(new PSNoteProperty("FinalUrl", result.Metadata.FinalUri?.AbsoluteUri ?? string.Empty));
            obj.Properties.Add(new PSNoteProperty("BaseUrl", result.Metadata.BaseUri?.AbsoluteUri ?? string.Empty));
        }
        return obj;
    }
}
