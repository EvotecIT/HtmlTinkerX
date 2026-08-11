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
                HtmlBrowserNetworkPolicyEvaluator policy = _networkPolicy;
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
                    using CancellationTokenSource setupDeadline = CancellationTokenSource.CreateLinkedTokenSource(operationToken);
                    setupDeadline.CancelAfter(_options.SetupTimeout);
                    CancellationToken setupToken = setupDeadline.Token;
                    BrowserSlot slot;
                    try {
                        slot = await RentSlotAsync(setupToken).ConfigureAwait(false);
                    } catch (OperationCanceledException) when (!operationToken.IsCancellationRequested && setupDeadline.IsCancellationRequested) {
                        throw new TimeoutException($"Browser capture setup did not complete within {_options.SetupTimeout.TotalMilliseconds:0} ms.");
                    }
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
                            setupToken,
                            operationToken).ConfigureAwait(false);
                    } catch (TimeoutException) {
                        throw;
                    } catch (Exception) when (CanRetryBrowserFailure(
                        request,
                        attempt,
                        operationToken.IsCancellationRequested,
                        IsBrowserProcessFailure(slot))) {
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

    internal static bool CanRetryBrowserFailure(
        HtmlBrowserPdfRequest request,
        int attempt,
        bool cancellationRequested,
        bool browserProcessFailure) =>
        request.RetryOnBrowserFailure
        && attempt == 0
        && !cancellationRequested
        && browserProcessFailure;

    private async Task<HtmlBrowserPdfResult> CaptureWithSlotAsync(
        BrowserSlot slot,
        HtmlBrowserPdfRequest request,
        HtmlBrowserNetworkPolicyEvaluator policy,
        string? selectedFileDirectory,
        TimeSpan queueDuration,
        bool retried,
        CancellationToken setupCancellationToken,
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
        HtmlBrowserScopedHeaderInterceptor? scopedHeaders = null;
        HtmlBrowserPopupHeaderCoordinator? popupCoordinator = null;
        try {
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
                    await route.ContinueAsync().ConfigureAwait(false);
                    return;
                }

                RecordBlockedRequest(route.Request.Url);
                await route.AbortAsync("blockedbyclient").ConfigureAwait(false);
            };
            IPage page;
            try {
                context = await ExecuteCancellableSlotOperationAsync(
                    slot,
                    () => slot.Browser.NewContextAsync(contextOptions),
                    setupCancellationToken).ConfigureAwait(false);
                await ExecuteCancellableSlotOperationAsync(
                    slot,
                    () => context.RouteAsync("**/*", policyRoute),
                    setupCancellationToken).ConfigureAwait(false);
                page = await ExecuteCancellableSlotOperationAsync(
                    slot,
                    () => context.NewPageAsync(),
                    setupCancellationToken).ConfigureAwait(false);
                await ExecuteCancellableSlotOperationAsync(
                    slot,
                    () => AddCookiesAsync(context, page, request.Cookies),
                    setupCancellationToken).ConfigureAwait(false);
                page.SetDefaultTimeout(request.Readiness.Timeout);
                await ExecuteCancellablePageOperationAsync(
                    page,
                    () => HtmlBrowserStorageInitializer.AddAsync(page, request),
                    setupCancellationToken).ConfigureAwait(false);
                scopedHeaders = request.Headers.Count == 0
                    ? null
                    : await ExecuteCancellableSlotOperationAsync(
                        slot,
                        () => HtmlBrowserScopedHeaderInterceptor.CreateAsync(
                            context,
                            page,
                            request.Source.SecurityOrigin,
                            request.Headers,
                            cancellationToken,
                            slot.MarkBroken),
                        setupCancellationToken).ConfigureAwait(false);
                popupCoordinator = request.Headers.Count == 0
                    ? null
                    : new HtmlBrowserPopupHeaderCoordinator(
                        context,
                        page,
                        request.Source.SecurityOrigin,
                        request.Headers,
                        cancellationToken,
                        slot.MarkBroken);
                if (popupCoordinator != null) {
                    await ExecuteCancellablePageOperationAsync(
                        page,
                        () => HtmlBrowserPopupHeaderCoordinator.AddNavigationShimAsync(page),
                        setupCancellationToken).ConfigureAwait(false);
                }
            } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && setupCancellationToken.IsCancellationRequested) {
                await AbortSlotAsync(slot).ConfigureAwait(false);
                throw new TimeoutException($"Browser capture setup did not complete within {_options.SetupTimeout.TotalMilliseconds:0} ms.");
            }
            long navigationStarted = Stopwatch.GetTimestamp();
            await LoadSourceAsync(page, request.Source, request.NavigationTimeout, cancellationToken).ConfigureAwait(false);
            TimeSpan navigationDuration = StopwatchElapsed(navigationStarted);

            long readinessStarted = Stopwatch.GetTimestamp();
            await PreparePageAsync(page, request, cancellationToken).ConfigureAwait(false);
            TimeSpan readinessDuration = StopwatchElapsed(readinessStarted);

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
                        () => request.MaximumPdfBytes == 0
                            ? page.PdfAsync(HtmlBrowserPdfCapture.CreatePageOptions(request.PdfOptions))
                            : HtmlBrowserPdfCapture.PrintToPdfBoundedAsync(
                                page,
                                request.PdfOptions,
                                request.MaximumPdfBytes,
                                pdfToken),
                        pdfToken),
                    () => AbortSlotAsync(slot),
                    pdfToken).ConfigureAwait(false);
                bytes = HtmlBrowserPdfCapture.ValidateOutputSize(bytes, request.MaximumPdfBytes);
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
            if (popupCoordinator != null) await popupCoordinator.DisposeAsync().ConfigureAwait(false);
            if (scopedHeaders != null) await scopedHeaders.DisposeAsync().ConfigureAwait(false);
            if (context != null) {
                await CloseContextAsync(context, slot).ConfigureAwait(false);
            }
            if (slot.PolicyProxy != null && blockedByProxy != null) slot.PolicyProxy.RequestBlocked -= blockedByProxy;
        }
    }

    internal BrowserNewContextOptions CreateContextOptions(HtmlBrowserPdfRequest request) {
        BrowserNewContextOptions options = new() {
            AcceptDownloads = false,
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
                string html = AddBaseElement(source.Html!, source.ResourceBaseUri);
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

        bool hasCaptureStyle = !string.IsNullOrWhiteSpace(request.StyleSheetContent);
        if (hasCaptureStyle) {
            await ApplyStyleSheetAsync(page, request.StyleSheetContent!, request.Readiness.Timeout, cancellationToken).ConfigureAwait(false);
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
            if (hasCaptureStyle) {
                await ApplyStyleSheetAsync(page, request.StyleSheetContent!, request.Readiness.Timeout, cancellationToken).ConfigureAwait(false);
            }
        }

        await HtmlBrowserPdfCapture.WaitForReadinessAsync(page, request.Readiness, cancellationToken).ConfigureAwait(false);
        if (hasCaptureStyle) {
            await ApplyStyleSheetAsync(page, request.StyleSheetContent!, request.Readiness.Timeout, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task AddCookiesAsync(
        IBrowserContext context,
        IPage page,
        IReadOnlyList<HtmlBrowserPdfCookie> cookies) {
        if (cookies.Count == 0) return;
        if (cookies.All(cookie => !cookie.Expires.HasValue)) {
            await context.AddCookiesAsync(cookies.Select(CreatePlaywrightCookie)).ConfigureAwait(false);
            return;
        }

        ICDPSession? session = null;
        try {
            session = await context.NewCDPSessionAsync(page).ConfigureAwait(false);
            await session.SendAsync("Network.setCookies", new Dictionary<string, object> {
                ["cookies"] = cookies.Select(CreateCdpCookie).ToArray()
            }).ConfigureAwait(false);
        } finally {
            if (session != null) await session.DetachAsync().ConfigureAwait(false);
        }
    }

    private static Cookie CreatePlaywrightCookie(HtmlBrowserPdfCookie cookie) => new() {
        Name = cookie.Name,
        Value = cookie.Value,
        Url = cookie.Url,
        Domain = cookie.Domain,
        Path = cookie.Path,
        HttpOnly = cookie.HttpOnly,
        Secure = cookie.Secure,
        SameSite = CreatePlaywrightSameSite(cookie.SameSite)
    };

    internal static Dictionary<string, object> CreateCdpCookie(HtmlBrowserPdfCookie cookie) {
        Dictionary<string, object> value = new() {
            ["name"] = cookie.Name,
            ["value"] = cookie.Value
        };
        if (cookie.Url != null) value["url"] = cookie.Url;
        if (cookie.Domain != null) value["domain"] = cookie.Domain;
        if (cookie.Path != null) value["path"] = cookie.Path;
        if (cookie.Expires.HasValue) value["expires"] = (double)cookie.Expires.Value;
        if (cookie.HttpOnly.HasValue) value["httpOnly"] = cookie.HttpOnly.Value;
        if (cookie.Secure.HasValue) value["secure"] = cookie.Secure.Value;
        if (cookie.SameSite.HasValue) value["sameSite"] = cookie.SameSite.Value switch {
            HtmlBrowserCookieSameSite.Lax => "Lax",
            HtmlBrowserCookieSameSite.Strict => "Strict",
            HtmlBrowserCookieSameSite.None => "None",
            _ => throw new ArgumentOutOfRangeException(nameof(cookie))
        };
        return value;
    }

    private static SameSiteAttribute? CreatePlaywrightSameSite(HtmlBrowserCookieSameSite? sameSite) => sameSite switch {
        HtmlBrowserCookieSameSite.Lax => SameSiteAttribute.Lax,
        HtmlBrowserCookieSameSite.Strict => SameSiteAttribute.Strict,
        HtmlBrowserCookieSameSite.None => SameSiteAttribute.None,
        _ => null
    };

    private static async Task ApplyStyleSheetAsync(IPage page, string styleSheetContent, int timeout, CancellationToken cancellationToken) {
        using CancellationTokenSource? deadline = timeout == 0
            ? null
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline?.CancelAfter(timeout);
        CancellationToken operationToken = deadline?.Token ?? cancellationToken;
        try {
            foreach (IFrame frame in page.Frames) {
                operationToken.ThrowIfCancellationRequested();
                if (frame.IsDetached) continue;
                try {
                    await ExecuteCancellablePageOperationAsync(
                        page,
                        () => frame.EvaluateAsync(@"css => {
                            const attribute = 'data-htmltinkerx-pdf-capture-style';
                            let style = document.querySelector(`style[${attribute}]`);
                            if (!style) {
                                style = document.createElement('style');
                                style.setAttribute(attribute, '');
                                (document.head || document.documentElement).prepend(style);
                            }
                            style.textContent = css;
                        }", styleSheetContent),
                        operationToken).ConfigureAwait(false);
                } catch (PlaywrightException) when (frame.IsDetached) {
                    // A frame can detach between the snapshot and style injection.
                }
            }
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline?.IsCancellationRequested == true) {
            throw new TimeoutException($"Capture stylesheet injection did not complete within {timeout} ms.");
        }
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
