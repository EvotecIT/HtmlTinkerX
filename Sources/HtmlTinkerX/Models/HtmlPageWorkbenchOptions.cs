using System;

namespace HtmlTinkerX;

/// <summary>
/// Options controlling static page workbench analysis.
/// </summary>
public sealed class HtmlPageWorkbenchOptions {
    /// <summary>Optional page URL used to resolve relative URLs and produce suggested commands.</summary>
    public Uri? BaseUri { get; set; }

    /// <summary>Includes the original HTML in the workbench result.</summary>
    public bool IncludeHtml { get; set; } = true;

    /// <summary>Optional rendered snapshot used to enrich the page workbench with browser-produced content.</summary>
    public HtmlRenderedPageSnapshot? RenderedSnapshot { get; set; }

    /// <summary>Compares static HTML with rendered HTML when a rendered snapshot is provided.</summary>
    public bool IncludeStaticRenderedComparison { get; set; } = true;

    /// <summary>Downloads same-origin linked JavaScript files and inspects them for endpoint hints.</summary>
    public bool IncludeLinkedScripts { get; set; }

    /// <summary>Allows linked-script endpoint inspection to download cross-origin scripts.</summary>
    public bool IncludeExternalLinkedScripts { get; set; }

    /// <summary>Runs the shared correctness, safety, and accessibility audit against the primary static or rendered document.</summary>
    public bool IncludeDocumentAudit { get; set; } = true;

    /// <summary>Optional settings for the shared document audit.</summary>
    public HtmlDocumentAuditOptions? DocumentAuditOptions { get; set; }
}
