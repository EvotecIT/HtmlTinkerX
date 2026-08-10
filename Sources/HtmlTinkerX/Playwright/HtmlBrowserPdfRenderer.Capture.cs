namespace HtmlTinkerX;

using Microsoft.Playwright;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

public sealed partial class HtmlBrowserPdfRenderer {
    /// <summary>Captures a URL, HTML string, or local HTML file into PDF bytes.</summary>
    public async Task<HtmlBrowserPdfResult> CaptureAsync(HtmlBrowserPdfRequest request, CancellationToken cancellationToken = default) {
        if (request == null) throw new ArgumentNullException(nameof(request));
        BeginOperation();
        using CancellationTokenSource operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeCancellation.Token);
        bool admissionAcquired = false;
        try {
            CancellationToken operationToken = operationCancellation.Token;
            operationToken.ThrowIfCancellationRequested();
            if (!_admissionGate.Wait(0)) {
                Interlocked.Increment(ref _rejected);
                throw new HtmlBrowserPdfCapacityException($"Browser PDF capacity is full ({_options.MaximumBrowserInstances} active, {_options.MaximumQueuedCaptures} queued).");
            }
            admissionAcquired = true;

            Interlocked.Increment(ref _accepted);
            long totalStarted = Stopwatch.GetTimestamp();
            long queueStarted = totalStarted;
            bool leaseAcquired = false;
            bool countedAsQueued = true;
            Interlocked.Increment(ref _queued);
            try {
                await _leaseGate.WaitAsync(operationToken).ConfigureAwait(false);
                leaseAcquired = true;
                Interlocked.Decrement(ref _queued);
                countedAsQueued = false;
                Interlocked.Increment(ref _active);
                TimeSpan queueDuration = StopwatchElapsed(queueStarted);

                bool retried = false;
                for (int attempt = 0; attempt < 2; attempt++) {
                    BrowserSlot slot = await RentSlotAsync(operationToken).ConfigureAwait(false);
                    bool returnSlot = true;
                    try {
                        HtmlBrowserPdfResult result = await CaptureWithSlotAsync(
                            slot,
                            request,
                            queueDuration,
                            totalStarted,
                            retried,
                            operationToken).ConfigureAwait(false);
                        Interlocked.Increment(ref _succeeded);
                        return result;
                    } catch (Exception ex) when (attempt == 0 && !operationToken.IsCancellationRequested && IsBrowserProcessFailure(ex, slot)) {
                        retried = true;
                        returnSlot = false;
                        slot.MarkBroken();
                        Interlocked.Increment(ref _retries);
                        await RecycleSlotAsync(slot).ConfigureAwait(false);
                    } finally {
                        if (returnSlot) await ReturnSlotAsync(slot).ConfigureAwait(false);
                    }
                }

                throw new InvalidOperationException("Browser PDF retry loop completed without a result.");
            } catch (OperationCanceledException) when (operationToken.IsCancellationRequested) {
                Interlocked.Increment(ref _cancelled);
                throw;
            } catch {
                Interlocked.Increment(ref _failed);
                throw;
            } finally {
                if (countedAsQueued) Interlocked.Decrement(ref _queued);
                if (leaseAcquired) {
                    Interlocked.Decrement(ref _active);
                    _leaseGate.Release();
                }
            }
        } finally {
            if (admissionAcquired) _admissionGate.Release();
            EndOperation();
        }
    }

    private async Task<HtmlBrowserPdfResult> CaptureWithSlotAsync(
        BrowserSlot slot,
        HtmlBrowserPdfRequest request,
        TimeSpan queueDuration,
        long totalStarted,
        bool retried,
        CancellationToken cancellationToken) {
        HtmlBrowserNetworkPolicyEvaluator policy = new(_options.NetworkPolicy);
        ConcurrentQueue<string> blockedRequests = new();
        int blockedRequestCount = 0;
        List<string> warnings = new();
        Action<Uri>? blockedByProxy = null;
        if (slot.PolicyProxy != null) {
            blockedByProxy = uri => {
                Interlocked.Increment(ref blockedRequestCount);
                if (blockedRequests.Count < _options.NetworkPolicy.BlockedRequestDiagnosticLimit) {
                    blockedRequests.Enqueue(SanitizeUri(uri.AbsoluteUri));
                }
            };
            slot.PolicyProxy.RequestBlocked += blockedByProxy;
        }
        string? selectedFileDirectory = request.Source.Kind == HtmlBrowserPdfSourceKind.File
            ? Path.GetDirectoryName(request.Source.FilePath!)
            : null;

        BrowserNewContextOptions contextOptions = CreateContextOptions(request);
        IBrowserContext? context = null;
        try {
            await ValidateInitialSourceAsync(request.Source, policy, selectedFileDirectory, cancellationToken).ConfigureAwait(false);
            context = await slot.Browser.NewContextAsync(contextOptions).ConfigureAwait(false);
            Func<IRoute, Task> policyRoute = async route => {
                bool allowed;
                try {
                    allowed = await policy.IsAllowedAsync(route.Request.Url, selectedFileDirectory, cancellationToken).ConfigureAwait(false);
                } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                    await route.AbortAsync("aborted").ConfigureAwait(false);
                    return;
                }

                if (allowed) {
                    await route.ContinueAsync().ConfigureAwait(false);
                    return;
                }

                Interlocked.Increment(ref blockedRequestCount);
                if (blockedRequests.Count < _options.NetworkPolicy.BlockedRequestDiagnosticLimit) {
                    blockedRequests.Enqueue(SanitizeUri(route.Request.Url));
                }
                await route.AbortAsync("blockedbyclient").ConfigureAwait(false);
            };
            await context.RouteAsync("**/*", policyRoute).ConfigureAwait(false);
            await AddStorageInitScriptAsync(context, request, cancellationToken).ConfigureAwait(false);
            await AddCookiesAsync(context, request.Cookies, cancellationToken).ConfigureAwait(false);

            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            page.SetDefaultTimeout(request.Readiness.Timeout);

            long navigationStarted = Stopwatch.GetTimestamp();
            await LoadSourceAsync(page, request.Source, request.Readiness.Timeout, cancellationToken).ConfigureAwait(false);
            TimeSpan navigationDuration = StopwatchElapsed(navigationStarted);

            long readinessStarted = Stopwatch.GetTimestamp();
            await PreparePageAsync(page, request, cancellationToken).ConfigureAwait(false);
            TimeSpan readinessDuration = StopwatchElapsed(readinessStarted);

            PagePdfOptions pdfOptions = CreatePagePdfOptions(request.PdfOptions);
            long pdfStarted = Stopwatch.GetTimestamp();
            byte[] bytes = await ExecuteCancellableContextOperationAsync(
                context,
                () => HtmlBrowser.ExecuteWithTemporaryVisualMaskAsync(
                    page,
                    request.PdfOptions.MaskSensitiveElements,
                    request.PdfOptions.MaskSelectors,
                    request.PdfOptions.MaskColor,
                    () => page.PdfAsync(pdfOptions),
                    cancellationToken),
                cancellationToken).ConfigureAwait(false);
            TimeSpan pdfDuration = StopwatchElapsed(pdfStarted);

            if (bytes.Length == 0) warnings.Add("Chromium returned an empty PDF payload.");
            HtmlBrowserPdfDiagnostics diagnostics = new(
                request.Source.Kind,
                slot.Id,
                slot.RenderCount > 0,
                retried,
                SanitizeUri(page.Url),
                slot.Browser.Version,
                queueDuration,
                navigationDuration,
                readinessDuration,
                pdfDuration,
                StopwatchElapsed(totalStarted),
                Volatile.Read(ref blockedRequestCount),
                blockedRequests.ToArray(),
                warnings.ToArray());
            return new HtmlBrowserPdfResult(bytes, diagnostics);
        } finally {
            if (context != null) {
                try {
                    await context.CloseAsync().ConfigureAwait(false);
                } catch (PlaywrightException) {
                    slot.MarkBroken();
                }
            }
            if (slot.PolicyProxy != null && blockedByProxy != null) slot.PolicyProxy.RequestBlocked -= blockedByProxy;
        }
    }

    private BrowserNewContextOptions CreateContextOptions(HtmlBrowserPdfRequest request) {
        BrowserNewContextOptions options = new() {
            IgnoreHTTPSErrors = _options.IgnoreHttpsErrors,
            BypassCSP = request.BypassContentSecurityPolicy,
            ServiceWorkers = ServiceWorkerPolicy.Block,
            StorageStatePath = _options.StorageStatePath,
            UserAgent = _options.UserAgent,
            Locale = _options.Locale,
            TimezoneId = _options.Timezone,
            ExtraHTTPHeaders = request.Headers.Count == 0
                ? null
                : request.Headers.ToDictionary(static item => item.Key, static item => item.Value, StringComparer.OrdinalIgnoreCase)
        };
        if (_options.ViewportWidth.HasValue && _options.ViewportHeight.HasValue) {
            options.ViewportSize = new ViewportSize { Width = _options.ViewportWidth.Value, Height = _options.ViewportHeight.Value };
        }
        return options;
    }

    private static async Task ValidateInitialSourceAsync(HtmlBrowserPdfSource source, HtmlBrowserNetworkPolicyEvaluator policy, string? fileDirectory, CancellationToken cancellationToken) {
        string? target = source.Kind switch {
            HtmlBrowserPdfSourceKind.Url => source.Uri!.AbsoluteUri,
            HtmlBrowserPdfSourceKind.File => new Uri(source.FilePath!).AbsoluteUri,
            _ => source.BaseUri?.AbsoluteUri
        };
        if (source.Kind == HtmlBrowserPdfSourceKind.File && !File.Exists(source.FilePath)) {
            throw new FileNotFoundException("HTML input file was not found.", source.FilePath);
        }
        if (target != null && !await policy.IsAllowedAsync(target, fileDirectory, cancellationToken).ConfigureAwait(false)) {
            throw new UnauthorizedAccessException($"Browser resource policy blocked the capture source '{SanitizeUri(target)}'.");
        }
    }

    private static async Task LoadSourceAsync(IPage page, HtmlBrowserPdfSource source, int timeout, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        switch (source.Kind) {
            case HtmlBrowserPdfSourceKind.Url:
                await ExecuteCancellablePageOperationAsync(page, () => page.GotoAsync(source.Uri!.AbsoluteUri, new PageGotoOptions {
                    Timeout = timeout,
                    WaitUntil = WaitUntilState.DOMContentLoaded
                }), cancellationToken).ConfigureAwait(false);
                break;
            case HtmlBrowserPdfSourceKind.File:
                await ExecuteCancellablePageOperationAsync(page, () => page.GotoAsync(new Uri(source.FilePath!).AbsoluteUri, new PageGotoOptions {
                    Timeout = timeout,
                    WaitUntil = WaitUntilState.DOMContentLoaded
                }), cancellationToken).ConfigureAwait(false);
                break;
            case HtmlBrowserPdfSourceKind.Html:
                string html = AddBaseElement(source.Html!, source.BaseUri);
                await ExecuteCancellablePageOperationAsync(page, () => page.SetContentAsync(html, new PageSetContentOptions {
                    Timeout = timeout,
                    WaitUntil = WaitUntilState.DOMContentLoaded
                }), cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(source));
        }
    }

    private static async Task PreparePageAsync(IPage page, HtmlBrowserPdfRequest request, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        await page.EmulateMediaAsync(new PageEmulateMediaOptions {
            Media = request.MediaType == HtmlBrowserPdfMediaType.Screen ? Media.Screen : Media.Print
        }).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(request.StyleSheetContent)) {
            await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = request.StyleSheetContent }).ConfigureAwait(false);
        }
        if (!string.IsNullOrWhiteSpace(request.BeforeCaptureScript)) {
            await ExecuteCancellablePageOperationAsync(page, () => page.EvaluateAsync(request.BeforeCaptureScript!), cancellationToken).ConfigureAwait(false);
        }

        HtmlBrowserPdfReadiness readiness = request.Readiness;
        if (!readiness.SkipLoadState) {
            await ExecuteCancellablePageOperationAsync(
                page,
                () => HtmlBrowser.WaitForLoadStateAsync(page, readiness.LoadState, readiness.Timeout, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }
        if (!string.IsNullOrWhiteSpace(readiness.Selector)) {
            await ExecuteCancellablePageOperationAsync(page, () => page.WaitForSelectorAsync(readiness.Selector!, new PageWaitForSelectorOptions { Timeout = readiness.Timeout }), cancellationToken).ConfigureAwait(false);
        }
        if (!string.IsNullOrWhiteSpace(readiness.Function)) {
            await ExecuteCancellablePageOperationAsync(page, () => page.WaitForFunctionAsync(readiness.Function!, null, new PageWaitForFunctionOptions { Timeout = readiness.Timeout }), cancellationToken).ConfigureAwait(false);
        }
        if (readiness.Stable) {
            await WaitForStableMarkupAsync(page, readiness, cancellationToken).ConfigureAwait(false);
        }
        if (readiness.DelayMilliseconds > 0) {
            await Task.Delay(readiness.DelayMilliseconds, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task WaitForStableMarkupAsync(IPage page, HtmlBrowserPdfReadiness readiness, CancellationToken cancellationToken) {
        long started = Stopwatch.GetTimestamp();
        long? stableSince = null;
        string? previous = null;
        while (StopwatchElapsed(started).TotalMilliseconds <= readiness.Timeout) {
            cancellationToken.ThrowIfCancellationRequested();
            string current = await page.ContentAsync().ConfigureAwait(false);
            if (string.Equals(previous, current, StringComparison.Ordinal)) {
                stableSince ??= Stopwatch.GetTimestamp();
                if (StopwatchElapsed(stableSince.Value).TotalMilliseconds >= readiness.StableMilliseconds) return;
            } else {
                previous = current;
                stableSince = null;
            }
            await Task.Delay(readiness.PollMilliseconds, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("Page markup did not remain stable within the configured readiness timeout.");
    }

    private static async Task AddCookiesAsync(IBrowserContext context, IReadOnlyList<HtmlBrowserPdfCookie> cookies, CancellationToken cancellationToken) {
        if (cookies.Count == 0) return;
        cancellationToken.ThrowIfCancellationRequested();
        Cookie[] values = cookies.Select(cookie => new Cookie {
            Name = cookie.Name,
            Value = cookie.Value,
            Url = cookie.Url,
            Domain = cookie.Domain,
            Path = cookie.Path,
            Expires = cookie.Expires.HasValue ? (float)cookie.Expires.Value : null,
            HttpOnly = cookie.HttpOnly,
            Secure = cookie.Secure,
            SameSite = cookie.SameSite switch {
                HtmlBrowserCookieSameSite.Lax => SameSiteAttribute.Lax,
                HtmlBrowserCookieSameSite.Strict => SameSiteAttribute.Strict,
                HtmlBrowserCookieSameSite.None => SameSiteAttribute.None,
                _ => null
            }
        }).ToArray();
        await context.AddCookiesAsync(values).ConfigureAwait(false);
    }

    private static async Task AddStorageInitScriptAsync(IBrowserContext context, HtmlBrowserPdfRequest request, CancellationToken cancellationToken) {
        if (request.LocalStorage.Count == 0 && request.SessionStorage.Count == 0) return;
        cancellationToken.ThrowIfCancellationRequested();
        string local = JsonSerializer.Serialize(request.LocalStorage);
        string session = JsonSerializer.Serialize(request.SessionStorage);
        string script = $"(() => {{ const local = {local}; const session = {session}; try {{ for (const key of Object.keys(local)) localStorage.setItem(key, local[key]); }} catch {{ }} try {{ for (const key of Object.keys(session)) sessionStorage.setItem(key, session[key]); }} catch {{ }} }})();";
        await context.AddInitScriptAsync(script).ConfigureAwait(false);
    }

    private static PagePdfOptions CreatePagePdfOptions(HtmlBrowserPdfOptions options) {
        PagePdfOptions result = new() {
            Landscape = options.Landscape,
            PrintBackground = options.PrintBackground,
            Format = ToPageFormat(options.Format),
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

    private static string? ToPageFormat(PdfPageFormat? format) => format switch {
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
    };

    private static string AddBaseElement(string html, Uri? baseUri) {
        if (baseUri == null) return html;
        string baseElement = "<base href=\"" + System.Net.WebUtility.HtmlEncode(baseUri.AbsoluteUri) + "\">";
        Match head = Regex.Match(html, "<head(?:\\s[^>]*)?>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return head.Success ? html.Insert(head.Index + head.Length, baseElement) : "<head>" + baseElement + "</head>" + html;
    }

    private static async Task<T> ExecuteCancellableContextOperationAsync<T>(IBrowserContext context, Func<Task<T>> operation, CancellationToken cancellationToken) {
        Task<T> task = operation();
        if (!cancellationToken.CanBeCanceled || task.IsCompleted) return await task.ConfigureAwait(false);
        TaskCompletionSource<bool> cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = cancellationToken.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), cancelled);
        if (await Task.WhenAny(task, cancelled.Task).ConfigureAwait(false) != task) {
            try { await context.CloseAsync().ConfigureAwait(false); } catch (PlaywrightException) { }
            _ = task.ContinueWith(static completed => _ = completed.Exception, TaskContinuationOptions.OnlyOnFaulted);
            cancellationToken.ThrowIfCancellationRequested();
        }
        return await task.ConfigureAwait(false);
    }

    private static async Task ExecuteCancellablePageOperationAsync(IPage page, Func<Task> operation, CancellationToken cancellationToken) {
        Task task = operation();
        if (!cancellationToken.CanBeCanceled || task.IsCompleted) {
            await task.ConfigureAwait(false);
            return;
        }
        TaskCompletionSource<bool> cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = cancellationToken.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), cancelled);
        if (await Task.WhenAny(task, cancelled.Task).ConfigureAwait(false) != task) {
            try { await page.CloseAsync().ConfigureAwait(false); } catch (PlaywrightException) { }
            _ = task.ContinueWith(static completed => _ = completed.Exception, TaskContinuationOptions.OnlyOnFaulted);
            cancellationToken.ThrowIfCancellationRequested();
        }
        await task.ConfigureAwait(false);
    }

    private static bool IsBrowserProcessFailure(Exception exception, BrowserSlot slot) {
        if (!slot.Browser.IsConnected || slot.IsBroken) return true;
        if (exception is not PlaywrightException playwright) return false;
        string message = playwright.Message ?? string.Empty;
        return message.IndexOf("browser has been closed", StringComparison.OrdinalIgnoreCase) >= 0
            || message.IndexOf("browser closed", StringComparison.OrdinalIgnoreCase) >= 0
            || message.IndexOf("target page, context or browser has been closed", StringComparison.OrdinalIgnoreCase) >= 0
            || message.IndexOf("connection closed", StringComparison.OrdinalIgnoreCase) >= 0
            || message.IndexOf("crash", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string SanitizeUri(string url) {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)) return url;
        if (uri.Scheme == Uri.UriSchemeFile) return uri.GetLeftPart(UriPartial.Path);
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return uri.Scheme + ":";
        UriBuilder builder = new(uri) { UserName = string.Empty, Password = string.Empty, Query = string.Empty, Fragment = string.Empty };
        return builder.Uri.AbsoluteUri;
    }
}
