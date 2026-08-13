namespace HtmlTinkerX;

using System;

/// <summary>Thrown when a browser PDF renderer has no active or queued capacity.</summary>
public sealed class HtmlBrowserPdfCapacityException : InvalidOperationException {
    /// <summary>Initializes the exception.</summary>
    public HtmlBrowserPdfCapacityException(string message) : base(message) { }
}
