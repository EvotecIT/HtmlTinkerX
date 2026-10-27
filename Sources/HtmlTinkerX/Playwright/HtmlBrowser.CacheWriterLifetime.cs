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
        if (!File.Exists(InstallationWriterPath)) return false;
        try {
            owner = JsonSerializer.Deserialize<HtmlBrowserInstallOwner>(File.ReadAllText(InstallationWriterPath));
            if (owner == null || owner.ProcessId <= 0 || owner.ProcessStartUtc == default)
                throw new InvalidDataException("Browser install writer metadata is invalid; verify the writer before repairing.");
            using Process writer = Process.GetProcessById(owner.ProcessId);
            return !writer.HasExited && writer.StartTime.ToUniversalTime() == owner.ProcessStartUtc;
        } catch (ArgumentException) { return false; }
        catch (System.ComponentModel.Win32Exception) { return true; }
        catch (InvalidOperationException) { return false; }
    }
}
