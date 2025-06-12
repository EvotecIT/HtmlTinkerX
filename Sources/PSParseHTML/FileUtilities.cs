using System;
using System.IO;

namespace PSParseHTML {
    /// <summary>
    /// Helper methods for working with file paths.
    /// </summary>
    internal static class FileUtilities {
        /// <summary>
        /// Resolves the provided path to an absolute file system path.
        /// Environment variables are expanded and relative paths are
        /// converted to full paths.
        /// </summary>
        /// <param name="path">File system path to resolve.</param>
        /// <returns>Absolute file path.</returns>
        /// <exception cref="ArgumentException">Thrown when path is null or empty.</exception>
        public static string ResolvePath(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Path cannot be null or empty", nameof(path));
            }
            string expanded = Environment.ExpandEnvironmentVariables(path);
            return Path.GetFullPath(expanded);
        }

        /// <summary>
        /// Reads the contents of a file after verifying that it exists.
        /// </summary>
        /// <param name="path">Path to the file.</param>
        /// <returns>File contents.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
        public static string ReadFileChecked(string path) {
            string fullPath = ResolvePath(path);
            if (!File.Exists(fullPath)) {
                throw new FileNotFoundException($"File not found: {path}", fullPath);
            }
            return File.ReadAllText(fullPath);
        }
    }
}
