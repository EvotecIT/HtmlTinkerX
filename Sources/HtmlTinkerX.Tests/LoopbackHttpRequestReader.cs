using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX.Tests;

internal static class LoopbackHttpRequestReader {
    private const int MaximumRequestBytes = 1024 * 1024;

    internal static async Task<string> ReadAsync(NetworkStream stream, CancellationToken cancellationToken) {
        using MemoryStream received = new();
        byte[] buffer = new byte[8192];
        int expectedLength = -1;
        while (received.Length < MaximumRequestBytes) {
            int read = await stream.ReadAsync(buffer, 0, Math.Min(buffer.Length, MaximumRequestBytes - (int)received.Length), cancellationToken);
            if (read == 0) break;
            received.Write(buffer, 0, read);
            byte[] bytes = received.ToArray();
            int headerEnd = FindHeaderEnd(bytes);
            if (headerEnd < 0) continue;
            if (expectedLength < 0) {
                string headers = Encoding.ASCII.GetString(bytes, 0, headerEnd);
                int contentLength = 0;
                foreach (string line in headers.Split(new[] { "\r\n" }, StringSplitOptions.None)) {
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) {
                        int.TryParse(line.Substring(line.IndexOf(':') + 1).Trim(), out contentLength);
                        break;
                    }
                }
                expectedLength = headerEnd + 4 + contentLength;
            }
            if (received.Length >= expectedLength) break;
        }
        return Encoding.ASCII.GetString(received.ToArray());
    }

    private static int FindHeaderEnd(byte[] bytes) {
        for (int index = 0; index <= bytes.Length - 4; index++) {
            if (bytes[index] == 13 && bytes[index + 1] == 10 && bytes[index + 2] == 13 && bytes[index + 3] == 10) return index;
        }
        return -1;
    }
}
