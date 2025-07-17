using Microsoft.Playwright;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for interacting with form elements using Playwright.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Fills text into an element identified by a CSS selector.
    /// </summary>
    public static async Task FillInputAsync(IPage page, string selector, string value, int timeout = 10000, CancellationToken cancellationToken = default) {
        var locator = page.Locator(selector);
        cancellationToken.ThrowIfCancellationRequested();
        await locator.WaitForAsync(new LocatorWaitForOptions { Timeout = timeout });
        cancellationToken.ThrowIfCancellationRequested();
        await locator.FillAsync(value, new LocatorFillOptions { Timeout = timeout });
    }

    /// <summary>
    /// Fills text into an element using an existing browser session.
    /// </summary>
    public static Task FillInputAsync(HtmlBrowserSession session, string selector, string value, int timeout = 10000, CancellationToken cancellationToken = default)
        => FillInputAsync(session.Page, selector, value, timeout, cancellationToken);

    /// <summary>
    /// Sets the checked state of a checkbox or radio input.
    /// </summary>
    public static async Task SetCheckedAsync(IPage page, string selector, bool check = true, int timeout = 10000, CancellationToken cancellationToken = default) {
        var locator = page.Locator(selector);
        cancellationToken.ThrowIfCancellationRequested();
        await locator.WaitForAsync(new LocatorWaitForOptions { Timeout = timeout });
        if (check) {
            cancellationToken.ThrowIfCancellationRequested();
            await locator.CheckAsync(new LocatorCheckOptions { Timeout = timeout });
        } else {
            cancellationToken.ThrowIfCancellationRequested();
            await locator.UncheckAsync(new LocatorUncheckOptions { Timeout = timeout });
        }
    }

    /// <summary>
    /// Sets the checked state of a checkbox or radio input using a session.
    /// </summary>
    public static Task SetCheckedAsync(HtmlBrowserSession session, string selector, bool check = true, int timeout = 10000, CancellationToken cancellationToken = default)
        => SetCheckedAsync(session.Page, selector, check, timeout, cancellationToken);

    /// <summary>
    /// Selects option values from a &lt;select&gt; element.
    /// </summary>
    public static async Task SelectOptionAsync(IPage page, string selector, IEnumerable<string> values, int timeout = 10000, CancellationToken cancellationToken = default) {
        var locator = page.Locator(selector);
        cancellationToken.ThrowIfCancellationRequested();
        await locator.WaitForAsync(new LocatorWaitForOptions { Timeout = timeout });
        cancellationToken.ThrowIfCancellationRequested();
        await locator.SelectOptionAsync(values, new LocatorSelectOptionOptions { Timeout = timeout });
    }

    /// <summary>
    /// Selects option values from a &lt;select&gt; element using a session.
    /// </summary>
    public static Task SelectOptionAsync(HtmlBrowserSession session, string selector, IEnumerable<string> values, int timeout = 10000, CancellationToken cancellationToken = default)
        => SelectOptionAsync(session.Page, selector, values, timeout, cancellationToken);

    /// <summary>
    /// Performs a mouse click on an element.
    /// </summary>
    public static async Task MouseClickAsync(IPage page, string selector, MouseButton button = MouseButton.Left, int clickCount = 1, KeyboardModifier[]? modifiers = null, int timeout = 10000, CancellationToken cancellationToken = default) {
        var locator = page.Locator(selector);
        cancellationToken.ThrowIfCancellationRequested();
        await locator.WaitForAsync(new LocatorWaitForOptions { Timeout = timeout });
        var options = new LocatorClickOptions { Button = button, ClickCount = clickCount, Timeout = timeout };
        if (modifiers != null) {
            options.Modifiers = modifiers;
        }
        cancellationToken.ThrowIfCancellationRequested();
        await locator.ClickAsync(options);
    }

    /// <summary>
    /// Performs a mouse click on an element using a session.
    /// </summary>
    public static Task MouseClickAsync(HtmlBrowserSession session, string selector, MouseButton button = MouseButton.Left, int clickCount = 1, KeyboardModifier[]? modifiers = null, int timeout = 10000, CancellationToken cancellationToken = default)
        => MouseClickAsync(session.Page, selector, button, clickCount, modifiers, timeout, cancellationToken);
}
