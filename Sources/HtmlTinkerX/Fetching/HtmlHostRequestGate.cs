using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Limits requests to each origin (scheme, host and port) and across all origins, and paces request starts to one
/// origin. Reading many pages from one site at once can trip its bot protection, so share one instance across every
/// handler that reads for the same process.
/// </summary>
/// <remarks>
/// Pacing adapts to the site: a 429 or 503 response, or any refusal with Retry-After, defers that origin and grows its
/// delay (doubling on repeated throttling, up to <see cref="MaximumDelay"/>); each later success halves it again until
/// only <see cref="MinimumDelay"/> remains. <see cref="HtmlFetchHandler"/> reports responses automatically.
/// </remarks>
public sealed class HtmlHostRequestGate : IDisposable {
    /// <summary>Requests one origin may have in flight at a time, unless configured otherwise.</summary>
    public const int DefaultPerHost = 2;

    /// <summary>Requests the gate admits across all origins, unless configured otherwise.</summary>
    public const int DefaultMaxConcurrentRequests = 8;

    /// <summary>Longest adaptive delay, unless configured otherwise.</summary>
    public static readonly TimeSpan DefaultMaximumDelay = TimeSpan.FromMinutes(2);

    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan NegligibleBackoff = TimeSpan.FromMilliseconds(50);

    private sealed class Origin(int concurrency) {
        internal readonly SemaphoreSlim Responses = new(concurrency, concurrency);
        internal readonly SemaphoreSlim Starts = new(1, 1);
        internal readonly object Clock = new();
        internal long NextStart;
        internal TimeSpan Backoff;
    }

    private sealed class Lease(SemaphoreSlim local, SemaphoreSlim? total) : IDisposable {
        private int _released;
        public void Dispose() {
            if (Interlocked.Exchange(ref _released, 1) != 0) return;
            total?.Release();
            local.Release();
        }
    }

    private readonly ConcurrentDictionary<string, Origin> _origins = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim? _total;
    private TimeSpan _minimumDelay;
    private TimeSpan _maximumDelay = DefaultMaximumDelay;

    /// <summary>Creates a gate.</summary>
    /// <param name="perHost">Maximum requests in flight to each origin.</param>
    /// <param name="maxConcurrentRequests">Maximum requests in flight across all origins.</param>
    public HtmlHostRequestGate(int perHost = DefaultPerHost, int maxConcurrentRequests = DefaultMaxConcurrentRequests) {
        if (perHost < 1) throw new ArgumentOutOfRangeException(nameof(perHost), perHost, "At least one request per host is required.");
        if (maxConcurrentRequests < 1) throw new ArgumentOutOfRangeException(nameof(maxConcurrentRequests), maxConcurrentRequests, "At least one request is required.");
        PerHost = perHost;
        MaxConcurrentRequests = maxConcurrentRequests;
        _total = maxConcurrentRequests == int.MaxValue ? null : new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);
    }

    /// <summary>Requests one origin may have in flight at a time.</summary>
    public int PerHost { get; }

    /// <summary>Maximum requests in flight across all origins.</summary>
    public int MaxConcurrentRequests { get; }

    /// <summary>Base delay between request starts to one origin. Zero by default.</summary>
    public TimeSpan MinimumDelay {
        get => _minimumDelay;
        set => _minimumDelay = value >= TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>Longest adaptive delay after throttling; a longer Retry-After is capped to it.</summary>
    public TimeSpan MaximumDelay {
        get => _maximumDelay;
        set => _maximumDelay = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Waits for a slot to the origin of <paramref name="uri"/> and for its paced start time. Dispose the result when
    /// the response, including its body, is finished.
    /// </summary>
    /// <param name="uri">An absolute request address.</param>
    /// <param name="cancellationToken">Cancels waiting; a cancelled wait holds no slot.</param>
    public Task<IDisposable> EnterAsync(Uri uri, CancellationToken cancellationToken = default) =>
        EnterAsync(uri, MinimumDelay, cancellationToken);

    internal async Task<IDisposable> EnterAsync(Uri uri, TimeSpan spacing, CancellationToken cancellationToken) {
        Origin origin = GetOrigin(uri);
        // A queue for one busy origin must not occupy the whole process budget while waiting for its local slot.
        await origin.Responses.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            await origin.Starts.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                while (true) {
                    double remainingMs;
                    lock (origin.Clock) {
                        long now = Stopwatch.GetTimestamp();
                        remainingMs = (origin.NextStart - now) * 1000d / Stopwatch.Frequency;
                        if (remainingMs <= 0) {
                            cancellationToken.ThrowIfCancellationRequested();
                            origin.NextStart = now + Ticks(spacing + origin.Backoff);
                            break;
                        }
                    }
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Ceiling(remainingMs)), cancellationToken).ConfigureAwait(false);
                }
            } finally {
                origin.Starts.Release();
            }
            if (_total != null) await _total.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new Lease(origin.Responses, _total);
        } catch {
            origin.Responses.Release();
            throw;
        }
    }

    /// <summary>Holds the next request start to the origin of <paramref name="uri"/> for at least <paramref name="delay"/>.</summary>
    /// <param name="uri">An absolute request address.</param>
    /// <param name="delay">The minimum wait from now; not capped.</param>
    public void Defer(Uri uri, TimeSpan delay) {
        if (uri == null) throw new ArgumentNullException(nameof(uri));
        if (delay <= TimeSpan.Zero) return;
        Origin origin = GetOrigin(uri);
        lock (origin.Clock) origin.NextStart = Math.Max(origin.NextStart, Stopwatch.GetTimestamp() + Ticks(delay));
    }

    /// <summary>
    /// Adapts pacing to a response from the origin of <paramref name="uri"/>: throttling defers and slows the origin,
    /// a success relaxes it. Other failures leave pacing unchanged.
    /// </summary>
    /// <param name="uri">The address the response came from.</param>
    /// <param name="response">The response; only its status and Retry-After are read.</param>
    public void ReportResponse(Uri uri, HttpResponseMessage response) {
        if (uri == null) throw new ArgumentNullException(nameof(uri));
        if (response == null) throw new ArgumentNullException(nameof(response));
        int status = (int)response.StatusCode;
        TimeSpan? retryAfter = status >= 400 ? HtmlFetchRules.GetServerRetryDelay(response) : null;
        Origin origin = GetOrigin(uri);
        lock (origin.Clock) {
            if (status == 429 || status == (int)HttpStatusCode.ServiceUnavailable || retryAfter != null) {
                TimeSpan grown = origin.Backoff > TimeSpan.Zero ? Double(origin.Backoff) : retryAfter ?? InitialBackoff;
                if (retryAfter > grown) grown = retryAfter.Value;
                origin.Backoff = grown > MaximumDelay ? MaximumDelay : grown;
                origin.NextStart = Math.Max(origin.NextStart, Stopwatch.GetTimestamp() + Ticks(origin.Backoff));
            } else if (status < 400) {
                TimeSpan relaxed = TimeSpan.FromTicks(origin.Backoff.Ticks / 2);
                origin.Backoff = relaxed < NegligibleBackoff ? TimeSpan.Zero : relaxed;
            }
        }
    }

    private Origin GetOrigin(Uri uri) {
        if (uri == null) throw new ArgumentNullException(nameof(uri));
        if (!uri.IsAbsoluteUri) throw new ArgumentException("An absolute address is required.", nameof(uri));
        return _origins.GetOrAdd(uri.Scheme + "://" + uri.IdnHost + ":" + uri.Port, _ => new Origin(PerHost));
    }

    private static TimeSpan Double(TimeSpan value) => value.Ticks > long.MaxValue / 2 ? TimeSpan.MaxValue : TimeSpan.FromTicks(value.Ticks * 2);

    private static long Ticks(TimeSpan delay) {
        double ticks = Math.Ceiling(delay.TotalSeconds * Stopwatch.Frequency);
        return ticks >= long.MaxValue / 2 ? long.MaxValue / 2 : (long)ticks;
    }

    /// <summary>Releases the gate's semaphores. Do not dispose a gate other handlers still use.</summary>
    public void Dispose() {
        foreach (Origin origin in _origins.Values) {
            origin.Responses.Dispose();
            origin.Starts.Dispose();
        }
        _total?.Dispose();
    }
}
