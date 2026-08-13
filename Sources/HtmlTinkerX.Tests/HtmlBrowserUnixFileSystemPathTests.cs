using System;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed class HtmlBrowserUnixFileSystemPathTests {
    [Theory]
    [InlineData(0x8000u, true)]
    [InlineData(0x4000u, true)]
    [InlineData(0x2000u, false)]
    [InlineData(0x6000u, false)]
    [InlineData(0x1000u, false)]
    [InlineData(0xC000u, false)]
    public void OnlyRegularFilesAndDirectoriesAreBrowserSources(uint mode, bool expected) {
        Assert.Equal(expected, HtmlBrowserUnixFileSystemPath.IsRegularFileOrDirectoryMode(mode));
    }

    [Fact]
    public void UnixDeviceAndPseudoFileSourcesAreRejectedBeforeBrowserIo() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        Assert.False(HtmlBrowserFileSystemPath.IsSafeLocalPath("/dev/null"));
        Assert.Throws<ArgumentException>(() => HtmlBrowser.CreateLocalFileUri("/dev/null"));
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            Assert.False(HtmlBrowserFileSystemPath.IsSafeLocalPath("/proc/self/environ"));
            Assert.Throws<ArgumentException>(() => HtmlBrowserPdfSource.FromFile("/proc/self/environ"));
        }
    }

    [Fact]
    public void UnixNamedPipesAreRejectedBeforeBrowserIo() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        string pipe = Path.Combine(Path.GetTempPath(), "HtmlTinkerX-Fifo-" + Guid.NewGuid().ToString("N"));
        Assert.Equal(0, CreateNamedPipe(pipe, 0x180));
        try {
            Assert.False(HtmlBrowserFileSystemPath.IsSafeLocalPath(pipe));
            Assert.Throws<ArgumentException>(() => HtmlBrowserPdfSource.FromFile(pipe));
        } finally {
            File.Delete(pipe);
        }
    }

    [Fact]
    public void UnixRegularFilesAndDirectoriesRemainAllowed() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        string root = Path.Combine(Path.GetTempPath(), "HtmlTinkerX-UnixPath-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string file = Path.Combine(root, "report.html");
        File.WriteAllText(file, "<p>local</p>");
        try {
            Assert.True(HtmlBrowserFileSystemPath.IsSafeLocalPath(root));
            Assert.True(HtmlBrowserFileSystemPath.IsSafeLocalPath(file));
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [DllImport("libc", EntryPoint = "mkfifo", SetLastError = true)]
    private static extern int CreateNamedPipe(string path, uint mode);
}
