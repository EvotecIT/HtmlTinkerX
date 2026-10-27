using System;
using System.IO;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlBrowser {
    private static readonly TimeSpan RepairFailureBackoff = TimeSpan.FromMinutes(5);

    private static string ValidateCacheMetadataDestination(string path) {
        try { _ = HtmlBrowserFileSystemPath.GetValidatedInstallationPathIdentity(path); }
        catch (System.ComponentModel.Win32Exception ex) {
            throw new ArgumentException("Browser cache metadata must use a verifiable direct local file path.", nameof(path), ex);
        }
        return path;
    }

    private static string RepairFailurePath(HtmlBrowserEngine engine) =>
        Path.Combine(ManagedCacheRoot, "repair-failed-" + DriverVersion + "-" + engine.ToString().ToLowerInvariant() + ".txt");

    private static bool TryUseRecentRepairFallback(HtmlBrowserEngine engine) {
        try {
            string marker = ValidateCacheMetadataDestination(RepairFailurePath(engine));
            if (!File.Exists(marker) || DateTime.UtcNow - File.GetLastWriteTimeUtc(marker) >= RepairFailureBackoff) return false;
            string? fallback = FindCacheSlot(engine, false);
            if (fallback == null) return false;
            Console.Error.WriteLine($"WARNING: {engine} repair recently failed. Using verified fallback {fallback}; automatic repair retries after five minutes. Run doctor with repair to retry now.");
            return true;
        } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException) {
            return false;
        }
    }

    private static void RecordRecentRepairFailure(HtmlBrowserEngine engine) {
        try { File.WriteAllText(ValidateCacheMetadataDestination(RepairFailurePath(engine)), DateTime.UtcNow.ToString("O")); }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException) {
            Console.Error.WriteLine($"WARNING: could not record browser repair retry time ({ex.Message}).");
        }
    }

    private static void ClearRecentRepairFailure(HtmlBrowserEngine engine) {
        try { TryDeleteFile(ValidateCacheMetadataDestination(RepairFailurePath(engine))); }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException) {
            Console.Error.WriteLine($"WARNING: could not clear browser repair retry time ({ex.Message}).");
        }
    }

    /// <summary>Serialize persistent rejection of a failed generation with installers and repair selection.</summary>
    private static async Task MarkCacheLaunchFailureAsync(string slot, string diagnosis) {
        await InstallationSemaphore.WaitAsync().ConfigureAwait(false);
        try {
            using FileStream installLock = await AcquireInstallationFileLockAsync().ConfigureAwait(false);
            ValidateDirectoryMutationPath(slot, "HtmlTinkerX failed browser generation");
            File.WriteAllText(ValidateCacheMetadataDestination(Path.Combine(slot, "launch-failed.txt")), diagnosis);
        } finally { InstallationSemaphore.Release(); }
    }
}
