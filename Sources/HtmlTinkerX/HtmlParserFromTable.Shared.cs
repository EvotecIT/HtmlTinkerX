using AngleSharp.Dom;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Text;

namespace HtmlTinkerX;

/// <summary>
/// Provides specialized functionality for parsing HTML tables.
/// </summary>
public static partial class HtmlParserFromTable {
    private static IElement? SelectBestHeaderRow(IElement table, IElement[] allRows) {
        // Look at thead rows first – prefer the one with the largest effective column span, then most cells, then the last occurrence.
        var theadRows = table.QuerySelectorAll("thead tr").ToArray();
        if (theadRows.Length == 0) {
            return null;
        }

        return theadRows
            .Select((r, idx) => new {
                Row = r,
                EffectiveCols = SumColSpans(r.QuerySelectorAll("th,td")),
                CellCount = r.QuerySelectorAll("th,td").Length,
                Index = idx
            })
            .OrderByDescending(x => x.EffectiveCols)
            .ThenByDescending(x => x.CellCount)
            .ThenByDescending(x => x.Index)
            .First().Row;
    }

    private static HtmlNode? SelectBestHeaderRow(HtmlNodeCollection rows) {
        if (rows == null || rows.Count == 0) {
            return null;
        }

        // Prefer thead rows when present; otherwise rows is already scoped.
        var candidates = rows.Where(r => r.ParentNode?.Name.Equals("thead", StringComparison.OrdinalIgnoreCase) == true).ToList();
        if (candidates.Count == 0) {
            return null;
        }

        return candidates
            .Select((r, idx) => new {
                Row = r,
                EffectiveCols = SumColSpans(r.SelectNodes("th|td")),
                CellCount = r.SelectNodes("th|td")?.Count ?? 0,
                Index = idx
            })
            .OrderByDescending(x => x.EffectiveCols)
            .ThenByDescending(x => x.CellCount)
            .ThenByDescending(x => x.Index)
            .First().Row;
    }

    private static IEnumerable<IElement> OrderDataRows(IElement[] rows, int startIndex) =>
        rows.Skip(startIndex)
            .OrderBy(r => r.Closest("tfoot") != null ? 1 : 0); // ensure tfoot rows come last

    private static IEnumerable<HtmlNode> OrderDataRows(HtmlNodeCollection rows, int startIndex) {
        if (rows == null) {
            yield break;
        }
        foreach (var row in rows.Cast<HtmlNode>().Skip(startIndex).OrderBy(r => r.Ancestors("tfoot").Any() ? 1 : 0)) {
            yield return row;
        }
    }

    private static string GetHeaderText(IElement cell) {
        // Prefer explicit dropdown header text if present
        var dropdownHead = cell.QuerySelector(".table-dropdown-head");
        if (dropdownHead != null) {
            return dropdownHead.TextContent.Trim();
        }

        // Otherwise, collect text but skip menus/lists that often live in header filters
        var sb = new StringBuilder();
        AppendHeaderNodeText(cell, sb);
        return CleanupHeader(sb.ToString());
    }

    private static string GetHeaderText(HtmlNode cell) {
        var dropdownHead = cell.SelectSingleNode(".//*[contains(@class,'table-dropdown-head')]");
        if (dropdownHead != null) {
            return HtmlEntity.DeEntitize(dropdownHead.InnerText ?? string.Empty)!.Trim();
        }

        var sb = new StringBuilder();
        AppendHeaderNodeText(cell, sb);
        return CleanupHeader(sb.ToString());
    }

    private static readonly HashSet<string> HeaderSkipTags = new(StringComparer.OrdinalIgnoreCase) { "ul", "ol", "li", "script", "style", "select", "option" };

    private static void AppendHeaderNodeText(INode node, StringBuilder sb) {
        if (node is IText textNode) {
            sb.Append(textNode.Data);
            return;
        }
        if (node is IElement el) {
            if (HeaderSkipTags.Contains(el.TagName)) {
                return;
            }
            foreach (var child in el.ChildNodes) {
                AppendHeaderNodeText(child, sb);
            }
        }
    }

    private static void AppendHeaderNodeText(HtmlNode node, StringBuilder sb) {
        if (node is HtmlTextNode textNode) {
            sb.Append(textNode.Text);
            return;
        }
        if (node.NodeType != HtmlNodeType.Element) {
            foreach (var child in node.ChildNodes) {
                AppendHeaderNodeText(child, sb);
            }
            return;
        }
        if (HeaderSkipTags.Contains(node.Name)) {
            return;
        }
        foreach (var child in node.ChildNodes) {
            AppendHeaderNodeText(child, sb);
        }
    }

    private static string CleanupHeader(string raw) {
        return Regex.Replace(raw, @"\s+", " ").Trim();
    }

    private static IDictionary<string, string> BuildDataValueLookup(IElement table) {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var li in table.QuerySelectorAll("li[data-value]")) {
            string text = li.TextContent.Trim();
            string key = (li.GetAttribute("data-value") ?? string.Empty).Trim();
            if (key.Length > 0 && !dict.ContainsKey(key)) {
                dict[key] = text;
            }
        }
        return dict;
    }

    private static IDictionary<string, string> BuildDataValueLookup(HtmlNode table) {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var items = table.SelectNodes(".//li[@data-value]");
        if (items != null) {
            foreach (var li in items) {
                string key = li.GetAttributeValue("data-value", string.Empty).Trim();
                string text = HtmlEntity.DeEntitize(li.InnerText ?? string.Empty).Trim();
                if (key.Length > 0 && !dict.ContainsKey(key)) {
                    dict[key] = text;
                }
            }
        }
        return dict;
    }

    private static string? FillFromDataAttributes(string header, string? current, IElement row, IDictionary<string, string>? lookup) {
        if (!string.IsNullOrEmpty(current) || lookup == null) {
            return current;
        }
        if (header.Equals("Category", StringComparison.OrdinalIgnoreCase)) {
            string key = row.GetAttribute("data-category") ?? string.Empty;
            key = key.Trim();
            if (key.Length == 0) {
                return current;
            }
            if (lookup.TryGetValue(key, out var label)) {
                return label;
            }
            return key; // fallback to raw code
        }
        if (header.Equals("Severity", StringComparison.OrdinalIgnoreCase)) {
            string key = row.GetAttribute("data-severity") ?? string.Empty;
            key = key.Trim();
            if (key.Length == 0) {
                return current;
            }
            if (lookup.TryGetValue(key, out var label)) {
                return label;
            }
            return key;
        }
        return current;
    }

    private static string? FillFromDataAttributes(string header, string? current, HtmlNode row, IDictionary<string, string>? lookup) {
        if (!string.IsNullOrEmpty(current) || lookup == null) {
            return current;
        }
        if (header.Equals("Category", StringComparison.OrdinalIgnoreCase)) {
            string key = row.GetAttributeValue("data-category", string.Empty).Trim();
            if (!string.IsNullOrEmpty(key)) {
                if (lookup.TryGetValue(key, out var label)) {
                    return label;
                }
                return key;
            }
        }
        if (header.Equals("Severity", StringComparison.OrdinalIgnoreCase)) {
            string key = row.GetAttributeValue("data-severity", string.Empty).Trim();
            if (!string.IsNullOrEmpty(key)) {
                if (lookup.TryGetValue(key, out var label)) {
                    return label;
                }
                return key;
            }
        }
        return current;
    }

    private static int SumColSpans(IEnumerable<IElement> cells) {
        int total = 0;
        foreach (var cell in cells) {
            if (int.TryParse(cell.GetAttribute("colspan"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cs) && cs > 0) {
                total += cs;
            } else {
                total += 1;
            }
        }
        return total;
    }

    private static int SumColSpans(HtmlNodeCollection? cells) {
        if (cells == null) {
            return 0;
        }
        int total = 0;
        foreach (var cell in cells) {
            if (int.TryParse(cell.GetAttributeValue("colspan", "1"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cs) && cs > 0) {
                total += cs;
            } else {
                total += 1;
            }
        }
        return total;
    }

    private static string FormatCellText(IElement cell, HtmlCellTextFormat format) {
        if (format == HtmlCellTextFormat.Compact) {
            return HtmlEntity.DeEntitize(cell.TextContent).Trim();
        }
        var sb = new StringBuilder();
        AppendNodeText(cell, sb);
        var cleaned = CleanupText(sb.ToString(), format);
        return HtmlEntity.DeEntitize(cleaned);
    }

    private static string FormatCellText(HtmlNode cell, HtmlCellTextFormat format) {
        if (format == HtmlCellTextFormat.Compact) {
            return HtmlEntity.DeEntitize(cell.InnerText ?? string.Empty).Trim();
        }
        var sb = new StringBuilder();
        AppendNodeText(cell, sb);
        var cleaned = CleanupText(sb.ToString(), format);
        return HtmlEntity.DeEntitize(cleaned);
    }

    private static Dictionary<int, string> BuildLinkHeaderNames(IReadOnlyList<string> headers) {
        var used = new HashSet<string>(headers, StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<int, string>();
        for (int i = 0; i < headers.Count; i++) {
            string header = headers[i];
            result[i] = CreateUniqueLinkHeaderName(header, i + 1, used);
        }

        return result;
    }

    private static string CreateUniqueLinkHeaderName(string header, int columnIndex, ISet<string> used) {
        string baseName = string.IsNullOrWhiteSpace(header)
            ? DefaultColumnNamePrefix + columnIndex.ToString(CultureInfo.InvariantCulture) + LinkUrlSuffix
            : header + LinkUrlSuffix;
        string candidate = baseName;
        int suffix = 2;
        while (!used.Add(candidate)) {
            candidate = baseName + suffix.ToString(CultureInfo.InvariantCulture);
            suffix++;
        }

        return candidate;
    }

    private static void AppendUsedLinkHeaders(
        IList<string> headers,
        IEnumerable<Dictionary<string, string?>> rows,
        IReadOnlyDictionary<int, string>? linkHeaderNames) {
        if (linkHeaderNames == null) {
            return;
        }

        var rowList = rows as IList<Dictionary<string, string?>> ?? rows.ToList();
        foreach (string linkHeader in linkHeaderNames.Values) {
            if (headers.Contains(linkHeader, StringComparer.OrdinalIgnoreCase)) {
                continue;
            }

            if (rowList.Any(row => row.TryGetValue(linkHeader, out string? value) && !string.IsNullOrWhiteSpace(value))) {
                headers.Add(linkHeader);
                foreach (Dictionary<string, string?> row in rowList) {
                    if (!row.ContainsKey(linkHeader)) {
                        row[linkHeader] = null;
                    }
                }
            }
        }
    }

    private static string GetDirectCaptionText(IElement table) {
        return table.Children
            .FirstOrDefault(child => child.TagName.Equals("caption", StringComparison.OrdinalIgnoreCase))
            ?.TextContent ?? string.Empty;
    }

    private static string GetDirectCaptionText(HtmlNode table) {
        return table.ChildNodes
            .FirstOrDefault(child => child.Name.Equals("caption", StringComparison.OrdinalIgnoreCase))
            ?.InnerText ?? string.Empty;
    }

    private static string? ExtractLinkUrls(IElement cell) {
        var links = cell.QuerySelectorAll("a[href]")
            .Select(link => (link.GetAttribute("href") ?? string.Empty).Trim())
            .Where(href => href.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return links.Length == 0 ? null : string.Join("; ", links);
    }

    private static string? ExtractLinkUrls(HtmlNode cell) {
        var links = cell.SelectNodes(".//a[@href]")?
            .Select(link => link.GetAttributeValue("href", string.Empty).Trim())
            .Where(href => href.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return links == null || links.Length == 0 ? null : string.Join("; ", links);
    }

    private static readonly HashSet<string> BlockTags = new(StringComparer.OrdinalIgnoreCase) { "p", "div", "section", "article", "header", "footer", "ul", "ol" };
    private static readonly HashSet<string> LineTags = new(StringComparer.OrdinalIgnoreCase) { "br", "tr", "li" };

    private static void AppendNodeText(INode node, StringBuilder sb) {
        switch (node) {
            case IText textNode:
                var text = textNode.Data;
                if (string.IsNullOrWhiteSpace(text)) {
                    return;
                }
                sb.Append(Regex.Replace(text, @"\s+", " "));
                break;
            case IElement element:
                bool addBullet = element.TagName.Equals("li", StringComparison.OrdinalIgnoreCase);
                int startLength = sb.Length;
                int beforeChildren = sb.Length;
                if (addBullet) {
                    AppendNewlineIfNeeded(sb);
                    sb.Append("- ");
                    beforeChildren = sb.Length;
                }
                foreach (var child in element.ChildNodes) {
                    AppendNodeText(child, sb);
                }
                bool wroteChildren = sb.Length > beforeChildren;
                if (addBullet) {
                    string addedText = sb.ToString(beforeChildren, sb.Length - beforeChildren);
                    if (string.IsNullOrWhiteSpace(addedText)) {
                        sb.Length = startLength; // remove stray bullet
                        wroteChildren = false;
                    }
                }
                if (wroteChildren && (BlockTags.Contains(element.TagName) || LineTags.Contains(element.TagName))) {
                    AppendNewlineIfNeeded(sb);
                }
                break;
        }
    }

    private static void AppendNodeText(HtmlNode node, StringBuilder sb) {
        if (node is HtmlTextNode textNode) {
            var text = textNode.Text;
            if (string.IsNullOrWhiteSpace(text)) {
                return;
            }
            sb.Append(Regex.Replace(text, @"\s+", " "));
            return;
        }

        if (node.NodeType != HtmlNodeType.Element) {
            foreach (var child in node.ChildNodes) {
                AppendNodeText(child, sb);
            }
            return;
        }

        var tag = node.Name;
        bool addBullet = tag.Equals("li", StringComparison.OrdinalIgnoreCase);
        int startLength = sb.Length;
        int beforeChildren = sb.Length;
        if (addBullet) {
            AppendNewlineIfNeeded(sb);
            sb.Append("- ");
            beforeChildren = sb.Length;
        }

        foreach (var child in node.ChildNodes) {
            AppendNodeText(child, sb);
        }

        bool wroteChildren = sb.Length > beforeChildren;
        if (addBullet) {
            string addedText = sb.ToString(beforeChildren, sb.Length - beforeChildren);
            if (string.IsNullOrWhiteSpace(addedText)) {
                sb.Length = startLength;
                wroteChildren = false;
            }
        }

        if (wroteChildren && (BlockTags.Contains(tag) || LineTags.Contains(tag))) {
            AppendNewlineIfNeeded(sb);
        }
    }

    private static void AppendNewlineIfNeeded(StringBuilder sb) {
        if (sb.Length == 0) {
            return;
        }
        if (sb[sb.Length - 1] != '\n') {
            sb.Append('\n');
        }
    }

    private static string CleanupText(string raw, HtmlCellTextFormat format) {
        if (format == HtmlCellTextFormat.Compact) {
            return raw.Trim();
        }

        // Collapse spaces but keep deliberate newlines.
        string normalized = Regex.Replace(raw, @"[ \t]+", " ");
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");

        if (format == HtmlCellTextFormat.Markdown) {
            // Ensure list items are on their own lines; already prefixed with "- ".
            normalized = Regex.Replace(normalized, @"\n- ", "\n- ");
        }

        // Drop empty bullet lines that can appear from list scaffolding.
        var lines = normalized.Split('\n')
            .Where(l => !Regex.IsMatch(l, @"^\s*-\s*$"))
            .ToArray();
        normalized = string.Join("\n", lines);

        return normalized.Trim();
    }

    private static string ReplaceCaseInsensitive(string input, string oldValue, string newValue) =>
        Regex.Replace(input, Regex.Escape(oldValue), _ => newValue,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
