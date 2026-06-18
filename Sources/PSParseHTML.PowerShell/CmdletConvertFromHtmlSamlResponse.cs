using HtmlTinkerX;
using System;
using System.Collections;
using System.Linq;
using System.Management.Automation;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Decodes a SAMLResponse value into a safe metadata summary.
/// </summary>
/// <example>
///   <summary>Inspect a captured SAML handoff without revealing subject or attribute values</summary>
///   <code>Get-HtmlBrowserSsoHandoff -Session $session -IncludeSensitiveValues | ConvertFrom-HtmlSamlResponse</code>
/// </example>
[Cmdlet(VerbsData.ConvertFrom, "HtmlSamlResponse", DefaultParameterSetName = ParameterSetSamlResponse)]
[OutputType(typeof(HtmlSamlResponseSummary))]
public sealed class CmdletConvertFromHtmlSamlResponse : PSCmdlet {
    private const string ParameterSetSamlResponse = "SamlResponse";
    private const string ParameterSetHandoff = "Handoff";

    /// <summary>Raw, URL-encoded, base64-encoded, or XML SAMLResponse value.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSamlResponse, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, Position = 0)]
    [Alias("Response")]
    public object? SamlResponse { get; set; }

    /// <summary>SSO handoff object returned by Get-HtmlBrowserSsoHandoff.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetHandoff, Position = 0)]
    public object? Handoff { get; set; }

    /// <summary>Include decoded XML in the output. Values remain redacted unless IncludeSensitiveValues is also set.</summary>
    [Parameter]
    public SwitchParameter IncludeXml { get; set; }

    /// <summary>Reveal subject values and unredacted XML. Use only for authorized troubleshooting.</summary>
    [Parameter]
    public SwitchParameter IncludeSensitiveValues { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        object? input = ParameterSetName == ParameterSetHandoff ? Handoff : SamlResponse;
        string value = input is string samlResponse
            ? samlResponse
            : GetSamlResponseFromHandoff(input!);

        WriteObject(HtmlSamlResponseParser.Parse(value, IncludeSensitiveValues.IsPresent, IncludeXml.IsPresent));
    }

    private static string GetSamlResponseFromHandoff(object handoff) {
        if (handoff is HtmlBrowserSsoHandoff typedHandoff) {
            return GetSamlResponseFromTypedHandoff(typedHandoff);
        }

        PSObject psObject = PSObject.AsPSObject(handoff);
        object? formData = psObject.Properties["FormData"]?.Value;
        if (formData is IDictionary dictionary) {
            foreach (DictionaryEntry entry in dictionary) {
                if (entry.Key != null && string.Equals(entry.Key.ToString(), "SAMLResponse", StringComparison.OrdinalIgnoreCase)) {
                    return entry.Value?.ToString() ?? string.Empty;
                }
            }
        }

        object? fields = psObject.Properties["Fields"]?.Value;
        if (fields is IEnumerable enumerable) {
            foreach (object field in enumerable) {
                PSObject fieldObject = PSObject.AsPSObject(field);
                string name = fieldObject.Properties["Name"]?.Value?.ToString() ?? string.Empty;
                if (string.Equals(name, "SAMLResponse", StringComparison.OrdinalIgnoreCase)) {
                    return fieldObject.Properties["Value"]?.Value?.ToString() ?? string.Empty;
                }
            }
        }

        return string.Empty;
    }

    private static string GetSamlResponseFromTypedHandoff(HtmlBrowserSsoHandoff handoff) {
        if (handoff.FormData.TryGetValue("SAMLResponse", out string? value)) {
            return value;
        }

        HtmlBrowserSsoField? field = handoff.Fields.FirstOrDefault(field => string.Equals(field.Name, "SAMLResponse", StringComparison.OrdinalIgnoreCase));
        return field?.Value ?? string.Empty;
    }
}
