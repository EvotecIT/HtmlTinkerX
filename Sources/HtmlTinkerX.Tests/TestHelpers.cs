using System;
using System.IO;
using System.Globalization;
using Xunit;

namespace HtmlTinkerX.Tests;

/// <summary>
/// Helper methods for unit tests.
/// </summary>
internal static class TestHelpers {
    /// <summary>
    /// Gets the full path to a test document file.
    /// </summary>
    /// <param name="name">The name of the document file.</param>
    /// <returns>The full path to the document.</returns>
    public static string GetDocumentPath(string name) {
        string baseDir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Documents", name));
    }

    /// <summary>
    /// Normalizes line endings to Unix-style (\n) for consistent testing across platforms.
    /// </summary>
    /// <param name="text">The text to normalize.</param>
    /// <returns>Text with normalized line endings.</returns>
    public static string NormalizeLineEndings(string text)
        => text.Replace("\r\n", "\n");

    /// <summary>
    /// Asserts that two strings are equal, ignoring line ending differences.
    /// </summary>
    /// <param name="expected">The expected string.</param>
    /// <param name="actual">The actual string.</param>
    public static void EqualIgnoringLineEndings(string expected, string actual) {
        Assert.Equal(NormalizeLineEndings(expected), NormalizeLineEndings(actual));
    }

    /// <summary>
    /// Executes an action using the specified culture and restores the original culture afterwards.
    /// </summary>
    /// <param name="cultureName">The culture to apply for the duration of the action.</param>
    /// <param name="action">The action to execute.</param>
    public static void WithCulture(string cultureName, Action action) {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUICulture = CultureInfo.CurrentUICulture;
        try {
            var culture = new CultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            action();
        } finally {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUICulture;
        }
    }
}