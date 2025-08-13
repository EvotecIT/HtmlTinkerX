using AngleSharp.Dom;
using System.Collections.Generic;
using System.Text.Json;

namespace HtmlTinkerX;

/// <summary>
/// Provides helpers for extracting microdata items from an <see cref="IDocument"/> instance.
/// </summary>
internal static class MicrodataParser {
    internal static List<HtmlMicrodataItem> ExtractItems(IDocument document) {
        List<HtmlMicrodataItem> items = new();
        foreach (var root in document.QuerySelectorAll("[itemscope]:not([itemprop])")) {
            items.Add(ParseItem(root));
        }
        return items;
    }

    internal static List<T> ExtractJsonLd<T>(IDocument document) {
        List<T> results = new();
        JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };
        foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']")) {
            string json = script.TextContent;
            if (string.IsNullOrWhiteSpace(json)) {
                continue;
            }
            try {
                T? model = JsonSerializer.Deserialize<T>(json, options);
                if (model != null) {
                    results.Add(model);
                }
            } catch (JsonException) {
                // Ignore invalid JSON-LD blocks
            }
        }
        return results;
    }

    private static HtmlMicrodataItem ParseItem(IElement element) {
        HtmlMicrodataItem item = new() {
            Type = element.GetAttribute("itemtype"),
            Id = element.GetAttribute("itemid")
        };

        foreach (var prop in element.QuerySelectorAll("[itemprop]")) {
            if (!IsDirectChildOf(prop, element)) {
                continue;
            }
            string name = prop.GetAttribute("itemprop") ?? string.Empty;
            string value = GetPropertyValue(prop);
            if (!item.Properties.TryGetValue(name, out var list)) {
                list = new List<string>();
                item.Properties[name] = list;
            }
            if (!string.IsNullOrEmpty(value)) {
                list.Add(value);
            }
        }
        return item;
    }

    private static bool IsDirectChildOf(IElement element, IElement parent) {
        for (var node = element.ParentElement; node != null; node = node.ParentElement) {
            if (node == parent) {
                return true;
            }
            if (node.HasAttribute("itemscope") && !node.HasAttribute("itemprop")) {
                break;
            }
        }
        return false;
    }

    private static string GetPropertyValue(IElement element) {
        return element.GetAttribute("content")
            ?? element.GetAttribute("href")
            ?? element.GetAttribute("src")
            ?? element.GetAttribute("data")
            ?? element.GetAttribute("value")
            ?? element.TextContent.Trim();
    }
}
