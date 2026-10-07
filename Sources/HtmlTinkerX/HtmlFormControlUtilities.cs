using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HtmlTinkerX;

/// <summary>Form ownership and successful values shared by extraction and hidden-form relays.</summary>
internal static class HtmlFormControlUtilities {
    internal static Dictionary<IElement, List<IElement>> GetControlsByForm(IDocument document) {
        Dictionary<IElement, List<IElement>> controls = new();
        foreach (IElement element in document.QuerySelectorAll("input,select,textarea,button")) {
            IHtmlFormElement? owner = GetOwner(element);
            if (owner == null) {
                continue;
            }
            if (!controls.TryGetValue(owner, out List<IElement>? fields)) {
                fields = new List<IElement>();
                controls.Add(owner, fields);
            }
            fields.Add(element);
        }
        return controls;
    }

    internal static IHtmlFormElement? GetOwner(IElement element) => element switch {
        IHtmlInputElement input => input.Form,
        IHtmlSelectElement select => select.Form,
        IHtmlTextAreaElement textarea => textarea.Form,
        IHtmlButtonElement button => button.Form,
        _ => null
    };

    internal static List<IElement> GetSuccessfulControls(IEnumerable<IElement> controls) =>
        controls.Where(IsSuccessfulControl).ToList();

    internal static List<KeyValuePair<string, string>> GetSubmittedValues(IEnumerable<IElement> controls) {
        List<KeyValuePair<string, string>> values = new();
        foreach (IElement control in controls) {
            string name = control.GetAttribute("name")!;
            if (control is IHtmlSelectElement select) {
                foreach (IHtmlOptionElement option in select.SelectedOptions) {
                    if (!option.IsDisabled && !IsInDisabledOptionGroup(option)) {
                        values.Add(new KeyValuePair<string, string>(name, option.Value));
                    }
                }
            } else {
                values.Add(new KeyValuePair<string, string>(name, HtmlFormFieldUtilities.GetSubmittedValue(control)));
            }
        }
        return values;
    }

    private static bool IsSuccessfulControl(IElement control) {
        if (control.HasAttribute("disabled") || IsDisabledByFieldset(control)
            || string.IsNullOrEmpty(control.GetAttribute("name")) || control.Closest("datalist") != null) {
            return false;
        }
        if (control.LocalName == "button") {
            return false;
        }
        if (control is IHtmlInputElement input) {
            if (input.Type is "submit" or "button" or "reset" or "image" or "file") {
                return false;
            }
            if ((input.Type is "checkbox" or "radio") && !input.IsChecked) {
                return false;
            }
        }
        return true;
    }

    private static bool IsInDisabledOptionGroup(IElement option) =>
        option.ParentElement is IHtmlOptionsGroupElement group && group.IsDisabled;

    private static bool IsDisabledByFieldset(IElement control) {
        for (IElement? ancestor = control.ParentElement; ancestor != null; ancestor = ancestor.ParentElement) {
            if (ancestor.LocalName != "fieldset" || !ancestor.HasAttribute("disabled")) {
                continue;
            }
            IElement? firstLegend = ancestor.Children.FirstOrDefault(static child => child.LocalName == "legend");
            if (firstLegend != null && IsDescendantOf(control, firstLegend)) {
                continue;
            }
            return true;
        }
        return false;
    }

    private static bool IsDescendantOf(IElement element, IElement ancestor) {
        for (IElement? parent = element.ParentElement; parent != null; parent = parent.ParentElement) {
            if (ReferenceEquals(parent, ancestor)) {
                return true;
            }
        }
        return false;
    }
}
