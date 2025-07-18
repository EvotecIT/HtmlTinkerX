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
[Cmdlet(VerbsData.ConvertFrom, "HtmlForm", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(PSObject))]
public sealed class CmdletConvertFromHtmlForm : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetUrl = "Url";

    /// <summary>HTML content containing forms.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>URL of a page with forms.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Include additional metadata like form index and CSS classes.</summary>
    [Parameter]
    public SwitchParameter IncludeMetadata { get; set; }

    /// <summary>Proxy server address for downloading when using <see cref="Url"/>.</summary>
    [Parameter]
    public string? Proxy { get; set; }

    /// <summary>Credentials for the proxy server.</summary>
    [Parameter]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        List<HtmlFormResult> forms;
        if (ParameterSetName == ParameterSetUrl) {
            using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential);
            forms = await HtmlParser.ParseUrlFormsWithAngleSharpAsync(Url.ToString(), client).ConfigureAwait(false);
        } else {
            forms = HtmlParser.ParseFormsWithAngleSharp(Content);
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
            fieldObjects.Add(f);
        }
        obj.Properties.Add(new PSNoteProperty("Fields", fieldObjects.ToArray()));
        obj.Properties.Add(new PSNoteProperty("Action", result.Metadata.Action));
        obj.Properties.Add(new PSNoteProperty("Method", result.Metadata.Method));

        if (IncludeMetadata.IsPresent) {
            obj.Properties.Add(new PSNoteProperty("FormIndex", result.Metadata.FormIndex));
            obj.Properties.Add(new PSNoteProperty("FormId", result.Metadata.Id ?? string.Empty));
            obj.Properties.Add(new PSNoteProperty("FormClasses", result.Metadata.Classes ?? string.Empty));
        }
        return obj;
    }
}