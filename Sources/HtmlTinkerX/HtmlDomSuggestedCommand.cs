using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HtmlTinkerX;

/// <summary>Suggested-command formatting for static DOM selector discovery.</summary>
public static partial class HtmlDomExtraction {
    private static void EnsureUniquePropertyNames(IReadOnlyList<HtmlDomSelectorFieldCandidate> fields) {
        Dictionary<string, int> counts = new(StringComparer.OrdinalIgnoreCase);
        foreach (HtmlDomSelectorFieldCandidate field in fields) {
            string baseName = string.IsNullOrWhiteSpace(field.Name) ? "Value" : field.Name;
            if (!counts.TryGetValue(baseName, out int count)) {
                counts[baseName] = 1;
                field.Name = baseName;
                continue;
            }

            count++;
            counts[baseName] = count;
            field.Name = baseName + count;
        }
    }

    private static SuggestedCommandBuildResult BuildSuggestedCommand(
        string itemSelector,
        IReadOnlyList<HtmlDomSelectorFieldCandidate> fields,
        HtmlDomCommandSource source) {
        SuggestedCommandBuildResult sourceResult = BuildCommandSource(source);
        StringBuilder builder = new();
        builder.Append("Select-HtmlData ")
            .Append(sourceResult.Command)
            .Append(" -ItemSelector '")
            .Append(EscapePowerShellSingleQuotedString(itemSelector))
            .AppendLine("' -Property @{");
        foreach (HtmlDomSelectorFieldCandidate field in fields
            .Where(IsUsefulSuggestedField)
            .OrderBy(GetSuggestedFieldOrder)
            .ThenByDescending(static field => field.Score)
            .Take(8)) {
            builder.Append("    ").Append(field.Name).Append(" = ");
            if (string.IsNullOrWhiteSpace(field.Attribute)) {
                builder.Append('\'')
                    .Append(EscapePowerShellSingleQuotedString(field.Selector))
                    .AppendLine("'");
            } else {
                builder.Append("@{ Selector = '")
                    .Append(EscapePowerShellSingleQuotedString(field.Selector))
                    .Append("'; Attribute = '")
                    .Append(EscapePowerShellSingleQuotedString(field.Attribute))
                    .AppendLine("' }");
            }
        }

        sourceResult.Command = builder.Append('}').ToString();
        return sourceResult;
    }

    private static SuggestedCommandBuildResult BuildCommandSource(HtmlDomCommandSource source) {
        StringBuilder builder = new();
        List<string> prerequisites = new();
        if (source.Url != null) {
            string redactedUrl = HtmlSensitiveValueRedactor.RedactSensitiveQueryValues(source.Url.AbsoluteUri);
            if (redactedUrl.Equals(source.Url.AbsoluteUri, StringComparison.Ordinal)) {
                builder.Append("-Url '")
                    .Append(EscapePowerShellSingleQuotedString(source.Url.AbsoluteUri))
                    .Append('\'');
            } else {
                builder.Append("-Url $Url");
                prerequisites.Add("set $Url to the original URL because its query contains sensitive values");
            }
        } else if (!string.IsNullOrWhiteSpace(source.Path)) {
            builder.Append("-Path '")
                .Append(EscapePowerShellSingleQuotedString(source.Path!))
                .Append('\'');
        } else {
            string expression = string.IsNullOrWhiteSpace(source.ContentExpression)
                ? "$html"
                : source.ContentExpression;
            builder.Append("-Content ").Append(expression);
            prerequisites.Add($"set {expression} to the HTML content");
        }

        if (source.Url == null && source.BaseUri != null) {
            builder.Append(" -BaseUrl '")
                .Append(EscapePowerShellSingleQuotedString(source.BaseUri.AbsoluteUri))
                .Append('\'');
        }

        if (!string.IsNullOrWhiteSpace(source.UserAgent)) {
            builder.Append(" -UserAgent '")
                .Append(EscapePowerShellSingleQuotedString(source.UserAgent!))
                .Append('\'');
        }

        if (!string.IsNullOrWhiteSpace(source.Proxy)) {
            if (Uri.TryCreate(source.Proxy, UriKind.Absolute, out Uri? proxyUri)
                && string.IsNullOrEmpty(proxyUri!.UserInfo)) {
                builder.Append(" -Proxy '")
                    .Append(EscapePowerShellSingleQuotedString(source.Proxy!))
                    .Append('\'');
            } else {
                builder.Append(" -Proxy $Proxy");
                prerequisites.Add("set $Proxy to the proxy address");
            }
        }
        if (source.UsesProxyCredential) {
            builder.Append(" -ProxyCredential $ProxyCredential");
            prerequisites.Add("set $ProxyCredential to the proxy credential");
        }
        if (source.UsesHeaders) {
            builder.Append(" -Header $Header");
            prerequisites.Add("set $Header to the request headers");
        }

        return new SuggestedCommandBuildResult {
            Command = builder.ToString(),
            IsReplayable = prerequisites.Count == 0,
            Note = prerequisites.Count == 0
                ? string.Empty
                : "Secure command template: " + string.Join("; ", prerequisites) + "."
        };
    }

    private static bool IsUsefulSuggestedField(HtmlDomSelectorFieldCandidate field) {
        if (ContainsSemanticToken(field.Name, "Fraction", "Separator", "Currency", "Whole")) {
            return false;
        }

        return !Regex.IsMatch(field.Name, @"\d+$", RegexOptions.CultureInvariant)
            || field.Attribute.Equals("href", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetSuggestedFieldOrder(HtmlDomSelectorFieldCandidate field) {
        if (ContainsSemanticToken(field.Name, "Title", "Name", "Heading")) return 0;
        if (field.Name.Equals("SellPrice", StringComparison.OrdinalIgnoreCase)) return 1;
        if (ContainsSemanticToken(field.Name, "Price", "Amount", "Cost")) return 2;
        if (field.Attribute.Equals("href", StringComparison.OrdinalIgnoreCase)) return 3;
        if (field.Attribute.Equals("src", StringComparison.OrdinalIgnoreCase)
            || field.Attribute.Equals("data-src", StringComparison.OrdinalIgnoreCase)) return 4;
        return 5;
    }

    private static string EscapePowerShellSingleQuotedString(string value) =>
        (value ?? string.Empty).Replace("'", "''");

    private sealed class SuggestedCommandBuildResult {
        internal string Command { get; set; } = string.Empty;
        internal bool IsReplayable { get; set; }
        internal string Note { get; set; } = string.Empty;
    }
}
