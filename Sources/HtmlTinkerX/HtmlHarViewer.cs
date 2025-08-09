using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Represents a HAR (HTTP Archive) document.
/// </summary>
public sealed class Har {
    /// <summary>Top-level log data.</summary>
    public HarLog? Log { get; set; }
}

/// <summary>HAR log root.</summary>
public sealed class HarLog {
    /// <summary>Log version.</summary>
    public string? Version { get; set; }

    /// <summary>Creator info.</summary>
    public HarCreator? Creator { get; set; }

    /// <summary>List of network entries.</summary>
    public HarEntry[]? Entries { get; set; }
}

/// <summary>Creator metadata.</summary>
public sealed class HarCreator {
    /// <summary>Name of the tool.</summary>
    public string? Name { get; set; }

    /// <summary>Tool version.</summary>
    public string? Version { get; set; }
}

/// <summary>HAR network entry.</summary>
public sealed class HarEntry {
    /// <summary>Request timestamp.</summary>
    public DateTime StartedDateTime { get; set; }

    /// <summary>Request details.</summary>
    public HarRequest? Request { get; set; }

    /// <summary>Response details.</summary>
    public HarResponse? Response { get; set; }
}

/// <summary>Request information.</summary>
public sealed class HarRequest {
    /// <summary>HTTP method.</summary>
    public string? Method { get; set; }

    /// <summary>Request URL.</summary>
    public string? Url { get; set; }
}

/// <summary>Response information.</summary>
public sealed class HarResponse {
    /// <summary>Status code.</summary>
    public int Status { get; set; }
}

/// <summary>
/// Utility methods for working with HAR files.
/// </summary>
public static class HtmlHarViewer {
    /// <summary>
    /// Reads a HAR file from disk.
    /// </summary>
    /// <param name="path">Path to the HAR file.</param>
    /// <returns>Parsed <see cref="Har"/> instance.</returns>
    /// <example>
    /// <code>
    /// Har har = await HtmlHarViewer.ReadHarAsync("session.har");
    /// </code>
    /// </example>
    public static async Task<Har> ReadHarAsync(string path) {
        if (!File.Exists(path)) {
            throw new FileNotFoundException($"File not found: {path}", path);
        }
        string json = await HtmlUtilities.ReadFileCheckedAsync(path).ConfigureAwait(false);
        var opts = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        try {
            Har? har = JsonSerializer.Deserialize<Har>(json, opts);
            return har ?? throw new InvalidDataException("Invalid HAR content");
        } catch (JsonException e) {
            throw new InvalidDataException("Invalid HAR content", e);
        }
    }

    /// <summary>
    /// Generates a simple HTML viewer for the provided HAR object.
    /// </summary>
    /// <param name="har">HAR data.</param>
    /// <returns>HTML string.</returns>
    /// <example>
    /// <code>
    /// Har har = await HtmlHarViewer.ReadHarAsync("session.har");
    /// string html = HtmlHarViewer.BuildViewerHtml(har);
    /// await File.WriteAllTextAsync("har.html", html);
    /// </code>
    /// </example>
    public static string BuildViewerHtml(Har har) {
        var opts = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.Default
        };
        string json = JsonSerializer.Serialize(har, opts);
        return $$"""
<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8' />
<title>HAR Viewer</title>
<style>
body { font-family: Arial, sans-serif; }
table { border-collapse: collapse; width: 100%; }
th, td { border: 1px solid #ccc; padding: 4px; text-align: left; }
thead { background: #eee; }
</style>
</head>
<body>
<table>
<thead>
<tr><th>Method</th><th>URL</th><th>Status</th></tr>
</thead>
<tbody id='entries'></tbody>
</table>
<script type='application/json' id='har-data'>{{json}}</script>
<script>
const har = JSON.parse(document.getElementById('har-data').textContent);
const entries = (har.log && har.log.entries) || [];
const tbody = document.getElementById('entries');
for (const e of entries) {
    const tr = document.createElement('tr');
    const m = e.request ? e.request.method : '';
    const u = e.request ? e.request.url : '';
    const s = e.response ? e.response.status : '';
    tr.innerHTML = `<td>${m}</td><td>${u}</td><td>${s}</td>`;
    tbody.appendChild(tr);
}
</script>
</body>
        </html>
""";
    }

    /// <summary>
    /// Writes the provided HAR data to the given stream.
    /// </summary>
    /// <param name="har">HAR object to serialize.</param>
    /// <param name="outputStream">Destination stream.</param>
    /// <returns>A task that completes when writing is finished.</returns>
    /// <example>
    /// <code>
    /// await using FileStream fs = File.Create("copy.har");
    /// await HtmlHarViewer.WriteHarAsync(har, fs);
    /// </code>
    /// </example>
    public static async Task WriteHarAsync(Har har, Stream outputStream) {
        if (har == null) {
            throw new ArgumentNullException(nameof(har));
        }
        if (outputStream == null) {
            throw new ArgumentNullException(nameof(outputStream));
        }

        if (har.Log == null) {
            har.Log = new HarLog();
        }

        var opts = new JsonSerializerOptions {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        string json = JsonSerializer.Serialize(har, opts);
#if NETSTANDARD2_0 || NETFRAMEWORK
        using (var writer = new StreamWriter(outputStream, new System.Text.UTF8Encoding(false), 1024, leaveOpen: true)) {
            await writer.WriteAsync(json).ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);
        }
#else
        await using var writer = new StreamWriter(outputStream, new System.Text.UTF8Encoding(false), 1024, leaveOpen: true);
        await writer.WriteAsync(json).ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
#endif
    }
}