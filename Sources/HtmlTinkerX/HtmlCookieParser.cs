using System;
using System.Collections.Generic;
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
            long? expires = null;
            if (long.TryParse(parts[4], out long l) && l != 0) {
                expires = l;
            }
            list.Add(new HtmlCookie {
                Domain = parts[0],
                Path = parts[2],
                Secure = parts[3].Equals("TRUE", StringComparison.OrdinalIgnoreCase),
                Expires = expires.HasValue ? (float?)expires.Value : null,
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
                        cookie.Expires = (float)new DateTimeOffset(dt).ToUnixTimeSeconds();
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
                Path = CookieHelpers.TryGetPropertyIgnoreCase(root, "Path", out var p) ? p.GetString() : null,
                Domain = CookieHelpers.TryGetPropertyIgnoreCase(root, "Domain", out var d) ? d.GetString() : null,
                Name = CookieHelpers.TryGetPropertyIgnoreCase(root, "name", out var n) ? n.GetString() ?? string.Empty : string.Empty,
                Value = CookieHelpers.TryGetPropertyIgnoreCase(root, "value", out var v) ? v.GetString() ?? string.Empty : string.Empty,
                Secure = CookieHelpers.TryGetPropertyIgnoreCase(root, "Secure", out var s) ? s.GetString()?.Equals("true", StringComparison.OrdinalIgnoreCase) : null,
                HttpOnly = CookieHelpers.TryGetPropertyIgnoreCase(root, "HttpOnly", out var h) ? h.GetString()?.Equals("true", StringComparison.OrdinalIgnoreCase) : null
            };
            if (CookieHelpers.TryGetPropertyIgnoreCase(root, "Expires", out var e) && DateTime.TryParse(e.GetString(), out DateTime dt)) {
                cookie.Expires = (float)new DateTimeOffset(dt).ToUnixTimeSeconds();
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
            Domain = CookieHelpers.TryGetPropertyIgnoreCase(root, "domain", out var d) && d.ValueKind != JsonValueKind.Null ? d.GetString() : null,
            Path = CookieHelpers.TryGetPropertyIgnoreCase(root, "path", out var p) ? p.GetString() : null,
            Name = CookieHelpers.TryGetPropertyIgnoreCase(root, "name", out var n) ? n.GetString() ?? string.Empty : string.Empty,
            Value = CookieHelpers.TryGetPropertyIgnoreCase(root, "value", out var v) ? v.GetString() ?? string.Empty : string.Empty,
            Secure = CookieHelpers.TryGetPropertyIgnoreCase(root, "secure", out var s) ? s.GetBoolean() : (bool?)null,
            HttpOnly = CookieHelpers.TryGetPropertyIgnoreCase(root, "httpOnly", out var h) ? h.GetBoolean() : (bool?)null,
            SameSite = CookieHelpers.TryGetPropertyIgnoreCase(root, "sameSite", out var ss) ? CookieHelpers.ParseSameSite(ss.GetString()) : null
        };
        if (CookieHelpers.TryGetPropertyIgnoreCase(root, "expires", out var e)) {
            double exp = e.GetDouble();
            cookie.Expires = (float)(exp >= 1e12 ? exp / 1000.0 : exp);
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
                    Name = CookieHelpers.TryGetPropertyIgnoreCase(el, "name", out var n) ? n.GetString() ?? string.Empty : string.Empty,
                    Value = CookieHelpers.TryGetPropertyIgnoreCase(el, "value", out var v) ? v.GetString() ?? string.Empty : string.Empty,
                    Domain = CookieHelpers.TryGetPropertyIgnoreCase(el, "domain", out var d) ? d.GetString() : null,
                    Path = CookieHelpers.TryGetPropertyIgnoreCase(el, "path", out var p) ? p.GetString() : null,
                    Secure = CookieHelpers.TryGetPropertyIgnoreCase(el, "secure", out var s) ? s.GetBoolean() : (bool?)null,
                    HttpOnly = CookieHelpers.TryGetPropertyIgnoreCase(el, "httpOnly", out var h) ? h.GetBoolean() : (bool?)null
                };
                if (CookieHelpers.TryGetPropertyIgnoreCase(el, "expires", out var e)) {
                    c.Expires = (float)e.GetDouble();
                }
                list.Add(c);
            }
        }
        return list;
    }
}
