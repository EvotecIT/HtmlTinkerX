using Microsoft.Win32.SafeHandles;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserCacheResilienceTests {
    [Fact]
    public async Task VerifiedGenerationWaitsForTransientWindowsDirectoryHandle() {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        using var fixture = new CacheFixture();
        Task release = Task.CompletedTask;
        HtmlBrowser.CacheSmokeTest = (slot, _) => {
            // Model a scanner or a just-closed browser holding the verified directory.
            SafeFileHandle handle = OpenDirectoryWithoutDeleteSharing(slot, 1,
                FileShare.ReadWrite, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
            Assert.False(handle.IsInvalid, $"Directory handle failed: {Marshal.GetLastWin32Error()}");
            release = ReleaseDirectoryHandleAsync(handle);
            return Task.CompletedTask;
        };

        try {
            await HtmlBrowser.EnsureInstalledAsync(HtmlBrowserEngine.Chromium);
            string generation = HtmlBrowser.GetBrowserRuntimeDirectory(HtmlBrowserEngine.Chromium);
            Assert.True(HtmlBrowser.VerifyCacheSlot(generation, true, out string diagnosis), diagnosis);
            Assert.StartsWith(Path.Combine(fixture.Cache, ".htmltinkerx", "versions"), generation);
            Assert.Equal(1, fixture.Installs);
            Assert.Empty(Directory.GetDirectories(Path.Combine(fixture.Cache, ".htmltinkerx"), "staging-*"));
        } finally {
            await release;
        }
    }

    private static async Task ReleaseDirectoryHandleAsync(SafeFileHandle handle) {
        using (handle) {
            // Exceed the previous five-second rename retry budget.
            await Task.Delay(TimeSpan.FromSeconds(6));
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle OpenDirectoryWithoutDeleteSharing(string path, uint access,
        FileShare share, IntPtr security, uint creation, uint flags, IntPtr template);
}
