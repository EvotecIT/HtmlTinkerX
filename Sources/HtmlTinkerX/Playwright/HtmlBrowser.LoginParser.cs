using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using System;
using System.Linq;

namespace HtmlTinkerX;

/// <summary>
/// Provides HTML parsing helpers for login form detection.
/// </summary>
public static class HtmlLoginParser {
    /// <summary>
    /// Attempts to detect a login form in raw HTML markup.
    /// </summary>
    /// <param name="html">HTML markup to analyze.</param>
    /// <param name="url">Optional URL associated with the markup.</param>
    /// <returns>Information about the login form or <c>null</c> if not found.</returns>
    public static HtmlFormLogin? Detect(string html, string? url = null) {
        if (html is null) {
            throw new ArgumentNullException(nameof(html));
        }

        var parser = new global::AngleSharp.Html.Parser.HtmlParser();
        IDocument doc = parser.ParseDocument(html);
        IElement? pwd = doc
            .QuerySelectorAll("input[type='password']")
            .FirstOrDefault(p => !IsHidden(p));
        if (pwd is null) {
            return null;
        }

        IElement? form = pwd.Closest("form");
        if (form is null) {
            return null;
        }

        IElement? user = form.QuerySelector("input[type='text'],input[type='email'],input[name*='user' i],input[name*='login' i]");
        IElement? submit = form.QuerySelector("input[type='submit'],button[type='submit'],button:not([type])");

        return new HtmlFormLogin {
            LoginUrl = url ?? string.Empty,
            UsernameSelector = ToSelector(user),
            PasswordSelector = ToSelector(pwd),
            SubmitSelector = ToSelector(submit)
        };
    }

    private static string ToSelector(IElement? el) {
        if (el is null) {
            return string.Empty;
        }

        string sel = el.TagName.ToLowerInvariant();
        string? id = el.Id;
        if (!string.IsNullOrEmpty(id)) {
            return sel + "#" + CssEscape(id!);
        }

        string? name = el.GetAttribute("name");
        if (!string.IsNullOrEmpty(name)) {
            return $"{sel}[name='{CssStringEscape(name!)}']";
        }

        string cls = el.ClassName ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(cls)) {
            string escaped = string.Join(".", cls.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(s => CssEscape(s.Trim())));
            return sel + "." + escaped;
        }

        return sel;
    }

    private static bool IsHidden(IElement el) {
        if (el.HasAttribute("hidden")) {
            return true;
        }

        string? aria = el.GetAttribute("aria-hidden");
        if (string.Equals(aria, "true", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        string? style = el.GetAttribute("style");
        if (!string.IsNullOrEmpty(style)) {
            style = style!.ToLowerInvariant();
            if (style.Contains("display:none") || style.Contains("visibility:hidden") || style.Contains("opacity:0")) {
                return true;
            }
        }

        return false;
    }

    private static string CssEscape(string value) {
        System.Text.StringBuilder sb = new(value.Length);
        foreach (char c in value) {
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) {
                sb.Append(c);
            } else {
                sb.Append('\\').Append(c);
            }
        }
        return sb.ToString();
    }

    private static string CssStringEscape(string value) =>
        value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("'", "\\'");
}