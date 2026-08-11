namespace HtmlTinkerX;

using System;
using System.IO;
using System.Runtime.InteropServices;

/// <summary>Classifies existing Unix browser sources without opening or reading their contents.</summary>
internal static class HtmlBrowserUnixFileSystemPath {
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

        switch (RuntimeInformation.ProcessArchitecture) {
            case Architecture.X64:
                if (StatLinuxX64(path, out LinuxX64Stat x64) != 0) return false;
                mode = x64.Mode;
                return true;
            case Architecture.Arm64:
                if (StatLinuxArm64(path, out LinuxArm64Stat arm64) != 0) return false;
                mode = arm64.Mode;
                return true;
            default:
                return false;
        }
    }

    private static bool IsWithin(string path, string root) =>
        string.Equals(path, root, StringComparison.Ordinal)
        || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    [StructLayout(LayoutKind.Explicit, Size = 144)]
    private struct MacStat {
        [FieldOffset(4)] internal ushort Mode;
    }

    [StructLayout(LayoutKind.Explicit, Size = 144)]
    private struct LinuxX64Stat {
        [FieldOffset(24)] internal uint Mode;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    private struct LinuxArm64Stat {
        [FieldOffset(16)] internal uint Mode;
    }

    [DllImport("libc", EntryPoint = "stat", SetLastError = true)]
    private static extern int StatMac(string path, out MacStat stat);

    [DllImport("libc", EntryPoint = "stat", SetLastError = true)]
    private static extern int StatLinuxX64(string path, out LinuxX64Stat stat);

    [DllImport("libc", EntryPoint = "stat", SetLastError = true)]
    private static extern int StatLinuxArm64(string path, out LinuxArm64Stat stat);
}
