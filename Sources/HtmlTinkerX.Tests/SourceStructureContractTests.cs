using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HtmlTinkerX.Tests;

public class SourceStructureContractTests {
    private const int MaximumLines = 1000;

    private static readonly HashSet<string> SourceExtensions = new(StringComparer.OrdinalIgnoreCase) {
        ".cs", ".csproj", ".css", ".js", ".ps1", ".psd1", ".psm1", ".props", ".targets", ".ts"
    };

    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase) {
        ".git", "Artefacts", "bin", "Documents", "node_modules", "obj", "packages"
    };

    [Fact]
    public void HandAuthoredSourceFiles_DoNotExceedMaintainabilityLimit() {
        string repositoryRoot = FindRepositoryRoot();
        List<string> oversized = Directory
            .EnumerateFiles(repositoryRoot, "*", SearchOption.AllDirectories)
            .Where(IsHandAuthoredSource)
            .Select(path => new { Path = path, Lines = CountLines(path) })
            .Where(item => item.Lines > MaximumLines)
            .OrderByDescending(item => item.Lines)
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .Select(item => $"{item.Path.Substring(repositoryRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)}: {item.Lines} lines")
            .ToList();

        Assert.True(
            oversized.Count == 0,
            $"Hand-authored source files must stay at or below {MaximumLines} lines.{Environment.NewLine}{string.Join(Environment.NewLine, oversized)}");
    }

    private static bool IsHandAuthoredSource(string path) {
        if (!SourceExtensions.Contains(Path.GetExtension(path))) {
            return false;
        }

        return !path
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => ExcludedDirectories.Contains(segment));
    }

    private static int CountLines(string path) {
        int count = 0;
        using StreamReader reader = File.OpenText(path);
        while (reader.ReadLine() != null) {
            count++;
        }
        return count;
    }

    private static string FindRepositoryRoot() {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null) {
            if (File.Exists(Path.Combine(directory.FullName, "PSParseHTML.psd1"))) {
                return directory.FullName;
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the PSParseHTML repository root.");
    }
}
