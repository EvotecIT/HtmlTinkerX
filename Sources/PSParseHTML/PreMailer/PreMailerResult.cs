using System.Collections.Generic;

namespace PSParseHTML;

/// <summary>
/// Result returned after processing HTML with PreMailer.
/// </summary>
public class PreMailerResult {
    /// <summary>
    /// HTML output with CSS inlined.
    /// </summary>
    public string Html { get; }

    /// <summary>
    /// Any warnings returned by PreMailer.
    /// </summary>
    public List<string> Warnings { get; }

    public PreMailerResult(string html, List<string> warnings) {
        Html = html;
        Warnings = warnings;
    }
}
