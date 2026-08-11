namespace HtmlTinkerX;

using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharpHtmlParser = AngleSharp.Html.Parser.HtmlParser;
using Microsoft.Playwright;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            bool leaseAcquired = false;
            bool countedAsQueued = false;
            try {
                HtmlBrowserNetworkPolicyEvaluator policy = new(_options.NetworkPolicy);
                string? selectedFileDirectory = request.Source.Kind == HtmlBrowserPdfSourceKind.File
                    ? Path.GetDirectoryName(request.Source.FilePath!)
                    : request.Source.FileBaseDirectory;
                await ValidateInitialSourceAsync(
                    request.Source,
                    policy,
                    selectedFileDirectory,
                    _options.ProxyOwnsNetworkResolution,
                    operationToken).ConfigureAwait(false);

                long queueStarted = Stopwatch.GetTimestamp();
                countedAsQueued = true;
                Interlocked.Increment(ref _queued);
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
                    HtmlBrowserPdfResult? completedResult = null;
                    try {
                        completedResult = await CaptureWithSlotAsync(
                            slot,
                            request,
                            policy,
                            selectedFileDirectory,
                            queueDuration,
                            retried,
                            operationToken).ConfigureAwait(false);
                    } catch (TimeoutException) {
                        throw;
                    } catch (Exception) when (attempt == 0 && !operationToken.IsCancellationRequested && IsBrowserProcessFailure(slot)) {
                        retried = true;
                        returnSlot = false;
                        slot.MarkBroken();
                        Interlocked.Increment(ref _retries);
                        await RecycleSlotAsync(slot).ConfigureAwait(false);
                    } finally {
                        if (returnSlot) await ReturnSlotAsync(slot).ConfigureAwait(false);
                    }
                    if (completedResult != null) {
                        Interlocked.Increment(ref _succeeded);
                        return completedResult.WithTotalDuration(StopwatchElapsed(totalStarted));
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
        HtmlBrowserNetworkPolicyEvaluator policy,
        string? selectedFileDirectory,
        TimeSpan queueDuration,
        bool retried,
        CancellationToken cancellationToken) {
        ConcurrentQueue<string> blockedRequests = new();
        int blockedRequestCount = 0;
        int blockedRequestSamples = 0;
        List<string> warnings = new();
        void RecordBlockedRequest(string url) {
            Interlocked.Increment(ref blockedRequestCount);
            if (Interlocked.Increment(ref blockedRequestSamples) <= _options.NetworkPolicy.BlockedRequestDiagnosticLimit) {
                blockedRequests.Enqueue(SanitizeUri(url));
            }
        }
        Action<Uri>? blockedByProxy = null;
        if (slot.PolicyProxy != null) {
            blockedByProxy = uri => RecordBlockedRequest(uri.AbsoluteUri);
            slot.PolicyProxy.RequestBlocked += blockedByProxy;
        }
        BrowserNewContextOptions contextOptions = CreateContextOptions(request);
        IBrowserContext? context = null;
        HtmlBrowserPopupHeaderCoordinator? popupHeaders = null;
        try {
            context = await ExecuteCancellableSlotOperationAsync(
                slot,
                () => slot.Browser.NewContextAsync(contextOptions),
                cancellationToken).ConfigureAwait(false);
            Func<IRoute, Task> policyRoute = async route => {
                bool allowed;
                try {
                    allowed = await policy.IsAllowedAsync(
                        route.Request.Url,
                        selectedFileDirectory,
                        _options.ProxyOwnsNetworkResolution,
                        cancellationToken).ConfigureAwait(false);
                } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                    await route.AbortAsync("aborted").ConfigureAwait(false);
                    return;
                }

                if (allowed) {
                    if (popupHeaders?.RequiresDocumentBridge(route.Request) == true) {
                        await popupHeaders.ContinueInitialDocumentAsync(route).ConfigureAwait(false);
                    } else {
                        await route.ContinueAsync().ConfigureAwait(false);
                    }
                    return;
                }

                RecordBlockedRequest(route.Request.Url);
                await route.AbortAsync("blockedbyclient").ConfigureAwait(false);
            };
            await ExecuteCancellableSlotOperationAsync(
                slot,
                () => context.RouteAsync("**/*", policyRoute),
                cancellationToken).ConfigureAwait(false);
            await ExecuteCancellableSlotOperationAsync(
                slot,
                () => AddCookiesAsync(context, request.Cookies),
                cancellationToken).ConfigureAwait(false);
            IPage page = await ExecuteCancellableSlotOperationAsync(
                slot,
                () => context.NewPageAsync(),
                cancellationToken).ConfigureAwait(false);
            page.SetDefaultTimeout(request.Readiness.Timeout);
            await ExecuteCancellablePageOperationAsync(
                page,
                () => HtmlBrowserStorageInitializer.AddAsync(page, request),
                cancellationToken).ConfigureAwait(false);
            await using HtmlBrowserScopedHeaderInterceptor? scopedHeaders = request.Headers.Count == 0
                ? null
                : await ExecuteCancellableSlotOperationAsync(
                    slot,
                    () => HtmlBrowserScopedHeaderInterceptor.CreateAsync(
                        context,
                        page,
                        request.Source.SecurityOrigin,
                        request.Headers,
                        cancellationToken),
                    cancellationToken).ConfigureAwait(false);
            await using HtmlBrowserPopupHeaderCoordinator? popupCoordinator = request.Headers.Count == 0
                ? null
                : new HtmlBrowserPopupHeaderCoordinator(
                    context,
                    page,
                    request.Source.SecurityOrigin,
                    request.Headers,
                    cancellationToken);
            popupHeaders = popupCoordinator;
            if (popupCoordinator != null) {
                await ExecuteCancellablePageOperationAsync(
                    page,
                    () => HtmlBrowserPopupHeaderCoordinator.AddNavigationShimAsync(page),
                    cancellationToken).ConfigureAwait(false);
            }
            long navigationStarted = Stopwatch.GetTimestamp();
            await LoadSourceAsync(page, request.Source, request.NavigationTimeout, cancellationToken).ConfigureAwait(false);
            TimeSpan navigationDuration = StopwatchElapsed(navigationStarted);

            long readinessStarted = Stopwatch.GetTimestamp();
            await PreparePageAsync(page, request, cancellationToken).ConfigureAwait(false);
            TimeSpan readinessDuration = StopwatchElapsed(readinessStarted);

            PagePdfOptions pdfOptions = HtmlBrowserPdfCapture.CreatePageOptions(request.PdfOptions);
            long pdfStarted = Stopwatch.GetTimestamp();
            using CancellationTokenSource? pdfDeadline = request.PdfTimeout == 0
                ? null
                : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            pdfDeadline?.CancelAfter(request.PdfTimeout);
            byte[] bytes;
            try {
                CancellationToken pdfToken = pdfDeadline?.Token ?? cancellationToken;
                bytes = await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(
                    () => HtmlBrowser.ExecuteWithTemporaryVisualMaskAsync(
                        page,
                        request.PdfOptions.MaskSensitiveElements,
                        request.PdfOptions.MaskSelectors,
                        request.PdfOptions.MaskColor,
                        () => page.PdfAsync(pdfOptions),
                        pdfToken),
                    () => AbortSlotAsync(slot),
                    pdfToken).ConfigureAwait(false);
            } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && pdfDeadline?.IsCancellationRequested == true) {
                throw new TimeoutException($"Chromium PDF generation did not complete within {request.PdfTimeout} ms.");
            }
            TimeSpan pdfDuration = StopwatchElapsed(pdfStarted);

            if (bytes.Length == 0) warnings.Add("Chromium returned an empty PDF payload.");
            string finalUrl = SanitizeUri(page.Url);
            string browserVersion = slot.Browser.Version;
            await CloseContextAsync(context, slot).ConfigureAwait(false);
            context = null;
            if (slot.PolicyProxy != null && blockedByProxy != null) {
                slot.PolicyProxy.RequestBlocked -= blockedByProxy;
                blockedByProxy = null;
            }
            HtmlBrowserPdfDiagnostics diagnostics = new(
                request.Source.Kind,
                slot.Id,
                slot.RenderCount > 0,
                retried,
                finalUrl,
                browserVersion,
                queueDuration,
                navigationDuration,
                readinessDuration,
                pdfDuration,
                TimeSpan.Zero,
                Volatile.Read(ref blockedRequestCount),
                blockedRequests.ToArray(),
                warnings.ToArray());
            return new HtmlBrowserPdfResult(bytes, diagnostics);
        } finally {
            if (context != null) {
                await CloseContextAsync(context, slot).ConfigureAwait(false);
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
            TimezoneId = _options.Timezone
        };
        if (_options.ViewportWidth.HasValue && _options.ViewportHeight.HasValue) {
            options.ViewportSize = new ViewportSize { Width = _options.ViewportWidth.Value, Height = _options.ViewportHeight.Value };
        }
        return options;
    }

    private static async Task ValidateInitialSourceAsync(
        HtmlBrowserPdfSource source,
        HtmlBrowserNetworkPolicyEvaluator policy,
        string? fileDirectory,
        bool deferNetworkResolutionToProxy,
        CancellationToken cancellationToken) {
        string? target = source.Kind switch {
            HtmlBrowserPdfSourceKind.Url => source.Uri!.AbsoluteUri,
            HtmlBrowserPdfSourceKind.File => new Uri(source.FilePath!).AbsoluteUri,
            HtmlBrowserPdfSourceKind.Html when source.BaseUri?.IsFile == true && fileDirectory != null =>
                new Uri(Path.GetFullPath(fileDirectory) + Path.DirectorySeparatorChar).AbsoluteUri,
            _ => source.BaseUri?.AbsoluteUri
        };
        if (source.Kind == HtmlBrowserPdfSourceKind.File) {
            if (!HtmlBrowserFileSystemPath.IsSafeLocalPath(source.FilePath!)) {
                throw new UnauthorizedAccessException($"Browser resource policy blocked the capture source '{SanitizeUri(target!)}'.");
            }
            if (!File.Exists(source.FilePath)) {
                throw new FileNotFoundException("HTML input file was not found.", source.FilePath);
            }
        }
        if (target != null && !await policy.IsAllowedAsync(target, fileDirectory, deferNetworkResolutionToProxy, cancellationToken).ConfigureAwait(false)) {
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
                if (source.HtmlDocumentUri == null) {
                    await ExecuteCancellablePageOperationAsync(page, () => page.SetContentAsync(html, new PageSetContentOptions {
                        Timeout = timeout,
                        WaitUntil = WaitUntilState.DOMContentLoaded
                    }), cancellationToken).ConfigureAwait(false);
                } else {
                    string documentUrl = source.HtmlDocumentUri.AbsoluteUri;
                    int initialDocumentFulfilled = 0;
                    Func<IRoute, Task> initialDocumentRoute = route => {
                        if (string.Equals(route.Request.ResourceType, "document", StringComparison.OrdinalIgnoreCase)
                            && Interlocked.CompareExchange(ref initialDocumentFulfilled, 1, 0) == 0) {
                            return route.FulfillAsync(new RouteFulfillOptions {
                                Body = html,
                                ContentType = "text/html; charset=utf-8",
                                Status = 200
                            });
                        }
                        return route.ContinueAsync();
                    };
                    await ExecuteCancellablePageOperationAsync(
                        page,
                        () => page.RouteAsync(documentUrl, initialDocumentRoute),
                        cancellationToken).ConfigureAwait(false);
                    try {
                        await ExecuteCancellablePageOperationAsync(page, () => page.GotoAsync(documentUrl, new PageGotoOptions {
                            Timeout = timeout,
                            WaitUntil = WaitUntilState.DOMContentLoaded
                        }), cancellationToken).ConfigureAwait(false);
                    } finally {
                        try {
                            await page.UnrouteAsync(documentUrl, initialDocumentRoute).ConfigureAwait(false);
                        } catch (PlaywrightException) when (page.IsClosed) {
                            // Cancellation closes the page and removes its routes.
                        }
                    }
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(source));
        }
    }

    private static async Task PreparePageAsync(IPage page, HtmlBrowserPdfRequest request, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        await ExecuteCancellablePageOperationAsync(
            page,
            () => page.EmulateMediaAsync(new PageEmulateMediaOptions {
                Media = request.MediaType == HtmlBrowserPdfMediaType.Screen ? Media.Screen : Media.Print
            }),
            cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(request.StyleSheetContent)) {
            await ExecuteCancellablePageOperationAsync(
                page,
                () => page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = request.StyleSheetContent }),
                cancellationToken).ConfigureAwait(false);
        }
        if (!string.IsNullOrWhiteSpace(request.BeforeCaptureScript)) {
            using CancellationTokenSource? deadline = request.BeforeCaptureScriptTimeout == 0
                ? null
                : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline?.CancelAfter(request.BeforeCaptureScriptTimeout);
            try {
                await ExecuteCancellablePageOperationAsync(
                    page,
                    () => page.EvaluateAsync(request.BeforeCaptureScript!),
                    deadline?.Token ?? cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline?.IsCancellationRequested == true) {
                throw new TimeoutException($"The pre-capture script did not complete within {request.BeforeCaptureScriptTimeout} ms.");
            }
        }

        await HtmlBrowserPdfCapture.WaitForReadinessAsync(page, request.Readiness, cancellationToken).ConfigureAwait(false);
    }

    private static async Task AddCookiesAsync(IBrowserContext context, IReadOnlyList<HtmlBrowserPdfCookie> cookies) {
        if (cookies.Count == 0) return;
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

    private static string AddBaseElement(string html, Uri? baseUri) {
        if (baseUri == null) return html;
        AngleSharpHtmlParser parser = new();
        using IHtmlDocument document = parser.ParseDocument(html);
        IElement baseElement = document.CreateElement("base");
        baseElement.SetAttribute("href", baseUri.AbsoluteUri);
        document.Head!.Prepend(baseElement);
        return document.ToHtml();
    }

    private static async Task ExecuteCancellablePageOperationAsync(IPage page, Func<Task> operation, CancellationToken cancellationToken) {
        await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(operation, () => page.CloseAsync(), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T> ExecuteCancellableSlotOperationAsync<T>(BrowserSlot slot, Func<Task<T>> operation, CancellationToken cancellationToken) {
        return await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(operation, () => AbortSlotAsync(slot), cancellationToken).ConfigureAwait(false);
    }

    private static async Task ExecuteCancellableSlotOperationAsync(BrowserSlot slot, Func<Task> operation, CancellationToken cancellationToken) {
        await HtmlBrowserPdfCapture.ExecuteWithCancellationAsync(operation, () => AbortSlotAsync(slot), cancellationToken).ConfigureAwait(false);
    }

    private static Task AbortSlotAsync(BrowserSlot slot) {
        slot.MarkBroken();
        try {
            if (slot.Browser.IsConnected) {
                Task close = slot.Browser.CloseAsync();
                _ = close.ContinueWith(static completed => _ = completed.Exception, TaskContinuationOptions.OnlyOnFaulted);
            }
        } catch (Exception) {
            // Closing the transport is best-effort; the broken slot will be recycled.
        }
        slot.DisposePlaywright();
        return Task.CompletedTask;
    }

    private static async Task CloseContextAsync(IBrowserContext context, BrowserSlot slot) {
        Task close;
        try {
            close = context.CloseAsync();
        } catch (Exception) {
            slot.MarkBroken();
            return;
        }

        Task completed = await Task.WhenAny(close, Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(false);
        if (completed != close) {
            await AbortSlotAsync(slot).ConfigureAwait(false);
            _ = close.ContinueWith(static finished => _ = finished.Exception, TaskContinuationOptions.OnlyOnFaulted);
            return;
        }
        try {
            await close.ConfigureAwait(false);
        } catch (Exception) {
            slot.MarkBroken();
        }
    }

    private static bool IsBrowserProcessFailure(BrowserSlot slot) =>
        !slot.Browser.IsConnected || slot.IsBroken;

    internal static string SanitizeUri(string url) {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)) return url;
        if (uri.Scheme == Uri.UriSchemeFile) return uri.GetLeftPart(UriPartial.Path);
        bool hasNetworkAuthority = uri.Scheme == Uri.UriSchemeHttp
            || uri.Scheme == Uri.UriSchemeHttps
            || string.Equals(uri.Scheme, "ws", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, "wss", StringComparison.OrdinalIgnoreCase);
        if (!hasNetworkAuthority) return uri.Scheme + ":";
        UriBuilder builder = new(uri) { UserName = string.Empty, Password = string.Empty, Query = string.Empty, Fragment = string.Empty };
        return builder.Uri.AbsoluteUri;
    }

}
