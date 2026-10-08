using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlFormSubmitter {
    private static readonly Lazy<string> BrowserSubmitScript = new(() => {
        using Stream stream = typeof(HtmlFormSubmitter).Assembly.GetManifestResourceStream(
            "HtmlTinkerX.Playwright.Scripts.HtmlFormSubmit.js")
            ?? throw new InvalidOperationException("The browser form submission script is missing.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    });

    /// <summary>Sets named form controls and submits with native validation and submit events.</summary>
    /// <param name="page">Page containing the form.</param>
    /// <param name="formSelector">Selector identifying exactly one form.</param>
    /// <param name="fields">Overrides by control name. Selects and radio groups use option values; a single checkbox also accepts true or false.</param>
    /// <param name="timeout">Timeout in milliseconds for the complete operation. Zero disables it.</param>
    /// <param name="cancellationToken">Cancellation covering control updates and navigation.</param>
    /// <remarks>Other values retain their current state. Controls associated through a form attribute are included.
    /// A navigation in this page is awaited through DOMContentLoaded. Validation failures and submit handlers that
    /// prevent default submission return without waiting for navigation. Other targets and asynchronous application
    /// handlers require their own completion checks.</remarks>
    public static Task SubmitAsync(IPage page, string formSelector, IDictionary<string, string> fields, int timeout = 10000, CancellationToken cancellationToken = default) {
        if (page == null) throw new ArgumentNullException(nameof(page));
        if (formSelector == null) throw new ArgumentNullException(nameof(formSelector));
        return SubmitBrowserFormAsync(page, page.Locator(formSelector), fields, timeout, cancellationToken);
    }

    /// <summary>Submits a parsed form in a browser using its exact id or document-wide form index.</summary>
    /// <param name="page">Page containing the parsed form.</param>
    /// <param name="form">Parsed form with metadata from the current document.</param>
    /// <param name="fields">Named control overrides. Other controls retain their current state.</param>
    /// <param name="timeout">Timeout in milliseconds for the complete operation. Zero disables it.</param>
    /// <param name="cancellationToken">Cancellation covering control updates and navigation.</param>
    /// <remarks>Refresh the metadata if the document's form structure changes.</remarks>
    public static Task SubmitAsync(IPage page, HtmlFormResult form, IDictionary<string, string> fields, int timeout = 10000, CancellationToken cancellationToken = default) {
        if (page == null) throw new ArgumentNullException(nameof(page));
        if (form == null) throw new ArgumentNullException(nameof(form));
        if (form.Metadata.FormIndex < 0) throw new ArgumentOutOfRangeException(nameof(form), "The form index must be zero or greater.");
        ILocator locator = !string.IsNullOrEmpty(form.Metadata.Id)
            ? page.Locator($"form[id=\"{HtmlLoginParser.CssStringEscape(form.Metadata.Id!)}\"]")
            : page.Locator("form").Nth(form.Metadata.FormIndex);
        return SubmitBrowserFormAsync(page, locator, fields, timeout, cancellationToken);
    }

    private static async Task SubmitBrowserFormAsync(IPage page, ILocator form, IDictionary<string, string> fields, int timeout, CancellationToken cancellationToken) {
        if (fields == null) throw new ArgumentNullException(nameof(fields));
        if (timeout < 0) throw new ArgumentOutOfRangeException(nameof(timeout));
        cancellationToken.ThrowIfCancellationRequested();
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout > 0) deadline.CancelAfter(timeout);
        CancellationToken token = deadline.Token;
        try {
            await form.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = timeout })
                .WaitWithCancellationAsync(token).ConfigureAwait(false);
            foreach (KeyValuePair<string, string> field in fields) {
                token.ThrowIfCancellationRequested();
                await SetBrowserFormFieldAsync(form, field.Key, field.Value, timeout, token).ConfigureAwait(false);
            }
            token.ThrowIfCancellationRequested();
            await RequestBrowserFormSubmissionAsync(page, form, timeout, token).ConfigureAwait(false);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw new OperationCanceledException(cancellationToken);
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested) {
            throw new TimeoutException($"Browser form submission exceeded its {timeout} ms timeout.");
        }
    }

    private static async Task SetBrowserFormFieldAsync(ILocator form, string name, string value, int timeout, CancellationToken token) {
        while (true) {
            token.ThrowIfCancellationRequested();
            IJSHandle controls = await AwaitOwnedBrowserResultAsync(form.EvaluateHandleAsync(@"(form, name) => {
                const prototype = form.ownerDocument.defaultView.HTMLFormElement.prototype;
                const elements = Object.getOwnPropertyDescriptor(prototype, 'elements').get.call(form);
                return Array.from(elements).filter(control => control.name === name);
            }", name), handle => handle.DisposeAsync().AsTask(), token).ConfigureAwait(false);
            IReadOnlyDictionary<string, IJSHandle>? properties = null;
            try {
                properties = await AwaitOwnedBrowserResultAsync(controls.GetPropertiesAsync(),
                    handles => Task.WhenAll(handles.Values.Select(handle => handle.DisposeAsync().AsTask())), token).ConfigureAwait(false);
                IElementHandle[] elements = properties.Values.Select(handle => handle.AsElement()).Where(element => element != null).Cast<IElementHandle>().ToArray();
                if (elements.Length == 0) {
                    // Forms may appear before their controls in applications that render asynchronously.
                    await Task.Delay(50, token).ConfigureAwait(false);
                    continue;
                }
                List<(IElementHandle Element, string Kind, string Value)> descriptions = new();
                foreach (IElementHandle element in elements) {
                    string[] description = await element.EvaluateAsync<string[]>("control => [control.tagName === 'INPUT' ? control.type : control.tagName.toLowerCase(), control.value]")
                        .WaitWithCancellationAsync(token).ConfigureAwait(false);
                    descriptions.Add((element, description[0], description[1]));
                }
                var toggles = descriptions.Where(control => control.Kind == "radio" || control.Kind == "checkbox").ToList();
                if (toggles.Count > 0) {
                    string kind = toggles[0].Kind;
                    if (descriptions.Any(control => control.Kind != kind && control.Kind != "hidden")) {
                        throw new ArgumentException($"Field '{name}' contains different control types.", nameof(name));
                    }
                    // A hidden fallback value paired with a checkbox retains its original value.
                    bool[] selected = toggles.Select(control => string.Equals(control.Value, value, StringComparison.Ordinal)).ToArray();
                    bool hasBooleanValue = bool.TryParse(value, out bool checkedValue);
                    bool singleCheckbox = kind == "checkbox" && toggles.Count == 1;
                    if (singleCheckbox && !selected[0] && hasBooleanValue) selected[0] = checkedValue;
                    bool clearsCheckboxes = kind == "checkbox" && (value.Length == 0 || (singleCheckbox && hasBooleanValue && !checkedValue));
                    if (!selected.Any(state => state) && !clearsCheckboxes) {
                        throw new ArgumentException($"Field '{name}' has no selectable value '{value}'.", nameof(value));
                    }
                    for (int index = 0; index < toggles.Count; index++) {
                        token.ThrowIfCancellationRequested();
                        IElementHandle element = toggles[index].Element;
                        if (selected[index]) await element.CheckAsync(new ElementHandleCheckOptions { Timeout = timeout }).WaitWithCancellationAsync(token).ConfigureAwait(false);
                        else if (kind == "checkbox") await element.UncheckAsync(new ElementHandleUncheckOptions { Timeout = timeout }).WaitWithCancellationAsync(token).ConfigureAwait(false);
                    }
                } else {
                    if (elements.Length != 1) throw new ArgumentException($"Field '{name}' needs exactly one text or select control.", nameof(name));
                    IElementHandle element = elements[0];
                    token.ThrowIfCancellationRequested();
                    if (descriptions[0].Kind == "select") {
                        await element.SelectOptionAsync(value, new ElementHandleSelectOptionOptions { Timeout = timeout }).WaitWithCancellationAsync(token).ConfigureAwait(false);
                    } else if (descriptions[0].Kind == "hidden") {
                        await element.EvaluateAsync(@"(control, value) => {
                            const view = control.ownerDocument.defaultView;
                            Object.getOwnPropertyDescriptor(view.HTMLInputElement.prototype, 'value').set.call(control, value);
                            control.dispatchEvent(new view.Event('input', { bubbles: true }));
                            control.dispatchEvent(new view.Event('change', { bubbles: true }));
                        }", value).WaitWithCancellationAsync(token).ConfigureAwait(false);
                    } else await element.FillAsync(value, new ElementHandleFillOptions { Timeout = timeout }).WaitWithCancellationAsync(token).ConfigureAwait(false);
                }
                return;
            } finally {
                List<Task> disposing = properties?.Values.Select(handle => handle.DisposeAsync().AsTask()).ToList() ?? new();
                disposing.Add(controls.DisposeAsync().AsTask());
                await Task.WhenAll(disposing).WaitWithCancellationAsync(token).ConfigureAwait(false);
            }
        }
    }

    private static async Task<T> AwaitOwnedBrowserResultAsync<T>(Task<T> pending, Func<T, Task> dispose, CancellationToken token) {
        try {
            return await pending.WaitWithCancellationAsync(token).ConfigureAwait(false);
        } catch (OperationCanceledException) {
            // Cancellation cannot cancel a Playwright protocol call. Release handles returned after it.
            _ = DisposeLateBrowserResultAsync(pending, dispose);
            throw;
        }
    }

    private static async Task DisposeLateBrowserResultAsync<T>(Task<T> pending, Func<T, Task> dispose) {
        try {
            T result = await pending.ConfigureAwait(false);
            await dispose(result).ConfigureAwait(false);
        } catch (Exception) {
            // The owning operation has already failed; observe protocol and late-disposal failures.
        }
    }

    private static async Task RequestBrowserFormSubmissionAsync(IPage page, ILocator form, int timeout, CancellationToken token) {
        string initialUrl = page.Url;
        string documentMarker = "__htmlTinkerXForm_" + Guid.NewGuid().ToString("N");
        TaskCompletionSource<bool> navigation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int observing = 1;
        int armed = 0;
        _ = navigation.Task.ContinueWith(static completed => _ = completed.Exception, TaskContinuationOptions.OnlyOnFaulted);
        async Task CheckDocumentAsync() {
            if (Volatile.Read(ref armed) == 0) return;
            try {
                bool completed = await page.EvaluateAsync<bool>(
                    "state => document[state.marker] !== true || location.href !== state.url",
                    new { marker = documentMarker, url = initialUrl })
                    .WaitWithCancellationAsync(token).ConfigureAwait(false);
                if (completed) navigation.TrySetResult(true);
            } catch (PlaywrightException ex) when (!page.IsClosed && ex.Message.Contains("Execution context was destroyed", StringComparison.Ordinal)) {
                // The new document's DOMContentLoaded event checks again after its context is ready.
            } catch (Exception ex) {
                if (Volatile.Read(ref observing) != 0) navigation.TrySetException(ex);
            }
        }
        void OnNavigated(object? sender, IFrame frame) {
            if (frame == page.MainFrame) _ = CheckDocumentAsync();
        }
        void OnContentLoaded(object? sender, IPage loadedPage) => _ = CheckDocumentAsync();
        void OnFailed(object? sender, IRequest request) {
            if (request.IsNavigationRequest && request.Frame == page.MainFrame) {
                navigation.TrySetException(new PlaywrightException($"Form navigation failed: {request.Failure}"));
            }
        }
        page.FrameNavigated += OnNavigated;
        page.DOMContentLoaded += OnContentLoaded;
        page.RequestFailed += OnFailed;
        try {
            await form.EvaluateAsync("(form, marker) => { Object.defineProperty(form.ownerDocument, marker, { value: true, configurable: true }); }", documentMarker)
                .WaitWithCancellationAsync(token).ConfigureAwait(false);
            Volatile.Write(ref armed, 1);
            BrowserFormSubmission submission = await form.EvaluateAsync<BrowserFormSubmission>(BrowserSubmitScript.Value).WaitWithCancellationAsync(token).ConfigureAwait(false);
            if (!submission.Submitted || !submission.NavigatesHere) return;
            if (submission.SameDocumentUrl != null) {
                await using IJSHandle location = await AwaitOwnedBrowserResultAsync(
                    page.WaitForFunctionAsync("destination => location.href === destination", submission.SameDocumentUrl,
                        new PageWaitForFunctionOptions { Timeout = timeout }), handle => handle.DisposeAsync().AsTask(), token).ConfigureAwait(false);
                return;
            }
            await navigation.Task.WaitWithCancellationAsync(token).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions { Timeout = timeout })
                .WaitWithCancellationAsync(token).ConfigureAwait(false);
        } finally {
            Volatile.Write(ref observing, 0);
            page.FrameNavigated -= OnNavigated;
            page.DOMContentLoaded -= OnContentLoaded;
            page.RequestFailed -= OnFailed;
            if (navigation.Task.IsFaulted) _ = navigation.Task.Exception;
            if (!page.IsClosed) {
                try {
                    await page.EvaluateAsync("marker => { delete document[marker]; }", documentMarker)
                        .WaitWithCancellationAsync(token).ConfigureAwait(false);
                } catch (PlaywrightException ex) when (ex.Message.Contains("Execution context was destroyed", StringComparison.Ordinal)) {
                    // The discarded document owns the marker and no longer needs cleanup.
                }
            }
        }
    }

    private sealed class BrowserFormSubmission {
        public bool Submitted { get; set; }
        public bool NavigatesHere { get; set; }
        public string? SameDocumentUrl { get; set; }
    }
}
