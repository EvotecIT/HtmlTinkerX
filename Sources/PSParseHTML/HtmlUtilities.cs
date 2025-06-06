using System;
using System.Linq;

namespace PSParseHTML;

/// <summary>
/// Utility helpers for working with HTML content.
/// </summary>
public static class HtmlUtilities {
    /// <summary>
    /// Converts HTML markup to plain text using NUglify.
    /// </summary>
    /// <param name="html">HTML string to convert.</param>
    /// <returns>Plain text extracted from the provided HTML.</returns>
    public static string ConvertToText(string html) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }
        var result = NUglify.Uglify.HtmlToText(html);
        if (result.HasErrors) {
            string errors = string.Join(", ", result.Errors.Select(e => e.ToString()));
            LoggingMessages.Logger.WriteWarning($"Convert-HTMLToText -Errors: {errors}");
        }
        return result.Code ?? string.Empty;
    }
}
