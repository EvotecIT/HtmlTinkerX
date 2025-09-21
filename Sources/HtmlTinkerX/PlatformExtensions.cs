using System.Runtime.InteropServices;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for working with <see cref="HtmlPlatform"/> values.
/// </summary>
internal static class PlatformExtensions {
    public static HtmlPlatform GetCurrentPlatform() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return RuntimeInformation.OSArchitecture == Architecture.Arm64
                ? HtmlPlatform.WindowsArm64
                : HtmlPlatform.WindowsX64;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return RuntimeInformation.OSArchitecture == Architecture.Arm64
                ? HtmlPlatform.MacArm64
                : HtmlPlatform.Mac;
        return RuntimeInformation.OSArchitecture == Architecture.Arm64
            ? HtmlPlatform.LinuxArm64
            : HtmlPlatform.LinuxX64;
    }

    public static string ToPlatformId(this HtmlPlatform platform) => platform switch {
        HtmlPlatform.WindowsX64 => "win32_x64",
        HtmlPlatform.WindowsArm64 => "win32_arm64",
        HtmlPlatform.Mac => "darwin-x64",
        HtmlPlatform.MacArm64 => "darwin-arm64",
        HtmlPlatform.LinuxArm64 => "linux-arm64",
        _ => "linux-x64"
    };

    public static string ToDownloadPlatformId(this HtmlPlatform platform) => platform switch {
        HtmlPlatform.WindowsX64 => "win32_x64",
        HtmlPlatform.WindowsArm64 => "win32_arm64",
        HtmlPlatform.Mac => "mac",
        HtmlPlatform.MacArm64 => "mac-arm64",
        HtmlPlatform.LinuxArm64 => "linux-arm64",
        _ => "linux"
    };
}
