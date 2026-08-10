namespace HtmlTinkerX;

using Microsoft.Playwright;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Shared Playwright PDF preparation, option mapping, and cancellation behavior.</summary>
internal static class HtmlBrowserPdfCapture {
    internal static PagePdfOptions CreatePageOptions(HtmlBrowserPdfOptions options, string? path = null) {
        if (options == null) throw new ArgumentNullException(nameof(options));

        PagePdfOptions result = new() {
            Path = path,
            Landscape = options.Landscape,
            PrintBackground = options.PrintBackground,
            Format = options.Format switch {
                PdfPageFormat.A0 => "A0",
                PdfPageFormat.A1 => "A1",
                PdfPageFormat.A2 => "A2",
                PdfPageFormat.A3 => "A3",
                PdfPageFormat.A4 => "A4",
                PdfPageFormat.A5 => "A5",
                PdfPageFormat.A6 => "A6",
                PdfPageFormat.Letter => "Letter",
                PdfPageFormat.Legal => "Legal",
                PdfPageFormat.Tabloid => "Tabloid",
                PdfPageFormat.Ledger => "Ledger",
                _ => null
            },
            Width = options.Width,
            Height = options.Height,
            PageRanges = options.PageRanges,
            Scale = options.Scale,
            DisplayHeaderFooter = options.DisplayHeaderFooter,
            HeaderTemplate = options.HeaderTemplate,
            FooterTemplate = options.FooterTemplate,
            PreferCSSPageSize = options.PreferCssPageSize,
            Outline = options.Outline,
            Tagged = options.Tagged
        };
        if (options.MarginTop != null || options.MarginRight != null || options.MarginBottom != null || options.MarginLeft != null) {
            result.Margin = new Margin {
                Top = options.MarginTop,
                Right = options.MarginRight,
                Bottom = options.MarginBottom,
                Left = options.MarginLeft
            };
        }
        return result;
    }

    internal static async Task WaitForReadinessAsync(IPage page, HtmlBrowserPdfReadiness readiness, CancellationToken cancellationToken) {
        if (page == null) throw new ArgumentNullException(nameof(page));
        if (readiness == null) throw new ArgumentNullException(nameof(readiness));

        if (!readiness.SkipLoadState) {
            await ExecuteWithCancellationAsync(
                () => HtmlBrowser.WaitForLoadStateAsync(page, readiness.LoadState, readiness.Timeout, cancellationToken),
                () => page.CloseAsync(),
                cancellationToken).ConfigureAwait(false);
        }
        if (!string.IsNullOrWhiteSpace(readiness.Selector)) {
            await ExecuteWithCancellationAsync(
                () => page.WaitForSelectorAsync(readiness.Selector!, new PageWaitForSelectorOptions { Timeout = readiness.Timeout }),
                () => page.CloseAsync(),
                cancellationToken).ConfigureAwait(false);
        }
        if (!string.IsNullOrWhiteSpace(readiness.Function)) {
            await ExecuteWithCancellationAsync(
                () => page.WaitForFunctionAsync(readiness.Function!, null, new PageWaitForFunctionOptions { Timeout = readiness.Timeout }),
                () => page.CloseAsync(),
                cancellationToken).ConfigureAwait(false);
        }
        if (readiness.Stable) {
            await WaitForStableMarkupAsync(page, readiness, cancellationToken).ConfigureAwait(false);
        }
        if (readiness.DelayMilliseconds > 0) {
            await Task.Delay(readiness.DelayMilliseconds, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static async Task ExecuteWithCancellationAsync(
        Func<Task> operation,
        Func<Task> abort,
        CancellationToken cancellationToken) {
        await ExecuteWithCancellationAsync(async () => {
            await operation().ConfigureAwait(false);
            return true;
        }, abort, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<T> ExecuteWithCancellationAsync<T>(
        Func<Task<T>> operation,
        Func<Task> abort,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        Task<T> task = operation();
        if (!cancellationToken.CanBeCanceled || task.IsCompleted) return await task.ConfigureAwait(false);

        TaskCompletionSource<bool> cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = cancellationToken.Register(
            static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true),
            cancelled);
        if (await Task.WhenAny(task, cancelled.Task).ConfigureAwait(false) != task) {
            Task? abortTask = null;
            try {
                abortTask = abort();
            } catch (Exception) {
                // Aborting the Playwright transport is best effort.
            }
            if (abortTask != null) {
                _ = abortTask.ContinueWith(static completed => _ = completed.Exception, TaskContinuationOptions.OnlyOnFaulted);
            }
            _ = task.ContinueWith(static completed => _ = completed.Exception, TaskContinuationOptions.OnlyOnFaulted);
            cancellationToken.ThrowIfCancellationRequested();
        }
        return await task.ConfigureAwait(false);
    }

    private static async Task WaitForStableMarkupAsync(IPage page, HtmlBrowserPdfReadiness readiness, CancellationToken cancellationToken) {
        Stopwatch stopwatch = Stopwatch.StartNew();
        TimeSpan? stableSince = null;
        string? previous = null;
        while (stopwatch.ElapsedMilliseconds <= readiness.Timeout) {
            cancellationToken.ThrowIfCancellationRequested();
            int remainingMilliseconds = readiness.Timeout - (int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds);
            if (remainingMilliseconds <= 0) break;
            using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(remainingMilliseconds);
            string current;
            try {
                current = await ExecuteWithCancellationAsync(
                    page.ContentAsync,
                    () => page.CloseAsync(),
                    deadline.Token).ConfigureAwait(false);
            } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested) {
                throw new TimeoutException("Page markup serialization exceeded the configured readiness timeout.");
            }
            if (string.Equals(previous, current, StringComparison.Ordinal)) {
                stableSince ??= stopwatch.Elapsed;
                if ((stopwatch.Elapsed - stableSince.Value).TotalMilliseconds >= readiness.StableMilliseconds) return;
            } else {
                previous = current;
                stableSince = null;
            }
            remainingMilliseconds = readiness.Timeout - (int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds);
            if (remainingMilliseconds <= 0) break;
            await Task.Delay(Math.Min(readiness.PollMilliseconds, remainingMilliseconds), cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("Page markup did not remain stable within the configured readiness timeout.");
    }
}
