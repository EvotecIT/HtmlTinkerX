using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlCrawler {
    private static Task WriteLinesAtomicallyAsync(string path, Func<StreamWriter, Task> write, CancellationToken cancellationToken) =>
        HtmlUtilities.WriteAtomicallyAsync(path, async (stream, token) => {
            using StreamWriter writer = new(stream, new UTF8Encoding(false), 8192, leaveOpen: true);
            await write(writer).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            await writer.FlushAsync().ConfigureAwait(false);
        }, cancellationToken);

    private static Task WriteJsonAtomicallyAsync<T>(string path, T value, JsonSerializerOptions options, CancellationToken cancellationToken) =>
        HtmlUtilities.WriteAtomicallyAsync(path, (stream, token) => JsonSerializer.SerializeAsync(stream, value, options, token), cancellationToken);
}
