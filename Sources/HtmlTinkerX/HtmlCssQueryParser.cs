using AngleSharp.Css.Dom;
using AngleSharp.Css.Parser;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HtmlTinkerX;

/// <summary>Parses CSS for rule, declaration, custom property, URL, and selector-specificity workflows.</summary>
public static class HtmlCssQueryParser {
    /// <summary>Selects CSS style rules from source text.</summary>
    public static IReadOnlyList<HtmlCssRuleMatch> SelectRules(string css, string? selector = null, bool contains = false) {
        ICssStyleSheet sheet = ParseStyleSheet(css);
        List<HtmlCssRuleMatch> matches = new();
        int index = 0;
        foreach ((ICssRule Rule, string? Context) item in EnumerateRules(sheet.Rules, null)) {
            if (item.Rule is not ICssStyleRule styleRule) {
                continue;
            }

            if (!Matches(styleRule.SelectorText, selector, contains)) {
                continue;
            }

            matches.Add(new HtmlCssRuleMatch {
                Index = index++,
                Selector = styleRule.SelectorText,
                RuleType = item.Rule.Type.ToString(),
                Context = item.Context,
                CssText = item.Rule.CssText
            });
        }

        return matches;
    }

    /// <summary>Selects CSS declarations from source text.</summary>
    public static IReadOnlyList<HtmlCssDeclarationMatch> SelectDeclarations(string css, string? property = null, string? selector = null, bool contains = false) {
        ICssStyleSheet sheet = ParseStyleSheet(css);
        List<HtmlCssDeclarationMatch> matches = new();
        int index = 0;
        foreach ((ICssRule Rule, string? Context) item in EnumerateRules(sheet.Rules, null)) {
            string? selectorText = GetDeclarationRuleSelector(item.Rule);
            if (selectorText == null || !Matches(selectorText, selector, contains)) {
                continue;
            }

            foreach (ICssProperty declaration in GetRuleDeclarations(item.Rule)) {
                bool matchesProperty = declaration.Name.StartsWith("--", StringComparison.Ordinal)
                    ? MatchesCssCustomProperty(declaration.Name, property, contains)
                    : Matches(declaration.Name, property, contains);
                if (!matchesProperty) {
                    continue;
                }

                matches.Add(new HtmlCssDeclarationMatch {
                    Index = index++,
                    Selector = selectorText,
                    Property = declaration.Name,
                    Value = declaration.Value,
                    Important = declaration.IsImportant,
                    Context = item.Context
                });
            }
        }

        return matches;
    }

    /// <summary>Selects CSS custom properties from source text.</summary>
    public static IReadOnlyList<HtmlCssVariableMatch> SelectVariables(string css, string? name = null, bool contains = false) {
        List<HtmlCssVariableMatch> matches = new();
        int index = 0;
        foreach (HtmlCssDeclarationMatch declaration in SelectDeclarations(css, null, null, contains: false)) {
            if (!declaration.Property.StartsWith("--", StringComparison.Ordinal) || !MatchesCssCustomProperty(declaration.Property, name, contains)) {
                continue;
            }

            matches.Add(new HtmlCssVariableMatch {
                Index = index++,
                Selector = declaration.Selector,
                Name = declaration.Property,
                Value = declaration.Value,
                Context = declaration.Context
            });
        }

        return matches;
    }

    private static bool MatchesCssCustomProperty(string value, string? pattern, bool contains) {
        if (string.IsNullOrEmpty(pattern)) {
            return true;
        }

        return contains ? value.IndexOf(pattern, StringComparison.Ordinal) >= 0 : string.Equals(value, pattern, StringComparison.Ordinal);
    }

    /// <summary>Extracts URL references from CSS declarations and imports.</summary>
    public static IReadOnlyList<HtmlCssUrlReference> SelectUrls(string css, Uri? baseUri = null) {
        ICssStyleSheet sheet = ParseStyleSheet(css);
        List<HtmlCssUrlReference> references = new();
        int index = 0;
        foreach ((ICssRule Rule, string? Context) item in EnumerateRules(sheet.Rules, null)) {
            if (item.Rule is ICssImportRule importRule && !string.IsNullOrWhiteSpace(importRule.Href)) {
                references.Add(CreateUrlReference(index++, null, "@import", importRule.Href, baseUri, item.Context));
                continue;
            }

            if (item.Rule is ICssStyleRule styleRule) {
                foreach (ICssProperty declaration in styleRule.Style) {
                    foreach (string url in ExtractCssUrls(declaration.Value)) {
                        references.Add(CreateUrlReference(index++, styleRule.SelectorText, declaration.Name, url, baseUri, item.Context));
                    }
                }

                continue;
            }

            if (item.Rule is IEnumerable<ICssProperty> declarationRule) {
                string? selector = GetDeclarationRuleSelector(item.Rule);
                foreach (ICssProperty declaration in declarationRule) {
                    foreach (string url in ExtractCssUrls(declaration.Value)) {
                        references.Add(CreateUrlReference(index++, selector, declaration.Name, url, baseUri, item.Context));
                    }
                }
            }
        }

        return references;
    }

    /// <summary>Measures selector specificity using AngleSharp's selector parser.</summary>
    public static IReadOnlyList<HtmlCssSpecificity> MeasureSpecificity(IEnumerable<string> selectors) {
        if (selectors == null) {
            throw new ArgumentNullException(nameof(selectors));
        }

        List<HtmlCssSpecificity> result = new();
        CssParser parser = new();
        foreach (string selector in selectors.Where(static item => !string.IsNullOrWhiteSpace(item))) {
            ICssStyleSheet sheet = parser.ParseStyleSheet($"{selector} {{ color: inherit; }}");
            ICssStyleRule? rule = sheet.Rules.OfType<ICssStyleRule>().FirstOrDefault();
            if (rule == null) {
                continue;
            }

            AngleSharp.Css.Priority specificity = rule.Selector.Specificity;
            result.Add(new HtmlCssSpecificity {
                Selector = selector,
                Inline = specificity.Inlines,
                Id = specificity.Ids,
                Class = specificity.Classes,
                Element = specificity.Tags,
                Value = $"{specificity.Inlines},{specificity.Ids},{specificity.Classes},{specificity.Tags}"
            });
        }

        return result;
    }

    private static ICssStyleSheet ParseStyleSheet(string css) {
        if (css == null) {
            throw new ArgumentNullException(nameof(css));
        }

        CssParser parser = new();
        return parser.ParseStyleSheet(css);
    }

    private static IEnumerable<(ICssRule Rule, string? Context)> EnumerateRules(ICssRuleList rules, string? context) {
        foreach (ICssRule rule in rules) {
            string? nextContext = CreateContext(rule, context);
            yield return (rule, context);

            if (rule is ICssGroupingRule groupingRule) {
                foreach ((ICssRule Rule, string? Context) child in EnumerateRules(groupingRule.Rules, nextContext)) {
                    yield return child;
                }
            }
        }
    }

    private static string? CreateContext(ICssRule rule, string? parentContext) {
        string? current = rule switch {
            ICssMediaRule mediaRule => $"@media {mediaRule.Media.MediaText}",
            ICssSupportsRule supportsRule => $"@supports {supportsRule.Condition}",
            _ => null
        };

        if (string.IsNullOrEmpty(parentContext)) {
            return current;
        }

        return string.IsNullOrEmpty(current) ? parentContext : $"{parentContext} / {current}";
    }

    private static bool Matches(string value, string? filter, bool contains) {
        if (string.IsNullOrEmpty(filter)) {
            return true;
        }

        return contains
            ? value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
            : string.Equals(value, filter, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> ExtractCssUrls(string value) {
        string cssValue = value ?? string.Empty;
        bool inString = false;
        char quote = '\0';
        for (int index = 0; index < cssValue.Length; index++) {
            char current = cssValue[index];
            if (inString) {
                if (current == '\\') {
                    index++;
                } else if (current == quote) {
                    inString = false;
                }

                continue;
            }

            if (current == '"' || current == '\'') {
                inString = true;
                quote = current;
                continue;
            }

            if (!StartsWithCssUrlFunction(cssValue, index)) {
                continue;
            }

            int openIndex = index + 3;
            int closeIndex = FindCssUrlFunctionEnd(cssValue, openIndex + 1);
            if (closeIndex < 0) {
                yield break;
            }

            string url = cssValue.Substring(openIndex + 1, closeIndex - openIndex - 1).Trim().Trim('"', '\'');
            if (url.Length > 0) {
                yield return url;
            }

            index = closeIndex;
        }
    }

    private static bool StartsWithCssUrlFunction(string value, int index) {
        if (index > 0 && (char.IsLetterOrDigit(value[index - 1]) || value[index - 1] == '-' || value[index - 1] == '_')) {
            return false;
        }

        if (index + 3 >= value.Length || !value.Substring(index, 3).Equals("url", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        return value[index + 3] == '(';
    }

    private static int FindCssUrlFunctionEnd(string value, int start) {
        bool inString = false;
        char quote = '\0';
        for (int index = start; index < value.Length; index++) {
            char current = value[index];
            if (inString) {
                if (current == '\\') {
                    index++;
                } else if (current == quote) {
                    inString = false;
                }

                continue;
            }

            if (current == '"' || current == '\'') {
                inString = true;
                quote = current;
                continue;
            }

            if (current == ')') {
                return index;
            }
        }

        return -1;
    }

    private static string? GetDeclarationRuleSelector(ICssRule rule) {
        if (rule is ICssStyleRule styleRule) {
            return styleRule.SelectorText;
        }

        if (rule is not IEnumerable<ICssProperty>) {
            return null;
        }

        return rule.Type == CssRuleType.FontFace ? "@font-face" : $"@{rule.Type}";
    }

    private static IEnumerable<ICssProperty> GetRuleDeclarations(ICssRule rule) {
        if (rule is ICssStyleRule styleRule) {
            return styleRule.Style;
        }

        return rule is IEnumerable<ICssProperty> declarationRule ? declarationRule : Enumerable.Empty<ICssProperty>();
    }

    private static HtmlCssUrlReference CreateUrlReference(int index, string? selector, string property, string url, Uri? baseUri, string? context) {
        string? resolvedUrl = null;
        bool isValid = Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out Uri? parsed);
        if (isValid) {
            Uri resolved = parsed!.IsAbsoluteUri
                ? parsed
                : baseUri != null
                    ? new Uri(baseUri, parsed)
                    : parsed;
            resolvedUrl = resolved.ToString();
        }

        return new HtmlCssUrlReference {
            Index = index,
            Selector = selector,
            Property = property,
            Url = url,
            ResolvedUrl = resolvedUrl,
            IsValidUrl = isValid,
            Context = context
        };
    }
}
