using AngleSharp.Css;
using AngleSharp.Css.Parser;
using HtmlTinkerX.JavaScriptBeautifier;
using NUglify;
using NUglify.Html;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Provides helpers for formatting markup and script content.
/// </summary>
public static class HtmlFormatter {
    /// <summary>
    /// Formats JavaScript code using JsBeautifier.
    /// </summary>
    /// <param name="js">JavaScript code to format.</param>
    /// <param name="options">Optional Beautifier options object.</param>
    /// <returns>Formatted JavaScript string.</returns>
    /// <example>
    /// <code>
    /// string pretty = HtmlFormatter.FormatJavaScript("function x(){return 1;}");
    /// </code>
    /// </example>
    public static string FormatJavaScript(string js, BeautifierOptions? options = null) {
        if (js == null) {
            throw new ArgumentNullException(nameof(js));
        }

        BeautifierOptions opts = options ?? new BeautifierOptions {
            IndentSize = 4,
            IndentChar = ' ',
            IndentWithTabs = false,
            PreserveNewlines = true,
            MaxPreserveNewlines = 10,
            JslintHappy = false,
            BraceStyle = BraceStyle.Collapse,
            KeepArrayIndentation = false,
            KeepFunctionIndentation = false,
            EvalCode = false,
            BreakChainedMethods = false
        };

        Beautifier beautifier = new Beautifier(opts);
        return beautifier.Beautify(js);
    }

    /// <summary>
    /// Asynchronously formats JavaScript code using default JsBeautifier options.
    /// </summary>
    /// <param name="js">JavaScript code to format.</param>
    /// <returns>Formatted JavaScript string.</returns>
    public static Task<string> FormatJavaScriptAsync(string js)
        => FormatJavaScriptAsync(js, cancellationToken: CancellationToken.None);

    /// <summary>
    /// Asynchronously formats JavaScript code using default JsBeautifier options.
    /// </summary>
    /// <param name="js">JavaScript code to format.</param>
    /// <param name="cancellationToken">Token to observe cancellation requests.</param>
    /// <returns>Formatted JavaScript string.</returns>
    public static Task<string> FormatJavaScriptAsync(string js, CancellationToken cancellationToken)
        => FormatJavaScriptAsync(js, null, cancellationToken);

    /// <summary>
    /// Formats JavaScript code from a file using default JsBeautifier options.
    /// </summary>
    /// <param name="filePath">Path to the JavaScript file.</param>
    /// <param name="options">Optional Beautifier options object.</param>
    /// <returns>Formatted JavaScript string.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static string FormatJavaScriptFile(string filePath, BeautifierOptions? options = null) {
        string js = HtmlUtilities.ReadFileChecked(filePath);
        return FormatJavaScript(js, options);
    }

    /// <summary>
    /// Asynchronously formats JavaScript code using JsBeautifier.
    /// </summary>
    /// <param name="js">JavaScript code to format.</param>
    /// <param name="options">Optional Beautifier options object.</param>
    /// <returns>A task returning the formatted JavaScript string.</returns>
    public static Task<string> FormatJavaScriptAsync(string js, BeautifierOptions? options = null)
        => FormatJavaScriptAsync(js, options, CancellationToken.None);

    /// <summary>
    /// Asynchronously formats JavaScript code using JsBeautifier.
    /// </summary>
    /// <param name="js">JavaScript code to format.</param>
    /// <param name="options">Optional Beautifier options object.</param>
    /// <param name="cancellationToken">Token to observe cancellation requests.</param>
    /// <returns>A task returning the formatted JavaScript string.</returns>
    public static Task<string> FormatJavaScriptAsync(string js, BeautifierOptions? options, CancellationToken cancellationToken)
        => Task.Run(() => FormatJavaScript(js, options), cancellationToken);

    /// <summary>
    /// Asynchronously formats JavaScript code from a file using JsBeautifier.
    /// </summary>
    /// <param name="filePath">Path to the JavaScript file.</param>
    /// <param name="options">Optional Beautifier options object.</param>
    /// <returns>A task returning the formatted JavaScript string.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static Task<string> FormatJavaScriptFileAsync(string filePath, BeautifierOptions? options = null)
        => FormatJavaScriptFileAsync(filePath, options, CancellationToken.None);

    /// <summary>
    /// Asynchronously formats JavaScript code from a file using JsBeautifier.
    /// </summary>
    /// <param name="filePath">Path to the JavaScript file.</param>
    /// <param name="options">Optional Beautifier options object.</param>
    /// <param name="cancellationToken">Token to observe cancellation requests.</param>
    /// <returns>A task returning the formatted JavaScript string.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static async Task<string> FormatJavaScriptFileAsync(string filePath, BeautifierOptions? options, CancellationToken cancellationToken) {
        string js = await HtmlUtilities.ReadFileCheckedAsync(filePath, cancellationToken).ConfigureAwait(false);
        return await FormatJavaScriptAsync(js, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously formats JavaScript code from a file using default JsBeautifier options.
    /// </summary>
    /// <param name="filePath">Path to the JavaScript file.</param>
    /// <param name="cancellationToken">Token to observe cancellation requests.</param>
    /// <returns>Formatted JavaScript string.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static Task<string> FormatJavaScriptFileAsync(string filePath, CancellationToken cancellationToken)
        => FormatJavaScriptFileAsync(filePath, null, cancellationToken);

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
    /// Asynchronously formats CSS content using AngleSharp.
    /// </summary>
    /// <param name="css">CSS content to format.</param>
    /// <returns>Formatted CSS string.</returns>
    public static Task<string> FormatCssAsync(string css)
        => FormatCssAsync(css, CancellationToken.None);

    /// <summary>
    /// Asynchronously formats CSS content using AngleSharp.
    /// </summary>
    /// <param name="css">CSS content to format.</param>
    /// <param name="cancellationToken">Token to observe cancellation requests.</param>
    /// <returns>Formatted CSS string.</returns>
    public static Task<string> FormatCssAsync(string css, CancellationToken cancellationToken)
        => Task.Run(() => FormatCss(css), cancellationToken);

    /// <summary>
    /// Formats a CSS file using AngleSharp's <see cref="CssParser"/> and <see cref="CssStyleFormatter"/>.
    /// </summary>
    /// <param name="filePath">Path to the CSS file.</param>
    /// <returns>Formatted CSS string.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static string FormatCssFile(string filePath) {
        string css = HtmlUtilities.ReadFileChecked(filePath);
        return FormatCss(css);
    }

    /// <summary>
    /// Asynchronously formats a CSS file using AngleSharp.
    /// </summary>
    /// <param name="filePath">Path to the CSS file.</param>
    /// <returns>Formatted CSS string.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static Task<string> FormatCssFileAsync(string filePath)
        => FormatCssFileAsync(filePath, CancellationToken.None);

    /// <summary>
    /// Asynchronously formats a CSS file using AngleSharp.
    /// </summary>
    /// <param name="filePath">Path to the CSS file.</param>
    /// <param name="cancellationToken">Token to observe cancellation requests.</param>
    /// <returns>Formatted CSS string.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static async Task<string> FormatCssFileAsync(string filePath, CancellationToken cancellationToken) {
        string css = await HtmlUtilities.ReadFileCheckedAsync(filePath, cancellationToken).ConfigureAwait(false);
        return await FormatCssAsync(css, cancellationToken).ConfigureAwait(false);
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
        settings.RemoveAttributeQuotes = false;

        settings.JsSettings.MinifyCode = true;
        settings.JsSettings.OutputMode = OutputMode.MultipleLines;
        settings.JsSettings.Indent = indent;
        settings.JsSettings.BlocksStartOnSameLine = blockStartLine;
        settings.JsSettings.PreserveFunctionNames = true;
        settings.JsSettings.LocalRenaming = NUglify.JavaScript.LocalRenaming.KeepAll;
        settings.JsSettings.NoAutoRenameList = true.ToString();
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

    /// <summary>
    /// Asynchronously formats HTML markup using NUglify's <see cref="HtmlSettings"/>.
    /// </summary>
    /// <inheritdoc cref="FormatHtml(string,string,BlockStart,bool,bool,bool,bool,bool,bool,bool)"/>
    public static Task<string> FormatHtmlAsync(
        string html,
        string indent = "    ",
        BlockStart blockStartLine = BlockStart.SameLine,
        bool removeComments = false,
        bool removeOptionalTags = false,
        bool outputTextNodesOnNewLine = false,
        bool removeEmptyAttributes = false,
        bool alphabeticallyOrderAttributes = false,
        bool removeEmptyBlocks = false,
        bool isFragment = false)
        => FormatHtmlAsync(
            html,
            indent,
            blockStartLine,
            removeComments,
            removeOptionalTags,
            outputTextNodesOnNewLine,
            removeEmptyAttributes,
            alphabeticallyOrderAttributes,
            removeEmptyBlocks,
            isFragment,
            CancellationToken.None);

    /// <summary>
    /// Asynchronously formats HTML markup using default settings.
    /// </summary>
    /// <param name="html">HTML content to format.</param>
    /// <param name="cancellationToken">Token to observe cancellation requests.</param>
    /// <returns>Formatted HTML string.</returns>
    public static Task<string> FormatHtmlAsync(string html, CancellationToken cancellationToken)
        => FormatHtmlAsync(
            html,
            "    ",
            BlockStart.SameLine,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            cancellationToken);

    /// <summary>
    /// Asynchronously formats HTML markup using NUglify's <see cref="HtmlSettings"/>.
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
    /// <param name="cancellationToken">Token to observe cancellation requests.</param>
    /// <returns>Formatted HTML string.</returns>
    public static Task<string> FormatHtmlAsync(
        string html,
        string indent,
        BlockStart blockStartLine,
        bool removeComments,
        bool removeOptionalTags,
        bool outputTextNodesOnNewLine,
        bool removeEmptyAttributes,
        bool alphabeticallyOrderAttributes,
        bool removeEmptyBlocks,
        bool isFragment,
        CancellationToken cancellationToken)
        => Task.Run(() =>
            FormatHtml(
                html,
                indent,
                blockStartLine,
                removeComments,
                removeOptionalTags,
                outputTextNodesOnNewLine,
                removeEmptyAttributes,
                alphabeticallyOrderAttributes,
                removeEmptyBlocks,
                isFragment), cancellationToken);

    /// <summary>
    /// Formats HTML markup from a file using NUglify's <see cref="HtmlSettings"/>.
    /// </summary>
    /// <param name="filePath">Path to the HTML file.</param>
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
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static string FormatHtmlFile(
        string filePath,
        string indent = "    ",
        BlockStart blockStartLine = BlockStart.SameLine,
        bool removeComments = false,
        bool removeOptionalTags = false,
        bool outputTextNodesOnNewLine = false,
        bool removeEmptyAttributes = false,
        bool alphabeticallyOrderAttributes = false,
        bool removeEmptyBlocks = false,
        bool isFragment = false) {

        string html = HtmlUtilities.ReadFileChecked(filePath);
        return FormatHtml(html, indent, blockStartLine, removeComments, removeOptionalTags, outputTextNodesOnNewLine, removeEmptyAttributes, alphabeticallyOrderAttributes, removeEmptyBlocks, isFragment);
    }

    /// <summary>
    /// Asynchronously formats HTML markup from a file using NUglify's <see cref="HtmlSettings"/>.
    /// </summary>
    /// <inheritdoc cref="FormatHtmlFile(string,string,BlockStart,bool,bool,bool,bool,bool,bool,bool)"/>
    public static Task<string> FormatHtmlFileAsync(
        string filePath,
        string indent = "    ",
        BlockStart blockStartLine = BlockStart.SameLine,
        bool removeComments = false,
        bool removeOptionalTags = false,
        bool outputTextNodesOnNewLine = false,
        bool removeEmptyAttributes = false,
        bool alphabeticallyOrderAttributes = false,
        bool removeEmptyBlocks = false,
        bool isFragment = false)
        => FormatHtmlFileAsync(
            filePath,
            indent,
            blockStartLine,
            removeComments,
            removeOptionalTags,
            outputTextNodesOnNewLine,
            removeEmptyAttributes,
            alphabeticallyOrderAttributes,
            removeEmptyBlocks,
            isFragment,
            CancellationToken.None);

    /// <summary>
    /// Asynchronously formats HTML markup from a file using default settings.
    /// </summary>
    /// <param name="filePath">Path to the HTML file.</param>
    /// <param name="cancellationToken">Token to observe cancellation requests.</param>
    /// <returns>Formatted HTML string.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static Task<string> FormatHtmlFileAsync(string filePath, CancellationToken cancellationToken)
        => FormatHtmlFileAsync(
            filePath,
            "    ",
            BlockStart.SameLine,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            cancellationToken);

    /// <summary>
    /// Asynchronously formats HTML markup from a file using NUglify's <see cref="HtmlSettings"/>.
    /// </summary>
    /// <param name="filePath">Path to the HTML file.</param>
    /// <param name="indent">Indentation string to use.</param>
    /// <param name="blockStartLine">How blocks should start.</param>
    /// <param name="removeComments">Whether to remove HTML comments.</param>
    /// <param name="removeOptionalTags">Whether to remove optional tags.</param>
    /// <param name="outputTextNodesOnNewLine">Whether to output text nodes on a new line.</param>
    /// <param name="removeEmptyAttributes">Whether to remove empty attributes.</param>
    /// <param name="alphabeticallyOrderAttributes">Whether to order attributes alphabetically.</param>
    /// <param name="removeEmptyBlocks">Whether to remove empty CSS blocks.</param>
    /// <param name="isFragment">Treat input as HTML fragment.</param>
    /// <param name="cancellationToken">Token to observe cancellation requests.</param>
    /// <returns>Formatted HTML string.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static async Task<string> FormatHtmlFileAsync(
        string filePath,
        string indent,
        BlockStart blockStartLine,
        bool removeComments,
        bool removeOptionalTags,
        bool outputTextNodesOnNewLine,
        bool removeEmptyAttributes,
        bool alphabeticallyOrderAttributes,
        bool removeEmptyBlocks,
        bool isFragment,
        CancellationToken cancellationToken) {

        string html = await HtmlUtilities.ReadFileCheckedAsync(filePath, cancellationToken).ConfigureAwait(false);
        return await FormatHtmlAsync(
            html,
            indent,
            blockStartLine,
            removeComments,
            removeOptionalTags,
            outputTextNodesOnNewLine,
            removeEmptyAttributes,
            alphabeticallyOrderAttributes,
            removeEmptyBlocks,
            isFragment,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously inlines CSS in the provided HTML string using PreMailer.Net.
    /// </summary>
    /// <param name="html">HTML markup to process.</param>
    /// <param name="options">Optional processing options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTML with CSS inlined.</returns>
    public static async Task<string> FormatHtmlInlineCssAsync(
        string html,
        PreMailerOptions? options = null,
        CancellationToken cancellationToken = default) {
        try {
            PreMailerResult result = await PreMailerClient
                .MoveCssInlineAsync(html, options, cancellationToken)
                .ConfigureAwait(false);
            return result.Html;
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception ex) when (options?.DownloadRemoteCss == true) {
            LoggingMessages.Logger.WriteWarning(
                "Failed to inline remote CSS: {0}", ex.Message);
            return html;
        }
    }
}
