using HtmlTinkerX;
using System;
using System.IO;
using Xunit;

namespace PSParseHTML.Tests;

internal static class TestHelpers {
    public static string GetDocumentPath(string name) {
        string baseDir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Documents", name));
    }

    public static string NormalizeLineEndings(string text)
        => text.Replace("\r\n", "\n");

    public static void EqualIgnoringLineEndings(string expected, string actual) {
        Assert.Equal(NormalizeLineEndings(expected), NormalizeLineEndings(actual));
    }
}