using System.Runtime.InteropServices;
using Xunit;

namespace HtmlTinkerX.Tests;

public class PlatformDetectionTests
{
    [Fact]
    public void GetCurrentPlatform_ReturnsExpectedForCurrentOS()
    {
        HtmlPlatform expected;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            expected = HtmlPlatform.WindowsX64; // current implementation treats all Windows as x64
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            expected = RuntimeInformation.OSArchitecture == Architecture.Arm64
                ? HtmlPlatform.MacArm64
                : HtmlPlatform.Mac;
        }
        else
        {
            expected = RuntimeInformation.OSArchitecture == Architecture.Arm64
                ? HtmlPlatform.LinuxArm64
                : HtmlPlatform.LinuxX64;
        }

        var actual = PlatformExtensions.GetCurrentPlatform();
        Assert.Equal(expected, actual);

        // Sanity: IDs should be non-empty and platform-specific
        Assert.False(string.IsNullOrWhiteSpace(actual.ToPlatformId()));
        Assert.False(string.IsNullOrWhiteSpace(actual.ToDownloadPlatformId()));
    }
}

