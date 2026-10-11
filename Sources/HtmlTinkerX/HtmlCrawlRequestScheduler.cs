using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>Schedules static page sends by actual origin; a lease covers the response body lifetime.</summary>
internal sealed class HtmlCrawlRequestScheduler(int perOrigin, Func<Uri, CancellationToken, Task<int>> getDelay) : IDisposable {
    private sealed class Origin(int concurrency) {
        internal readonly SemaphoreSlim Responses = new(concurrency, concurrency);
        internal readonly SemaphoreSlim Starts = new(1, 1);
        internal readonly object Clock = new();
        internal long NextStart;
    }

    private sealed class Lease(SemaphoreSlim responses) : IDisposable {
        private SemaphoreSlim? _responses = responses;
        public void Dispose() => Interlocked.Exchange(ref _responses, null)?.Release();
    }

    private readonly ConcurrentDictionary<string, Origin> _origins = new(StringComparer.OrdinalIgnoreCase);
    private static string Key(Uri uri) => uri.Scheme + "://" + uri.IdnHost + ":" + uri.Port;

    internal async Task<IDisposable> EnterAsync(Uri uri, CancellationToken token) {
        int delay = await getDelay(uri, token).ConfigureAwait(false);
        Origin origin = _origins.GetOrAdd(Key(uri), _ => new Origin(perOrigin));
        await origin.Responses.WaitAsync(token).ConfigureAwait(false);
        try {
            await origin.Starts.WaitAsync(token).ConfigureAwait(false);
            try {
                while (true) {
                    double remainingMs;
                    lock (origin.Clock) {
                        long now = Stopwatch.GetTimestamp();
                        remainingMs = (origin.NextStart - now) * 1000d / Stopwatch.Frequency;
                        if (remainingMs <= 0) {
                            token.ThrowIfCancellationRequested();
                            origin.NextStart = now + (long)Math.Ceiling(delay * Stopwatch.Frequency / 1000d);
                            return new Lease(origin.Responses);
                        }
                    }
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Ceiling(remainingMs)), token).ConfigureAwait(false);
                }
            } finally {
                origin.Starts.Release();
            }
        } catch {
            origin.Responses.Release();
            throw;
        }
    }

    internal void Defer(Uri uri, TimeSpan delay) {
        if (delay <= TimeSpan.Zero) return;
        Origin origin = _origins.GetOrAdd(Key(uri), _ => new Origin(perOrigin));
        lock (origin.Clock) origin.NextStart = Math.Max(origin.NextStart,
            Stopwatch.GetTimestamp() + (long)Math.Ceiling(delay.TotalSeconds * Stopwatch.Frequency));
    }

    public void Dispose() {
        foreach (Origin origin in _origins.Values) {
            origin.Responses.Dispose();
            origin.Starts.Dispose();
        }
    }
}
