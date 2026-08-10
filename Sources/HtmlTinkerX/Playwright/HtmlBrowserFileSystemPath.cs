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
            return true;
        } catch {
            // Path safety checks fail closed before existence or content probes.
            return false;
        }
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
        return ContainsReparsePointBeforeTargetProbe(components, path => {
            try {
                return File.GetAttributes(path);
            } catch (FileNotFoundException) {
                return null;
            } catch (DirectoryNotFoundException) {
                return null;
            }
        });
    }

    /// <summary>Stops component inspection as soon as a Windows reparse point is observed.</summary>
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
