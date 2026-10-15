namespace HtmlTinkerX;

using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

internal static class HtmlBrowserFileSystemPath {
    private const uint FileReadAttributes = 0x80;
    private const uint FileShareRead = 0x1;
    private const uint FileShareWrite = 0x2;
    private const uint FileShareDelete = 0x4;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint DriveRemote = 4;
    private const int ErrorInsufficientBuffer = 122;

    internal static string GetValidatedLocalPath(string path) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("File path cannot be empty.", nameof(path));
        }
        if (!IsSafeLocalPath(path)) {
            throw new ArgumentException("Only direct local paths are supported as browser file sources; network, device, mapped, substituted, and reparse paths are rejected.", nameof(path));
        }
        return Path.GetFullPath(path);
    }

    internal static bool TryResolveExistingPath(string path, out string resolved) {
        resolved = string.Empty;
        try {
            string fullPath = Path.GetFullPath(path);
            if (!IsSafeLocalPath(fullPath)) return false;
            if (!File.Exists(fullPath) && !Directory.Exists(fullPath)) return false;
            resolved = Environment.OSVersion.Platform == PlatformID.Win32NT
                ? ResolveWindowsPath(fullPath)
                : ResolveUnixPath(fullPath);
            return !string.IsNullOrWhiteSpace(resolved);
        } catch (Exception ex) when (ex is ArgumentException
                                     || ex is NotSupportedException
                                     || ex is PathTooLongException
                                     || ex is IOException
                                     || ex is UnauthorizedAccessException
                                     || ex is Win32Exception) {
            return false;
        }
    }

    /// <summary>
    /// Resolves physical identity using the nearest existing ancestor, including for a not-yet-created destination.
    /// This read-only metadata helper does not establish path safety; mutation callers must validate direct local paths first.
    /// </summary>
    internal static string GetPathIdentity(string path) {
        string existing = Path.GetFullPath(path);
        var suffix = new Stack<string>();
        while (!Directory.Exists(existing) && !File.Exists(existing)) {
            string? parent = Path.GetDirectoryName(existing);
            if (string.IsNullOrEmpty(parent)) throw new IOException("The filesystem path identity could not be resolved.");
            suffix.Push(Path.GetFileName(existing));
            existing = parent;
        }
        string resolved = Environment.OSVersion.Platform == PlatformID.Win32NT
            ? ResolveWindowsPath(existing) : ResolveUnixPath(existing);
        while (suffix.Count > 0) resolved = Path.Combine(resolved, suffix.Pop());
        return resolved;
    }

    /// <summary>
    /// Compares resolved physical identities without merging distinct case-sensitive filesystem entries.
    /// This is an identity comparison, not authorization for a supplied mutable path.
    /// </summary>
    internal static bool IsWithinPathIdentity(string identity, string rootIdentity) {
        string root = rootIdentity.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string candidate = identity.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(candidate, root, StringComparison.Ordinal)
            || candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    /// <summary>
    /// Validates an installation destination while preserving verified filesystem case aliases on macOS.
    /// File-source validation remains stricter; installation aliases must still contain no mutable reparse components.
    /// </summary>
    internal static string GetValidatedInstallationPathIdentity(string path) {
        string fullPath = Path.GetFullPath(path);
        string identity;
        try {
            _ = GetValidatedLocalPath(fullPath);
            identity = GetPathIdentity(fullPath);
        } catch (ArgumentException) when (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && !IsNetworkOrDevicePath(path)) {
            identity = GetPathIdentity(fullPath);
            if (!AreEquivalentDirectUnixPaths(fullPath, identity, StringComparison.OrdinalIgnoreCase)) throw;
            _ = GetValidatedLocalPath(identity);
        }

        // Inspect the requested entries, not just realpath's final result, so a case-only symlink cannot masquerade as a case alias.
        string directPath = fullPath;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            && !string.Equals(fullPath, identity, StringComparison.OrdinalIgnoreCase)
            && AreEquivalentDirectUnixPaths(fullPath, identity, StringComparison.OrdinalIgnoreCase)) {
            directPath = "/private" + fullPath;
        }
        var components = new List<string>();
        for (string? component = directPath; !string.IsNullOrEmpty(component); component = Path.GetDirectoryName(component)) {
            components.Add(component!);
        }
        components.Reverse();
        if (ContainsReparsePointBeforeTargetProbe(components, GetExistingPathAttributes)) {
            throw new ArgumentException("Playwright installation destinations must not contain filesystem links or reparse points.", nameof(path));
        }
        return identity;
    }

    /// <summary>Checks path trust boundaries without opening the target or reading its content.</summary>
    internal static bool IsSafeLocalPath(string path) {
        if (IsNetworkOrDevicePath(path)) return false;
        try {
            string fullPath = Path.GetFullPath(path);
            if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
                string root = Path.GetPathRoot(fullPath) ?? string.Empty;
                if (IsWindowsUnsafeDriveRoot(root, GetDriveType, QueryDosDeviceTarget)) return false;
                return !ContainsWindowsReparsePoint(fullPath);
            }
            if (HtmlBrowserUnixFileSystemPath.IsPseudoFileSystemPath(fullPath)) return false;
            string resolved;
            try {
                resolved = ResolveUnixPath(fullPath);
            } catch (Win32Exception ex) when (ex.NativeErrorCode == 2) {
                // Preserve validation of not-yet-created ordinary local paths, but
                // reject an existing parent whose canonical path reveals a symlink.
                return HasOnlyDirectUnixAncestors(fullPath);
            }
            return AreEquivalentDirectUnixPaths(fullPath, resolved)
                && HtmlBrowserUnixFileSystemPath.IsRegularFileOrDirectory(resolved);
        } catch {
            // Path safety checks fail closed before existence or content probes.
            return false;
        }
    }

    private static bool HasOnlyDirectUnixAncestors(string fullPath) {
        string? ancestor = Path.GetDirectoryName(fullPath);
        while (!string.IsNullOrEmpty(ancestor)) {
            try {
                string resolved = ResolveUnixPath(ancestor);
                return AreEquivalentDirectUnixPaths(ancestor, resolved)
                    && HtmlBrowserUnixFileSystemPath.IsRegularFileOrDirectory(resolved);
            } catch (Win32Exception ex) when (ex.NativeErrorCode == 2) {
                ancestor = Path.GetDirectoryName(ancestor);
            }
        }
        return false;
    }

    private static bool AreEquivalentDirectUnixPaths(string requested, string resolved, StringComparison comparison = StringComparison.Ordinal) {
        if (requested.Length > 1) requested = requested.TrimEnd(Path.DirectorySeparatorChar);
        if (resolved.Length > 1) resolved = resolved.TrimEnd(Path.DirectorySeparatorChar);
        if (string.Equals(requested, resolved, comparison)) return true;
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return false;
        // Darwin exposes these operating-system-owned compatibility roots through
        // /private. Preserve ordinary temporary/configuration paths while rejecting
        // mutable symlink components below them.
        foreach (string root in new[] { "/etc", "/tmp", "/var" }) {
            if ((string.Equals(requested, root, comparison)
                 || requested.StartsWith(root + "/", comparison))
                && string.Equals(resolved, "/private" + requested, comparison)) return true;
        }
        return false;
    }

    internal static bool IsNetworkOrDevicePath(string path) {
        if (string.IsNullOrWhiteSpace(path)) return false;
        string candidate = path.TrimStart();
        if (Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri)
            && uri.IsFile
            && !string.IsNullOrWhiteSpace(uri.Host)) return true;
        return candidate.StartsWith(@"\\", StringComparison.Ordinal)
               || candidate.StartsWith("//", StringComparison.Ordinal)
               || candidate.StartsWith(@"\??\", StringComparison.OrdinalIgnoreCase)
               || candidate.StartsWith(@"\Device\", StringComparison.OrdinalIgnoreCase)
               || candidate.StartsWith(@"\GLOBAL??\", StringComparison.OrdinalIgnoreCase)
               || candidate.StartsWith(@"\DosDevices\", StringComparison.OrdinalIgnoreCase)
               || candidate.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase)
               || candidate.StartsWith(@"\\.\", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Classifies a Windows drive root from local drive and DOS-device mapping metadata.</summary>
    internal static bool IsWindowsUnsafeDriveRoot(
        string root,
        Func<string, uint> getDriveType,
        Func<string, string?> queryDosDevice) {
        if (string.IsNullOrWhiteSpace(root)) return false;
        if (getDriveType(root) == DriveRemote) return true;
        if (root.Length < 2 || root[1] != ':') return false;
        string? target = queryDosDevice(root.Substring(0, 2));
        return target != null
               && (target.IndexOf(@"\Device\Mup", StringComparison.OrdinalIgnoreCase) >= 0
                   || target.IndexOf("Redirector", StringComparison.OrdinalIgnoreCase) >= 0
                   || target.IndexOf(@"\Device\Rdr", StringComparison.OrdinalIgnoreCase) >= 0
                   || target.StartsWith(@"\??\", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsWindowsReparsePoint(string fullPath) {
        string root = Path.GetPathRoot(fullPath) ?? string.Empty;
        string current = root;
        string remainder = fullPath.Substring(root.Length);
        List<string> components = new();
        foreach (string segment in remainder.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)) {
            current = Path.Combine(current, segment);
            components.Add(current);
        }
        return ContainsReparsePointBeforeTargetProbe(components, GetExistingPathAttributes);
    }

    private static FileAttributes? GetExistingPathAttributes(string path) {
        try {
            return File.GetAttributes(path);
        } catch (FileNotFoundException) {
            return null;
        } catch (DirectoryNotFoundException) {
            return null;
        }
    }

    /// <summary>Stops component inspection as soon as a reparse point is observed.</summary>
    internal static bool ContainsReparsePointBeforeTargetProbe(
        IEnumerable<string> pathComponents,
        Func<string, FileAttributes?> getAttributes) {
        foreach (string component in pathComponents) {
            FileAttributes? attributes = getAttributes(component);
            if (!attributes.HasValue) return false;
            if ((attributes.Value & FileAttributes.ReparsePoint) != 0) return true;
        }
        return false;
    }

    private static string? QueryDosDeviceTarget(string deviceName) {
        int capacity = 512;
        while (capacity <= 32768) {
            StringBuilder buffer = new(capacity);
            uint length = QueryDosDevice(deviceName, buffer, buffer.Capacity);
            if (length > 0) return buffer.ToString();
            if (Marshal.GetLastWin32Error() != ErrorInsufficientBuffer) return null;
            capacity *= 2;
        }
        return null;
    }

    private static string ResolveWindowsPath(string path) {
        using SafeFileHandle handle = CreateFile(
            path,
            FileReadAttributes,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExisting,
            FileFlagBackupSemantics,
            IntPtr.Zero);
        if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());

        StringBuilder buffer = new(512);
        uint length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
        if (length == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (length >= buffer.Capacity) {
            buffer = new StringBuilder(checked((int)length + 1));
            length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
            if (length == 0 || length >= buffer.Capacity) throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        string finalPath = buffer.ToString();
        if (finalPath.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) return @"\\" + finalPath.Substring(8);
        return finalPath.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase) ? finalPath.Substring(4) : finalPath;
    }

    private static string ResolveUnixPath(string path) {
        IntPtr pointer = realpath(path, IntPtr.Zero);
        if (pointer == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        try {
            return Marshal.PtrToStringAnsi(pointer) ?? throw new IOException("The operating system returned an empty canonical path.");
        } finally {
            free(pointer);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle file, StringBuilder filePath, uint filePathLength, uint flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern uint GetDriveType(string rootPathName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint QueryDosDevice(string deviceName, StringBuilder targetPath, int maximumLength);

    [DllImport("libc", SetLastError = true)]
    private static extern IntPtr realpath(string path, IntPtr resolvedPath);

    [DllImport("libc")]
    private static extern void free(IntPtr pointer);
}
