using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Playwright;

namespace HtmlTinkerX;

/// <summary>
/// Provides helpers for parsing common cookie formats.
/// </summary>
public static class HtmlCookieParser {
    private static class CookieHelpers {
        public static SameSiteAttribute? ParseSameSite(string? value) {
            return value?.ToLowerInvariant() switch {
                "lax" => SameSiteAttribute.Lax,
                "strict" => SameSiteAttribute.Strict,
                "none" => SameSiteAttribute.None,
                _ => null
            };
        }

        public static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value) {
            foreach (JsonProperty property in element.EnumerateObject()) {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) {
                    value = property.Value;
                    return true;
                }
            }
            value = default;
            return false;
        }
    }

    /// <summary>
    /// Parses cookies from a Netscape HTTP cookie file.
    /// </summary>
    /// <param name="content">Cookie file content.</param>
    public static List<HtmlCookie> ParseNetscapeFile(string content) {
        if (content == null) {
            throw new ArgumentNullException(nameof(content));
        }
        List<HtmlCookie> list = new();
        string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string line in lines) {
            if (line.StartsWith("#", StringComparison.Ordinal)) {
                continue;
            }
            string[] parts = line.Split('\t');
            if (parts.Length < 7) {
                continue;
            }
            double? expires = null;
            if (double.TryParse(parts[4], out double d) && d != 0) {
                expires = d;
            }
            list.Add(new HtmlCookie {
                Domain = parts[0],
                Path = parts[2],
                Secure = parts[3].Equals("TRUE", StringComparison.OrdinalIgnoreCase),
                Expires = expires,
                Name = parts[5],
                Value = parts[6]
            });
        }
        return list;
    }

    /// <summary>
    /// Parses a Set-Cookie header value.
    /// </summary>
    /// <param name="header">Set-Cookie header string.</param>
    public static HtmlCookie ParseSetCookieHeader(string header) {
        if (header == null) {
            throw new ArgumentNullException(nameof(header));
        }
        if (header.StartsWith("Set-Cookie:", StringComparison.OrdinalIgnoreCase)) {
            header = header.Substring(header.IndexOf(':') + 1).Trim();
        }
        string[] parts = header.Split(';');
        HtmlCookie cookie = new();
        bool first = true;
        foreach (string part in parts) {
            string piece = part.Trim();
            if (first) {
                int idx = piece.IndexOf('=');
                if (idx > 0) {
                    cookie.Name = piece.Substring(0, idx);
                    cookie.Value = piece.Substring(idx + 1);
                }
                first = false;
                continue;
            }
            string[] kv = piece.Split(new[] { '=' }, 2);
            string key = kv[0].Trim().ToLowerInvariant();
            string? val = kv.Length > 1 ? kv[1].Trim() : null;
            switch (key) {
                case "path":
                    cookie.Path = val;
                    break;
                case "domain":
                    cookie.Domain = val;
                    break;
                case "expires":
                    if (DateTime.TryParse(val, out DateTime dt)) {
                        cookie.Expires = new DateTimeOffset(dt).ToUnixTimeSeconds();
                    }
                    break;
                case "secure":
                    cookie.Secure = val is null || val.Equals("true", StringComparison.OrdinalIgnoreCase);
                    break;
                case "httponly":
                    cookie.HttpOnly = val is null || val.Equals("true", StringComparison.OrdinalIgnoreCase);
                    break;
                case "samesite":
                    cookie.SameSite = CookieHelpers.ParseSameSite(val);
                    break;
            }
        }
        return cookie;
    }

    /// <summary>
    /// Parses a cookie from the org.json JSON string or cookie string.
    /// </summary>
    public static HtmlCookie ParseOrgJsonCookie(string input) {
        if (input == null) {
            throw new ArgumentNullException(nameof(input));
        }
        input = input.Trim();
        if (input.StartsWith("{", StringComparison.Ordinal)) {
            using JsonDocument doc = JsonDocument.Parse(input);
            JsonElement root = doc.RootElement;
            HtmlCookie cookie = new() {
                Path = root.TryGetProperty("Path", out var p) ? p.GetString() : null,
                Domain = root.TryGetProperty("Domain", out var d) ? d.GetString() : null,
                Name = root.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty,
                Value = root.TryGetProperty("value", out var v) ? v.GetString() ?? string.Empty : string.Empty,
                Secure = CookieHelpers.TryGetPropertyIgnoreCase(root, "Secure", out var s) ? s.GetString()?.Equals("true", StringComparison.OrdinalIgnoreCase) : null,
                HttpOnly = CookieHelpers.TryGetPropertyIgnoreCase(root, "HttpOnly", out var h) ? h.GetString()?.Equals("true", StringComparison.OrdinalIgnoreCase) : null
            };
            if (root.TryGetProperty("Expires", out var e) && DateTime.TryParse(e.GetString(), out DateTime dt)) {
                cookie.Expires = new DateTimeOffset(dt).ToUnixTimeSeconds();
            }
            return cookie;
        }
        return ParseSetCookieHeader(input);
    }

    /// <summary>
    /// Parses a JSON representation of a CookieStore entry.
    /// </summary>
    public static HtmlCookie ParseCookieStoreJson(string json) {
        if (json == null) {
            throw new ArgumentNullException(nameof(json));
        }
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        HtmlCookie cookie = new() {
            Domain = root.TryGetProperty("domain", out var d) && d.ValueKind != JsonValueKind.Null ? d.GetString() : null,
            Path = root.TryGetProperty("path", out var p) ? p.GetString() : null,
            Name = root.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty,
            Value = root.TryGetProperty("value", out var v) ? v.GetString() ?? string.Empty : string.Empty,
            Secure = root.TryGetProperty("secure", out var s) ? s.GetBoolean() : (bool?)null,
            HttpOnly = root.TryGetProperty("httpOnly", out var h) ? h.GetBoolean() : (bool?)null,
            SameSite = root.TryGetProperty("sameSite", out var ss) ? CookieHelpers.ParseSameSite(ss.GetString()) : null
        };
        if (root.TryGetProperty("expires", out var e)) {
            double exp = e.GetDouble();
            cookie.Expires = exp >= 1e12 ? exp / 1000.0 : exp;
        }
        return cookie;
    }

    /// <summary>
    /// Parses cookies from Puppeteer CookieParam or CookieData JSON.
    /// </summary>
    public static List<HtmlCookie> ParsePuppeteerJson(string json) {
        if (json == null) {
            throw new ArgumentNullException(nameof(json));
        }
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        List<HtmlCookie> list = new();
        if (root.ValueKind == JsonValueKind.Array) {
            foreach (JsonElement el in root.EnumerateArray()) {
                HtmlCookie c = new() {
                    Name = el.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty,
                    Value = el.TryGetProperty("value", out var v) ? v.GetString() ?? string.Empty : string.Empty,
                    Domain = el.TryGetProperty("domain", out var d) ? d.GetString() : null,
                    Path = el.TryGetProperty("path", out var p) ? p.GetString() : null,
                    Secure = el.TryGetProperty("secure", out var s) ? s.GetBoolean() : (bool?)null,
                    HttpOnly = el.TryGetProperty("httpOnly", out var h) ? h.GetBoolean() : (bool?)null
                };
                if (el.TryGetProperty("expires", out var e)) {
                    c.Expires = e.GetDouble();
                }
                list.Add(c);
            }
        }
        return list;
    }

    /// <summary>
    /// Converts cookies to the Netscape HTTP cookie file format.
    /// </summary>
    /// <param name="cookies">Collection of cookies to convert.</param>
    public static string ToNetscapeFile(IEnumerable<HtmlCookie> cookies) {
        if (cookies == null) {
            throw new ArgumentNullException(nameof(cookies));
        }
        StringBuilder sb = new();
        sb.AppendLine("# Netscape HTTP Cookie File");
        foreach (HtmlCookie cookie in cookies) {
            string domain = cookie.Domain ?? string.Empty;
            bool tail = domain.StartsWith(".", StringComparison.Ordinal);
            string flag = tail ? "TRUE" : "FALSE";
            string path = cookie.Path ?? "/";
            string secure = cookie.Secure == true ? "TRUE" : "FALSE";
            string expires = cookie.Expires.HasValue ? cookie.Expires.Value.ToString(CultureInfo.InvariantCulture) : "0";
            string name = cookie.Name ?? string.Empty;
            string value = cookie.Value ?? string.Empty;
            sb.Append(domain).Append('\t')
              .Append(flag).Append('\t')
              .Append(path).Append('\t')
              .Append(secure).Append('\t')
              .Append(expires).Append('\t')
              .Append(name).Append('\t')
              .Append(value).Append('\n');
        }
        return sb.ToString();
    }
}