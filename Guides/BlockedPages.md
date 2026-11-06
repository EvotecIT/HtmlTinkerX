# Read sites that challenge or block requests

[Back to the project overview](../README.MD)

Some sites challenge automated requests or block them outright. HtmlTinkerX
gives you choices for reading such sites politely. Each one is off until you
ask for it. HtmlTinkerX ships no CAPTCHA or Turnstile solver and never
completes a site's check automatically. When a check needs a person, you
complete it yourself in a browser.

Every cmdlet that downloads with `-Url` takes the same parameters, such as
`Get-HtmlPage`, `ConvertFrom-Html`, `ConvertFrom-HtmlTable`,
`Convert-HtmlToMarkdown` and `ConvertFrom-HtmlMeta`. In .NET they are
properties of `HtmlFetchPolicy`, passed to `HtmlHttpClientFactory.Create`.

| Choice | PowerShell | .NET |
| --- | --- | --- |
| Retry transient failures, honouring Retry-After | `-RetryCount 2` | `RetryCount` |
| Pace requests per host | — | `Gate = new HtmlHostRequestGate(...)` |
| Present one consistent browser identity | `-BrowserHeaders` | `BrowserHeaders` |
| Rotate through your proxies | `-Proxy a, b, c` | `HtmlProxyRotator.Create(proxies)` |
| Render a blocked page in a headless browser | `-RenderWhenBlocked` | `OnBlocked` returning `Render`, `Renderer` |
| Use your own running Chrome | `-CdpEndpointUrl http://127.0.0.1:9222` | `HtmlBrowserLaunchOptions.CdpEndpointUrl` |
| Finish a check yourself, then continue | `-ResumeInBrowser` | `HtmlBrowser.CreateBlockedResume` |
| Replay stored responses while developing | `-ReplayCache ./replay` | `ReplayCache = new HtmlReplayCache(dir)` |

## One identity for HTTP and the browser

`-BrowserHeaders` sends a User-Agent, Accept, Accept-Language and client hints
that agree with each other. On its own it describes an ordinary desktop Chrome.
With `-RenderWhenBlocked` or `-ResumeInBrowser` the headers are read from the
browser that would take over (the bundled Chromium, or your Chrome over CDP),
and that browser is given the same identity. The HTTP requests and the browser
then present the same browser version, platform, locale and client hints.

The bundled headless Chromium calls itself `HeadlessChrome`. The profile uses
the name the same build reports when it runs headed, and the browser session
reports that name too. The version and platform stay the real ones. Firefox and
WebKit send no client hints. A popup opened by the page keeps the browser's own
identity.

```csharp
HtmlBrowserHeaderProfile identity = await HtmlBrowser.GetHeaderProfileAsync();
var policy = new HtmlFetchPolicy {
    BrowserHeaders = identity,
    OnBlocked = (uri, failure, token) => Task.FromResult(HtmlBlockedAction.Render),
    Renderer = HtmlBrowser.CreateFetchRenderer(new HtmlBrowserLaunchOptions { HeaderProfile = identity })
};
```

## Finish a check yourself and continue

`-ResumeInBrowser` opens a challenged or blocked page in a visible browser and
waits while you complete what the site asks. When the page loads normally, the
site's browser cookies are copied into the HTTP session and the request is sent
again. The wait ends after `-ResumeTimeoutSeconds` (300 by default), or when you
close the page, and the request then fails as blocked. Combine it with
`-BrowserHeaders`, because sites often tie their clearance cookies to the
browser that earned them:

```powershell
Get-HtmlPage -Url https://example.org/report -BrowserHeaders -ResumeInBrowser
Get-HtmlPage -Url https://example.org/report -BrowserHeaders -ResumeInBrowser -CdpEndpointUrl http://127.0.0.1:9222
```

With your own Chrome, the cookies copied are that browser's cookies for the
site. In .NET, give `CreateBlockedResume` the cookie container your transport
sends, and decide yourself when the person has finished if you prefer:

```csharp
var cookies = new CookieContainer();
var policy = new HtmlFetchPolicy {
    RetryCount = 1,
    BrowserHeaders = identity,
    OnBlocked = HtmlBrowser.CreateBlockedResume(cookies, new HtmlBrowserResumeOptions {
        Browser = new HtmlBrowserLaunchOptions { Headless = false, HeaderProfile = identity },
        Timeout = TimeSpan.FromMinutes(3)
    })
};
using HttpClient client = HtmlHttpClientFactory.Create(policy, HtmlProxyRotator.Create(proxies, cookies: cookies));
```

## Replay responses while you develop

`-ReplayCache <dir>` stores every successful page response on disk and replays
it on the next run without contacting the site. Use it while you write
extraction code, so you do not fetch the same pages again on every attempt.
Stored responses never expire; delete the directory to fetch fresh copies.
Requests that carry cookies, credentials or custom headers are neither stored
nor replayed. Set-Cookie headers are never written. In .NET,
`HtmlReplayCache.IncludeRequesterState` opts in to caching signed-in
responses; anyone who uses the directory can then read them.

The replay cache is separate from `HtmlFetchPolicy.ValidatorCache`, which
revalidates with the site and reuses a body only when the site confirms it is
unchanged.
