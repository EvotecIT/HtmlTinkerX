using HtmlTinkerX;
using System;
using System.Collections;
using System.Linq;
using System.Management.Automation;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Decodes an OAuth or OpenID Connect JSON Web Token into a safe metadata summary.
/// </summary>
/// <example>
///   <summary>Inspect an OIDC token captured from an SSO handoff</summary>
///   <code>Get-HtmlBrowserSsoHandoff -Session $session -IncludeSensitiveValues | ConvertFrom-HtmlJsonWebToken</code>
/// </example>
[Cmdlet(VerbsData.ConvertFrom, "HtmlJsonWebToken", DefaultParameterSetName = ParameterSetToken)]
[OutputType(typeof(HtmlJsonWebTokenSummary))]
public sealed class CmdletConvertFromHtmlJsonWebToken : PSCmdlet {
    private const string ParameterSetToken = "Token";
    private const string ParameterSetHandoff = "Handoff";

    /// <summary>Raw compact JSON Web Token value.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetToken, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, Position = 0)]
    [Alias("Jwt", "JsonWebToken")]
    public object? Token { get; set; }

    /// <summary>SSO handoff object returned by Get-HtmlBrowserSsoHandoff.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetHandoff, ValueFromPipeline = true, Position = 0)]
    public object? Handoff { get; set; }

    /// <summary>Specific handoff field name to inspect. Defaults to id_token and then access_token.</summary>
    [Parameter]
    public string? FieldName { get; set; }

    /// <summary>Include decoded header and payload JSON. Payload values remain redacted unless IncludeSensitiveValues is also set.</summary>
    [Parameter]
    public SwitchParameter IncludeJson { get; set; }

    /// <summary>Reveal subject and user-identifying claim values. Use only for authorized troubleshooting.</summary>
    [Parameter]
    public SwitchParameter IncludeSensitiveValues { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        object? input = ParameterSetName == ParameterSetHandoff ? Handoff : Token;
        string value = input is string token
            ? token
            : GetTokenFromHandoff(input!, FieldName);

        WriteObject(HtmlJsonWebTokenParser.Parse(value, IncludeSensitiveValues.IsPresent, IncludeJson.IsPresent));
    }

    private static string GetTokenFromHandoff(object handoff, string? fieldName) {
        string[] names = string.IsNullOrWhiteSpace(fieldName)
            ? new[] { "id_token", "access_token" }
            : new[] { fieldName! };

        if (handoff is HtmlBrowserSsoHandoff typedHandoff) {
            return GetTokenFromTypedHandoff(typedHandoff, names);
        }

        PSObject psObject = PSObject.AsPSObject(handoff);
        object? formData = psObject.Properties["FormData"]?.Value;
        if (formData is IDictionary dictionary) {
            foreach (string name in names) {
                foreach (DictionaryEntry entry in dictionary) {
                    if (entry.Key != null && string.Equals(entry.Key.ToString(), name, StringComparison.OrdinalIgnoreCase)) {
                        return entry.Value?.ToString() ?? string.Empty;
                    }
                }
            }
        }

        object? fields = psObject.Properties["Fields"]?.Value;
        if (fields is IEnumerable enumerable) {
            foreach (string name in names) {
                foreach (object field in enumerable) {
                    PSObject fieldObject = PSObject.AsPSObject(field);
                    string observedName = fieldObject.Properties["Name"]?.Value?.ToString() ?? string.Empty;
                    if (string.Equals(observedName, name, StringComparison.OrdinalIgnoreCase)) {
                        return fieldObject.Properties["Value"]?.Value?.ToString() ?? string.Empty;
                    }
                }
            }
        }

        return string.Empty;
    }

    private static string GetTokenFromTypedHandoff(HtmlBrowserSsoHandoff handoff, string[] names) {
        foreach (string name in names) {
            if (handoff.FormData.TryGetValue(name, out string? value)) {
                return value;
            }

            HtmlBrowserSsoField? field = handoff.Fields.FirstOrDefault(field => string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase));
            if (field != null) {
                return field.Value;
            }
        }

        return string.Empty;
    }
}
