using System;
using Jsbeautifier;
using System.IO;
using AngleSharp.Css.Parser;
using AngleSharp.Css;
using NUglify;
using NUglify.Html;
using System.Linq;

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

    /// <summary>
    /// Formats CSS using AngleSharp's <see cref="CssParser"/> and <see cref="CssStyleFormatter"/>.
    /// </summary>
    /// <param name="css">CSS content to format.</param>
    /// <returns>Formatted CSS string.</returns>
    public static string FormatCss(string css) {
        if (css == null) {
            throw new ArgumentNullException(nameof(css));
        }

        var parser = new CssParser();
        var sheet = parser.ParseStyleSheet(css);
        using var writer = new StringWriter();
        var formatter = new CssStyleFormatter();
        sheet.ToCss(writer, formatter);
        return writer.ToString();
    }

    /// <summary>
    /// Formats HTML markup using NUglify's <see cref="HtmlSettings"/>.
    /// </summary>
    /// <param name="html">HTML content to format.</param>
    /// <param name="indent">Indentation string to use.</param>
    /// <param name="blockStartLine">How blocks should start.</param>
    /// <param name="removeComments">Whether to remove HTML comments.</param>
    /// <param name="removeOptionalTags">Whether to remove optional tags.</param>
    /// <param name="outputTextNodesOnNewLine">Whether to output text nodes on a new line.</param>
    /// <param name="removeEmptyAttributes">Whether to remove empty attributes.</param>
    /// <param name="alphabeticallyOrderAttributes">Whether to order attributes alphabetically.</param>
    /// <param name="removeEmptyBlocks">Whether to remove empty CSS blocks.</param>
    /// <param name="isFragment">Treat input as HTML fragment.</param>
    /// <returns>Formatted HTML string.</returns>
    public static string FormatHtml(
        string html,
        string indent = "    ",
        BlockStart blockStartLine = BlockStart.SameLine,
        bool removeComments = false,
        bool removeOptionalTags = false,
        bool outputTextNodesOnNewLine = false,
        bool removeEmptyAttributes = false,
        bool alphabeticallyOrderAttributes = false,
        bool removeEmptyBlocks = false,
        bool isFragment = false) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        HtmlSettings settings = new();

        if (isFragment) {
            settings.IsFragmentOnly = true;
        }

        settings.RemoveOptionalTags = removeOptionalTags;
        settings.PrettyPrint = true;
        settings.Indent = indent;
        settings.OutputTextNodesOnNewLine = outputTextNodesOnNewLine;
        settings.RemoveEmptyAttributes = removeEmptyAttributes;
        settings.AlphabeticallyOrderAttributes = alphabeticallyOrderAttributes;
        settings.RemoveComments = removeComments;
        settings.RemoveQuotedAttributes = false;

        settings.JsSettings.MinifyCode = true;
        settings.JsSettings.OutputMode = OutputMode.MultipleLines;
        settings.JsSettings.Indent = indent;
        settings.JsSettings.BlocksStartOnSameLine = blockStartLine;
        settings.JsSettings.PreserveFunctionNames = true;
        settings.JsSettings.LocalRenaming = NUglify.JavaScript.LocalRenaming.KeepAll;
        settings.JsSettings.NoAutoRenameList = true.ToString();
        settings.JsSettings.PreserveFunctionNames = true;
        settings.JsSettings.ReorderScopeDeclarations = false;
        settings.JsSettings.TermSemicolons = true;
        settings.JsSettings.RemoveUnneededCode = false;
        settings.JsSettings.RemoveFunctionExpressionNames = false;

        settings.CssSettings.OutputMode = OutputMode.MultipleLines;
        settings.CssSettings.Indent = indent;
        settings.CssSettings.BlocksStartOnSameLine = blockStartLine;
        settings.CssSettings.RemoveEmptyBlocks = removeEmptyBlocks;
        settings.CssSettings.DecodeEscapes = false;

        var result = Uglify.Html(html, settings);
        if (result.HasErrors) {
            string errors = string.Join(", ", result.Errors.Select(e => e.ToString()));
            LoggingMessages.Logger.WriteWarning($"FormatHtml -Errors: {errors}");
        }

        return result.Code ?? string.Empty;
    }
}
