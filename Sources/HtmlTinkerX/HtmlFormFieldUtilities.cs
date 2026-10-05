using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using System;
using System.Linq;

namespace HtmlTinkerX;

internal static class HtmlFormFieldUtilities {
    public static HtmlFormFieldType GetFieldType(IElement field) =>
        MapType(field is IHtmlInputElement input ? input.Type : field.GetAttribute("type") ?? field.LocalName);

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
        if (field is IHtmlSelectElement select) {
            return string.Join(",", select.SelectedOptions.Select(static option => option.Value));
        }

        if (field.NodeName.Equals("textarea", StringComparison.OrdinalIgnoreCase)) {
            return field.TextContent ?? string.Empty;
        }

        if (field is IHtmlInputElement input && input.Type is "checkbox" or "radio") {
            if (!input.IsChecked) {
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

}
