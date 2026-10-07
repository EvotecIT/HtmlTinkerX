using AngleSharp.Dom;
using System;

namespace HtmlTinkerX;

/// <summary>HTTP action resolution shared by form parsing and hidden-form relays.</summary>
internal static class HtmlFormUrlUtilities {
    internal static bool TryResolveAction(string action, Uri baseUri, out Uri actionUri) {
        if (string.IsNullOrWhiteSpace(action)) {
            actionUri = baseUri;
            return IsHttpUri(baseUri);
        }

        if (HasExplicitScheme(action)) {
            if (Uri.TryCreate(action, UriKind.Absolute, out Uri? absoluteUri)) {
                if (IsHttpUri(absoluteUri)) {
                    actionUri = absoluteUri;
                    return true;
                }

                actionUri = null!;
                return false;
            }

            actionUri = null!;
            return false;
        }

        if (Uri.TryCreate(baseUri, action, out Uri? resolved)) {
            actionUri = resolved;
            return IsHttpUri(resolved);
        }

        actionUri = null!;
        return false;
    }

    private static bool IsHttpUri(Uri uri) =>
        uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
        || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static bool HasExplicitScheme(string value) {
        int colonIndex = value.IndexOf(':');
        if (colonIndex <= 0) {
            return false;
        }

        for (int index = 0; index < colonIndex; index++) {
            char c = value[index];
            bool valid = char.IsLetterOrDigit(c) || c == '+' || c == '-' || c == '.';
            if (!valid) {
                return false;
            }
        }

        return char.IsLetter(value[0]);
    }

    internal static Uri? GetEffectiveBaseUri(IDocument document, Uri? responseUri) {
        string? href = document.QuerySelector("base[href]")?.GetAttribute("href");
        if (!string.IsNullOrWhiteSpace(href)) {
            Uri? resolved;
            bool parsed = responseUri == null
                ? Uri.TryCreate(href, UriKind.Absolute, out resolved)
                : Uri.TryCreate(responseUri, href, out resolved);
            if (parsed && resolved != null
                && !resolved.Scheme.Equals("data", StringComparison.OrdinalIgnoreCase)
                && !resolved.Scheme.Equals("javascript", StringComparison.OrdinalIgnoreCase)) {
                return resolved;
            }
        }
        return responseUri;
    }

}
