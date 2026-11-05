using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>Stores validators and bodies so <see cref="HtmlFetchHandler"/> can send conditional GETs and reuse a 304.</summary>
public interface IHtmlHttpValidatorCache {
    /// <summary>Returns the stored entry for an address, or null.</summary>
    /// <param name="uri">The absolute request address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HtmlHttpValidatorEntry?> GetAsync(Uri uri, CancellationToken cancellationToken);

    /// <summary>Stores or replaces the entry for an address.</summary>
    /// <param name="uri">The absolute request address.</param>
    /// <param name="entry">Validators and body from a successful response.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetAsync(Uri uri, HtmlHttpValidatorEntry entry, CancellationToken cancellationToken);
}

/// <summary>A response body with the validators that let the publisher confirm it is unchanged.</summary>
public sealed class HtmlHttpValidatorEntry {
    /// <summary>Creates an entry.</summary>
    /// <param name="entityTag">The response ETag, or null.</param>
    /// <param name="lastModified">The response Last-Modified date, or null.</param>
    /// <param name="content">The response body.</param>
    /// <param name="contentType">The response Content-Type header, or null.</param>
    public HtmlHttpValidatorEntry(string? entityTag, DateTimeOffset? lastModified, byte[] content, string? contentType) {
        if (string.IsNullOrEmpty(entityTag) && lastModified == null) throw new ArgumentException("An entity tag or Last-Modified date is required.");
        EntityTag = entityTag;
        LastModified = lastModified;
        Content = content ?? throw new ArgumentNullException(nameof(content));
        ContentType = contentType;
    }

    /// <summary>The response ETag, or null.</summary>
    public string? EntityTag { get; }

    /// <summary>The response Last-Modified date, or null.</summary>
    public DateTimeOffset? LastModified { get; }

    /// <summary>The response body.</summary>
    public byte[] Content { get; }

    /// <summary>The response Content-Type header, or null.</summary>
    public string? ContentType { get; }
}

/// <summary>An in-memory <see cref="IHtmlHttpValidatorCache"/> holding a bounded number of entries.</summary>
public sealed class HtmlMemoryValidatorCache : IHtmlHttpValidatorCache {
    private readonly ConcurrentDictionary<string, HtmlHttpValidatorEntry> _entries = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> _order = new();
    private readonly int _capacity;

    /// <summary>Creates a cache.</summary>
    /// <param name="capacity">Entries kept; the oldest stored address is dropped first.</param>
    public HtmlMemoryValidatorCache(int capacity = 1024) {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be at least one.");
        _capacity = capacity;
    }

    /// <inheritdoc />
    public Task<HtmlHttpValidatorEntry?> GetAsync(Uri uri, CancellationToken cancellationToken) =>
        Task.FromResult(_entries.TryGetValue(Key(uri), out HtmlHttpValidatorEntry? entry) ? entry : null);

    /// <inheritdoc />
    public Task SetAsync(Uri uri, HtmlHttpValidatorEntry entry, CancellationToken cancellationToken) {
        if (entry == null) throw new ArgumentNullException(nameof(entry));
        string key = Key(uri);
        if (_entries.TryAdd(key, entry)) {
            _order.Enqueue(key);
            while (_entries.Count > _capacity && _order.TryDequeue(out string? oldest)) _entries.TryRemove(oldest, out _);
        } else {
            _entries[key] = entry;
        }
        return Task.CompletedTask;
    }

    private static string Key(Uri uri) => (uri ?? throw new ArgumentNullException(nameof(uri))).AbsoluteUri;
}
