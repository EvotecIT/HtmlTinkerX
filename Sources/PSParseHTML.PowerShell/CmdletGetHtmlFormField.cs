using System.Collections.Generic;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that returns all form input fields from HTML content or a URL.
/// </summary>
[Cmdlet(VerbsCommon.Get, "HTMLFormField", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(HtmlFormField))]
public sealed class CmdletGetHtmlFormField : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetUrl = "Url";

    /// <summary>HTML content to parse.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>URL of the page to download.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Proxy server address used when downloading.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? Proxy { get; set; }

    /// <summary>Credentials for the proxy server.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        List<HtmlFormField> fields;
        if (ParameterSetName == ParameterSetUrl) {
            using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential);
            fields = await HtmlFormFieldExtractor.ExtractUrlFieldsAsync(Url.ToString(), client).ConfigureAwait(false);
        } else {
            fields = HtmlFormFieldExtractor.ExtractFields(Content);
        }

        WriteObject(fields.ToArray(), true);
    }
}