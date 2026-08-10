namespace HtmlTinkerX;

/// <summary>Identifies the source used for a browser-backed PDF capture.</summary>
public enum HtmlBrowserPdfSourceKind {
    /// <summary>An absolute URL navigated by the browser.</summary>
    Url,
    /// <summary>An HTML string loaded into an isolated browser page.</summary>
    Html,
    /// <summary>A local HTML file navigated by the browser.</summary>
    File
}
