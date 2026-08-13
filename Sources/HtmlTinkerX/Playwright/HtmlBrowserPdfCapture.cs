namespace HtmlTinkerX;

using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Shared Playwright PDF preparation, option mapping, and cancellation behavior.</summary>
internal static class HtmlBrowserPdfCapture {
    internal static byte[] ValidateOutputSize(byte[] bytes, long maximumPdfBytes) {
        if (bytes == null) throw new ArgumentNullException(nameof(bytes));
        if (maximumPdfBytes < 0) throw new ArgumentOutOfRangeException(nameof(maximumPdfBytes));
        if (maximumPdfBytes != 0 && bytes.LongLength > maximumPdfBytes) {
            throw new InvalidOperationException(
                $"Chromium generated {bytes.LongLength} PDF bytes, exceeding the configured limit of {maximumPdfBytes} bytes.");
        }
        return bytes;
    }

    internal static async Task<byte[]> PrintToPdfBoundedAsync(
        IPage page,
        HtmlBrowserPdfOptions options,
        long maximumPdfBytes,
        CancellationToken cancellationToken) {
        if (page == null) throw new ArgumentNullException(nameof(page));
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (maximumPdfBytes <= 0 || maximumPdfBytes > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(maximumPdfBytes));

        ICDPSession session = await page.Context.NewCDPSessionAsync(page).ConfigureAwait(false);
        string? streamHandle = null;
        try {
            cancellationToken.ThrowIfCancellationRequested();
            Dictionary<string, object> parameters = CreateCdpPrintParameters(options);
            parameters["transferMode"] = "ReturnAsStream";
            JsonElement? printResult = await session.SendAsync("Page.printToPDF", parameters).ConfigureAwait(false);
            if (!printResult.HasValue
                || !printResult.Value.TryGetProperty("stream", out JsonElement streamElement)
                || string.IsNullOrWhiteSpace(streamElement.GetString())) {
                throw new InvalidOperationException("Chromium did not return a PDF stream handle.");
            }
            streamHandle = streamElement.GetString();
            using MemoryStream output = new(capacity: (int)Math.Min(maximumPdfBytes, 1024L * 1024));
            while (true) {
                cancellationToken.ThrowIfCancellationRequested();
                JsonElement? readResult = await session.SendAsync("IO.read", new Dictionary<string, object> {
                    ["handle"] = streamHandle!,
                    ["size"] = 64 * 1024
                }).ConfigureAwait(false);
                if (!readResult.HasValue) throw new InvalidOperationException("Chromium returned an empty PDF stream response.");
                JsonElement response = readResult.Value;
                string data = response.TryGetProperty("data", out JsonElement dataElement)
                    ? dataElement.GetString() ?? string.Empty
                    : string.Empty;
                bool base64Encoded = response.TryGetProperty("base64Encoded", out JsonElement encodedElement)
                    && encodedElement.ValueKind == JsonValueKind.True;
                byte[] chunk = base64Encoded ? Convert.FromBase64String(data) : Encoding.UTF8.GetBytes(data);
                if (chunk.LongLength > maximumPdfBytes - output.Length) {
                    throw new InvalidOperationException(
                        $"Chromium PDF output exceeded the configured limit of {maximumPdfBytes} bytes.");
                }
                output.Write(chunk, 0, chunk.Length);
                if (response.TryGetProperty("eof", out JsonElement eofElement)
                    && eofElement.ValueKind == JsonValueKind.True) break;
            }
            return output.ToArray();
        } finally {
            if (streamHandle != null) {
                try {
                    await session.SendAsync("IO.close", new Dictionary<string, object> { ["handle"] = streamHandle }).ConfigureAwait(false);
                } catch (PlaywrightException) when (page.IsClosed) {
                    // Cancellation closes the renderer-owned page and stream transport.
                }
            }
            try {
                await session.DetachAsync().ConfigureAwait(false);
            } catch (PlaywrightException) when (page.IsClosed) {
                // The renderer-owned context already disconnected the session.
            }
        }
    }

    internal static Dictionary<string, object> CreateCdpPrintParameters(HtmlBrowserPdfOptions options) {
        (double width, double height) = options.Format.HasValue
            ? GetPaperSize(options.Format.Value)
            : (ParseLength(options.Width, 8.5), ParseLength(options.Height, 11));
        return new Dictionary<string, object> {
            ["landscape"] = options.Landscape,
            ["displayHeaderFooter"] = options.DisplayHeaderFooter,
            ["printBackground"] = options.PrintBackground,
            ["scale"] = options.Scale ?? 1f,
            ["paperWidth"] = width,
            ["paperHeight"] = height,
            ["marginTop"] = ParseLength(options.MarginTop, 0),
            ["marginBottom"] = ParseLength(options.MarginBottom, 0),
            ["marginLeft"] = ParseLength(options.MarginLeft, 0),
            ["marginRight"] = ParseLength(options.MarginRight, 0),
            ["pageRanges"] = options.PageRanges ?? string.Empty,
            ["headerTemplate"] = options.HeaderTemplate ?? string.Empty,
            ["footerTemplate"] = options.FooterTemplate ?? string.Empty,
            ["preferCSSPageSize"] = options.PreferCssPageSize,
            ["generateDocumentOutline"] = options.Outline,
            ["generateTaggedPDF"] = options.Tagged
        };
    }

    private static (double Width, double Height) GetPaperSize(PdfPageFormat format) => format switch {
        PdfPageFormat.A0 => (33.1102, 46.811),
        PdfPageFormat.A1 => (23.3858, 33.1102),
        PdfPageFormat.A2 => (16.5354, 23.3858),
        PdfPageFormat.A3 => (11.6929, 16.5354),
        PdfPageFormat.A4 => (8.26772, 11.6929),
        PdfPageFormat.A5 => (5.82677, 8.26772),
        PdfPageFormat.A6 => (4.13386, 5.82677),
        PdfPageFormat.Letter => (8.5, 11),
        PdfPageFormat.Legal => (8.5, 14),
        PdfPageFormat.Tabloid => (11, 17),
        PdfPageFormat.Ledger => (17, 11),
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    private static double ParseLength(string? value, double defaultInches) {
        if (string.IsNullOrWhiteSpace(value)) return defaultInches;
        string text = value!.Trim().ToLowerInvariant();
        double multiplier = 1d / 96d;
        foreach ((string Unit, double Multiplier) unit in new[] {
            ("in", 1d), ("cm", 1d / 2.54d), ("mm", 1d / 25.4d), ("px", 1d / 96d)
        }) {
            if (!text.EndsWith(unit.Unit, StringComparison.Ordinal)) continue;
            multiplier = unit.Multiplier;
            text = text.Substring(0, text.Length - unit.Unit.Length).Trim();
            break;
        }
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
            || double.IsNaN(number)
            || double.IsInfinity(number)
            || number < 0) {
            throw new ArgumentException($"PDF length '{value}' is not a non-negative CSS length.", nameof(value));
        }
        return number * multiplier;
    }

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
        bool unlimited = readiness.Timeout == 0;
        while (unlimited || stopwatch.ElapsedMilliseconds <= readiness.Timeout) {
            cancellationToken.ThrowIfCancellationRequested();
            int remainingMilliseconds = unlimited
                ? int.MaxValue
                : readiness.Timeout - (int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds);
            if (!unlimited && remainingMilliseconds <= 0) break;
            using CancellationTokenSource? deadline = unlimited
                ? null
                : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline?.CancelAfter(remainingMilliseconds);
            string current;
            try {
                current = await GetFrameMarkupSnapshotAsync(
                    page,
                    deadline?.Token ?? cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline?.IsCancellationRequested == true) {
                throw new TimeoutException("Page markup serialization exceeded the configured readiness timeout.");
            }
            if (string.Equals(previous, current, StringComparison.Ordinal)) {
                stableSince ??= stopwatch.Elapsed;
                if ((stopwatch.Elapsed - stableSince.Value).TotalMilliseconds >= readiness.StableMilliseconds) return;
            } else {
                previous = current;
                stableSince = null;
            }
            if (!unlimited) {
                remainingMilliseconds = readiness.Timeout - (int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds);
                if (remainingMilliseconds <= 0) break;
            }
            await Task.Delay(unlimited ? readiness.PollMilliseconds : Math.Min(readiness.PollMilliseconds, remainingMilliseconds), cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("Page markup did not remain stable within the configured readiness timeout.");
    }

    private static async Task<string> GetFrameMarkupSnapshotAsync(IPage page, CancellationToken cancellationToken) {
        StringBuilder snapshot = new();
        foreach (IFrame frame in page.Frames) {
            cancellationToken.ThrowIfCancellationRequested();
            if (frame.IsDetached) continue;
            try {
                string markup = await ExecuteWithCancellationAsync(
                    frame.ContentAsync,
                    () => page.CloseAsync(),
                    cancellationToken).ConfigureAwait(false);
                snapshot.Append(frame.Url).Append('\0').Append(markup).Append('\0');
            } catch (PlaywrightException) when (frame.IsDetached) {
                // A frame can detach between the snapshot and ContentAsync. Its disappearance
                // changes the next snapshot, so it cannot create a false stable interval.
            }
        }
        return snapshot.ToString();
    }
}
