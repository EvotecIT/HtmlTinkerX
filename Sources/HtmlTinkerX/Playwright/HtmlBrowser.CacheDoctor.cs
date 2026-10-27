using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlBrowser {
    /// <summary>Checks cache files with SHA-256 and launches about:blank; optionally repairs each requested engine.</summary>
    /// <param name="engine">One browser, or null for all supported engines.</param>
    /// <param name="repair">Download and verify replacement generations where required.</param>
    /// <param name="cancellationToken">Cancels diagnostics between revisions.</param>
    /// <returns>A report suitable for JSON output and CI exit codes.</returns>
    public static async Task<HtmlBrowserCacheReport> TestBrowserCacheAsync(HtmlBrowserEngine? engine = null, bool repair = false,
        CancellationToken cancellationToken = default) {
        var report = new HtmlBrowserCacheReport { CacheRoot = GetBrowserInstallPath(), ManagedRoot = ManagedCacheRoot, PlaywrightVersion = DriverVersion };
        try { report.AvailableBytes = new DriveInfo(Path.GetPathRoot(report.CacheRoot)!).AvailableFreeSpace; }
        catch (Exception ex) when (ex is IOException || ex is ArgumentException) { }
        try { report.LockOwner = JsonSerializer.Deserialize<HtmlBrowserInstallOwner>(ReadInstallOwnerText()); }
        catch (JsonException) { }
        string lockPath = GetInstallationLockPath();
        if (File.Exists(lockPath)) {
            try { using var probe = new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { report.LockHeld = true; report.RetryAfterSeconds = 1; }
        }
        try { if (IsInstallationWriterAlive(out HtmlBrowserInstallOwner? writer)) {
            report.LockHeld = true;
            report.LockOwner = writer;
            report.RetryAfterSeconds = 1;
        } } catch (InvalidOperationException ex) {
            report.LockHeld = true;
            report.Revisions.Add(new HtmlBrowserCacheRevision { Browser = "installer", State = "incomplete", Message = ex.Message });
        }
        var engines = engine.HasValue ? new[] { engine.Value } : new[] { HtmlBrowserEngine.Chromium, HtmlBrowserEngine.Firefox, HtmlBrowserEngine.WebKit };
        foreach (HtmlBrowserEngine selected in engines) {
            cancellationToken.ThrowIfCancellationRequested();
            bool canRepair = !report.LockHeld;
            async Task<(bool Exact, bool Any)> InspectAsync() {
                bool exact = false;
                bool any = false;
                foreach (string slot in CacheSlots().ToArray()) {
                    CacheManifest manifest;
                    try { manifest = ReadCacheManifest(slot); }
                    catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is JsonException || ex is UnauthorizedAccessException) {
                        report.Revisions.Add(new HtmlBrowserCacheRevision { Directory = slot, State = "incomplete", Message = ex.Message });
                        continue;
                    }
                    if (manifest.Engine != selected) continue;
                    bool verified = VerifyCacheSlot(slot, true, out string diagnosis);
                    if (verified) {
                        try { await SmokeCacheSlotAsync(slot, selected).ConfigureAwait(false); }
                        catch (Exception ex) {
                            verified = false;
                            diagnosis = ex.Message;
                            if (!report.LockHeld) {
                                try { await MarkCacheLaunchFailureAsync(slot, diagnosis).ConfigureAwait(false); }
                                catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is InvalidOperationException || failure is ArgumentException) {
                                    canRepair = false;
                                    diagnosis += " Failed to record unusable generation; repair stopped to preserve fallback safety: " + failure.Message;
                                }
                            }
                        }
                    }
                    any |= verified;
                    exact |= verified && IsExpectedSlot(slot, manifest, selected);
                    foreach (CacheRuntime runtime in manifest.Runtimes) report.Revisions.Add(new HtmlBrowserCacheRevision {
                        Browser = runtime.Name, Revision = Path.GetFileName(runtime.Directory), Directory = CacheFilePath(slot, runtime.Directory),
                        State = verified ? "verified" : "incomplete", Message = diagnosis
                    });
                }
                return (exact, any);
            }
            int firstRow = report.Revisions.Count;
            var state = await InspectAsync().ConfigureAwait(false);
            if (repair && canRepair && !state.Exact) {
                string? repairFailure = null;
                try { await RepairInstallationAsync(selected).ConfigureAwait(false); }
                catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException) {
                    repairFailure = "Browser repair stopped: " + ex.Message;
                }
                report.Revisions.RemoveRange(firstRow, report.Revisions.Count - firstRow);
                state = await InspectAsync().ConfigureAwait(false);
                if (repairFailure != null) report.Revisions.Add(new HtmlBrowserCacheRevision { Browser = selected.ToString(), State = "missing", Message = repairFailure });
            }
            bool foundExpected = state.Exact;
            bool foundVerified = state.Any;
            if (!foundExpected) {
                report.Revisions.Add(new HtmlBrowserCacheRevision { Browser = selected.ToString(), State = "missing", Message = "Requested revision is not verified. Run the browser doctor with repair." });
                report.ExitCode = Math.Max(report.ExitCode, foundVerified ? 4 : 2);
            }
        }
        // Inventory the legacy shared cache without modifying it or treating its completion marker as verification.
        if (Directory.Exists(report.CacheRoot)) foreach (string directory in Directory.EnumerateDirectories(report.CacheRoot)) {
            string name = Path.GetFileName(directory);
            if (!new[] { "chromium", "firefox", "webkit" }.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal))) continue;
            report.Revisions.Add(new HtmlBrowserCacheRevision { Browser = name.Split('-')[0], Revision = name,
                Directory = directory, State = "unverified", Message = "Legacy shared revision has no HtmlTinkerX integrity manifest; retained unchanged." });
        }
        string log = Path.Combine(ManagedCacheRoot, "install.jsonl");
        try { if (File.Exists(log)) report.RecentInstalls = File.ReadLines(ValidateCacheMetadataDestination(log)).Reverse().Take(20).Reverse().ToList(); }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException) {
            Console.Error.WriteLine($"WARNING: could not read browser cache history ({ex.Message}).");
        }
        if (report.LockHeld && report.ExitCode != 0) report.ExitCode = 3;
        if (!report.LockHeld) {
            try { report.LockOwner = JsonSerializer.Deserialize<HtmlBrowserInstallOwner>(ReadInstallOwnerText()); }
            catch (JsonException) { }
        }
        return report;
    }
}
