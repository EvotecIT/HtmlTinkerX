using AngleSharp.Dom;
using HtmlAgilityPack;
using System;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Returns an HTML attribute value from AngleSharp or HtmlAgilityPack elements, attributes, and matching object properties.</summary>
/// <example>
///   <summary>Read href values from links</summary>
///   <code>ConvertFrom-Html -Content $html | Select-HtmlNode -XPath '//a' | Select-HtmlAttributeValue -AttributeName href</code>
/// </example>
[Cmdlet(VerbsCommon.Select, "HtmlAttributeValue")]
[OutputType(typeof(string))]
public sealed class CmdletSelectHtmlAttributeValue : AsyncPSCmdlet {
    /// <summary>AngleSharp or HtmlAgilityPack element, attribute, document, or an object with a matching property.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0)]
    public object InputObject { get; set; } = null!;

    /// <summary>Attribute or property name to read.</summary>
    [Parameter(Position = 1)]
    public string? AttributeName { get; set; }

    /// <summary>Value returned when the requested attribute is missing.</summary>
    [Parameter]
    public string DefaultValue { get; set; } = string.Empty;

    /// <summary>Returns DefaultValue when the attribute exists but its value is empty.</summary>
    [Parameter]
    public SwitchParameter TreatEmptyAsMissing { get; set; }

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        object value = HtmlPipelineInput.Unwrap(InputObject);
        string result = value switch {
            IElement element => GetAngleSharpAttributeValue(element),
            HtmlAttribute attribute => GetAttributeValue(attribute),
            HtmlNode node => GetNodeAttributeValue(node),
            HtmlDocument document => GetNodeAttributeValue(document.DocumentNode),
            _ => GetObjectPropertyValue(InputObject)
        };

        WriteObject(result);
        return Task.CompletedTask;
    }

    private string GetAttributeValue(HtmlAttribute attribute) {
        if (!string.IsNullOrWhiteSpace(AttributeName) &&
            !string.Equals(attribute.Name, AttributeName, StringComparison.OrdinalIgnoreCase)) {
            return DefaultValue;
        }

        return NormalizeValue(attribute.Value);
    }

    private string GetNodeAttributeValue(HtmlNode node) {
        if (string.IsNullOrWhiteSpace(AttributeName)) {
            throw new PSArgumentException("AttributeName is required when InputObject is an HtmlNode or HtmlDocument.");
        }

        return NormalizeValue(node.GetAttributeValue(AttributeName!, DefaultValue));
    }

    private string GetAngleSharpAttributeValue(IElement element) {
        if (string.IsNullOrWhiteSpace(AttributeName)) {
            throw new PSArgumentException("AttributeName is required when InputObject is an AngleSharp element.");
        }

        return NormalizeValue(element.GetAttribute(AttributeName!) ?? DefaultValue);
    }

    private string GetObjectPropertyValue(object input) {
        if (string.IsNullOrWhiteSpace(AttributeName)) {
            throw new PSArgumentException("AttributeName is required when InputObject is not an HtmlAttribute.");
        }

        if (!HtmlPipelineInput.TryGetPropertyValue(input, AttributeName!, out object? propertyValue) || propertyValue == null) {
            return DefaultValue;
        }

        return NormalizeValue(propertyValue.ToString() ?? string.Empty);
    }

    private string NormalizeValue(string value) {
        return TreatEmptyAsMissing.IsPresent && value.Length == 0 ? DefaultValue : value;
    }
}
