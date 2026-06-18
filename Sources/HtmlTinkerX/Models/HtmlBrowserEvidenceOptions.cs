using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Controls which artifacts are produced by a browser evidence export.
/// </summary>
public sealed class HtmlBrowserEvidenceOptions {
    /// <summary>Base file name used for page-level artifacts.</summary>
    public string BaseFileName { get; set; } = "page";

    /// <summary>Capture the current viewport as a PNG screenshot.</summary>
    public bool Screenshot { get; set; } = true;

    /// <summary>Capture the whole scrollable page as a PNG screenshot.</summary>
    public bool FullPageScreenshot { get; set; }

    /// <summary>Export a PDF print of the current page. Playwright supports this only for Chromium.</summary>
    public bool Pdf { get; set; }

    /// <summary>Write the rendered page HTML.</summary>
    public bool Html { get; set; } = true;

    /// <summary>Write visible browser text extracted from the rendered page.</summary>
    public bool VisibleText { get; set; } = true;

    /// <summary>Write Markdown converted from the rendered HTML.</summary>
    public bool Markdown { get; set; } = true;

    /// <summary>Write a redacted network-request summary without request or response headers.</summary>
    public bool NetworkSummary { get; set; }

    /// <summary>Write a redacted summary of SAML, WS-Federation, OAuth, or OpenID Connect handoff forms.</summary>
    public bool SsoHandoffSummary { get; set; }

    /// <summary>Mask common sensitive fields in visual artifacts such as screenshots and PDFs.</summary>
    public bool MaskSensitiveScreenshotElements { get; set; } = true;

    /// <summary>Additional selectors to mask in visual artifacts such as screenshots and PDFs.</summary>
    public IList<string> ScreenshotMaskSelectors { get; } = new List<string>();

    /// <summary>CSS color used for visual artifact masks.</summary>
    public string? ScreenshotMaskColor { get; set; }

    /// <summary>Redact common tokens, passwords, secrets, and sensitive URL query values from text artifacts and manifests.</summary>
    public bool RedactSensitiveValues { get; set; } = true;

    /// <summary>Write an evidence manifest that lists produced artifacts and hashes.</summary>
    public bool Manifest { get; set; } = true;
}
