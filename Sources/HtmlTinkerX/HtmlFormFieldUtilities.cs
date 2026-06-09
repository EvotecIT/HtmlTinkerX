using AngleSharp.Dom;
using System;
using System.Linq;

namespace HtmlTinkerX;

internal static class HtmlFormFieldUtilities {
    public static HtmlFormFieldType MapType(string? type) {
        return type?.ToLowerInvariant() switch {
            "text" => HtmlFormFieldType.Text,
            "password" => HtmlFormFieldType.Password,
            "hidden" => HtmlFormFieldType.Hidden,
            "checkbox" => HtmlFormFieldType.Checkbox,
            "radio" => HtmlFormFieldType.Radio,
            "submit" => HtmlFormFieldType.Submit,
            "select" => HtmlFormFieldType.Select,
            "textarea" => HtmlFormFieldType.Textarea,
            "button" => HtmlFormFieldType.Button,
            _ => HtmlFormFieldType.Other,
        };
    }

    public static string GetSubmittedValue(IElement field) {
        if (field.NodeName.Equals("select", StringComparison.OrdinalIgnoreCase)) {
            IElement[] selectedOptions = field.QuerySelectorAll("option[selected]").ToArray();

            if (selectedOptions.Length == 0) {
                if (field.HasAttribute("multiple")) {
                    return string.Empty;
                }

                IElement? firstOption = field.QuerySelector("option");
                if (firstOption != null) {
                    return GetOptionSubmittedValue(firstOption);
                }
            }

            return string.Join(",", selectedOptions.Select(GetOptionSubmittedValue));
        }

        if (field.NodeName.Equals("textarea", StringComparison.OrdinalIgnoreCase)) {
            return field.TextContent ?? string.Empty;
        }

        string type = field.GetAttribute("type") ?? string.Empty;
        if (field.NodeName.Equals("input", StringComparison.OrdinalIgnoreCase)
            && (type.Equals("checkbox", StringComparison.OrdinalIgnoreCase) || type.Equals("radio", StringComparison.OrdinalIgnoreCase))) {
            if (!field.HasAttribute("checked")) {
                return string.Empty;
            }

            return field.GetAttribute("value") ?? "on";
        }

        string? value = field.GetAttribute("value");
        if (value != null) {
            return value;
        }

        return field.TextContent ?? string.Empty;
    }

    private static string GetOptionSubmittedValue(IElement option) =>
        option.GetAttribute("value") ?? option.TextContent ?? string.Empty;
}
