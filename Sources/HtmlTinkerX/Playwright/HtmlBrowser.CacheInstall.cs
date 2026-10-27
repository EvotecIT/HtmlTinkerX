using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlBrowser {
    // External process boundary; tests supply a staged, contract-shaped browser image instead of downloading gigabytes.
    internal static Func<string, HtmlBrowserEngine, Task>? CacheStageInstaller { get; set; }
    internal static Func<string, HtmlBrowserEngine, Task>? CacheSmokeTest { get; set; }

    private static async Task EnsureVerifiedBrowserAsync(HtmlBrowserEngine engine, bool force) {
        if (!force && IsDriverPresent() && FindCacheSlot(engine, true) != null) {
            EnsureDriverSearchPath();
            return;
        }
        await InstallationSemaphore.WaitAsync().ConfigureAwait(false);
        try {
            using FileStream installLock = await AcquireInstallationFileLockAsync().ConfigureAwait(false);
            ValidateInstallationMutationPaths();
            ValidateDirectoryMutationPath(ManagedCacheRoot, "HtmlTinkerX managed browser cache");
            if (!IsDriverPresent()) await DownloadAndInstallDriverAsync().ConfigureAwait(false);
            EnsureDriverSearchPath();
            if (!force && FindCacheSlot(engine, true) != null) return;
            Console.Error.WriteLine($"{engine} browser cache requires verification or repair. Installing into staging; existing generations are retained.");

            // A repair must not fall back to a generation already known to fail full integrity checks.
            foreach (string slot in CacheSlots().ToArray()) {
                try {
                    if (ReadCacheManifest(slot).Engine == engine && !VerifyCacheSlot(slot, true, out string diagnosis))
                        File.WriteAllText(Path.Combine(slot, "launch-failed.txt"), diagnosis);
                } catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is JsonException || ex is UnauthorizedAccessException) { }
            }

            string? fallback = FindCacheSlot(engine, false);
            string root = ManagedCacheRoot;
            Directory.CreateDirectory(root);
            string staging = Path.Combine(root, "staging-" + Guid.NewGuid().ToString("N"));
            string generation = Path.Combine(root, "versions", engine.ToString().ToLowerInvariant() + "-" + Guid.NewGuid().ToString("N"));
            AppendCacheLog(engine, "install-started", Array.Empty<string>(), null);
            try {
                if (ReadBooleanEnvironmentVariable("HTMLTINKERX_BROWSER_OFFLINE") == true)
                    throw new IOException("Browser cache repair is offline (HTMLTINKERX_BROWSER_OFFLINE=1). No installed revision was removed.");
                Directory.CreateDirectory(staging);
                if (CacheStageInstaller != null) await CacheStageInstaller(staging, engine).ConfigureAwait(false);
                else {
                    if (RuntimeInformationIsLinuxWithDependencies())
                        await Task.Run(() => PlaywrightInstaller(new[] { "install-deps", engine.ToString().ToLowerInvariant() })).ConfigureAwait(false);
                    await RunCacheScriptAsync(staging, engine, "install").ConfigureAwait(false);
                }
                var runtimes = JsonSerializer.Deserialize<List<CacheRuntime>>(File.ReadAllText(Path.Combine(staging, "runtime-plan.json")))
                    ?? throw new InvalidDataException("Browser installer did not produce a runtime plan.");
                WriteCacheManifest(staging, engine, runtimes);
                if (!IsExpectedSlot(staging, ReadCacheManifest(staging), engine) || !VerifyCacheSlot(staging, true, out _))
                    throw new InvalidDataException("Downloaded browser cache is incomplete or has invalid integrity metadata.");
                await SmokeCacheSlotAsync(staging, engine).ConfigureAwait(false);
                // Staging and generations share a volume. One rename makes an entirely verified generation visible.
                Directory.CreateDirectory(Path.GetDirectoryName(generation)!);
                await MoveVerifiedGenerationAsync(staging, generation).ConfigureAwait(false);
                AppendCacheLog(engine, "verified", runtimes.Select(runtime => Path.GetFileName(runtime.Directory)).ToArray(), null);
            } catch (Exception ex) {
                AppendCacheLog(engine, "repair-failed", Array.Empty<string>(), ex.Message);
                if (fallback == null || !VerifyCacheSlot(fallback, false, out _)) {
                    throw new InvalidOperationException($"{engine} browser cache repair failed: {ex.Message} No working revisions were deleted. Run the browser doctor with repair; this is a cache failure, not a product failure.", ex);
                }
                Console.Error.WriteLine($"WARNING: {engine} cache repair failed ({ex.Message}). Using verified fallback {fallback}; browser and driver versions may differ.");
            } finally {
                // Only this invocation's uncommitted staging tree is disposable; previous generations are retained.
                TryDeleteDirectory(staging);
            }
        } finally {
            InstallationSemaphore.Release();
        }
    }

    private static async Task MoveVerifiedGenerationAsync(string staging, string generation) {
        // Windows scanners and just-closed browser children can briefly retain directory handles.
        for (int attempt = 0; ; attempt++) {
            try { Directory.Move(staging, generation); return; }
            catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && attempt < 20) {
                await Task.Delay(250).ConfigureAwait(false);
            }
        }
    }

    private static bool RuntimeInformationIsLinuxWithDependencies() =>
        System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux)
        && ShouldUsePlaywrightWithDepsOnLinux();

    private static Task SmokeCacheSlotAsync(string slot, HtmlBrowserEngine engine) =>
        CacheSmokeTest != null ? CacheSmokeTest(slot, engine) : RunCacheScriptAsync(slot, engine, "smoke");

    private static void AppendCacheLog(HtmlBrowserEngine engine, string outcome, string[] added, string? message) {
        Directory.CreateDirectory(ManagedCacheRoot);
        using Process current = Process.GetCurrentProcess();
        File.AppendAllText(Path.Combine(ManagedCacheRoot, "install.jsonl"), JsonSerializer.Serialize(new {
            TimeUtc = DateTime.UtcNow,
            ProcessId = current.Id,
            ProcessStartUtc = current.StartTime.ToUniversalTime(),
            Command = current.ProcessName,
            Repository = Environment.CurrentDirectory,
            PlaywrightVersion = DriverVersion,
            Browser = engine.ToString(),
            Added = added,
            Removed = Array.Empty<string>(),
            Outcome = outcome,
            Message = message
        }) + Environment.NewLine);
    }

    private static async Task RunCacheScriptAsync(string root, HtmlBrowserEngine engine, string mode) {
        string script = Path.Combine(Path.GetTempPath(), "htmltinkerx-cache-" + Guid.NewGuid().ToString("N") + ".cjs");
        string driver = GetDriverPath();
        try {
            using (Stream source = typeof(HtmlBrowser).Assembly.GetManifestResourceStream("HtmlTinkerX.Playwright.Scripts.HtmlBrowserCache.cjs")
                ?? throw new InvalidOperationException("The browser cache installer resource is missing."))
            using (FileStream target = File.Create(script)) source.CopyTo(target);
            var start = new ProcessStartInfo {
                FileName = Environment.GetEnvironmentVariable("PLAYWRIGHT_NODEJS_PATH") ?? Path.Combine(driver, "node", PlatformId, NodeExecutable),
                Arguments = string.Join(" ", new[] { script, Path.Combine(driver, "package"), root, engine.ToString().ToLowerInvariant(), mode,
                    Path.Combine(ManagedCacheRoot, "downloads", DriverVersion + "-" + PlatformId) }.Select(QuoteInstallerArgument)),
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true
            };
            start.EnvironmentVariables["PLAYWRIGHT_BROWSERS_PATH"] = root;
            start.EnvironmentVariables["PLAYWRIGHT_SKIP_BROWSER_GC"] = "1";
            start.EnvironmentVariables["PLAYWRIGHT_DOWNLOAD_CONNECTION_TIMEOUT"] = Environment.GetEnvironmentVariable("PLAYWRIGHT_DOWNLOAD_CONNECTION_TIMEOUT") ?? "180000";
            start.EnvironmentVariables["HTMLTINKERX_PARENT_PIPE"] = "1";
            using Process process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start browser cache installer.");
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> errors = process.StandardError.ReadToEndAsync();
            try {
                if (mode == "install") RegisterInstallationWriter(process);
                await process.StandardInput.WriteLineAsync("start").ConfigureAwait(false);
                await process.StandardInput.FlushAsync().ConfigureAwait(false);
                await Task.Run(() => process.WaitForExit()).ConfigureAwait(false);
            } finally {
                if (!process.HasExited) { process.Kill(); process.WaitForExit(); }
                if (mode == "install") TryDeleteFile(InstallationWriterPath);
            }
            string stdout = await output.ConfigureAwait(false);
            string stderr = await errors.ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(stdout)) Console.Error.Write(stdout);
            if (process.ExitCode != 0) throw new IOException($"Browser cache {mode} failed (exit {process.ExitCode}): {stderr.Trim()}");
        } finally { TryDeleteFile(script); }
    }
}
