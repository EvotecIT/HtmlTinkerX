using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlUtilities {
    /// <summary>Reads a text file without retaining more than the permitted number of decoded characters.</summary>
    /// <param name="path">File path, resolved using the usual path helpers.</param>
    /// <param name="maximumCharacters">Positive limit on decoded characters. BOM detection follows <see cref="StreamReader"/>.</param>
    /// <param name="cancellationToken">Cancellation covering file reads.</param>
    /// <returns>The decoded text within the limit.</returns>
    /// <exception cref="InvalidDataException">The decoded text exceeds the character limit.</exception>
    public static async Task<string> ReadFileCheckedAsync(string path, int maximumCharacters, CancellationToken cancellationToken) {
        if (maximumCharacters <= 0) throw new ArgumentOutOfRangeException(nameof(maximumCharacters));
        string fullPath = ResolvePath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException($"File not found: {path}", fullPath);
        cancellationToken.ThrowIfCancellationRequested();

        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        // Framework's StreamReader.ReadAsync has no cancellation-token overload.
        using CancellationTokenRegistration cancellation = cancellationToken.Register(() => stream.Dispose());
        var text = new StringBuilder(Math.Min(maximumCharacters, 8192));
        var buffer = new char[8192];
        try {
            while (true) {
                cancellationToken.ThrowIfCancellationRequested();
                int remaining = maximumCharacters - text.Length;
                int count = remaining < buffer.Length ? remaining + 1 : buffer.Length;
                int read = await reader.ReadAsync(buffer, 0, count).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (read == 0) return text.ToString();
                if (read > remaining) throw new InvalidDataException("File exceeds the configured character limit.");
                text.Append(buffer, 0, read);
            }
        } catch (Exception exception) when (cancellationToken.IsCancellationRequested &&
            (exception is IOException || exception is ObjectDisposedException)) {
            throw new OperationCanceledException("Text file reading was canceled.", exception, cancellationToken);
        }
    }
}
