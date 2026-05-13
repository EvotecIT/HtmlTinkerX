using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Selects HtmlAgilityPack nodes using XPath or common tag and attribute predicates.</summary>
/// <example>
///   <summary>Select links by XPath</summary>
///   <code>ConvertFrom-HTML -Content $html | Select-HtmlNode -XPath '//a'</code>
/// </example>
/// <example>
///   <summary>Select the first heading with a specific class</summary>
///   <code>ConvertFrom-HTML -Content $html | Select-HtmlNode -Tag h3 -AttributeName class -AttributeValue 'title' -Single</code>
/// </example>
[Cmdlet(VerbsCommon.Select, "HtmlNode", DefaultParameterSetName = ParameterSetXPath)]
[OutputType(typeof(HtmlNode))]
public sealed class CmdletSelectHtmlNode : AsyncPSCmdlet {
    private const string ParameterSetXPath = "XPath";
    private const string ParameterSetPredicate = "Predicate";

    /// <summary>HtmlAgilityPack node, document, or raw HTML content to search.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0)]
    public object InputObject { get; set; } = null!;

    /// <summary>XPath expression passed to HtmlNode.SelectNodes or HtmlNode.SelectSingleNode.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetXPath, Position = 1)]
    public string XPath { get; set; } = string.Empty;

    /// <summary>Tag name used to build a simple XPath expression.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetPredicate)]
    public string Tag { get; set; } = "*";

    /// <summary>Attribute name used to build a simple XPath predicate.</summary>
    [Parameter(ParameterSetName = ParameterSetPredicate)]
    public string? AttributeName { get; set; }

    /// <summary>Attribute value used to build a simple XPath predicate.</summary>
    [Parameter(ParameterSetName = ParameterSetPredicate)]
    public string? AttributeValue { get; set; }

    /// <summary>Builds a contains() predicate for attribute matching.</summary>
    [Parameter(ParameterSetName = ParameterSetPredicate)]
    public SwitchParameter Contains { get; set; }

    /// <summary>Builds a starts-with() predicate for attribute matching.</summary>
    [Parameter(ParameterSetName = ParameterSetPredicate)]
    public SwitchParameter StartsWith { get; set; }

    /// <summary>Returns text nodes beneath the matching elements.</summary>
    [Parameter(ParameterSetName = ParameterSetPredicate)]
    public SwitchParameter Text { get; set; }

    /// <summary>Returns only the first matching node.</summary>
    [Parameter]
    public SwitchParameter Single { get; set; }

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        string xpath = ParameterSetName == ParameterSetPredicate ? BuildPredicateXPath() : XPath;
        HtmlNode node = HtmlPipelineInput.ToHtmlNode(InputObject);

        if (Single.IsPresent) {
            HtmlNode? result = node.SelectSingleNode(xpath);
            if (result != null) {
                WriteObject(result);
            }

            return Task.CompletedTask;
        }

        HtmlNodeCollection? nodes = node.SelectNodes(xpath);
        if (nodes != null) {
            foreach (HtmlNode result in nodes) {
                WriteObject(result);
            }
        }

        return Task.CompletedTask;
    }

    private string BuildPredicateXPath() {
        if (Contains.IsPresent && StartsWith.IsPresent) {
            throw new PSArgumentException("Use either -Contains or -StartsWith, not both.");
        }

        string tag = string.IsNullOrWhiteSpace(Tag) ? "*" : Tag.Trim();
        StringBuilder xpath = new($".//{tag}");

        if (!string.IsNullOrWhiteSpace(AttributeName)) {
            string attribute = AttributeName!.Trim();
            if (string.IsNullOrWhiteSpace(AttributeValue)) {
                xpath.Append("[@").Append(attribute).Append(']');
            } else {
                string value = ToXPathLiteral(AttributeValue!);
                if (Contains.IsPresent) {
                    xpath.Append("[contains(@").Append(attribute).Append(", ").Append(value).Append(")]");
                } else if (StartsWith.IsPresent) {
                    xpath.Append("[starts-with(@").Append(attribute).Append(", ").Append(value).Append(")]");
                } else {
                    xpath.Append("[@").Append(attribute).Append(" = ").Append(value).Append(']');
                }
            }
        }

        if (Text.IsPresent) {
            xpath.Append("/text()");
        }

        return xpath.ToString();
    }

    private static string ToXPathLiteral(string value) {
        if (!value.Contains("'")) {
            return $"'{value}'";
        }

        if (!value.Contains("\"")) {
            return $"\"{value}\"";
        }

        string[] parts = value.Split('\'');
        List<string> literals = new(parts.Length * 2);
        foreach (string part in parts) {
            if (part.Length > 0) {
                literals.Add($"'{part}'");
            }

            literals.Add("\"'\"");
        }

        if (literals.Count > 0) {
            literals.RemoveAt(literals.Count - 1);
        }

        return "concat(" + string.Join(", ", literals) + ")";
    }
}
