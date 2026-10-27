using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace HtmlTinkerX;

public static partial class HtmlBrowser {
    internal const string CacheManifestName = "htmltinkerx-integrity.json";

    internal sealed class CacheManifest {
        public int Format { get; set; } = 1;
        public string Driver { get; set; } = string.Empty;
        public HtmlBrowserEngine Engine { get; set; }
        public DateTime VerifiedUtc { get; set; }
        public List<CacheRuntime> Runtimes { get; set; } = new();
        public List<CacheFile> Files { get; set; } = new();
    }

    internal sealed class CacheRuntime {
        public string Name { get; set; } = string.Empty;
        public string Directory { get; set; } = string.Empty;
        public string Executable { get; set; } = string.Empty;
    }

    internal sealed class CacheFile {
        public string Path { get; set; } = string.Empty;
        public long Size { get; set; }
        public string Sha256 { get; set; } = string.Empty;
    }

    private static string ManagedCacheRoot => ManagedRootDuringDriverStart ?? (Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH") == "0"
        ? Path.Combine(Path.GetDirectoryName(GetDriverPath())!, ".htmltinkerx-browsers")
        : Path.Combine(GetBrowserInstallPath(), ".htmltinkerx"));

    internal static string CacheFilePath(string root, string relative) {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
            throw new InvalidDataException("Browser integrity manifest contains an invalid relative path.");
        string path = Path.GetFullPath(Path.Combine(root, relative));
        RequireBrowserRuntimeDescendant(path, Path.GetFullPath(root));
        return path;
    }

    private static string RelativeCachePath(string root, string path) =>
        path.Substring(root.TrimEnd(Path.DirectorySeparatorChar).Length + 1).Replace('\\', '/');

    internal static string CacheHash(string path) {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
    }

    internal static CacheManifest ReadCacheManifest(string slot) {
        CacheManifest manifest = JsonSerializer.Deserialize<CacheManifest>(File.ReadAllText(Path.Combine(slot, CacheManifestName)))
            ?? throw new InvalidDataException("Browser integrity manifest is empty.");
        if (manifest.Format != 1 || !Enum.IsDefined(typeof(HtmlBrowserEngine), manifest.Engine) ||
            manifest.Files == null || manifest.Runtimes == null || manifest.Files.Count == 0 || manifest.Runtimes.Count == 0 ||
            manifest.Files.Any(file => file == null || string.IsNullOrWhiteSpace(file.Path) || file.Size < 0 || file.Sha256 == null ||
                file.Sha256.Length != 64 || !file.Sha256.All(character => Uri.IsHexDigit(character))) ||
            manifest.Runtimes.Any(runtime => runtime == null || string.IsNullOrWhiteSpace(runtime.Name) ||
                string.IsNullOrWhiteSpace(runtime.Directory) || string.IsNullOrWhiteSpace(runtime.Executable)))
            throw new InvalidDataException("Browser integrity manifest has invalid file or runtime metadata.");
        return manifest;
    }

    internal static void WriteCacheManifest(string slot, HtmlBrowserEngine engine, List<CacheRuntime> runtimes) {
        foreach (CacheRuntime runtime in runtimes) {
            runtime.Directory = runtime.Directory.Replace('\\', '/');
            runtime.Executable = runtime.Executable.Replace('\\', '/');
        }
        var manifest = new CacheManifest { Driver = DriverVersion, Engine = engine, VerifiedUtc = DateTime.UtcNow, Runtimes = runtimes };
        foreach (string file in Directory.EnumerateFiles(slot, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal)) {
            if (Path.GetFileName(file) == CacheManifestName) continue;
            manifest.Files.Add(new CacheFile { Path = RelativeCachePath(slot, file), Size = new FileInfo(file).Length, Sha256 = CacheHash(file) });
        }
        File.WriteAllText(Path.Combine(slot, CacheManifestName), JsonSerializer.Serialize(manifest));
    }

    /// <summary>Checks existence and lengths before every session; doctor additionally verifies all SHA-256 values.</summary>
    internal static bool VerifyCacheSlot(string slot, bool hashes, out string diagnosis) {
        try {
            if (File.Exists(Path.Combine(slot, "launch-failed.txt"))) throw new InvalidDataException("Browser revision failed its launch smoke test; repair required.");
            CacheManifest manifest = ReadCacheManifest(slot);
            if (manifest.Format != 1 || manifest.Files.Count == 0 || manifest.Runtimes.Count == 0)
                throw new InvalidDataException("Browser integrity manifest has no verified files or launch binaries.");
            var files = new HashSet<string>(manifest.Files.Select(f => f.Path), StringComparer.Ordinal);
            if (!manifest.Runtimes.Any(runtime => runtime.Name == "ffmpeg"))
                throw new InvalidDataException("Browser cache incomplete (missing FFmpeg runtime required for video recording).");
            foreach (CacheRuntime runtime in manifest.Runtimes) {
                if (!files.Contains(runtime.Executable) || !File.Exists(CacheFilePath(slot, runtime.Executable)))
                    throw new InvalidDataException($"{runtime.Name} cache incomplete (missing launch binary {runtime.Executable}).");
                if (runtime.Name.StartsWith("chromium", StringComparison.Ordinal)) {
                    string icu = RelativeCachePath(slot, Path.Combine(Path.GetDirectoryName(CacheFilePath(slot, runtime.Executable))!, "icudtl.dat"));
                    bool appBundle = runtime.Executable.Replace('\\', '/').Contains(".app/Contents/MacOS/");
                    bool hasIcu = appBundle
                        ? files.Any(file => file.StartsWith(runtime.Directory.TrimEnd('/') + "/", StringComparison.Ordinal) && file.EndsWith("/icudtl.dat", StringComparison.Ordinal))
                        : files.Contains(icu);
                    if (!hasIcu) throw new InvalidDataException($"{runtime.Name} cache incomplete (missing icudtl.dat).");
                }
            }
            foreach (CacheFile file in manifest.Files) {
                string path = CacheFilePath(slot, file.Path);
                if (!File.Exists(path)) throw new InvalidDataException($"Browser cache incomplete (missing {file.Path}).");
                if (new FileInfo(path).Length != file.Size) throw new InvalidDataException($"Browser cache incomplete (size mismatch: {file.Path}).");
                if (hashes && !string.Equals(CacheHash(path), file.Sha256, StringComparison.Ordinal))
                    throw new InvalidDataException($"Browser cache corrupt (SHA-256 mismatch: {file.Path}).");
            }
            diagnosis = "Verified";
            return true;
        } catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException || ex is JsonException || ex is InvalidOperationException || ex is ArgumentException) {
            diagnosis = ex.Message;
            return false;
        }
    }

    private static IEnumerable<string> CacheSlots() {
        string versions = Path.Combine(ManagedCacheRoot, "versions");
        return Directory.Exists(versions) ? Directory.EnumerateDirectories(versions) : Array.Empty<string>();
    }

    private static string? FindCacheSlot(HtmlBrowserEngine engine, bool exact, bool compatibleAuxiliary = false) {
        return CacheSlots().Select(slot => {
            try { return new { Slot = slot, Manifest = ReadCacheManifest(slot) }; }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is JsonException || ex is UnauthorizedAccessException) { return null; }
        }).Where(item => item != null && item.Manifest.Engine == engine && (!exact || IsExpectedSlot(item.Slot, item.Manifest, engine))
            && (!compatibleAuxiliary || HasExpectedAuxiliary(item.Slot, item.Manifest, engine)))
          .OrderByDescending(item => item!.Manifest.VerifiedUtc)
          .Select(item => item!.Slot).FirstOrDefault(slot => VerifyCacheSlot(slot, false, out _));
    }

    private static bool HasExpectedAuxiliary(string slot, CacheManifest manifest, HtmlBrowserEngine engine) {
        var expected = GetExpectedRuntimeDirectories(slot, engine);
        var auxiliary = expected.Where(pair => pair.Key == "ffmpeg" || pair.Key == "winldd").ToArray();
        return auxiliary.Any(pair => pair.Key == "ffmpeg") && auxiliary.All(pair => manifest.Runtimes.Any(runtime => runtime.Name == pair.Key &&
            pair.Value.Any(path => string.Equals(path, CacheFilePath(slot, runtime.Directory), FileSystemPathComparison))));
    }

    private static bool IsExpectedSlot(string slot, CacheManifest manifest, HtmlBrowserEngine engine) {
        var expected = GetExpectedRuntimeDirectories(slot, engine);
        return expected.Count > 0 && expected.All(pair => manifest.Runtimes.Any(runtime => runtime.Name == pair.Key &&
            pair.Value.Any(path => string.Equals(path, CacheFilePath(slot, runtime.Directory), FileSystemPathComparison))));
    }

    /// <summary>Returns the verified generation usable by this driver, or a same-engine fallback after failed repair.</summary>
    /// <remarks>Pass this directory as PLAYWRIGHT_BROWSERS_PATH to a separate Node test runner after EnsureInstalledAsync.</remarks>
    public static string GetBrowserRuntimeDirectory(HtmlBrowserEngine engine) =>
        FindCacheSlot(engine, true) ?? FindCacheSlot(engine, false)
        ?? throw new InvalidOperationException($"{engine} browser cache is missing or incomplete. Run EnsureInstalledAsync or the browser doctor with repair.");
}
