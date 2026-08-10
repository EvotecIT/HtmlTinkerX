namespace HtmlTinkerX;

using Microsoft.Win32.SafeHandles;
using System;
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

    internal static bool TryResolveExistingPath(string path, out string resolved) {
        resolved = string.Empty;
        if (IsNetworkOrDevicePath(path)) return false;
        try {
            string fullPath = Path.GetFullPath(path);
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

    [DllImport("libc", SetLastError = true)]
    private static extern IntPtr realpath(string path, IntPtr resolvedPath);

    [DllImport("libc")]
    private static extern void free(IntPtr pointer);
}
