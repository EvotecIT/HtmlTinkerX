using System;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlBrowser {
    /// <summary>
    /// Creates an <see cref="HtmlFetchPolicy.Renderer"/> that loads a blocked page in a browser and returns its rendered
    /// HTML. Each call opens and closes one session. Set <see cref="HtmlBrowserLaunchOptions.CdpEndpointUrl"/> to read
    /// through the operator's own running Chrome, whose profile and reputation a CDN may already trust.
    /// </summary>
    /// <param name="options">Browser launch options; headless Chromium with network-idle loading when null.</param>
    public static Func<Uri, CancellationToken, Task<string>> CreateFetchRenderer(HtmlBrowserLaunchOptions? options = null) {
        HtmlBrowserLaunchOptions launch = options ?? new HtmlBrowserLaunchOptions();
        return async (uri, cancellationToken) => {
            if (uri == null) throw new ArgumentNullException(nameof(uri));
            await using HtmlBrowserSession session = await OpenSessionAsync(uri.AbsoluteUri, launch, cancellationToken).ConfigureAwait(false);
            return await session.Page.ContentAsync().ConfigureAwait(false);
        };
    }
}
