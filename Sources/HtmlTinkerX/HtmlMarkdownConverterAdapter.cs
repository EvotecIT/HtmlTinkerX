using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace HtmlTinkerX;

internal static class HtmlMarkdownConverterAdapter {
    private static readonly Regex ConsecutiveBlankLinesRegex = new(@"\n{3,}", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public static string ConvertToMarkdown(string html, string? pageUrl) {
        if (string.IsNullOrWhiteSpace(html)) {
            return string.Empty;
        }

        if (TryConvertWithOfficeImo(html, pageUrl, out string markdown)) {
            return markdown;
        }

        return ConvertWithFallback(html, pageUrl);
    }

    private static bool TryConvertWithOfficeImo(string html, string? pageUrl, out string markdown) {
        markdown = string.Empty;
        try {
            Assembly? assembly = TryLoadOfficeImoMarkdownAssembly();
            if (assembly == null) {
                return false;
            }

            Type? optionsType = assembly.GetType("OfficeIMO.Markdown.Html.HtmlToMarkdownOptions", throwOnError: false);
            Type? extensionsType = assembly.GetType("OfficeIMO.Markdown.Html.HtmlMarkdownConverterExtensions", throwOnError: false);
            if (optionsType == null || extensionsType == null) {
                return false;
            }

            object? options = optionsType.GetMethod("CreatePortableProfile", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, Array.Empty<object>());
            if (options == null) {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(pageUrl) && Uri.TryCreate(pageUrl, UriKind.Absolute, out Uri? baseUri)) {
                optionsType.GetProperty("BaseUri", BindingFlags.Public | BindingFlags.Instance)?.SetValue(options, baseUri);
            }

            MethodInfo? toMarkdownMethod = extensionsType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method => string.Equals(method.Name, "ToMarkdown", StringComparison.Ordinal) && method.GetParameters().Length == 2);
            if (toMarkdownMethod == null) {
                return false;
            }

            markdown = NormalizeMarkdown((string?)toMarkdownMethod.Invoke(null, new[] { html, options }) ?? string.Empty);
            return !string.IsNullOrWhiteSpace(markdown);
        } catch {
            markdown = string.Empty;
            return false;
        }
    }

    private static Assembly? TryLoadOfficeImoMarkdownAssembly() {
        Assembly? loadedAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, "OfficeIMO.Markdown.Html", StringComparison.OrdinalIgnoreCase));
        if (loadedAssembly != null) {
            return loadedAssembly;
        }

        try {
            return Assembly.Load("OfficeIMO.Markdown.Html");
        } catch {
        }

        foreach (string candidatePath in GetAssemblyProbePaths()) {
            if (!File.Exists(candidatePath)) {
                continue;
            }

            try {
                return Assembly.LoadFrom(candidatePath);
            } catch {
            }
        }

        return null;
    }

    private static IEnumerable<string> GetAssemblyProbePaths() {
        string[] roots = {
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory()
        };

        foreach (string root in roots.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase)) {
            yield return Path.Combine(root, "OfficeIMO.Markdown.Html.dll");
            yield return Path.Combine(root, "OfficeIMO.Markdown.Html", "bin", "Debug", "net8.0", "OfficeIMO.Markdown.Html.dll");
            yield return Path.Combine(root, "OfficeIMO.Markdown.Html", "bin", "Debug", "netstandard2.0", "OfficeIMO.Markdown.Html.dll");
            yield return Path.Combine(root, "..", "OfficeIMO", "OfficeIMO.Markdown.Html", "bin", "Debug", "net8.0", "OfficeIMO.Markdown.Html.dll");
            yield return Path.Combine(root, "..", "OfficeIMO", "OfficeIMO.Markdown.Html", "bin", "Debug", "netstandard2.0", "OfficeIMO.Markdown.Html.dll");
        }
    }

    private static string ConvertWithFallback(string html, string? pageUrl) {
        global::AngleSharp.Html.Parser.HtmlParser parser = new();
        IDocument document = parser.ParseDocument(html);
        Uri? baseUri = !string.IsNullOrWhiteSpace(pageUrl) && Uri.TryCreate(pageUrl, UriKind.Absolute, out Uri? resolvedBaseUri)
            ? resolvedBaseUri
            : null;
        INode container = document.Body ?? (INode?)document.DocumentElement ?? document;
        StringBuilder builder = new();

        foreach (INode node in container.ChildNodes) {
            AppendBlockNode(builder, node, baseUri, 0);
        }

        return NormalizeMarkdown(builder.ToString());
    }

    private static void AppendBlockNode(StringBuilder builder, INode node, Uri? baseUri, int listDepth) {
        if (node is IText textNode) {
            AppendParagraph(builder, NormalizeInlineText(textNode.Data));
            return;
        }

        if (node is not IElement element) {
            return;
        }

        string tagName = element.LocalName.ToLowerInvariant();
        switch (tagName) {
            case "h1":
            case "h2":
            case "h3":
            case "h4":
            case "h5":
            case "h6":
                int level = int.Parse(tagName.Substring(1), System.Globalization.CultureInfo.InvariantCulture);
                AppendParagraph(builder, new string('#', level) + " " + RenderInlineChildren(element, baseUri));
                return;
            case "p":
                AppendParagraph(builder, RenderInlineChildren(element, baseUri));
                return;
            case "ul":
                AppendList(builder, element.Children.Where(child => string.Equals(child.LocalName, "li", StringComparison.OrdinalIgnoreCase)), baseUri, listDepth, ordered: false);
                return;
            case "ol":
                AppendList(builder, element.Children.Where(child => string.Equals(child.LocalName, "li", StringComparison.OrdinalIgnoreCase)), baseUri, listDepth, ordered: true);
                return;
            case "pre":
                AppendCodeBlock(builder, element);
                return;
            case "blockquote":
                AppendBlockQuote(builder, element, baseUri);
                return;
            case "table":
                AppendTable(builder, element, baseUri);
                return;
            case "hr":
                AppendParagraph(builder, "---");
                return;
            case "script":
            case "style":
            case "noscript":
            case "template":
                return;
            default:
                foreach (INode child in element.ChildNodes) {
                    AppendBlockNode(builder, child, baseUri, listDepth);
                }
                return;
        }
    }

    private static void AppendList(StringBuilder builder, IEnumerable<IElement> items, Uri? baseUri, int listDepth, bool ordered) {
        int index = 1;
        foreach (IElement item in items) {
            string prefix = ordered ? index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ". " : "- ";
            string indent = new string(' ', listDepth * 2);
            string text = RenderListItem(item, baseUri, listDepth);
            if (!string.IsNullOrWhiteSpace(text)) {
                EnsureBlockSeparation(builder);
                builder.Append(indent);
                builder.Append(prefix);
                builder.AppendLine(text);
            }
            index++;
        }
        EnsureTrailingBlankLine(builder);
    }

    private static string RenderListItem(IElement item, Uri? baseUri, int listDepth) {
        StringBuilder builder = new();
        foreach (INode child in item.ChildNodes) {
            if (child is IElement childElement && (string.Equals(childElement.LocalName, "ul", StringComparison.OrdinalIgnoreCase) || string.Equals(childElement.LocalName, "ol", StringComparison.OrdinalIgnoreCase))) {
                if (builder.Length > 0 && builder[builder.Length - 1] != '\n') {
                    builder.AppendLine();
                }
                AppendList(builder, childElement.Children.Where(element => string.Equals(element.LocalName, "li", StringComparison.OrdinalIgnoreCase)), baseUri, listDepth + 1, ordered: string.Equals(childElement.LocalName, "ol", StringComparison.OrdinalIgnoreCase));
            } else {
                builder.Append(RenderInlineNode(child, baseUri));
            }
        }

        return NormalizeInlineText(builder.ToString());
    }

    private static void AppendCodeBlock(StringBuilder builder, IElement element) {
        string language = element.QuerySelector("code")?.GetAttribute("class")
            ?.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.StartsWith("language-", StringComparison.OrdinalIgnoreCase)
                ? value.Substring("language-".Length)
                : value.StartsWith("lang-", StringComparison.OrdinalIgnoreCase)
                    ? value.Substring("lang-".Length)
                    : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? string.Empty;
        string code = (element.TextContent ?? string.Empty).Replace("\r\n", "\n").TrimEnd();
        if (string.IsNullOrWhiteSpace(code)) {
            return;
        }

        EnsureBlockSeparation(builder);
        builder.Append("```");
        builder.AppendLine(language);
        builder.AppendLine(code);
        builder.AppendLine("```");
        builder.AppendLine();
    }

    private static void AppendBlockQuote(StringBuilder builder, IElement element, Uri? baseUri) {
        string content = NormalizeInlineText(RenderInlineChildren(element, baseUri));
        if (string.IsNullOrWhiteSpace(content)) {
            return;
        }

        EnsureBlockSeparation(builder);
        foreach (string line in content.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
            builder.Append("> ");
            builder.AppendLine(line.Trim());
        }
        builder.AppendLine();
    }

    private static void AppendTable(StringBuilder builder, IElement table, Uri? baseUri) {
        List<IElement> rows = table.QuerySelectorAll("tr").OfType<IElement>().ToList();
        if (rows.Count == 0) {
            return;
        }

        List<string[]> cells = rows
            .Select(row => row.Children
                .Where(cell => string.Equals(cell.LocalName, "th", StringComparison.OrdinalIgnoreCase) || string.Equals(cell.LocalName, "td", StringComparison.OrdinalIgnoreCase))
                .Select(cell => NormalizeInlineText(RenderInlineChildren(cell, baseUri)))
                .ToArray())
            .Where(row => row.Length > 0)
            .ToList();
        if (cells.Count == 0) {
            return;
        }

        int width = cells.Max(row => row.Length);
        string[] header = cells[0].Concat(Enumerable.Repeat(string.Empty, Math.Max(0, width - cells[0].Length))).ToArray();

        EnsureBlockSeparation(builder);
        builder.AppendLine("| " + string.Join(" | ", header) + " |");
        builder.AppendLine("| " + string.Join(" | ", Enumerable.Repeat("---", width)) + " |");
        foreach (string[] row in cells.Skip(1)) {
            string[] paddedRow = row.Concat(Enumerable.Repeat(string.Empty, Math.Max(0, width - row.Length))).ToArray();
            builder.AppendLine("| " + string.Join(" | ", paddedRow) + " |");
        }
        builder.AppendLine();
    }

    private static void AppendParagraph(StringBuilder builder, string? value) {
        string content = NormalizeInlineText(value);
        if (string.IsNullOrWhiteSpace(content)) {
            return;
        }

        EnsureBlockSeparation(builder);
        builder.AppendLine(content);
        builder.AppendLine();
    }

    private static string RenderInlineChildren(IElement element, Uri? baseUri) {
        StringBuilder builder = new();
        foreach (INode child in element.ChildNodes) {
            builder.Append(RenderInlineNode(child, baseUri));
        }

        return NormalizeInlineText(builder.ToString());
    }

    private static string RenderInlineNode(INode node, Uri? baseUri) {
        if (node is IText textNode) {
            return NormalizeInlineText(textNode.Data);
        }

        if (node is not IElement element) {
            return string.Empty;
        }

        return element.LocalName.ToLowerInvariant() switch {
            "strong" or "b" => WrapInline("**", RenderInlineChildren(element, baseUri)),
            "em" or "i" => WrapInline("*", RenderInlineChildren(element, baseUri)),
            "code" when element.Closest("pre") == null => WrapInline("`", NormalizeInlineText(element.TextContent)),
            "a" => RenderLink(element, baseUri),
            "br" => "\n",
            "img" => RenderImage(element, baseUri),
            "ul" or "ol" => string.Empty,
            _ => RenderInlineChildren(element, baseUri)
        };
    }

    private static string RenderLink(IElement element, Uri? baseUri) {
        string label = RenderInlineChildren(element, baseUri);
        string? href = element.GetAttribute("href");
        if (string.IsNullOrWhiteSpace(label)) {
            label = string.IsNullOrWhiteSpace(href) ? string.Empty : href;
        }
        if (string.IsNullOrWhiteSpace(href)) {
            return label;
        }

        return "[" + label + "](" + ResolveUrl(href, baseUri) + ")";
    }

    private static string RenderImage(IElement element, Uri? baseUri) {
        string? src = element.GetAttribute("src");
        if (string.IsNullOrWhiteSpace(src)) {
            return string.Empty;
        }

        string alt = NormalizeInlineText(element.GetAttribute("alt"));
        return "![" + alt + "](" + ResolveUrl(src, baseUri) + ")";
    }

    private static string ResolveUrl(string href, Uri? baseUri) {
        if (baseUri != null && Uri.TryCreate(baseUri, href, out Uri? resolvedUri)) {
            return resolvedUri.ToString();
        }

        return href;
    }

    private static string WrapInline(string delimiter, string? content) {
        string value = NormalizeInlineText(content);
        return string.IsNullOrWhiteSpace(value) ? string.Empty : delimiter + value + delimiter;
    }

    private static void EnsureBlockSeparation(StringBuilder builder) {
        if (builder.Length == 0) {
            return;
        }

        string current = builder.ToString();
        if (current.EndsWith("\n\n", StringComparison.Ordinal)) {
            return;
        }
        if (current.EndsWith("\n", StringComparison.Ordinal)) {
            builder.AppendLine();
            return;
        }

        builder.AppendLine();
        builder.AppendLine();
    }

    private static void EnsureTrailingBlankLine(StringBuilder builder) {
        if (builder.Length == 0) {
            return;
        }

        if (!builder.ToString().EndsWith("\n\n", StringComparison.Ordinal)) {
            builder.AppendLine();
        }
    }

    private static string NormalizeInlineText(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }

        return WhitespaceRegex.Replace(value.Replace("\r\n", "\n"), " ").Trim();
    }

    private static string NormalizeMarkdown(string value) {
        string normalized = value.Replace("\r\n", "\n").Trim();
        return ConsecutiveBlankLinesRegex.Replace(normalized, "\n\n");
    }
}
