using HtmlTinkerX;
using System;
using System.Collections;
using System.Linq;
using System.Management.Automation;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Analyzes an SSO handoff form and safely decodes known protocol artifacts.
/// </summary>
/// <example>
///   <summary>Analyze a captured SSO handoff without choosing a protocol-specific decoder</summary>
///   <code>Get-HtmlBrowserSsoHandoff -Session $session -IncludeSensitiveValues | ConvertFrom-HtmlSsoHandoff</code>
/// </example>
[Cmdlet(VerbsData.ConvertFrom, "HtmlSsoHandoff")]
[OutputType(typeof(HtmlSsoHandoffAnalysis))]
public sealed class CmdletConvertFromHtmlSsoHandoff : PSCmdlet {
    /// <summary>SSO handoff object returned by Get-HtmlBrowserSsoHandoff.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0)]
    public object? Handoff { get; set; }

    /// <summary>Include decoded SAML XML when a SAMLResponse is present. Values remain redacted unless IncludeSensitiveValues is also set.</summary>
    [Parameter]
    public SwitchParameter IncludeXml { get; set; }

    /// <summary>Include decoded JWT header and payload JSON when id_token or access_token fields are present. Values remain redacted unless IncludeSensitiveValues is also set.</summary>
    [Parameter]
    public SwitchParameter IncludeJson { get; set; }

    /// <summary>Reveal subject, user-identifying, and assertion values in nested summaries. Use only for authorized troubleshooting.</summary>
    [Parameter]
    public SwitchParameter IncludeSensitiveValues { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        HtmlBrowserSsoHandoff handoff = ConvertToHandoff(Handoff!);
        WriteObject(HtmlSsoHandoffAnalyzer.Analyze(
            handoff,
            IncludeSensitiveValues.IsPresent,
            IncludeXml.IsPresent,
            IncludeJson.IsPresent));
    }

    private static HtmlBrowserSsoHandoff ConvertToHandoff(object value) {
        if (value is HtmlBrowserSsoHandoff typed) {
            return typed;
        }

        PSObject psObject = PSObject.AsPSObject(value);
        HtmlBrowserSsoHandoff handoff = new() {
            Index = GetPropertyValue<int>(psObject, nameof(HtmlBrowserSsoHandoff.Index)),
            Kind = GetHandoffKind(psObject),
            PageUrl = GetPropertyString(psObject, nameof(HtmlBrowserSsoHandoff.PageUrl)),
            Title = GetPropertyString(psObject, nameof(HtmlBrowserSsoHandoff.Title)),
            FormSelector = GetPropertyString(psObject, nameof(HtmlBrowserSsoHandoff.FormSelector)),
            Action = GetPropertyString(psObject, nameof(HtmlBrowserSsoHandoff.Action)),
            Method = GetPropertyString(psObject, nameof(HtmlBrowserSsoHandoff.Method)),
            AutoSubmitPrevented = GetPropertyValue<bool>(psObject, nameof(HtmlBrowserSsoHandoff.AutoSubmitPrevented)),
            ContainsSensitiveValues = GetPropertyValue<bool>(psObject, nameof(HtmlBrowserSsoHandoff.ContainsSensitiveValues))
        };

        object? fields = psObject.Properties[nameof(HtmlBrowserSsoHandoff.Fields)]?.Value;
        if (fields is IEnumerable fieldItems) {
            foreach (object fieldItem in fieldItems) {
                handoff.Fields.Add(ConvertToField(fieldItem));
            }
        }

        object? formData = psObject.Properties[nameof(HtmlBrowserSsoHandoff.FormData)]?.Value;
        if (formData is IDictionary dictionary) {
            foreach (DictionaryEntry entry in dictionary) {
                if (entry.Key == null) {
                    continue;
                }

                handoff.FormData[entry.Key.ToString() ?? string.Empty] = entry.Value?.ToString() ?? string.Empty;
            }
        }

        return handoff;
    }

    private static HtmlBrowserSsoField ConvertToField(object value) {
        if (value is HtmlBrowserSsoField typed) {
            return typed;
        }

        PSObject psObject = PSObject.AsPSObject(value);
        return new HtmlBrowserSsoField {
            Name = GetPropertyString(psObject, nameof(HtmlBrowserSsoField.Name)),
            Type = GetPropertyString(psObject, nameof(HtmlBrowserSsoField.Type)),
            Value = GetPropertyString(psObject, nameof(HtmlBrowserSsoField.Value)),
            ValueLength = GetPropertyValue<int>(psObject, nameof(HtmlBrowserSsoField.ValueLength)),
            IsSensitive = GetPropertyValue<bool>(psObject, nameof(HtmlBrowserSsoField.IsSensitive)),
            Redacted = GetPropertyValue<bool>(psObject, nameof(HtmlBrowserSsoField.Redacted)),
            Truncated = GetPropertyValue<bool>(psObject, nameof(HtmlBrowserSsoField.Truncated))
        };
    }

    private static HtmlBrowserSsoHandoffKind GetHandoffKind(PSObject psObject) {
        object? value = psObject.Properties[nameof(HtmlBrowserSsoHandoff.Kind)]?.Value;
        if (value is HtmlBrowserSsoHandoffKind typed) {
            return typed;
        }

        return Enum.TryParse(value?.ToString(), ignoreCase: true, out HtmlBrowserSsoHandoffKind parsed)
            ? parsed
            : HtmlBrowserSsoHandoffKind.Unknown;
    }

    private static string GetPropertyString(PSObject psObject, string propertyName) =>
        psObject.Properties[propertyName]?.Value?.ToString() ?? string.Empty;

    private static T GetPropertyValue<T>(PSObject psObject, string propertyName) {
        object? value = psObject.Properties[propertyName]?.Value;
        if (value is T typed) {
            return typed;
        }

        if (value == null) {
            return default!;
        }

        try {
            return (T)Convert.ChangeType(value, typeof(T));
        } catch (InvalidCastException) {
            return default!;
        } catch (FormatException) {
            return default!;
        }
    }
}
