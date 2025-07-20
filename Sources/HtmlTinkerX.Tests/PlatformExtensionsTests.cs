using HtmlTinkerX;
using Xunit;

namespace PSParseHTML.Tests;

public class PlatformExtensionsTests {
    [Theory]
    [InlineData(HtmlPlatform.WindowsX64, "win32_x64", "win32_x64")]
    [InlineData(HtmlPlatform.Mac, "darwin-x64", "mac")]
    [InlineData(HtmlPlatform.MacArm64, "darwin-arm64", "mac-arm64")]
    [InlineData(HtmlPlatform.LinuxX64, "linux-x64", "linux")]
    [InlineData(HtmlPlatform.LinuxArm64, "linux-arm64", "linux-arm64")]
    public void PlatformConversions_ReturnExpected(HtmlPlatform platform, string platformId, string downloadId) {
        Assert.Equal(platformId, platform.ToPlatformId());
        Assert.Equal(downloadId, platform.ToDownloadPlatformId());
    }
}