namespace HtmlTinkerX;

using System;
using System.IO;
using System.Runtime.InteropServices;

/// <summary>Classifies existing Unix browser sources without opening or reading their contents.</summary>
internal static class HtmlBrowserUnixFileSystemPath {
    private const int LinuxStatBufferSize = 256;
    private const int CurrentWorkingDirectory = -100;
    private const uint StatxType = 0x00000001;
    private const uint FileTypeMask = 0xF000;
    private const uint DirectoryType = 0x4000;
    private const uint RegularFileType = 0x8000;
    private const uint MacMountLocal = 0x00001000;

    internal static bool IsPseudoFileSystemPath(string path) {
        string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        return IsWithin(fullPath, "/dev")
            || IsWithin(fullPath, "/proc")
            || IsWithin(fullPath, "/sys");
    }

    internal static bool IsRegularFileOrDirectory(string path) {
        if (IsPseudoFileSystemPath(path) || IsRemoteFileSystemPath(path) || !TryGetMode(path, out uint mode)) return false;
        uint fileType = mode & FileTypeMask;
        return fileType == RegularFileType || fileType == DirectoryType;
    }

    internal static bool IsRegularFileOrDirectoryMode(uint mode) {
        uint fileType = mode & FileTypeMask;
        return fileType == RegularFileType || fileType == DirectoryType;
    }

    internal static bool IsRemoteFileSystemType(long fileSystemType) => fileSystemType switch {
        0x00006969 => true, // NFS
        0x0000517B => true, // SMB
        0xFF534D42 => true, // CIFS
        0xFE534D42 => true, // SMB2/SMB3
        0x5346414F => true, // AFS
        0x73757245 => true, // Coda
        0x0000564C => true, // NCP
        0x01021997 => true, // 9P
        0x00C36400 => true, // Ceph
        0x65735546 => true, // FUSE, including sshfs/rclone/s3fs
        0x0BD00BD0 => true, // Lustre
        0x20030528 => true, // OrangeFS/PVFS2
        0x19830326 => true, // BeeGFS
        0x01161970 => true, // GFS2
        0x7461636F => true, // OCFS2
        _ => false
    };

    private static bool IsRemoteFileSystemPath(string path) {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            return StatFsMac(path, out MacStatFs stat) != 0 || (stat.Flags & MacMountLocal) == 0;
        }
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return false;
        IntPtr buffer = Marshal.AllocHGlobal(LinuxStatBufferSize);
        try {
            for (int offset = 0; offset < LinuxStatBufferSize; offset += sizeof(long)) {
                Marshal.WriteInt64(buffer, offset, 0);
            }
            if (StatFsLinux(path, buffer) != 0) return true;
            long fileSystemType = IntPtr.Size == 8
                ? Marshal.ReadInt64(buffer, 0)
                : unchecked((uint)Marshal.ReadInt32(buffer, 0));
            return IsRemoteFileSystemType(fileSystemType);
        } catch (EntryPointNotFoundException) {
            return true;
        } finally {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool TryGetMode(string path, out uint mode) {
        mode = 0;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            if (StatMac(path, out MacStat stat) != 0) return false;
            mode = stat.Mode;
            return true;
        }
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return false;
        try {
            if (StatLinux(CurrentWorkingDirectory, path, 0, StatxType, out LinuxStat statx) == 0) {
                mode = statx.Mode;
                return true;
            }
        } catch (EntryPointNotFoundException) {
            // Older kernels/libcs and some Mono runtimes do not expose statx.
        }
        return TryGetLinuxStatMode(path, out mode);
    }

    internal static bool TryGetLinuxStatMode(string path, out uint mode) {
        mode = 0;
        IntPtr buffer = Marshal.AllocHGlobal(LinuxStatBufferSize);
        try {
            for (int offset = 0; offset < LinuxStatBufferSize; offset += sizeof(long)) {
                Marshal.WriteInt64(buffer, offset, 0);
            }
            if (StatLinuxFallback(path, buffer) != 0) return false;
            // Linux/glibc places st_mode at byte 24 on x86-64 and byte 16 on
            // x86, ARM, and ARM64. Reading from an oversized native buffer avoids
            // coupling the fallback to each architecture's complete struct stat.
            int modeOffset = RuntimeInformation.ProcessArchitecture == Architecture.X64 ? 24 : 16;
            mode = unchecked((uint)Marshal.ReadInt32(buffer, modeOffset));
            return true;
        } catch (EntryPointNotFoundException) {
            return false;
        } finally {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool IsWithin(string path, string root) =>
        string.Equals(path, root, StringComparison.Ordinal)
        || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    [StructLayout(LayoutKind.Explicit, Size = 144)]
    private struct MacStat {
        [FieldOffset(4)] internal ushort Mode;
    }

    [StructLayout(LayoutKind.Explicit, Size = 2168)]
    private struct MacStatFs {
        [FieldOffset(64)] internal uint Flags;
    }

    // Linux statx has one fixed userspace ABI across 32-bit and 64-bit architectures.
    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct LinuxStat {
        [FieldOffset(28)] internal ushort Mode;
    }

    [DllImport("libc", EntryPoint = "stat", SetLastError = true)]
    private static extern int StatMac(string path, out MacStat stat);

    [DllImport("libc", EntryPoint = "statfs", SetLastError = true)]
    private static extern int StatFsMac(string path, out MacStatFs stat);

    [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static extern int StatLinux(int directoryFileDescriptor, string path, int flags, uint mask, out LinuxStat stat);

    [DllImport("libc", EntryPoint = "stat", SetLastError = true)]
    private static extern int StatLinuxFallback(string path, IntPtr stat);

    [DllImport("libc", EntryPoint = "statfs", SetLastError = true)]
    private static extern int StatFsLinux(string path, IntPtr stat);
}
