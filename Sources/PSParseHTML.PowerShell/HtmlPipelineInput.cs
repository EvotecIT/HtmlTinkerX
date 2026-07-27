using AngleSharp.Dom;
using HtmlAgilityPack;
using HtmlTinkerX;
using System;
using System.Management.Automation;

namespace PSParseHTML.PowerShell;

internal static class HtmlPipelineInput {
    internal static HtmlNode ToHtmlNode(object input) {
        object value = Unwrap(input);
        return value switch {
            HtmlNode node => node,
            HtmlDocument document => document.DocumentNode,
            string html => HtmlParser.ParseWithHtmlAgilityPack(html).DocumentNode,
            _ => throw new PSArgumentException($"Input object of type '{value.GetType().FullName}' cannot be converted to an HtmlAgilityPack.HtmlNode.")
        };
    }

    internal static string ToHtmlMarkup(object input) {
        object value = Unwrap(input);
        return value switch {
            IElement element => element.OuterHtml,
            IDocument document => document.DocumentElement?.OuterHtml ?? string.Empty,
            HtmlNode node => node.OuterHtml,
            HtmlDocument document => document.DocumentNode.OuterHtml,
            string html => html,
            _ => throw new PSArgumentException($"Input object of type '{value.GetType().FullName}' cannot be converted to HTML markup.")
        };
    }

    internal static object Unwrap(object input) {
        if (input == null) {
            throw new PSArgumentNullException(nameof(input));
        }

        object value = input;
        while (value is PSObject psObject && psObject.BaseObject != null && !ReferenceEquals(psObject.BaseObject, value)) {
            value = psObject.BaseObject;
        }

        return value;
    }

    internal static bool TryGetPropertyValue(object input, string propertyName, out object? value) {
        PSPropertyInfo? property = PSObject.AsPSObject(input).Properties[propertyName];
        if (property == null) {
            value = null;
            return false;
        }

        value = property.Value;
        return true;
    }
}
