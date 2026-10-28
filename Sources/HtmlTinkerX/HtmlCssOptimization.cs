using NUglify.Helpers;
using NUglify.Html;
using System;
using System.Collections.Generic;
using System.IO;

namespace HtmlTinkerX;

// Uses NUglify's own HTML parser so fragments and embedded CSS are inspected
// using the same interpretation as the minifier.
internal static class HtmlCssOptimization {
    internal static string? Optimize(string html, HtmlSettings settings) {
        var parser = new NUglify.Html.HtmlParser(html, settings: settings);
        HtmlDocument document = parser.Parse();

        var rawValues = new List<(HtmlRaw Node, CssMathProtection Protection)>();
        var attributes = new List<(HtmlAttribute Attribute, CssMathProtection Protection, bool EncodeAmpersands)>();
        foreach (var node in document.FindAllDescendants()) {
            if (!(node is HtmlElement element)) continue;
            if (string.Equals(element.Name, "style", StringComparison.OrdinalIgnoreCase) && element.FirstChild is HtmlRaw raw) {
                string css = raw.Slice.ToString();
                if (settings.MinifyCss) {
                    var protection = CssMathProtection.Create(css, out string protectedCss);
                    if (protection.HasProtectedValues) {
                        raw.Slice = new StringSlice(protectedCss);
                        rawValues.Add((raw, protection));
                    }
                }
                // NUglify's pretty printer inserts indentation into CSS string continuations.
                if (css.IndexOf("\\\n", StringComparison.Ordinal) >= 0 || css.IndexOf("\\\r", StringComparison.Ordinal) >= 0)
                    settings.PrettyPrint = false;
            }
            if (!settings.MinifyCssAttributes || element.Attributes == null) continue;
            foreach (var attribute in element.Attributes) {
                if (!string.Equals(attribute.Name, "style", StringComparison.OrdinalIgnoreCase) || attribute.Value == null) continue;
                bool encoded = attribute.Value.IndexOf('&') >= 0;
                string css = encoded ? DecodeAttribute(attribute.Value) : attribute.Value;
                var protection = CssMathProtection.Create(css, out string protectedCss);
                if (!protection.HasProtectedValues) continue;
                attribute.Value = protectedCss;
                attributes.Add((attribute, protection, encoded));
            }
        }

        var minifier = new HtmlMinifier(document, settings);
        minifier.Minify();
        foreach (var item in rawValues) {
            if (!item.Protection.TryRestore(item.Node.Slice.ToString(), out string css)) return null;
            item.Node.Slice = new StringSlice(css);
        }
        foreach (var item in attributes) {
            if (!item.Protection.TryRestore(item.Attribute.Value ?? string.Empty, out string css)) return null;
            item.Attribute.Value = item.EncodeAmpersands ? css.Replace("&", "&amp;") : css;
        }
        using var writer = new StringWriter();
        new HtmlWriterToHtml(writer, settings).Write(document);
        return writer.ToString();
    }

    private static string DecodeAttribute(string value) {
        // Attribute references follow HTML's full named-entity rules, not just
        // the smaller set decoded by WebUtility.HtmlDecode.
        using var document = HtmlParser.ParseWithAngleSharp("<div style=\"" + value.Replace("\"", "&quot;") + "\"></div>");
        return document.QuerySelector("div")?.GetAttribute("style") ?? value;
    }
}
