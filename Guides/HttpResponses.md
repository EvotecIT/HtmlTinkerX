# Control HTTP response size, encoding, cancellation and refusals

[Back to the project overview](../README.MD)

## HTTP response controls

The shared HTML response reader, HTTP form submissions, hidden-form relays,
and remote stylesheet downloads default to a 16 MiB response limit. The limit
applies to declared lengths and streamed bodies. HTML decoding uses a BOM
before the HTTP charset, followed by an HTML meta charset and UTF-8 fallback.

For larger trusted responses, set `MaximumResponseBytes` on
`Submit-HtmlForm` or `Invoke-HtmlFormRelay`, or pass `HtmlHttpFetchOptions`
to the .NET overloads. Relay and PreMailer options also accept `FetchOptions`.
Existing .NET overloads use the same bounded default.

Crawls also accept `MaximumTotalPageResponseBytes` and
`MaximumTotalAssetResponseBytes` in `HtmlCrawlOptions` or `Invoke-HtmlCrawl`.
Static pages, robots.txt, and sitemaps share the page budget. Downloaded assets,
including nested CSS assets, share the asset budget. These optional limits count
HTTP body bytes read during one invocation; browser traffic and retained result
memory are outside them. Exceeding a limit throws `HtmlCrawlBudgetExceededException`.
Use `ResumePath` to continue from the preceding checkpoint with a fresh budget.

`Submit-HtmlForm -Timeout` covers the complete HTTP submission, including the
response body, in milliseconds; zero disables that deadline. Stopping the
PowerShell pipeline cancels the request. .NET callers can reuse an `HttpClient`
and supply a cancellation token; its timeout also covers response body reads.

## Why a page was refused

URL reads report a refused page as `HtmlHttpException`, which derives from
`HttpRequestException`. Its `Failure` says why without keeping the URL,
cookies or body: `Challenge` (Cloudflare's `cf-mitigated: challenge`),
`BlockPage` (an Azure Front Door or Akamai block page, with a short redacted
`Title` and `Excerpt`), `AccessDenied`, `AuthenticationRequired`,
`RateLimited` or `HttpError`, plus `Provider`, `StatusCode` and `RetryAt`.
Block pages are recognised only by named provider signatures; any other 403
stays `AccessDenied`. In PowerShell the error record's `TargetObject` is the
failure:

```powershell
try { Get-HtmlPage -Url $url } catch { $_.TargetObject | Format-List Kind, Provider, Excerpt }
```

`Get-HtmlPage` also accepts `-RetryCount`, `-BrowserHeaders` for a
consistent desktop Chrome header set, several `-Proxy` addresses to rotate
through, and `-RenderWhenBlocked` to render a challenged or blocked page in a
headless browser instead. A page the browser is refused too still fails with
`HtmlHttpException`. HtmlTinkerX does not solve CAPTCHAs.

.NET callers get the same behaviour from `HtmlHttpClientFactory.Create(HtmlFetchPolicy)`:

```csharp
var policy = new HtmlFetchPolicy {
    Gate = sharedGate,                     // HtmlHostRequestGate: per-host limits and adaptive pacing
    RetryCount = 2,
    BrowserHeaders = HtmlBrowserHeaderProfile.Chrome(),
    ValidatorCache = new HtmlMemoryValidatorCache(),
    OnBlocked = (uri, failure, token) => Task.FromResult(
        failure.Kind == HtmlHttpFailureKind.BlockPage ? HtmlBlockedAction.Render : HtmlBlockedAction.Fail),
    Renderer = HtmlBrowser.CreateFetchRenderer(new HtmlBrowserLaunchOptions { CdpEndpointUrl = "http://127.0.0.1:9222" })
};
using HttpClient client = HtmlHttpClientFactory.Create(policy, HtmlProxyRotator.Create(proxies));
```

Share one `HtmlHostRequestGate` across clients that read for the same
process. A 429 or 503, or a refusal with Retry-After, defers that host and
doubles its delay on repeated throttling up to `MaximumDelay`; successes halve
it again. Set `AdaptivePacing = false` to keep only the fixed limits. `HtmlResponseClassifier` classifies responses you read yourself.
