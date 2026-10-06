# Benchmark corpus

This optional lane exercises the current source with the existing PSPublishModule/PowerForge runner. It is separate from the product solution and ordinary test workflows. Run it in a fresh PowerShell 7.4 or later process with a .NET 8 SDK and PSPublishModule 3.0.156 or later installed. The wrapper limits this optional build to `net8.0` without changing the product's framework list.

```powershell
pwsh -NoProfile -File ./Build/Benchmarks/Invoke-Corpus.ps1 -Plan
pwsh -NoProfile -File ./Build/Benchmarks/Invoke-Corpus.ps1 -IncludeBrowser -WarmupCount 1 -IterationCount 1 -Purpose correctness-smoke
```

The first command builds the optional harness and lists the matrix. The second checks all fourteen cases, including Chromium. Browser installation and the first render use HtmlTinkerX's installer and renderer outside the measured operation. Without `-IncludeBrowser`, the two browser rows are explicitly skipped. Validation failures fail the invocation and remain in the native results.

| Family | Sizes | Public operation and validation |
|---|---|---|
| Nested | 25 / 250 paragraphs, 24 levels | Readable extraction; every numbered paragraph and Unicode text |
| Tables | 25 / 2,500 rows | Detailed table parsing; row IDs, value checksum and labels |
| Cards | 25 / 250 cards | Page reader; headings, links, complete repeated collection and text |
| Unicode | 25 / 250 paragraphs | DOM parsing; title, paragraph count and multilingual text including emoji |
| StaticRead | 25 / 250 paragraphs | HTTP fetch and parsing; UTF-8 decoding and complete content |
| WarmBrowser | 25 / 250 table rows | Reused Chromium renderer; DOM assertions, PDF payload and one browser instance |
| Crawl | 10 / 50 linked pages | Static crawl; no failed or duplicate pages and every unique page marker |

The HTTP cases use an immutable loopback site. Crawl runs keep results in memory and omit robots, sitemap discovery, asset download and Markdown conversion. This measures sequential static fetching and processing of a bounded chain; it is not evidence for browser crawls, public-network throughput or unbounded scale.

PowerForge owns warmups, rotated ordering, memory sampling, process placement and JSON/CSV/Markdown artifacts. HTML generation, fixture startup, previous-document disposal and validation sit outside the measured operation. Fixtures and browser processes are disposed when the wrapper exits, including validation failures.

For controlled measurements, discover the machine's topology and select a fixed domain. On Windows, pass its native-width mask and an explicit priority:

```powershell
pwsh -NoProfile -File ./Build/Benchmarks/Invoke-Corpus.ps1 -IncludeBrowser -ProcessorAffinityMask 0xFFFF -ProcessPriority AboveNormal -DomainLabel 'verified-domain' -HostMetadataPath ./host.json -OutputRoot ./Ignore/Benchmarks
```

The mask above is an example; verify it against the actual host. `host.json` supplies processor/topology, OS, power-plan and other relevant host details as a JSON object. Check host load before and after the full matrix and retain both observations. Use the same placement, options, fixtures and runtime for each compared revision. Do not use busy-host smoke timings or shared hosted-runner timings as a product ranking.

Results record source commit/state and actual core/harness assembly hashes. Native placement metadata records applied and original settings when requested. Use a clean committed source for comparisons; `-SkipBuild` requires matching existing Release binaries. `-Case Tables-2500` selects an individual case. Run each changed revision in a fresh process so cached assemblies cannot carry over.

The **Benchmark corpus** GitHub workflow is manual. It uploads seven-day native artifacts and labels runs as correctness smoke because hosted runner load and placement are uncontrolled. It adds no performance threshold to correctness CI. Keep only the compact evidence needed for comparisons and remove obsolete build/results output; the default results folder is ignored.
