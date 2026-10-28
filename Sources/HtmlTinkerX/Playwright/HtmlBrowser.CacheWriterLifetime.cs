using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace HtmlTinkerX;

public static partial class HtmlBrowser {
    private static string InstallationWriterPath => GetInstallationLockPath() + ".writer.json";

    /// <summary>Child identity is persisted before its stdin handshake permits any shared-cache write.
    /// A new lock holder waits for this actual writer, even if the original .NET host has died.</summary>
    private static void RegisterInstallationWriter(Process process) {
        string temporary = InstallationWriterPath + "." + Guid.NewGuid().ToString("N");
        try {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new HtmlBrowserInstallOwner {
                ProcessId = process.Id, ProcessStartUtc = process.StartTime.ToUniversalTime(), AcquiredUtc = DateTime.UtcNow,
                Command = "Playwright browser downloader", Repository = Environment.CurrentDirectory, CacheRoot = GetBrowserInstallPath()
            }));
            // The caller owns the OS lock and has verified that a prior writer is gone.
            if (File.Exists(InstallationWriterPath)) File.Delete(InstallationWriterPath);
            File.Move(temporary, InstallationWriterPath);
        } finally { TryDeleteFile(temporary); }
    }

    private static bool IsInstallationWriterAlive(out HtmlBrowserInstallOwner? owner) {
        owner = null;
        try {
            _ = ValidateCacheMetadataDestination(InstallationWriterPath);
            owner = JsonSerializer.Deserialize<HtmlBrowserInstallOwner>(File.ReadAllText(InstallationWriterPath));
            if (owner == null || owner.ProcessId <= 0 || owner.ProcessStartUtc == default)
                throw new InvalidOperationException("Browser install writer metadata is invalid. Verify that the previous downloader has exited before removing its writer record and retrying repair.");
        } catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
        catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException) {
            throw new InvalidOperationException("Browser install writer metadata is unreadable. Verify that the previous downloader has exited before removing its writer record and retrying repair.", ex);
        }
        try {
            using Process writer = Process.GetProcessById(owner.ProcessId);
            return !writer.HasExited && writer.StartTime.ToUniversalTime() == owner.ProcessStartUtc;
        } catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (System.ComponentModel.Win32Exception ex) {
            throw new InvalidOperationException("The previous browser downloader's process identity cannot be inspected. Repair stopped without displacing a potentially active writer; verify process access and retry.", ex);
        }
    }
}
