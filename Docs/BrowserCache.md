# Browser cache diagnosis and repair

HtmlTinkerX verifies browser files before opening a session. Missing files and size
mismatches trigger a staged replacement. The requested browser is downloaded into
an isolated directory, checked, and launched headless against `about:blank`. A
single directory rename then makes the verified generation available. Existing
generations stay in place, including when a download or smoke test fails.

```csharp
await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);
await using var session = await HtmlBrowser.OpenSessionAsync("https://example.com");

var report = await HtmlBrowser.TestBrowserCacheAsync(HtmlBrowserEngine.Chromium, repair: true);
```

The PowerShell command returns the same structured report:

```powershell
Import-Module PSParseHTML
Test-HtmlTinkerXBrowser -Browser Chromium -Repair | ConvertTo-Json -Depth 8
```

Build the `HtmlTinkerX.Cli` project to use the standalone doctor:

```shell
dotnet run --project Sources/HtmlTinkerX.Cli -- doctor --browser=Chromium
dotnet run --project Sources/HtmlTinkerX.Cli -- doctor --browser=Firefox --repair
```

The CLI writes JSON to stdout and diagnostics to stderr. Its exit codes are:

| Code | Meaning |
| --- | --- |
| 0 | The requested revisions passed hashes and headless launch checks. |
| 1 | Invalid command arguments. |
| 2 | The cache needs repair or diagnosis could not complete. |
| 3 | Another process is repairing the cache; inspect `LockOwner` and `RetryAfterSeconds`. |
| 4 | A verified fallback is available, but the requested revision is missing or incomplete. |

Omit `--browser` or `-Browser` to diagnose Chromium, Firefox, and WebKit. Doctor
checks SHA-256 for each recorded file and starts every browser launch binary.
Normal session startup checks file existence and lengths, without hashing every
file. A known ICU, missing executable, or Firefox exit-255 launch error triggers
one repair and retry. Other launch errors remain available as product or host
diagnostics.

After a failed repair, ordinary launches reuse a verified same-engine fallback
for five minutes before retrying the download. The retry marker is specific to
the loaded Playwright version and browser. Doctor with repair and forced repair
retry immediately. A generic exit-255 or missing-file error triggers repair only
when a full integrity check confirms corruption in the generation actually used
by that launch; host and profile failures leave healthy generations available.

## Storage and concurrency

`PLAYWRIGHT_BROWSERS_PATH` selects the parent cache. Managed browser generations
live in its `.htmltinkerx/versions` directory. The nested layout protects them
from ordinary Playwright garbage collection in the parent cache. Every installer
subprocess also sets `PLAYWRIGHT_SKIP_BROWSER_GC=1`. Existing parent revisions and
`.links` records remain untouched.

With the hermetic `PLAYWRIGHT_BROWSERS_PATH=0` setting, managed generations live
in `.htmltinkerx-browsers` beside the selected `.playwright` driver. This keeps
them outside the driver payload that a version update can replace. `ManagedRoot`
in the doctor report identifies the actual managed location. Mutable drivers are
also staged before replacement; the previous payload survives a failed download.

Each generation has an `htmltinkerx-integrity.json` manifest containing its driver
version, revision directories, launch binaries, file sizes, and SHA-256 hashes.
The append-only `ManagedRoot/install.jsonl` log records process identity, process
start time, working directory, Playwright version, added revisions, and outcome.
Command arguments are omitted so credentials passed to a host cannot enter the
log. Bulk cache cleanup refuses to delete verified generations and their history.
There is no automatic revision pruning.

One OS-held installation lock serializes HtmlTinkerX installers across processes
and cache roots for the machine's shared user account. Its adjacent owner JSON
contains the PID, process start time, command name, repository, cache, and acquisition
time. A crashed process releases the OS lock. A later installer can replace its
stale attribution only after acquiring that lock; a timestamp or apparently dead
PID never permits breaking an occupied lock. Launching an intact generation does
not wait for an installation of another browser.

The downloader's PID and process start time are recorded before a parent-pipe
handshake permits it to write shared download state. If its .NET host is killed,
the child exits when that pipe closes. A new lock holder also checks the recorded
child identity and waits until the actual writer has exited, covering the interval
between parent death and child shutdown.

An unreadable writer record or an inaccessible process identity stops repair
immediately with an actionable diagnosis instead of waiting through the install
timeout. These cases preserve the record: elapsed time does not prove that a
writer has exited. Verify the previous downloader has stopped before removing
an invalid record. Doctor reports the coordination problem without starting a
second writer. Valid dead or reused process identities recover automatically.

Downloads use the loaded Playwright package's platform registry, official URLs,
fallback hosts where supplied, and proxy handling. Transfers stream in chunks,
resume with HTTP Range and If-Range validators, and retry with exponential backoff.
Connection and read inactivity timeouts are at least 180 seconds. Partial transfers
survive an installer crash in `.htmltinkerx/downloads`. A changed mirror or validator
starts a fresh transfer. ZIP entry CRCs and any server-supplied SHA-256 digest are
verified before extraction; archive and installed-file SHA-256 values are recorded.
Playwright does not supply a publisher SHA-256 for every archive, so a locally
recorded hash is an integrity baseline, not an independent publisher signature.
Every engine generation includes its official FFmpeg runtime for video recording.

When repair cannot complete, HtmlTinkerX warns and selects the latest verified
generation of the same engine. Browser and driver versions can differ in that
case. Set `HTMLTINKERX_BROWSER_OFFLINE=1` to prohibit downloads: an existing verified
fallback remains usable, while an empty cache produces an actionable error.

External browser channels, explicit executables, and CDP attachment keep their
existing runtime ownership. For a separate Node test runner, call
`GetBrowserRuntimeDirectory(engine)` after `EnsureInstalledAsync` and pass that
directory as the child process's `PLAYWRIGHT_BROWSERS_PATH`. Keep the parent
application's configured cache root unchanged.

For isolated installation tests, set `PLAYWRIGHT_BROWSERS_PATH` to a disposable
local directory. `HTMLTINKERX_INSTALL_LOCK_PATH` can select a separate lock for
that test environment; ordinary consumers should leave it unset so they share the
machine's default installation lock.
