using Microsoft.Playwright;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PSParseHTML;

/// <summary>
/// Helper methods for interacting with form elements using Playwright.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Fills text into an element identified by a CSS selector.
    /// </summary>
    public static async Task FillInputAsync(IPage page, string selector, string value, int timeout = 30000) {
        var locator = page.Locator(selector);
        await locator.WaitForAsync(new LocatorWaitForOptions { Timeout = timeout });
        await locator.FillAsync(value, new LocatorFillOptions { Timeout = timeout });
    }

    /// <summary>
    /// Fills text into an element using an existing browser session.
    /// </summary>
    public static Task FillInputAsync(HtmlBrowserSession session, string selector, string value, int timeout = 30000)
        => FillInputAsync(session.Page, selector, value, timeout);

    /// <summary>
    /// Sets the checked state of a checkbox or radio input.
    /// </summary>
    public static async Task SetCheckedAsync(IPage page, string selector, bool check = true, int timeout = 30000) {
        var locator = page.Locator(selector);
        await locator.WaitForAsync(new LocatorWaitForOptions { Timeout = timeout });
        if (check) {
            await locator.CheckAsync(new LocatorCheckOptions { Timeout = timeout });
        } else {
            await locator.UncheckAsync(new LocatorUncheckOptions { Timeout = timeout });
        }
    }

    /// <summary>
    /// Sets the checked state of a checkbox or radio input using a session.
    /// </summary>
    public static Task SetCheckedAsync(HtmlBrowserSession session, string selector, bool check = true, int timeout = 30000)
        => SetCheckedAsync(session.Page, selector, check, timeout);

    /// <summary>
    /// Selects option values from a &lt;select&gt; element.
    /// </summary>
    public static async Task SelectOptionAsync(IPage page, string selector, IEnumerable<string> values, int timeout = 30000) {
        var locator = page.Locator(selector);
        await locator.WaitForAsync(new LocatorWaitForOptions { Timeout = timeout });
        await locator.SelectOptionAsync(values, new LocatorSelectOptionOptions { Timeout = timeout });
    }

    /// <summary>
    /// Selects option values from a &lt;select&gt; element using a session.
    /// </summary>
    public static Task SelectOptionAsync(HtmlBrowserSession session, string selector, IEnumerable<string> values, int timeout = 30000)
        => SelectOptionAsync(session.Page, selector, values, timeout);

    /// <summary>
    /// Performs a mouse click on an element.
    /// </summary>
    public static async Task MouseClickAsync(IPage page, string selector, MouseButton button = MouseButton.Left, int clickCount = 1, KeyboardModifier[]? modifiers = null, int timeout = 30000) {
        var locator = page.Locator(selector);
        await locator.WaitForAsync(new LocatorWaitForOptions { Timeout = timeout });
        var options = new LocatorClickOptions { Button = button, ClickCount = clickCount, Timeout = timeout };
        if (modifiers != null) {
            options.Modifiers = modifiers;
        }
        await locator.ClickAsync(options);
    }

    /// <summary>
    /// Performs a mouse click on an element using a session.
    /// </summary>
    public static Task MouseClickAsync(HtmlBrowserSession session, string selector, MouseButton button = MouseButton.Left, int clickCount = 1, KeyboardModifier[]? modifiers = null, int timeout = 30000)
        => MouseClickAsync(session.Page, selector, button, clickCount, modifiers, timeout);
}
