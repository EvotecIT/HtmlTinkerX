using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// A development cache of whole responses on disk, so parsing code can be run again without fetching the site again.
/// Unlike <see cref="HtmlFetchPolicy.ValidatorCache"/> it never asks the publisher: a stored response is replayed as it
/// is until its file is deleted. Use it while writing extraction code, not for production reads.
/// </summary>
/// <remarks>
/// GET responses with a 2xx status are stored after the caller reads the whole body, keyed by method, address,
/// Accept and Accept-Language. Set-Cookie headers are never written. Requests carrying requester state (credentials,
/// cookies, custom headers, or a transport that adds cookies, credentials or a client certificate) are neither replayed
/// nor stored unless <see cref="IncludeRequesterState"/> is set.
/// </remarks>
public sealed class HtmlReplayCache {
    private static readonly HashSet<string> SkippedHeaders = new(StringComparer.OrdinalIgnoreCase) {
        "Set-Cookie", "Set-Cookie2", "Transfer-Encoding", "Connection", "Keep-Alive", "Content-Length", "Content-Encoding"
    };

    /// <summary>Creates a cache in a directory, which is created when the first response is stored.</summary>
    /// <param name="directory">Directory that holds one file per stored response.</param>
    public HtmlReplayCache(string directory) {
        if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("A cache directory is required.", nameof(directory));
        Directory = Path.GetFullPath(directory);
    }

    /// <summary>The directory that holds the stored responses.</summary>
    public string Directory { get; }

    /// <summary>
    /// Also replays and stores requests that carry requester state, such as a signed-in session. Their bodies are
    /// written to disk and replayed to whoever uses the directory next. Off by default.
    /// </summary>
    public bool IncludeRequesterState { get; set; }

    internal bool Accepts(HttpRequestMessage request, bool sendsRequesterState) =>
        request.Method == HttpMethod.Get
        && (IncludeRequesterState || HtmlFetchRules.CanCacheByAddress(request) && !sendsRequesterState);

    internal HttpResponseMessage? TryReplay(HttpRequestMessage request) {
        string path = PathFor(request);
        if (!File.Exists(path)) return null;
        Entry? entry;
        try {
            entry = JsonSerializer.Deserialize<Entry>(File.ReadAllText(path));
        } catch (Exception ex) when (ex is JsonException or IOException) {
            return null;
        }
        if (entry?.Body == null || !Uri.TryCreate(entry.FinalUrl, UriKind.Absolute, out Uri? final)) return null;
        HttpResponseMessage response = new((HttpStatusCode)entry.Status) {
            ReasonPhrase = entry.Reason,
            RequestMessage = final == request.RequestUri ? request : new HttpRequestMessage(request.Method, final),
            Content = new ByteArrayContent(Convert.FromBase64String(entry.Body))
        };
        foreach (string[] header in entry.Headers) response.Headers.TryAddWithoutValidation(header[0], header[1]);
        foreach (string[] header in entry.ContentHeaders) response.Content.Headers.TryAddWithoutValidation(header[0], header[1]);
        return response;
    }

    /// <summary>Returns the response with its body stored once the caller has read all of it.</summary>
    internal HttpResponseMessage Record(HttpRequestMessage request, HttpResponseMessage response) {
        if (!response.IsSuccessStatusCode || response.Content == null) return response;
        string path = PathFor(request);
        Uri final = response.RequestMessage?.RequestUri ?? request.RequestUri!;
        int status = (int)response.StatusCode;
        string? reason = response.ReasonPhrase;
        string[][] headers = Keep(response.Headers);
        string[][] contentHeaders = Keep(response.Content.Headers);
        response.Content = new HtmlStoringContent(response.Content, HtmlHttpFetchOptions.DefaultMaximumResponseBytes, (body, _) => {
            Write(path, new Entry {
                Method = request.Method.Method, Url = request.RequestUri!.AbsoluteUri, FinalUrl = final.AbsoluteUri,
                Status = status, Reason = reason, Headers = headers, ContentHeaders = contentHeaders, Body = Convert.ToBase64String(body)
            });
            return Task.CompletedTask;
        });
        return response;
    }

    private void Write(string path, Entry entry) {
        System.IO.Directory.CreateDirectory(Directory);
        // Write beside the target and move, so a reader never sees half a file.
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(entry));
        if (File.Exists(path)) File.Delete(path);
        File.Move(temporary, path);
    }

    private string PathFor(HttpRequestMessage request) {
        Uri uri = request.RequestUri!;
        string address = uri.GetComponents(UriComponents.AbsoluteUri & ~UriComponents.Fragment, UriFormat.UriEscaped);
        string key = string.Join("\n", request.Method.Method, address, Value(request, "Accept"), Value(request, "Accept-Language"));
        using SHA256 sha = SHA256.Create();
        string hash = string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(key)).Select(static b => b.ToString("x2")));
        return Path.Combine(Directory, hash + ".json");
    }

    private static string Value(HttpRequestMessage request, string name) =>
        request.Headers.TryGetValues(name, out IEnumerable<string>? values) ? string.Join(", ", values) : string.Empty;

    private static string[][] Keep(System.Net.Http.Headers.HttpHeaders headers) =>
        headers.Where(static header => !SkippedHeaders.Contains(header.Key))
            .SelectMany(static header => header.Value.Select(value => new[] { header.Key, value }))
            .ToArray();

    private sealed class Entry {
        public string Method { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string FinalUrl { get; set; } = string.Empty;
        public int Status { get; set; }
        public string? Reason { get; set; }
        public string[][] Headers { get; set; } = Array.Empty<string[]>();
        public string[][] ContentHeaders { get; set; } = Array.Empty<string[]>();
        public string? Body { get; set; }
    }
}
