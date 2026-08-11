namespace HtmlTinkerX;

using System;
using System.IO;
using System.Runtime.InteropServices;

/// <summary>Classifies existing Unix browser sources without opening or reading their contents.</summary>
internal static class HtmlBrowserUnixFileSystemPath {
    private const int CurrentWorkingDirectory = -100;
    private const uint StatxType = 0x00000001;
    private const uint FileTypeMask = 0xF000;
    private const uint DirectoryType = 0x4000;
    private const uint RegularFileType = 0x8000;

    internal static bool IsPseudoFileSystemPath(string path) {
        string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        return IsWithin(fullPath, "/dev")
            || IsWithin(fullPath, "/proc")
            || IsWithin(fullPath, "/sys");
    }

    internal static bool IsRegularFileOrDirectory(string path) {
        if (IsPseudoFileSystemPath(path) || !TryGetMode(path, out uint mode)) return false;
        uint fileType = mode & FileTypeMask;
        return fileType == RegularFileType || fileType == DirectoryType;
    }

    internal static bool IsRegularFileOrDirectoryMode(uint mode) {
        uint fileType = mode & FileTypeMask;
        return fileType == RegularFileType || fileType == DirectoryType;
    }

    private static bool TryGetMode(string path, out uint mode) {
        mode = 0;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            if (StatMac(path, out MacStat stat) != 0) return false;
            mode = stat.Mode;
            return true;
        }
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return false;
        if (StatLinux(CurrentWorkingDirectory, path, 0, StatxType, out LinuxStat statx) != 0) return false;
        mode = statx.Mode;
        return true;
    }

    private static bool IsWithin(string path, string root) =>
        string.Equals(path, root, StringComparison.Ordinal)
        || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    [StructLayout(LayoutKind.Explicit, Size = 144)]
    private struct MacStat {
        [FieldOffset(4)] internal ushort Mode;
    }

    // Linux statx has one fixed userspace ABI across 32-bit and 64-bit architectures.
    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct LinuxStat {
        [FieldOffset(28)] internal ushort Mode;
    }

    [DllImport("libc", EntryPoint = "stat", SetLastError = true)]
    private static extern int StatMac(string path, out MacStat stat);

    [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static extern int StatLinux(int directoryFileDescriptor, string path, int flags, uint mask, out LinuxStat stat);
}
