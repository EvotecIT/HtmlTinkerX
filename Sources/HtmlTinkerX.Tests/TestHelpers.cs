using System;
using System.IO;
using Xunit;

namespace PSParseHTML.Tests;

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
}