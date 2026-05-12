using HtmlAgilityPack;
using System;

namespace HtmlTinkerX;

/// <summary>
/// Provides HTML entity encoding and decoding helpers without requiring callers to reference HtmlAgilityPack directly.
/// </summary>
public static class HtmlEntityUtility {
    /// <summary>
    /// Decodes HTML entities in <paramref name="text"/>.
    /// </summary>
    /// <param name="text">Text containing HTML entities.</param>
    /// <returns>Decoded text.</returns>
    public static string DeEntitize(string text) {
        if (text == null) {
            throw new ArgumentNullException(nameof(text));
        }

        return HtmlEntity.DeEntitize(text);
    }

    /// <summary>
    /// Encodes HTML-sensitive characters in <paramref name="text"/> as HTML entities.
    /// </summary>
    /// <param name="text">Text to encode.</param>
    /// <returns>HTML entity encoded text.</returns>
    public static string Entitize(string text) {
        if (text == null) {
            throw new ArgumentNullException(nameof(text));
        }

        return HtmlEntity.Entitize(text, useNames: true, entitizeQuotAmpAndLtGt: true);
    }
}
