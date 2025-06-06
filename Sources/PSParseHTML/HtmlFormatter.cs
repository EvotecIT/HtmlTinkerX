using System;
using Jsbeautifier;

namespace PSParseHTML;

/// <summary>
/// Provides helpers for formatting markup and script content.
/// </summary>
public static class HtmlFormatter {
    /// <summary>
    /// Formats JavaScript code using default JsBeautifier options.
    /// </summary>
    /// <param name="js">JavaScript code to format.</param>
    /// <returns>Formatted JavaScript string.</returns>
    public static string FormatJavaScript(string js) {
        if (js == null) {
            throw new ArgumentNullException(nameof(js));
        }

        var beautifier = new Beautifier();
        beautifier.Opts.IndentSize = 4;
        beautifier.Opts.IndentChar = ' ';
        beautifier.Opts.IndentWithTabs = false;
        beautifier.Opts.PreserveNewlines = true;
        beautifier.Opts.MaxPreserveNewlines = 10;
        beautifier.Opts.JslintHappy = false;
        beautifier.Opts.BraceStyle = BraceStyle.Collapse;
        beautifier.Opts.KeepArrayIndentation = false;
        beautifier.Opts.KeepFunctionIndentation = false;
        beautifier.Opts.EvalCode = false;
        beautifier.Opts.BreakChainedMethods = false;

        return beautifier.Beautify(js);
    }
}
