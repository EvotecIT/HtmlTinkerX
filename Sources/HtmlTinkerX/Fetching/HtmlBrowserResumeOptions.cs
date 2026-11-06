using System;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// How <see cref="HtmlBrowser.CreateBlockedResume"/> lets a person clear a block: which browser opens the page, how
/// long to wait, and how to tell that the person has finished. Nothing in it solves a check automatically.
/// </summary>
public sealed class HtmlBrowserResumeOptions {
    /// <summary>How long a person has by default to finish in the browser.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The browser that shows the page: a launched headed Chromium when null. Set
    /// <see cref="HtmlBrowserLaunchOptions.CdpEndpointUrl"/> to use the person's own running Chrome, and
    /// <see cref="HtmlBrowserLaunchOptions.HeaderProfile"/> to the policy's <see cref="HtmlFetchPolicy.BrowserHeaders"/>
    /// so the cookies are earned with the identity the HTTP requests carry.
    /// </summary>
    public HtmlBrowserLaunchOptions? Browser { get; set; }

    /// <summary>How long to wait for the person, including opening the page; the request fails afterwards.</summary>
    public TimeSpan Timeout { get; set; } = DefaultTimeout;

    /// <summary>
    /// Waits for the person and returns whether to continue. When null, the wait ends once the page the browser shows is
    /// no longer a challenge or block page, or fails when the person closes it.
    /// </summary>
    public Func<HtmlBrowserSession, CancellationToken, Task<bool>>? WaitForPerson { get; set; }
}
