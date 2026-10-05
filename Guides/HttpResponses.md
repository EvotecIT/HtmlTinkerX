# Control HTTP response size, encoding and cancellation

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
