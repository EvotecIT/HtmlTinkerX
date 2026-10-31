using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HtmlTinkerX;

// Temporary protection for CSS values that NUglify currently changes unsafely.
internal sealed class CssMathProtection {
    private readonly string _prefix;
    private readonly List<KeyValuePair<string, string>> _values = new();

    private CssMathProtection(string css) {
        do { _prefix = "--htx-" + Guid.NewGuid().ToString("N") + "-"; }
        while (css.IndexOf(_prefix, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    internal static CssMathProtection Create(string css, out string protectedCss) {
        var protection = new CssMathProtection(css);
        protectedCss = protection.Protect(css);
        return protection;
    }

    internal bool HasProtectedValues => _values.Count != 0;

    internal bool TryRestore(string css, out string restored) {
        foreach (var value in _values) {
            int index = css.IndexOf(value.Key, StringComparison.Ordinal);
            if (index < 0 || css.IndexOf(value.Key, index + value.Key.Length, StringComparison.Ordinal) >= 0) {
                restored = string.Empty;
                return false;
            }
            css = css.Substring(0, index) + value.Value + css.Substring(index + value.Key.Length);
        }
        restored = css;
        return true;
    }

    private string Mask(string value) {
        string marker = "var(" + _prefix + _values.Count.ToString(CultureInfo.InvariantCulture) + ")";
        _values.Add(new KeyValuePair<string, string>(marker, value));
        return marker;
    }

    private string MaskName(string value) {
        string marker = _prefix + _values.Count.ToString(CultureInfo.InvariantCulture) + "x";
        _values.Add(new KeyValuePair<string, string>(marker, value));
        return marker;
    }

    private string Protect(string css) {
        var output = new StringBuilder(css.Length);
        int index = 0;
        bool declarationStart = true;
        while (index < css.Length) {
            int start = index;
            if (SkipTriviaOrString(css, ref index)) {
                output.Append(css, start, index - start);
                continue;
            }
            if (declarationStart && TryCustomPropertyValue(css, index, out int valueStart, out int valueEnd)) {
                int nameEnd = index;
                ReadName(css, ref nameEnd);
                string propertyName = css.Substring(index, nameEnd - index);
                output.Append(propertyName.IndexOf('\\') >= 0 ? MaskName(propertyName) : propertyName);
                output.Append(css, nameEnd, valueStart - nameEnd);
                output.Append(Mask(css.Substring(valueStart, valueEnd - valueStart)));
                index = valueEnd;
                declarationStart = false;
                continue;
            }
            if (IsNameStart(css[index])) {
                string name = ReadName(css, ref index);
                if (index < css.Length && css[index] == '(' && IsMath(name) && TryBalanced(css, index, out int end)) {
                    output.Append(Mask(css.Substring(start, end - start)));
                    index = end;
                } else {
                    output.Append(css, start, index - start);
                }
                declarationStart = false;
                continue;
            }
            char current = css[index++];
            output.Append(current);
            if (current == '{' || current == '}' || current == ';') declarationStart = true;
            else if (!char.IsWhiteSpace(current)) declarationStart = false;
        }
        return output.ToString();
    }

    private static bool TryCustomPropertyValue(string css, int start, out int valueStart, out int valueEnd) {
        valueStart = valueEnd = start;
        if (!IsNameStart(css[start])) return false;
        int index = start;
        string name = ReadName(css, ref index);
        if (!name.StartsWith("--", StringComparison.Ordinal)) return false;
        while (index < css.Length) {
            if (char.IsWhiteSpace(css[index])) { index++; continue; }
            if (css[index] == '/' && index + 1 < css.Length && css[index + 1] == '*') {
                SkipTriviaOrString(css, ref index); continue;
            }
            break;
        }
        if (index >= css.Length || css[index] != ':') return false;
        valueStart = ++index;
        int depth = 0;
        while (index < css.Length) {
            if (SkipTriviaOrString(css, ref index)) continue;
            if (css[index] == '\\' && index + 1 < css.Length) { index += 2; continue; }
            char ch = css[index];
            if (ch == '(' || ch == '[' || ch == '{') depth++;
            else if (ch == ')' || ch == ']') depth--;
            else if (ch == '}') { if (depth == 0) break; depth--; }
            else if (ch == ';' && depth == 0) break;
            index++;
        }
        valueEnd = index;
        return valueEnd > valueStart;
    }

    private static bool TryBalanced(string css, int start, out int end) {
        int depth = 0;
        int index = start;
        while (index < css.Length) {
            if (SkipTriviaOrString(css, ref index)) continue;
            if (css[index] == '\\' && index + 1 < css.Length) { index += 2; continue; }
            if (css[index] == '(') depth++;
            else if (css[index] == ')' && --depth == 0) { end = index + 1; return true; }
            index++;
        }
        end = start;
        return false;
    }

    private static bool SkipTriviaOrString(string text, ref int index) {
        if (text[index] == '/' && index + 1 < text.Length && text[index + 1] == '*') {
            int end = text.IndexOf("*/", index + 2, StringComparison.Ordinal);
            index = end < 0 ? text.Length : end + 2;
            return true;
        }
        if (text[index] != '\'' && text[index] != '"') return false;
        char quote = text[index++];
        while (index < text.Length) {
            char ch = text[index++];
            if (ch == '\\' && index < text.Length) index++;
            else if (ch == quote) break;
        }
        return true;
    }

    private static bool IsNameStart(char ch) => char.IsLetter(ch) || ch == '_' || ch == '-' || ch == '\\' || ch >= 128;

    private static string ReadName(string text, ref int index) {
        var name = new StringBuilder();
        while (index < text.Length) {
            char ch = text[index];
            if (ch == '\\' && index + 1 >= text.Length) { name.Append(ch); index++; }
            else if (ch == '\\' && index + 1 < text.Length) {
                index++;
                int value = 0, digits = 0;
                while (index < text.Length && digits < 6 && Uri.IsHexDigit(text[index])) {
                    value = value * 16 + int.Parse(text[index++].ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    digits++;
                }
                if (digits > 0) {
                    if (index < text.Length && char.IsWhiteSpace(text[index])) {
                        if (text[index++] == '\r' && index < text.Length && text[index] == '\n') index++;
                    }
                    name.Append(value > 0 && value <= 0xffff ? (char)value : '\ufffd');
                } else name.Append(text[index++]);
            } else if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch >= 128) {
                name.Append(ch); index++;
            } else break;
        }
        return name.ToString();
    }

    private static bool IsMath(string name) {
        switch (name.ToLowerInvariant()) {
            case "calc": case "min": case "max": case "clamp": case "round": case "mod": case "rem":
            case "abs": case "sign": case "sin": case "cos": case "tan": case "asin": case "acos":
            case "atan": case "atan2": case "pow": case "sqrt": case "hypot": case "log": case "exp":
                return true;
            default: return false;
        }
    }
}
