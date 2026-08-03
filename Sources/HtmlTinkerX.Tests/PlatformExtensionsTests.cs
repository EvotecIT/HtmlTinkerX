using HtmlTinkerX;
using Xunit;

namespace HtmlTinkerX.Tests;

public class PlatformExtensionsTests {
    [Theory]
    [InlineData(HtmlPlatform.WindowsX64, "win32_x64")]
    [InlineData(HtmlPlatform.Mac, "darwin-x64")]
    [InlineData(HtmlPlatform.MacArm64, "darwin-arm64")]
    [InlineData(HtmlPlatform.LinuxX64, "linux-x64")]
    [InlineData(HtmlPlatform.LinuxArm64, "linux-arm64")]
    public void PlatformConversions_ReturnExpected(HtmlPlatform platform, string platformId) {
        Assert.Equal(platformId, platform.ToPlatformId());
    }
}
