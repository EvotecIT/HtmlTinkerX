namespace HtmlTinkerX;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Immutable Chromium print options for browser-backed PDF capture.</summary>
public sealed class HtmlBrowserPdfOptions {
    /// <summary>Initializes PDF print options.</summary>
    public HtmlBrowserPdfOptions(
        bool landscape = false,
        bool printBackground = true,
        PdfPageFormat? format = PdfPageFormat.A4,
        string? width = null,
        string? height = null,
        string? marginTop = null,
        string? marginRight = null,
        string? marginBottom = null,
        string? marginLeft = null,
        string? pageRanges = null,
        float? scale = null,
        bool displayHeaderFooter = false,
        string? headerTemplate = null,
        string? footerTemplate = null,
        bool preferCssPageSize = false,
        bool outline = false,
        bool tagged = false,
        bool maskSensitiveElements = false,
        IEnumerable<string>? maskSelectors = null,
        string? maskColor = null) {
        if (scale.HasValue && (scale.Value < 0.1f || scale.Value > 2f)) {
            throw new ArgumentOutOfRangeException(nameof(scale), "PDF scale must be between 0.1 and 2.0.");
        }

        Landscape = landscape;
        PrintBackground = printBackground;
        Format = !string.IsNullOrWhiteSpace(width) || !string.IsNullOrWhiteSpace(height) ? null : format;
        Width = width;
        Height = height;
        MarginTop = marginTop;
        MarginRight = marginRight;
        MarginBottom = marginBottom;
        MarginLeft = marginLeft;
        PageRanges = pageRanges;
        Scale = scale;
        DisplayHeaderFooter = displayHeaderFooter;
        HeaderTemplate = headerTemplate;
        FooterTemplate = footerTemplate;
        PreferCssPageSize = preferCssPageSize;
        Outline = outline;
        Tagged = tagged;
        MaskSensitiveElements = maskSensitiveElements;
        MaskSelectors = Array.AsReadOnly((maskSelectors ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray());
        MaskColor = maskColor;
    }

    /// <summary>Gets whether pages use landscape orientation.</summary>
    public bool Landscape { get; }
    /// <summary>Gets whether background graphics are printed.</summary>
    public bool PrintBackground { get; }
    /// <summary>Gets the standard paper format, or <see langword="null"/> when a custom width or height is supplied.</summary>
    public PdfPageFormat? Format { get; }
    /// <summary>Gets the custom paper width.</summary>
    public string? Width { get; }
    /// <summary>Gets the custom paper height.</summary>
    public string? Height { get; }
    /// <summary>Gets the top margin.</summary>
    public string? MarginTop { get; }
    /// <summary>Gets the right margin.</summary>
    public string? MarginRight { get; }
    /// <summary>Gets the bottom margin.</summary>
    public string? MarginBottom { get; }
    /// <summary>Gets the left margin.</summary>
    public string? MarginLeft { get; }
    /// <summary>Gets the page ranges to print.</summary>
    public string? PageRanges { get; }
    /// <summary>Gets the print scale.</summary>
    public float? Scale { get; }
    /// <summary>Gets whether header and footer templates are shown.</summary>
    public bool DisplayHeaderFooter { get; }
    /// <summary>Gets the header HTML template.</summary>
    public string? HeaderTemplate { get; }
    /// <summary>Gets the footer HTML template.</summary>
    public string? FooterTemplate { get; }
    /// <summary>Gets whether CSS <c>@page</c> sizing takes precedence.</summary>
    public bool PreferCssPageSize { get; }
    /// <summary>Gets whether Chromium generates a document outline.</summary>
    public bool Outline { get; }
    /// <summary>Gets whether Chromium generates a tagged PDF.</summary>
    public bool Tagged { get; }
    /// <summary>Gets whether common sensitive controls are masked before printing.</summary>
    public bool MaskSensitiveElements { get; }
    /// <summary>Gets additional selectors to mask before printing.</summary>
    public IReadOnlyList<string> MaskSelectors { get; }
    /// <summary>Gets the CSS color used for masks.</summary>
    public string? MaskColor { get; }
}
