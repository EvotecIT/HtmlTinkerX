namespace HtmlTinkerX;

using System;

public static partial class HtmlBrowser {
    /// <summary>
    /// Creates a file URI for a direct local path that is safe to pass to a browser.
    /// </summary>
    /// <param name="path">Local file path to validate and normalize.</param>
    /// <returns>An absolute file URI.</returns>
    /// <exception cref="ArgumentException">
    /// The path is empty, invalid, remote, device-backed, mapped, substituted, or traverses
    /// a Windows symbolic link, junction, or other reparse point.
    /// </exception>
    public static Uri CreateLocalFileUri(string path) {
        return new Uri(HtmlBrowserFileSystemPath.GetValidatedLocalPath(path));
    }
}
