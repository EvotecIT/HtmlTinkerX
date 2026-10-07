using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlCrawler {
    private static bool IsTransientHttpStatus(int status) => status == 408 || status == 429
        || status == 500 || status == 502 || status == 503 || status == 504;

    private static TimeSpan GetHttpRetryDelay(HttpResponseMessage response, int retries) =>
        GetServerRetryDelay(response) ?? TimeSpan.FromMilliseconds(Math.Min(30000, 250 * Math.Pow(2, retries)));

    private static TimeSpan? GetServerRetryDelay(HttpResponseMessage response) {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is TimeSpan delta) return delta;
        if (retryAfter?.Date is DateTimeOffset date) {
            TimeSpan remaining = date - DateTimeOffset.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
        return null;
    }

    private static async Task WaitForHttpRetryDelayAsync(TimeSpan delay, CancellationToken token) {
        token.ThrowIfCancellationRequested();
        Stopwatch elapsed = Stopwatch.StartNew();
        TimeSpan remaining = delay;
        while (remaining > TimeSpan.Zero) {
            // Timers can wake slightly early. Keep the minimum delay before another request.
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Ceiling(remaining.TotalMilliseconds)), token).ConfigureAwait(false);
            remaining = delay - elapsed.Elapsed;
        }
    }
}
